﻿//-----------------------------------------------------------------------
// <copyright file="CosmosQueryExecutionContextFactory.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Query.ParallelQuery;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Factory class for creating the appropriate DocumentQueryExecutionContext for the provided type of query.
    /// </summary>
    internal sealed class CosmosQueryExecutionContextFactory : CosmosQueryExecutionContext
    {
        internal const string InternalPartitionKeyDefinitionProperty = "x-ms-query-partitionkey-definition";
        private const int PageSizeFactorForTop = 5;

        private readonly CosmosQueryContext cosmosQueryContext;
        private CosmosQueryExecutionContext innerExecutionContext;

        /// <summary>
        /// Test flag for making the query use the opposite code path for query plan retrieval.
        /// If the SDK would have went to Gateway, then it will use ServiceInterop and visa versa.
        /// </summary>
        public static bool TestFlag = true;

        public CosmosQueryExecutionContextFactory(
            CosmosQueryClient client,
            ResourceType resourceTypeEnum,
            OperationType operationType,
            Type resourceType,
            SqlQuerySpec sqlQuerySpec,
            QueryRequestOptions queryRequestOptions,
            Uri resourceLink,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            Guid correlatedActivityId)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (sqlQuerySpec == null)
            {
                throw new ArgumentNullException(nameof(sqlQuerySpec));
            }

            if (queryRequestOptions == null)
            {
                throw new ArgumentNullException(nameof(queryRequestOptions));
            }

            if (resourceLink == null)
            {
                throw new ArgumentNullException(nameof(resourceLink));
            }

            // Prevent users from updating the values after creating the execution context.
            QueryRequestOptions cloneQueryRequestOptions = queryRequestOptions.Clone();

            // Swapping out negative values in feedOptions for int.MaxValue
            if (cloneQueryRequestOptions.MaxBufferedItemCount.HasValue && cloneQueryRequestOptions.MaxBufferedItemCount < 0)
            {
                cloneQueryRequestOptions.MaxBufferedItemCount = int.MaxValue;
            }

            if (cloneQueryRequestOptions.MaxConcurrency.HasValue && cloneQueryRequestOptions.MaxConcurrency < 0)
            {
                cloneQueryRequestOptions.MaxConcurrency = int.MaxValue;
            }

            if (cloneQueryRequestOptions.MaxItemCount.HasValue && cloneQueryRequestOptions.MaxItemCount < 0)
            {
                cloneQueryRequestOptions.MaxItemCount = int.MaxValue;
            }

            this.cosmosQueryContext = new CosmosQueryContext(
                  client: client,
                  resourceTypeEnum: resourceTypeEnum,
                  operationType: operationType,
                  resourceType: resourceType,
                  sqlQuerySpecFromUser: sqlQuerySpec,
                  queryRequestOptions: cloneQueryRequestOptions,
                  resourceLink: resourceLink,
                  getLazyFeedResponse: isContinuationExpected,
                  isContinuationExpected: isContinuationExpected,
                  allowNonValueAggregateQuery: allowNonValueAggregateQuery,
                  correlatedActivityId: correlatedActivityId);
        }

        public override bool IsDone
        {
            get
            {
                return this.innerExecutionContext != null ? this.innerExecutionContext.IsDone : false;
            }
        }

        public override async Task<QueryResponse> ExecuteNextAsync(CancellationToken token)
        {
            if(this.innerExecutionContext == null)
            {
                this.innerExecutionContext = await this.CreateItemQueryExecutionContextAsync(token);
            }

            QueryResponse response = await this.innerExecutionContext.ExecuteNextAsync(token);
            response.CosmosSerializationOptions = this.cosmosQueryContext.QueryRequestOptions.CosmosSerializationOptions;

            return response;
        }

        public override void Dispose()
        {
            if (this.innerExecutionContext != null)
            {
                this.innerExecutionContext.Dispose();
            }
        }

        private async Task<CosmosContainerSettings> GetContainerSettingsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CosmosContainerSettings containerSettings = null;
            if (this.cosmosQueryContext.ResourceTypeEnum.IsCollectionChild())
            {
                CollectionCache collectionCache = await this.cosmosQueryContext.QueryClient.GetCollectionCacheAsync();
                using (
                    DocumentServiceRequest request = DocumentServiceRequest.Create(
                        OperationType.Query,
                        this.cosmosQueryContext.ResourceTypeEnum,
                        this.cosmosQueryContext.ResourceLink.OriginalString,
                        AuthorizationTokenType.Invalid)) //this request doesn't actually go to server
                {
                    containerSettings = await collectionCache.ResolveCollectionAsync(request, cancellationToken);
                }
            }

            if (containerSettings == null)
            {
                throw new ArgumentException($"The container was not found for resource: {this.cosmosQueryContext.ResourceLink.OriginalString} ");
            }

            return containerSettings;
        }

        private async Task<CosmosQueryExecutionContext> CreateItemQueryExecutionContextAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CosmosContainerSettings containerSettings = await this.GetContainerSettingsAsync(cancellationToken);
            this.cosmosQueryContext.ContainerResourceId = containerSettings.ResourceId;

            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo;
            if (this.cosmosQueryContext.QueryClient.ByPassQueryParsing() && TestFlag)
            {
                // For non-Windows platforms(like Linux and OSX) in .NET Core SDK, we cannot use ServiceInterop, so need to bypass in that case.
                // We are also now bypassing this for 32 bit host process running even on Windows as there are many 32 bit apps that will not work without this
                partitionedQueryExecutionInfo = await QueryPlanRetriever.GetQueryPlanThroughGatewayAsync(
                    this.cosmosQueryContext.QueryClient,
                    this.cosmosQueryContext.SqlQuerySpec,
                    this.cosmosQueryContext.ResourceLink,
                    cancellationToken);
            }
            else
            {
                //todo:elasticcollections this may rely on information from collection cache which is outdated
                //if collection is deleted/created with same name.
                //need to make it not rely on information from collection cache.
                PartitionKeyDefinition partitionKeyDefinition;
                object partitionKeyDefinitionObject;
                if (this.cosmosQueryContext.QueryRequestOptions?.Properties != null
                    && this.cosmosQueryContext.QueryRequestOptions.Properties.TryGetValue(InternalPartitionKeyDefinitionProperty, out partitionKeyDefinitionObject))
                {
                    if (partitionKeyDefinitionObject is PartitionKeyDefinition definition)
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
                    partitionKeyDefinition = containerSettings.PartitionKey;
                }

                partitionedQueryExecutionInfo = await QueryPlanRetriever.GetQueryPlanWithServiceInteropAsync(
                    this.cosmosQueryContext.QueryClient,
                    this.cosmosQueryContext.SqlQuerySpec,
                    partitionKeyDefinition,
                    cancellationToken);
            }

            List<PartitionKeyRange> targetRanges = await GetTargetPartitionKeyRanges(
                   this.cosmosQueryContext.QueryClient,
                   this.cosmosQueryContext.ResourceLink.OriginalString,
                   partitionedQueryExecutionInfo,
                   containerSettings,
                   this.cosmosQueryContext.QueryRequestOptions);

            CosmosQueryContext rewrittenComosQueryContext;
            if(!string.IsNullOrEmpty(partitionedQueryExecutionInfo.QueryInfo.RewrittenQuery))
            {
                // We need pass down the rewritten query.
                SqlQuerySpec rewrittenQuerySpec = new SqlQuerySpec()
                {
                    QueryText = partitionedQueryExecutionInfo.QueryInfo.RewrittenQuery,
                    Parameters = this.cosmosQueryContext.SqlQuerySpec.Parameters
                };

                rewrittenComosQueryContext = new CosmosQueryContext(
                    this.cosmosQueryContext.QueryClient,
                    this.cosmosQueryContext.ResourceTypeEnum,
                    this.cosmosQueryContext.OperationTypeEnum,
                    this.cosmosQueryContext.ResourceType,
                    rewrittenQuerySpec,
                    this.cosmosQueryContext.QueryRequestOptions,
                    this.cosmosQueryContext.ResourceLink,
                    this.cosmosQueryContext.IsContinuationExpected,
                    this.cosmosQueryContext.CorrelatedActivityId,
                    this.cosmosQueryContext.IsContinuationExpected,
                    this.cosmosQueryContext.AllowNonValueAggregateQuery,
                    this.cosmosQueryContext.ContainerResourceId);
            }
            else
            {
                rewrittenComosQueryContext = this.cosmosQueryContext;
            }

            return await CreateSpecializedDocumentQueryExecutionContext(
                rewrittenComosQueryContext,
                partitionedQueryExecutionInfo,
                targetRanges,
                containerSettings.ResourceId,
                cancellationToken);
        }

        public static async Task<CosmosQueryExecutionContext> CreateSpecializedDocumentQueryExecutionContext(
            CosmosQueryContext cosmosQueryContext,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            List<PartitionKeyRange> targetRanges,
            string collectionRid,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(partitionedQueryExecutionInfo.QueryInfo?.RewrittenQuery))
            {
                cosmosQueryContext.SqlQuerySpec = new SqlQuerySpec(
                    partitionedQueryExecutionInfo.QueryInfo.RewrittenQuery,
                    cosmosQueryContext.SqlQuerySpec.Parameters);
            }

            // Figure out the optimal page size.
            long initialPageSize = cosmosQueryContext.QueryRequestOptions.MaxItemCount.GetValueOrDefault(ParallelQueryConfig.GetConfig().ClientInternalPageSize);

            if (initialPageSize < -1 || initialPageSize == 0)
            {
                throw new BadRequestException(string.Format(CultureInfo.InvariantCulture, "Invalid MaxItemCount {0}", initialPageSize));
            }

            QueryInfo queryInfo = partitionedQueryExecutionInfo.QueryInfo;

            bool getLazyFeedResponse = queryInfo.HasTop;

            // We need to compute the optimal initial page size for order-by queries
            if (queryInfo.HasOrderBy)
            {
                int top;
                if (queryInfo.HasTop && (top = partitionedQueryExecutionInfo.QueryInfo.Top.Value) > 0)
                {
                    // All partitions should initially fetch about 1/nth of the top value.
                    long pageSizeWithTop = (long)Math.Min(
                        Math.Ceiling(top / (double)targetRanges.Count) * PageSizeFactorForTop,
                        top);

                    if (initialPageSize > 0)
                    {
                        initialPageSize = Math.Min(pageSizeWithTop, initialPageSize);
                    }
                    else
                    {
                        initialPageSize = pageSizeWithTop;
                    }
                }
                else if (cosmosQueryContext.IsContinuationExpected)
                {
                    if (initialPageSize < 0)
                    {
                        if (cosmosQueryContext.QueryRequestOptions.MaxBufferedItemCount.HasValue)
                        {
                            // Max of what the user is willing to buffer and the default (note this is broken if MaxBufferedItemCount = -1)
                            initialPageSize = Math.Max(cosmosQueryContext.QueryRequestOptions.MaxBufferedItemCount.Value, ParallelQueryConfig.GetConfig().DefaultMaximumBufferSize);
                        }
                        else
                        {
                            initialPageSize = ParallelQueryConfig.GetConfig().DefaultMaximumBufferSize;
                        }
                    }

                    initialPageSize = (long)Math.Min(
                        Math.Ceiling(initialPageSize / (double)targetRanges.Count) * PageSizeFactorForTop,
                        initialPageSize);
                }
            }

            Debug.Assert(initialPageSize > 0 && initialPageSize <= int.MaxValue,
                string.Format(CultureInfo.InvariantCulture, "Invalid MaxItemCount {0}", initialPageSize));

            return await PipelinedDocumentQueryExecutionContext.CreateAsync(
                cosmosQueryContext,
                collectionRid,
                partitionedQueryExecutionInfo,
                targetRanges,
                (int)initialPageSize,
                cosmosQueryContext.QueryRequestOptions.RequestContinuation,
                cancellationToken);
        }

        /// <summary>
        /// Gets the list of partition key ranges. 
        /// 1. Check partition key range id
        /// 2. Check Partition key
        /// 3. Check the effective partition key
        /// 4. Get the range from the PartitionedQueryExecutionInfo
        /// </summary>
        internal static async Task<List<PartitionKeyRange>> GetTargetPartitionKeyRanges(
            CosmosQueryClient queryClient,
            string resourceLink,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            CosmosContainerSettings collection,
            QueryRequestOptions queryRequestOptions)
        {
            List<PartitionKeyRange> targetRanges;
            if (queryRequestOptions.PartitionKey != null)
            {
                // Dis-ambiguate the NonePK if used 
                PartitionKeyInternal partitionKeyInternal = null;
                if (Object.ReferenceEquals(queryRequestOptions.PartitionKey, CosmosContainerSettings.NonePartitionKeyValue))
                {
                    partitionKeyInternal = collection.GetNoneValue();
                }
                else
                {
                    partitionKeyInternal = new PartitionKey(queryRequestOptions.PartitionKey).InternalKey;
                }

                targetRanges = await queryClient.GetTargetPartitionKeyRangesByEpkString(
                    resourceLink,
                    collection.ResourceId,
                    partitionKeyInternal.GetEffectivePartitionKeyString(collection.PartitionKey));
            }
            else if (TryGetEpkProperty(queryRequestOptions, out string effectivePartitionKeyString))
            {
                targetRanges = await queryClient.GetTargetPartitionKeyRangesByEpkString(
                    resourceLink,
                    collection.ResourceId,
                    effectivePartitionKeyString);
            }
            else
            {
                targetRanges = await queryClient.GetTargetPartitionKeyRanges(
                    resourceLink,
                    collection.ResourceId,
                    partitionedQueryExecutionInfo.QueryRanges);
            }

            return targetRanges;
        }

        public static Task<PartitionedQueryExecutionInfo> GetPartitionedQueryExecutionInfoAsync(
            CosmosQueryClient queryClient,
            SqlQuerySpec sqlQuerySpec,
            PartitionKeyDefinition partitionKeyDefinition,
            bool requireFormattableOrderByQuery,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            CancellationToken cancellationToken)
        {
            // $ISSUE-felixfan-2016-07-13: We should probably get PartitionedQueryExecutionInfo from Gateway in GatewayMode

            return queryClient.GetPartitionedQueryExecutionInfoAsync(
                sqlQuerySpec,
                partitionKeyDefinition,
                requireFormattableOrderByQuery,
                isContinuationExpected,
                allowNonValueAggregateQuery,
                cancellationToken);
        }

        private static bool TryGetEpkProperty(
            QueryRequestOptions queryRequestOptions,
            out string effectivePartitionKeyString)
        {
            if (queryRequestOptions?.Properties != null
                && queryRequestOptions.Properties.TryGetValue(
                   WFConstants.BackendHeaders.EffectivePartitionKeyString,
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
    }
}
