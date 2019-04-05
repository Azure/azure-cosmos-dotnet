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
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.ParallelQuery;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Factory class for creating the appropriate DocumentQueryExecutionContext for the provided type of query.
    /// </summary>
    internal class CosmosQueryExecutionContextFactory : IDocumentQueryExecutionContext
    {
        private IDocumentQueryExecutionContext innerExecutionContext;
        private CosmosQueryContext cosmosQueryContext;

        private const int PageSizeFactorForTop = 5;

        public bool IsDone => this.innerExecutionContext == null ? false : this.innerExecutionContext.IsDone;

        public CosmosQueryExecutionContextFactory(
            CosmosQueries client,
            ResourceType resourceTypeEnum,
            OperationType operationType,
            Type resourceType,
            SqlQuerySpec sqlQuerySpec,
            CosmosQueryRequestOptions queryRequestOptions,
            Uri resourceLink,
            bool isContinuationExpected,
            Guid correlatedActivityId)
        {
            this.cosmosQueryContext = new CosmosQueryContext(
                  client: client,
                  resourceTypeEnum: resourceTypeEnum,
                  operationType: operationType,
                  resourceType: resourceType,
                  sqlQuerySpecFromUser: sqlQuerySpec,
                  queryRequestOptions: queryRequestOptions,
                  resourceLink: resourceLink,
                  getLazyFeedResponse: isContinuationExpected,
                  isContinuationExpected: isContinuationExpected,
                  correlatedActivityId: correlatedActivityId);
        }

        private async Task<IDocumentQueryExecutionContext> CreateItemQueryExecutionContextAsync(CancellationToken cancellationToken)
        {
            CosmosContainerSettings collection = null;
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
                    collection = await collectionCache.ResolveCollectionAsync(request, cancellationToken);
                }
            }

            this.cosmosQueryContext.ContainerResourceId = collection.ResourceId;

            // For non-Windows platforms(like Linux and OSX) in .NET Core SDK, we cannot use ServiceInterop, so need to bypass in that case.
            // We are also now bypassing this for 32 bit host process running even on Windows as there are many 32 bit apps that will not work without this
            if (CustomTypeExtensions.ByPassQueryParsing())
            {
                // We create a ProxyDocumentQueryExecutionContext that will be initialized with DefaultDocumentQueryExecutionContext
                // which will be used to send the query to Gateway and on getting 400(bad request) with 1004(cross partition query not servable), we initialize it with
                // PipelinedDocumentQueryExecutionContext by providing the partition query execution info that's needed(which we get from the exception returned from Gateway).
                CosmosProxyItemQueryExecutionContext proxyQueryExecutionContext =
                    CosmosProxyItemQueryExecutionContext.CreateAsync(
                        queryContext: cosmosQueryContext,
                        token: cancellationToken,
                        collection: collection);

                return proxyQueryExecutionContext;
            }

            //todo:elasticcollections this may rely on information from collection cache which is outdated
            //if collection is deleted/created with same name.
            //need to make it not rely on information from collection cache.
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo = await GetPartitionedQueryExecutionInfoAsync(
                cosmosQueryContext.QueryClient,
                cosmosQueryContext.SqlQuerySpecFromUser,
                collection.PartitionKey,
                true,
                true,
                cancellationToken);

            List<PartitionKeyRange> targetRanges = await GetTargetPartitionKeyRanges(
                cosmosQueryContext.QueryClient,
                cosmosQueryContext.ResourceLink.OriginalString,
                partitionedQueryExecutionInfo,
                collection,
                cosmosQueryContext.QueryRequestOptions);

            return await CreateSpecializedDocumentQueryExecutionContext(
                cosmosQueryContext,
                partitionedQueryExecutionInfo,
                targetRanges,
                collection.ResourceId,
                cancellationToken);
        }

        public static async Task<IDocumentQueryExecutionContext> CreateSpecializedDocumentQueryExecutionContext(
            CosmosQueryContext constructorParams,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            List<PartitionKeyRange> targetRanges,
            string collectionRid,
            CancellationToken cancellationToken)
        {
            // Figure out the optimal page size.
            long initialPageSize = constructorParams.QueryRequestOptions.MaxItemCount.GetValueOrDefault(ParallelQueryConfig.GetConfig().ClientInternalPageSize);

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
                else if (constructorParams.IsContinuationExpected)
                {
                    if (initialPageSize < 0)
                    {
                        // Max of what the user is willing to buffer and the default (note this is broken if MaxBufferedItemCount = -1)
                        initialPageSize = Math.Max(constructorParams.QueryRequestOptions.MaxBufferedItemCount, ParallelQueryConfig.GetConfig().DefaultMaximumBufferSize);
                    }

                    initialPageSize = (long)Math.Min(
                        Math.Ceiling(initialPageSize / (double)targetRanges.Count) * PageSizeFactorForTop,
                        initialPageSize);
                }
            }

            Debug.Assert(initialPageSize > 0 && initialPageSize <= int.MaxValue,
                string.Format(CultureInfo.InvariantCulture, "Invalid MaxItemCount {0}", initialPageSize));

            return await CosmosPipelinedItemQueryExecutionContext.CreateAsync(
                constructorParams,
                collectionRid,
                partitionedQueryExecutionInfo,
                targetRanges,
                (int)initialPageSize,
                constructorParams.QueryRequestOptions.RequestContinuation,
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
            CosmosQueries queryClient,
            string resourceLink,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            CosmosContainerSettings collection,
            CosmosQueryRequestOptions queryRequestOptions)
        {
            List<PartitionKeyRange> targetRanges = null;
            if (queryRequestOptions.PartitionKey != null)
            {
                targetRanges = await queryClient.GetTargetPartitionKeyRangesByEpkString(
                    resourceLink,
                    collection.ResourceId,
                    queryRequestOptions.PartitionKey.InternalKey.GetEffectivePartitionKeyString(collection.PartitionKey));
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

        public static async Task<PartitionedQueryExecutionInfo> GetPartitionedQueryExecutionInfoAsync(
            CosmosQueries queryClient,
            SqlQuerySpec sqlQuerySpec,
            PartitionKeyDefinition partitionKeyDefinition,
            bool requireFormattableOrderByQuery,
            bool isContinuationExpected,
            CancellationToken cancellationToken)
        {
            // $ISSUE-felixfan-2016-07-13: We should probably get PartitionedQueryExecutionInfo from Gateway in GatewayMode

            QueryPartitionProvider queryPartitionProvider = await queryClient.GetQueryPartitionProviderAsync(cancellationToken);
            return queryPartitionProvider.GetPartitionedQueryExecutionInfo(sqlQuerySpec, partitionKeyDefinition, requireFormattableOrderByQuery, isContinuationExpected);
        }

        private static bool IsTopOrderByQuery(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            return (partitionedQueryExecutionInfo.QueryInfo != null)
                && (partitionedQueryExecutionInfo.QueryInfo.HasOrderBy || partitionedQueryExecutionInfo.QueryInfo.HasTop);
        }

        private static bool IsAggregateQuery(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            return (partitionedQueryExecutionInfo.QueryInfo != null)
                && (partitionedQueryExecutionInfo.QueryInfo.HasAggregates);
        }

        private static bool IsAggregateQueryWithoutContinuation(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo, bool isContinuationExpected)
        {
            return IsAggregateQuery(partitionedQueryExecutionInfo) && !isContinuationExpected;
        }

        private static bool IsDistinctQuery(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            return partitionedQueryExecutionInfo.QueryInfo.HasDistinct;
        }

        private static bool IsParallelQuery(CosmosQueryRequestOptions feedOptions)
        {
            return (feedOptions.MaxConcurrency != 0);
        }

        private static bool IsOffsetLimitQuery(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            return partitionedQueryExecutionInfo.QueryInfo.HasOffset && partitionedQueryExecutionInfo.QueryInfo.HasLimit;
        }

        private static bool TryGetEpkProperty(
            CosmosQueryRequestOptions queryRequestOptions,
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

        public async Task<FeedResponse<CosmosElement>> ExecuteNextAsync(CancellationToken token)
        {
            if(this.innerExecutionContext == null)
            {
                this.innerExecutionContext = await this.CreateItemQueryExecutionContextAsync(token);
            }

            return await this.innerExecutionContext.ExecuteNextAsync(token);
        }

        public void Dispose()
        {
            if (this.innerExecutionContext != null)
            {
                this.innerExecutionContext.Dispose();
            }
        }
    }
}
