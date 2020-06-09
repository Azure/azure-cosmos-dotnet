﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Distinct;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Sql;

    internal static class CosmosQueryExecutionContextFactory
    {
        private const string InternalPartitionKeyDefinitionProperty = "x-ms-query-partitionkey-definition";
        private const int PageSizeFactorForTop = 5;

        public static CosmosQueryExecutionContext Create(
            CosmosQueryContext cosmosQueryContext,
            InputParameters inputParameters)
        {
            if (cosmosQueryContext == null)
            {
                throw new ArgumentNullException(nameof(cosmosQueryContext));
            }

            if (inputParameters == null)
            {
                throw new ArgumentNullException(nameof(inputParameters));
            }

            CosmosQueryExecutionContextWithNameCacheStaleRetry cosmosQueryExecutionContextWithNameCacheStaleRetry = new CosmosQueryExecutionContextWithNameCacheStaleRetry(
                cosmosQueryContext: cosmosQueryContext,
                cosmosQueryExecutionContextFactory: () =>
                {
                    // Query Iterator requires that the creation of the query context is deferred until the user calls ReadNextAsync
                    AsyncLazy<TryCatch<CosmosQueryExecutionContext>> lazyTryCreateCosmosQueryExecutionContext = new AsyncLazy<TryCatch<CosmosQueryExecutionContext>>(valueFactory: (innerCancellationToken) =>
                    {
                        innerCancellationToken.ThrowIfCancellationRequested();
                        return CosmosQueryExecutionContextFactory.TryCreateCoreContextAsync(
                            cosmosQueryContext,
                            inputParameters,
                            innerCancellationToken);
                    });
                    LazyCosmosQueryExecutionContext lazyCosmosQueryExecutionContext = new LazyCosmosQueryExecutionContext(lazyTryCreateCosmosQueryExecutionContext);
                    return lazyCosmosQueryExecutionContext;
                });

            CatchAllCosmosQueryExecutionContext catchAllCosmosQueryExecutionContext = new CatchAllCosmosQueryExecutionContext(cosmosQueryExecutionContextWithNameCacheStaleRetry);

            return catchAllCosmosQueryExecutionContext;
        }

        private static async Task<TryCatch<CosmosQueryExecutionContext>> TryCreateCoreContextAsync(
            CosmosQueryContext cosmosQueryContext,
            InputParameters inputParameters,
            CancellationToken cancellationToken)
        {
            // The default
            using (cosmosQueryContext.CreateDiagnosticScope("CreateQueryPipeline"))
            {
                // Try to parse the continuation token.
                CosmosElement continuationToken = inputParameters.InitialUserContinuationToken;
                PartitionedQueryExecutionInfo queryPlanFromContinuationToken = inputParameters.PartitionedQueryExecutionInfo;
                if (continuationToken != null)
                {
                    if (!PipelineContinuationToken.TryCreateFromCosmosElement(
                        continuationToken,
                        out PipelineContinuationToken pipelineContinuationToken))
                    {
                        return TryCatch<CosmosQueryExecutionContext>.FromException(
                            new MalformedContinuationTokenException(
                                $"Malformed {nameof(PipelineContinuationToken)}: {continuationToken}."));
                    }

                    if (PipelineContinuationToken.IsTokenFromTheFuture(pipelineContinuationToken))
                    {
                        return TryCatch<CosmosQueryExecutionContext>.FromException(
                            new MalformedContinuationTokenException(
                                $"{nameof(PipelineContinuationToken)} Continuation token is from a newer version of the SDK. " +
                                $"Upgrade the SDK to avoid this issue." +
                                $"{continuationToken}."));
                    }

                    if (!PipelineContinuationToken.TryConvertToLatest(
                        pipelineContinuationToken,
                        out PipelineContinuationTokenV1_1 latestVersionPipelineContinuationToken))
                    {
                        return TryCatch<CosmosQueryExecutionContext>.FromException(
                            new MalformedContinuationTokenException(
                                $"{nameof(PipelineContinuationToken)}: '{continuationToken}' is no longer supported."));
                    }

                    continuationToken = latestVersionPipelineContinuationToken.SourceContinuationToken;
                    if (latestVersionPipelineContinuationToken.QueryPlan != null)
                    {
                        queryPlanFromContinuationToken = latestVersionPipelineContinuationToken.QueryPlan;
                    }
                }

                CosmosQueryClient cosmosQueryClient = cosmosQueryContext.QueryClient;
                ContainerQueryProperties containerQueryProperties = await cosmosQueryClient.GetCachedContainerQueryPropertiesAsync(
                    cosmosQueryContext.ResourceLink,
                    inputParameters.PartitionKey,
                    cancellationToken);
                cosmosQueryContext.ContainerResourceId = containerQueryProperties.ResourceId;

                PartitionedQueryExecutionInfo partitionedQueryExecutionInfo;
                if (inputParameters.ForcePassthrough)
                {
                    partitionedQueryExecutionInfo = new PartitionedQueryExecutionInfo()
                    {
                        QueryInfo = new QueryInfo()
                        {
                            Aggregates = null,
                            DistinctType = DistinctQueryType.None,
                            GroupByAliases = null,
                            GroupByAliasToAggregateType = null,
                            GroupByExpressions = null,
                            HasSelectValue = false,
                            Limit = null,
                            Offset = null,
                            OrderBy = null,
                            OrderByExpressions = null,
                            RewrittenQuery = null,
                            Top = null,
                        },
                        QueryRanges = new List<Documents.Routing.Range<string>>(),
                    };
                }
                else if (queryPlanFromContinuationToken != null)
                {
                    partitionedQueryExecutionInfo = queryPlanFromContinuationToken;
                }
                else
                {
                    // If the query would go to gateway, but we have a partition key,
                    // then try seeing if we can execute as a passthrough using client side only logic.
                    // This is to short circuit the need to go to the gateway to get the query plan.
                    if (cosmosQueryContext.QueryClient.ByPassQueryParsing()
                        && inputParameters.PartitionKey.HasValue)
                    {
                        bool parsed;
                        SqlQuery sqlQuery;
                        using (cosmosQueryContext.CreateDiagnosticScope("QueryParsing"))
                        {
                            parsed = SqlQuery.TryParse(inputParameters.SqlQuerySpec.QueryText, out sqlQuery);
                        }

                        if (parsed)
                        {
                            bool hasDistinct = sqlQuery.SelectClause.HasDistinct;
                            bool hasGroupBy = sqlQuery.GroupByClause != default;
                            bool hasAggregates = AggregateProjectionDector.HasAggregate(sqlQuery.SelectClause.SelectSpec);
                            bool createPassthroughQuery = !hasAggregates && !hasDistinct && !hasGroupBy;

                            if (createPassthroughQuery)
                            {
                                TestInjections.ResponseStats responseStats = inputParameters?.TestInjections?.Stats;
                                if (responseStats != null)
                                {
                                    responseStats.PipelineType = TestInjections.PipelineType.Passthrough;
                                }

                                // Only thing that matters is that we target the correct range.
                                Documents.PartitionKeyDefinition partitionKeyDefinition = GetPartitionKeyDefinition(inputParameters, containerQueryProperties);
                                List<Documents.PartitionKeyRange> targetRanges = await cosmosQueryContext.QueryClient.GetTargetPartitionKeyRangesByEpkStringAsync(
                                    cosmosQueryContext.ResourceLink.OriginalString,
                                    containerQueryProperties.ResourceId,
                                    inputParameters.PartitionKey.Value.InternalKey.GetEffectivePartitionKeyString(partitionKeyDefinition));

                                return await CosmosQueryExecutionContextFactory.TryCreatePassthroughQueryExecutionContextAsync(
                                    cosmosQueryContext,
                                    inputParameters,
                                    partitionedQueryExecutionInfo: new PartitionedQueryExecutionInfo(),
                                    targetRanges,
                                    containerQueryProperties.ResourceId,
                                    cancellationToken);
                            }
                        }
                    }

                    if (cosmosQueryContext.QueryClient.ByPassQueryParsing())
                    {
                        // For non-Windows platforms(like Linux and OSX) in .NET Core SDK, we cannot use ServiceInterop, so need to bypass in that case.
                        // We are also now bypassing this for 32 bit host process running even on Windows as there are many 32 bit apps that will not work without this
                        partitionedQueryExecutionInfo = await QueryPlanRetriever.GetQueryPlanThroughGatewayAsync(
                            cosmosQueryContext,
                            inputParameters.SqlQuerySpec,
                            cosmosQueryContext.ResourceLink,
                            inputParameters.PartitionKey,
                            cancellationToken);
                    }
                    else
                    {
                        using (cosmosQueryContext.CreateDiagnosticScope("ServiceInterop"))
                        {
                            Documents.PartitionKeyDefinition partitionKeyDefinition = GetPartitionKeyDefinition(inputParameters, containerQueryProperties);

                            partitionedQueryExecutionInfo = await QueryPlanRetriever.GetQueryPlanWithServiceInteropAsync(
                                cosmosQueryContext.QueryClient,
                                inputParameters.SqlQuerySpec,
                                partitionKeyDefinition,
                                inputParameters.PartitionKey != null,
                                cancellationToken);
                        }
                    }
                }

                return await TryCreateFromPartitionedQuerExecutionInfoAsync(
                    partitionedQueryExecutionInfo,
                    containerQueryProperties,
                    cosmosQueryContext,
                    inputParameters,
                    cancellationToken);
            }
        }

        public static async Task<TryCatch<CosmosQueryExecutionContext>> TryCreateFromPartitionedQuerExecutionInfoAsync(
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            ContainerQueryProperties containerQueryProperties,
            CosmosQueryContext cosmosQueryContext,
            InputParameters inputParameters,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<Documents.PartitionKeyRange> targetRanges = await CosmosQueryExecutionContextFactory.GetTargetPartitionKeyRangesAsync(
                   cosmosQueryContext.QueryClient,
                   cosmosQueryContext.ResourceLink.OriginalString,
                   partitionedQueryExecutionInfo,
                   containerQueryProperties,
                   inputParameters.Properties,
                   inputParameters.InitialFeedRange);

            bool singleLogicalPartitionKeyQuery = inputParameters.PartitionKey.HasValue
                || ((partitionedQueryExecutionInfo.QueryRanges.Count == 1)
                    && partitionedQueryExecutionInfo.QueryRanges[0].IsSingleValue);
            bool serverStreamingQuery = !partitionedQueryExecutionInfo.QueryInfo.HasAggregates
                && !partitionedQueryExecutionInfo.QueryInfo.HasDistinct
                && !partitionedQueryExecutionInfo.QueryInfo.HasGroupBy;
            bool streamingSinglePartitionQuery = singleLogicalPartitionKeyQuery && serverStreamingQuery;

            bool clientStreamingQuery =
                serverStreamingQuery
                && !partitionedQueryExecutionInfo.QueryInfo.HasOrderBy
                && !partitionedQueryExecutionInfo.QueryInfo.HasTop
                && !partitionedQueryExecutionInfo.QueryInfo.HasLimit
                && !partitionedQueryExecutionInfo.QueryInfo.HasOffset;
            bool streamingCrossContinuationQuery = !singleLogicalPartitionKeyQuery && clientStreamingQuery;

            bool createPassthoughQuery = streamingSinglePartitionQuery || streamingCrossContinuationQuery;

            Task<TryCatch<CosmosQueryExecutionContext>> tryCreateContextTask;
            if (createPassthoughQuery)
            {
                TestInjections.ResponseStats responseStats = inputParameters?.TestInjections?.Stats;
                if (responseStats != null)
                {
                    responseStats.PipelineType = TestInjections.PipelineType.Passthrough;
                }

                tryCreateContextTask = CosmosQueryExecutionContextFactory.TryCreatePassthroughQueryExecutionContextAsync(
                    cosmosQueryContext,
                    inputParameters,
                    partitionedQueryExecutionInfo,
                    targetRanges,
                    containerQueryProperties.ResourceId,
                    cancellationToken);
            }
            else
            {
                TestInjections.ResponseStats responseStats = inputParameters?.TestInjections?.Stats;
                if (responseStats != null)
                {
                    responseStats.PipelineType = TestInjections.PipelineType.Specialized;
                }

                if (!string.IsNullOrEmpty(partitionedQueryExecutionInfo.QueryInfo.RewrittenQuery))
                {
                    // We need pass down the rewritten query.
                    SqlQuerySpec rewrittenQuerySpec = new SqlQuerySpec()
                    {
                        QueryText = partitionedQueryExecutionInfo.QueryInfo.RewrittenQuery,
                        Parameters = inputParameters.SqlQuerySpec.Parameters
                    };

                    inputParameters = new InputParameters(
                        rewrittenQuerySpec,
                        inputParameters.InitialUserContinuationToken,
                        inputParameters.InitialFeedRange,
                        inputParameters.MaxConcurrency,
                        inputParameters.MaxItemCount,
                        inputParameters.MaxBufferedItemCount,
                        inputParameters.PartitionKey,
                        inputParameters.Properties,
                        inputParameters.PartitionedQueryExecutionInfo,
                        inputParameters.ExecutionEnvironment,
                        inputParameters.ReturnResultsInDeterministicOrder,
                        inputParameters.ForcePassthrough,
                        inputParameters.TestInjections);
                }

                tryCreateContextTask = CosmosQueryExecutionContextFactory.TryCreateSpecializedDocumentQueryExecutionContextAsync(
                    cosmosQueryContext,
                    inputParameters,
                    partitionedQueryExecutionInfo,
                    targetRanges,
                    containerQueryProperties.ResourceId,
                    cancellationToken);
            }

            return await tryCreateContextTask;
        }

        private static Task<TryCatch<CosmosQueryExecutionContext>> TryCreatePassthroughQueryExecutionContextAsync(
            CosmosQueryContext cosmosQueryContext,
            InputParameters inputParameters,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            List<Documents.PartitionKeyRange> targetRanges,
            string collectionRid,
            CancellationToken cancellationToken)
        {
            CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams initParams = new CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams(
                sqlQuerySpec: inputParameters.SqlQuerySpec,
                collectionRid: collectionRid,
                partitionedQueryExecutionInfo: partitionedQueryExecutionInfo,
                partitionKeyRanges: targetRanges,
                initialPageSize: inputParameters.MaxItemCount,
                maxConcurrency: inputParameters.MaxConcurrency,
                maxItemCount: inputParameters.MaxItemCount,
                maxBufferedItemCount: inputParameters.MaxBufferedItemCount,
                returnResultsInDeterministicOrder: inputParameters.ReturnResultsInDeterministicOrder,
                testSettings: inputParameters.TestInjections);

            return PipelinedDocumentQueryExecutionContext.TryCreatePassthroughAsync(
                inputParameters.ExecutionEnvironment,
                cosmosQueryContext,
                initParams,
                inputParameters.InitialUserContinuationToken,
                cancellationToken);
        }

        private static async Task<TryCatch<CosmosQueryExecutionContext>> TryCreateSpecializedDocumentQueryExecutionContextAsync(
            CosmosQueryContext cosmosQueryContext,
            InputParameters inputParameters,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            List<Documents.PartitionKeyRange> targetRanges,
            string collectionRid,
            CancellationToken cancellationToken)
        {
            QueryInfo queryInfo = partitionedQueryExecutionInfo.QueryInfo;

            bool getLazyFeedResponse = queryInfo.HasTop;

            // We need to compute the optimal initial page size for order-by queries
            long optimalPageSize = inputParameters.MaxItemCount;
            if (queryInfo.HasOrderBy)
            {
                int top;
                if (queryInfo.HasTop && (top = partitionedQueryExecutionInfo.QueryInfo.Top.Value) > 0)
                {
                    // All partitions should initially fetch about 1/nth of the top value.
                    long pageSizeWithTop = (long)Math.Min(
                        Math.Ceiling(top / (double)targetRanges.Count) * CosmosQueryExecutionContextFactory.PageSizeFactorForTop,
                        top);

                    optimalPageSize = Math.Min(pageSizeWithTop, optimalPageSize);
                }
                else if (cosmosQueryContext.IsContinuationExpected)
                {
                    optimalPageSize = (long)Math.Min(
                        Math.Ceiling(optimalPageSize / (double)targetRanges.Count) * CosmosQueryExecutionContextFactory.PageSizeFactorForTop,
                        optimalPageSize);
                }
            }

            Debug.Assert(
                (optimalPageSize > 0) && (optimalPageSize <= int.MaxValue),
                $"Invalid MaxItemCount {optimalPageSize}");

            CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams initParams = new CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams(
                sqlQuerySpec: inputParameters.SqlQuerySpec,
                collectionRid: collectionRid,
                partitionedQueryExecutionInfo: partitionedQueryExecutionInfo,
                partitionKeyRanges: targetRanges,
                initialPageSize: (int)optimalPageSize,
                maxConcurrency: inputParameters.MaxConcurrency,
                maxItemCount: inputParameters.MaxItemCount,
                maxBufferedItemCount: inputParameters.MaxBufferedItemCount,
                returnResultsInDeterministicOrder: inputParameters.ReturnResultsInDeterministicOrder,
                testSettings: inputParameters.TestInjections);

            return await PipelinedDocumentQueryExecutionContext.TryCreateAsync(
                inputParameters.ExecutionEnvironment,
                cosmosQueryContext,
                initParams,
                inputParameters.InitialUserContinuationToken,
                cancellationToken);
        }

        /// <summary>
        /// Gets the list of partition key ranges. 
        /// 1. Check partition key range id
        /// 2. Check Partition key
        /// 3. Check the effective partition key
        /// 4. Get the range from the FeedToken
        /// 5. Get the range from the PartitionedQueryExecutionInfo
        /// </summary>
        internal static async Task<List<Documents.PartitionKeyRange>> GetTargetPartitionKeyRangesAsync(
            CosmosQueryClient queryClient,
            string resourceLink,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            ContainerQueryProperties containerQueryProperties,
            IReadOnlyDictionary<string, object> properties,
            FeedRangeInternal feedRangeInternal)
        {
            List<Documents.PartitionKeyRange> targetRanges;
            if (containerQueryProperties.EffectivePartitionKeyString != null)
            {
                targetRanges = await queryClient.GetTargetPartitionKeyRangesByEpkStringAsync(
                    resourceLink,
                    containerQueryProperties.ResourceId,
                    containerQueryProperties.EffectivePartitionKeyString);
            }
            else if (TryGetEpkProperty(properties, out string effectivePartitionKeyString))
            {
                targetRanges = await queryClient.GetTargetPartitionKeyRangesByEpkStringAsync(
                    resourceLink,
                    containerQueryProperties.ResourceId,
                    effectivePartitionKeyString);
            }
            else if (feedRangeInternal != null)
            {
                targetRanges = await queryClient.GetTargetPartitionKeyRangeByFeedRangeAsync(
                    resourceLink,
                    containerQueryProperties.ResourceId,
                    containerQueryProperties.PartitionKeyDefinition,
                    feedRangeInternal);
            }
            else
            {
                targetRanges = await queryClient.GetTargetPartitionKeyRangesAsync(
                    resourceLink,
                    containerQueryProperties.ResourceId,
                    partitionedQueryExecutionInfo.QueryRanges);
            }

            return targetRanges;
        }

        private static bool TryGetEpkProperty(
            IReadOnlyDictionary<string, object> properties,
            out string effectivePartitionKeyString)
        {
            if (properties != null
                && properties.TryGetValue(
                   Documents.WFConstants.BackendHeaders.EffectivePartitionKeyString,
                   out object effectivePartitionKeyStringObject))
            {
                effectivePartitionKeyString = effectivePartitionKeyStringObject as string;
                if (string.IsNullOrEmpty(effectivePartitionKeyString))
                {
                    throw new ArgumentOutOfRangeException(nameof(effectivePartitionKeyString));
                }

                return true;
            }

            effectivePartitionKeyString = null;
            return false;
        }

        private static Documents.PartitionKeyDefinition GetPartitionKeyDefinition(InputParameters inputParameters, ContainerQueryProperties containerQueryProperties)
        {
            //todo:elasticcollections this may rely on information from collection cache which is outdated
            //if collection is deleted/created with same name.
            //need to make it not rely on information from collection cache.

            Documents.PartitionKeyDefinition partitionKeyDefinition;
            if ((inputParameters.Properties != null)
                && inputParameters.Properties.TryGetValue(InternalPartitionKeyDefinitionProperty, out object partitionKeyDefinitionObject))
            {
                if (partitionKeyDefinitionObject is Documents.PartitionKeyDefinition definition)
                {
                    partitionKeyDefinition = definition;
                }
                else
                {
                    throw new ArgumentException(
                        "partitionkeydefinition has invalid type",
                        nameof(partitionKeyDefinitionObject));
                }
            }
            else
            {
                partitionKeyDefinition = containerQueryProperties.PartitionKeyDefinition;
            }

            return partitionKeyDefinition;
        }

        public sealed class InputParameters
        {
            private const int DefaultMaxConcurrency = 0;
            private const int DefaultMaxItemCount = 1000;
            private const int DefaultMaxBufferedItemCount = 1000;
            private const bool DefaultReturnResultsInDeterministicOrder = true;
            private const ExecutionEnvironment DefaultExecutionEnvironment = ExecutionEnvironment.Client;

            public InputParameters(
                SqlQuerySpec sqlQuerySpec,
                CosmosElement initialUserContinuationToken,
                FeedRangeInternal initialFeedRange,
                int? maxConcurrency,
                int? maxItemCount,
                int? maxBufferedItemCount,
                PartitionKey? partitionKey,
                IReadOnlyDictionary<string, object> properties,
                PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
                ExecutionEnvironment? executionEnvironment,
                bool? returnResultsInDeterministicOrder,
                bool forcePassthrough,
                TestInjections testInjections)
            {
                this.SqlQuerySpec = sqlQuerySpec ?? throw new ArgumentNullException(nameof(sqlQuerySpec));
                this.InitialUserContinuationToken = initialUserContinuationToken;
                this.InitialFeedRange = initialFeedRange;

                int resolvedMaxConcurrency = maxConcurrency.GetValueOrDefault(InputParameters.DefaultMaxConcurrency);
                if (resolvedMaxConcurrency < 0)
                {
                    resolvedMaxConcurrency = int.MaxValue;
                }
                this.MaxConcurrency = resolvedMaxConcurrency;

                int resolvedMaxItemCount = maxItemCount.GetValueOrDefault(InputParameters.DefaultMaxItemCount);
                if (resolvedMaxItemCount < 0)
                {
                    resolvedMaxItemCount = int.MaxValue;
                }
                this.MaxItemCount = resolvedMaxItemCount;

                int resolvedMaxBufferedItemCount = maxBufferedItemCount.GetValueOrDefault(InputParameters.DefaultMaxBufferedItemCount);
                if (resolvedMaxBufferedItemCount < 0)
                {
                    resolvedMaxBufferedItemCount = int.MaxValue;
                }
                this.MaxBufferedItemCount = resolvedMaxBufferedItemCount;

                this.PartitionKey = partitionKey;
                this.Properties = properties;
                this.PartitionedQueryExecutionInfo = partitionedQueryExecutionInfo;
                this.ExecutionEnvironment = executionEnvironment.GetValueOrDefault(InputParameters.DefaultExecutionEnvironment);
                this.ReturnResultsInDeterministicOrder = returnResultsInDeterministicOrder.GetValueOrDefault(InputParameters.DefaultReturnResultsInDeterministicOrder);
                this.ForcePassthrough = forcePassthrough;
                this.TestInjections = testInjections;
            }

            public SqlQuerySpec SqlQuerySpec { get; }
            public CosmosElement InitialUserContinuationToken { get; }
            public FeedRangeInternal InitialFeedRange { get; }
            public int MaxConcurrency { get; }
            public int MaxItemCount { get; }
            public int MaxBufferedItemCount { get; }
            public PartitionKey? PartitionKey { get; }
            public IReadOnlyDictionary<string, object> Properties { get; }
            public PartitionedQueryExecutionInfo PartitionedQueryExecutionInfo { get; }
            public ExecutionEnvironment ExecutionEnvironment { get; }
            public bool ReturnResultsInDeterministicOrder { get; }
            public TestInjections TestInjections { get; }
            public bool ForcePassthrough { get; }
        }

        internal sealed class AggregateProjectionDector
        {
            /// <summary>
            /// Determines whether or not the SqlSelectSpec has an aggregate in the outer most query.
            /// </summary>
            /// <param name="selectSpec">The select spec to traverse.</param>
            /// <returns>Whether or not the SqlSelectSpec has an aggregate in the outer most query.</returns>
            public static bool HasAggregate(SqlSelectSpec selectSpec)
            {
                return selectSpec.Accept(AggregateProjectionDectorVisitor.Singleton);
            }

            private sealed class AggregateProjectionDectorVisitor : SqlSelectSpecVisitor<bool>
            {
                public static readonly AggregateProjectionDectorVisitor Singleton = new AggregateProjectionDectorVisitor();

                public override bool Visit(SqlSelectListSpec selectSpec)
                {
                    bool hasAggregates = false;
                    foreach (SqlSelectItem selectItem in selectSpec.Items)
                    {
                        hasAggregates |= selectItem.Expression.Accept(AggregateScalarExpressionDetector.Singleton);
                    }

                    return hasAggregates;
                }

                public override bool Visit(SqlSelectValueSpec selectSpec)
                {
                    return selectSpec.Expression.Accept(AggregateScalarExpressionDetector.Singleton);
                }

                public override bool Visit(SqlSelectStarSpec selectSpec)
                {
                    return false;
                }

                /// <summary>
                /// Determines if there is an aggregate in a scalar expression.
                /// </summary>
                private sealed class AggregateScalarExpressionDetector : SqlScalarExpressionVisitor<bool>
                {
                    private enum Aggregate
                    {
                        Min,
                        Max,
                        Sum,
                        Count,
                        Avg,
                    }

                    public static readonly AggregateScalarExpressionDetector Singleton = new AggregateScalarExpressionDetector();

                    public override bool Visit(SqlArrayCreateScalarExpression sqlArrayCreateScalarExpression)
                    {
                        bool hasAggregates = false;
                        foreach (SqlScalarExpression item in sqlArrayCreateScalarExpression.Items)
                        {
                            hasAggregates |= item.Accept(this);
                        }

                        return hasAggregates;
                    }

                    public override bool Visit(SqlArrayScalarExpression sqlArrayScalarExpression)
                    {
                        // No need to worry about aggregates in the subquery (they will recursively get rewritten).
                        return false;
                    }

                    public override bool Visit(SqlBetweenScalarExpression sqlBetweenScalarExpression)
                    {
                        return sqlBetweenScalarExpression.Expression.Accept(this) ||
                            sqlBetweenScalarExpression.StartInclusive.Accept(this) ||
                            sqlBetweenScalarExpression.EndInclusive.Accept(this);
                    }

                    public override bool Visit(SqlBinaryScalarExpression sqlBinaryScalarExpression)
                    {
                        return sqlBinaryScalarExpression.LeftExpression.Accept(this) ||
                            sqlBinaryScalarExpression.RightExpression.Accept(this);
                    }

                    public override bool Visit(SqlCoalesceScalarExpression sqlCoalesceScalarExpression)
                    {
                        return sqlCoalesceScalarExpression.Left.Accept(this) ||
                            sqlCoalesceScalarExpression.Right.Accept(this);
                    }

                    public override bool Visit(SqlConditionalScalarExpression sqlConditionalScalarExpression)
                    {
                        return sqlConditionalScalarExpression.Condition.Accept(this) ||
                            sqlConditionalScalarExpression.Consequent.Accept(this) ||
                            sqlConditionalScalarExpression.Alternative.Accept(this);
                    }

                    public override bool Visit(SqlExistsScalarExpression sqlExistsScalarExpression)
                    {
                        // No need to worry about aggregates within the subquery (they will recursively get rewritten).
                        return false;
                    }

                    public override bool Visit(SqlFunctionCallScalarExpression sqlFunctionCallScalarExpression)
                    {
                        return !sqlFunctionCallScalarExpression.IsUdf &&
                            Enum.TryParse<Aggregate>(value: sqlFunctionCallScalarExpression.Name.Value, ignoreCase: true, result: out _);
                    }

                    public override bool Visit(SqlInScalarExpression sqlInScalarExpression)
                    {
                        bool hasAggregates = false;
                        for (int i = 0; i < sqlInScalarExpression.Haystack.Count; i++)
                        {
                            hasAggregates |= sqlInScalarExpression.Haystack[i].Accept(this);
                        }

                        return hasAggregates;
                    }

                    public override bool Visit(SqlLiteralScalarExpression sqlLiteralScalarExpression)
                    {
                        return false;
                    }

                    public override bool Visit(SqlMemberIndexerScalarExpression sqlMemberIndexerScalarExpression)
                    {
                        return sqlMemberIndexerScalarExpression.Member.Accept(this) ||
                            sqlMemberIndexerScalarExpression.Indexer.Accept(this);
                    }

                    public override bool Visit(SqlObjectCreateScalarExpression sqlObjectCreateScalarExpression)
                    {
                        bool hasAggregates = false;
                        foreach (SqlObjectProperty property in sqlObjectCreateScalarExpression.Properties)
                        {
                            hasAggregates |= property.Value.Accept(this);
                        }

                        return hasAggregates;
                    }

                    public override bool Visit(SqlPropertyRefScalarExpression sqlPropertyRefScalarExpression)
                    {
                        bool hasAggregates = false;
                        if (sqlPropertyRefScalarExpression.Member != null)
                        {
                            hasAggregates = sqlPropertyRefScalarExpression.Member.Accept(this);
                        }

                        return hasAggregates;
                    }

                    public override bool Visit(SqlSubqueryScalarExpression sqlSubqueryScalarExpression)
                    {
                        // No need to worry about the aggregates within the subquery since they get recursively evaluated.
                        return false;
                    }

                    public override bool Visit(SqlUnaryScalarExpression sqlUnaryScalarExpression)
                    {
                        return sqlUnaryScalarExpression.Expression.Accept(this);
                    }

                    public override bool Visit(SqlParameterRefScalarExpression scalarExpression)
                    {
                        return false;
                    }
                }
            }
        }
    }
}
