﻿//-----------------------------------------------------------------------
// <copyright file="DefaultDocumentQueryExecutionContext.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;

    /// <summary>
    /// Default document query execution context for single partition queries or for split proofing general requests.
    /// </summary>
    internal sealed class DefaultDocumentQueryExecutionContext : DocumentQueryExecutionContextBase
    {
        // For a single partition collection the only partition is 0
        private const string SinglePartitionKeyId = "0";

        /// <summary>
        /// Whether or not a continuation is expected.
        /// </summary>
        private readonly bool isContinuationExpected;
        private readonly SchedulingStopwatch fetchSchedulingMetrics;
        private readonly FetchExecutionRangeAccumulator fetchExecutionRangeAccumulator;
        private readonly IDictionary<string, IReadOnlyList<Range<string>>> providedRangesCache;
        private long retries;
        private readonly PartitionRoutingHelper partitionRoutingHelper;

        public DefaultDocumentQueryExecutionContext(
            DocumentQueryExecutionContextBase.InitParams constructorParams,
            bool isContinuationExpected) :
            base(constructorParams)
        {
            this.isContinuationExpected = isContinuationExpected;
            this.fetchSchedulingMetrics = new SchedulingStopwatch();
            this.fetchSchedulingMetrics.Ready();
            this.fetchExecutionRangeAccumulator = new FetchExecutionRangeAccumulator(SinglePartitionKeyId);
            this.providedRangesCache = new Dictionary<string, IReadOnlyList<Range<string>>>();
            this.retries = -1;
            this.partitionRoutingHelper = new PartitionRoutingHelper();
        }

        public static Task<DefaultDocumentQueryExecutionContext> CreateAsync(
            DocumentQueryExecutionContextBase.InitParams constructorParams,
            bool isContinuationExpected,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(new DefaultDocumentQueryExecutionContext(
                constructorParams,
                isContinuationExpected));
        }

        public override void Dispose()
        {
        }

        protected override async Task<FeedResponse<CosmosElement>> ExecuteInternalAsync(CancellationToken token)
        {
            CollectionCache collectionCache = await this.Client.GetCollectionCacheAsync();
            PartitionKeyRangeCache partitionKeyRangeCache = await this.Client.GetPartitionKeyRangeCache();
            IDocumentClientRetryPolicy retryPolicyInstance = this.Client.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            retryPolicyInstance = new InvalidPartitionExceptionRetryPolicy(collectionCache, retryPolicyInstance);
            if (base.ResourceTypeEnum.IsPartitioned())
            {
                retryPolicyInstance = new PartitionKeyRangeGoneRetryPolicy(
                    collectionCache,
                    partitionKeyRangeCache,
                    PathsHelper.GetCollectionPath(base.ResourceLink),
                    retryPolicyInstance);
            }

            return await BackoffRetryUtility<FeedResponse<CosmosElement>>.ExecuteAsync(
                async () =>
                {
                    this.fetchExecutionRangeAccumulator.BeginFetchRange();
                    ++this.retries;
                    FeedResponse<CosmosElement> response = await this.ExecuteOnceAsync(retryPolicyInstance, token);
                    if (!string.IsNullOrEmpty(response.ResponseHeaders[HttpConstants.HttpHeaders.QueryMetrics]))
                    {
                        this.fetchExecutionRangeAccumulator.EndFetchRange(
                            response.ActivityId, 
                            response.Count, 
                            this.retries);
                        response = new FeedResponse<CosmosElement>(
                            response,
                            response.Count,
                            response.Headers,
                            response.UseETagAsContinuation,
                            new Dictionary<string, QueryMetrics>
                            {
                                {
                                    SinglePartitionKeyId, 
                                    QueryMetrics.CreateFromDelimitedStringAndClientSideMetrics(
                                        response.ResponseHeaders[HttpConstants.HttpHeaders.QueryMetrics],
                                        new ClientSideMetrics(
                                            this.retries,
                                            response.RequestCharge,
                                            this.fetchExecutionRangeAccumulator.GetExecutionRanges(),
                                            string.IsNullOrEmpty(response.ResponseContinuation) ? new List<Tuple<string, SchedulingTimeSpan>>()
                                            {
                                                new Tuple<string, SchedulingTimeSpan>(SinglePartitionKeyId, this.fetchSchedulingMetrics.Elapsed)
                                            } : new List<Tuple<string, SchedulingTimeSpan>>()))
                                }
                            },
                            response.RequestStatistics,
                            response.DisallowContinuationTokenMessage,
                            response.ResponseLengthBytes);
                    }

                    this.retries = -1;
                    return response;
                },
                retryPolicyInstance,
                token);
        }

        private async Task<FeedResponse<CosmosElement>> ExecuteOnceAsync(IDocumentClientRetryPolicy retryPolicyInstance, CancellationToken cancellationToken)
        {
            // Don't reuse request, as the rest of client SDK doesn't reuse requests between retries.
            // The code leaves some temporary garbage in request (in RequestContext etc.),
            // which shold be erased during retries.
            using (DocumentServiceRequest request = await this.CreateRequestAsync())
            {
                if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.PartitionKey])
                    || !request.ResourceType.IsPartitioned())
                {
                    return await this.ExecuteRequestAsync(request, cancellationToken);
                }

                CollectionCache collectionCache = await this.Client.GetCollectionCacheAsync();

                CosmosContainerSettings collection =
                    await collectionCache.ResolveCollectionAsync(request, CancellationToken.None);

                if (!string.IsNullOrEmpty(base.PartitionKeyRangeId))
                {
                    request.RouteTo(new PartitionKeyRangeIdentity(collection.ResourceId, base.PartitionKeyRangeId));
                    return await this.ExecuteRequestAsync(request, cancellationToken);
                }

                // For non-Windows platforms(like Linux and OSX) in .NET Core SDK, we cannot use ServiceInterop for parsing the query, 
                // so forcing the request through Gateway. We are also now by-passing this for 32-bit host process in NETFX on Windows
                // as the ServiceInterop dll is only available in 64-bit.
                if (CustomTypeExtensions.ByPassQueryParsing())
                {
                    request.UseGatewayMode = true;
                    return await this.ExecuteRequestAsync(request, cancellationToken);
                }

                QueryPartitionProvider queryPartitionProvider = await this.Client.GetQueryPartitionProviderAsync(cancellationToken);
                IRoutingMapProvider routingMapProvider = await this.Client.GetRoutingMapProviderAsync();

                List<CompositeContinuationToken> suppliedTokens;
                Range<string> rangeFromContinuationToken =
                    this.partitionRoutingHelper.ExtractPartitionKeyRangeFromContinuationToken(request.Headers, out suppliedTokens);
                Tuple<PartitionRoutingHelper.ResolvedRangeInfo, IReadOnlyList<Range<string>>> queryRoutingInfo =
                    await this.TryGetTargetPartitionKeyRangeAsync(
                        request,
                        collection,
                        queryPartitionProvider,
                        routingMapProvider,
                        rangeFromContinuationToken,
                        suppliedTokens);

                if (request.IsNameBased && queryRoutingInfo == null)
                {
                    request.ForceNameCacheRefresh = true;
                    collection = await collectionCache.ResolveCollectionAsync(request, CancellationToken.None);
                    queryRoutingInfo = await this.TryGetTargetPartitionKeyRangeAsync(
                        request,
                        collection,
                        queryPartitionProvider,
                        routingMapProvider,
                        rangeFromContinuationToken,
                        suppliedTokens);
                }

                if (queryRoutingInfo == null)
                {
                    throw new NotFoundException($"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}: Was not able to get queryRoutingInfo even after resolve collection async with force name cache refresh to the following collectionRid: {collection.ResourceId} with the supplied tokens: {JsonConvert.SerializeObject(suppliedTokens)}");
                }

                request.RouteTo(new PartitionKeyRangeIdentity(collection.ResourceId, queryRoutingInfo.Item1.ResolvedRange.Id));

                FeedResponse<CosmosElement> response = await this.ExecuteRequestLazyAsync(request, cancellationToken);

                if (!await this.partitionRoutingHelper.TryAddPartitionKeyRangeToContinuationTokenAsync(
                    response.Headers,
                    providedPartitionKeyRanges: queryRoutingInfo.Item2,
                    routingMapProvider: routingMapProvider,
                    collectionRid: collection.ResourceId,
                    resolvedRangeInfo: queryRoutingInfo.Item1))
                {
                    // Collection to which this request was resolved doesn't exist.
                    // Retry policy will refresh the cache and return NotFound.
                    throw new NotFoundException($"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}: Call to TryAddPartitionKeyRangeToContinuationTokenAsync failed to the following collectionRid: {collection.ResourceId} with the supplied tokens: {JsonConvert.SerializeObject(suppliedTokens)}");
                }

                return response;
            }
        }

        private async Task<Tuple<PartitionRoutingHelper.ResolvedRangeInfo, IReadOnlyList<Range<string>>>> TryGetTargetPartitionKeyRangeAsync(
           DocumentServiceRequest request,
           CosmosContainerSettings collection,
           QueryPartitionProvider queryPartitionProvider,
           IRoutingMapProvider routingMapProvider,
           Range<string> rangeFromContinuationToken,
           List<CompositeContinuationToken> suppliedTokens)
        {
            string version = request.Headers[HttpConstants.HttpHeaders.Version];
            version = string.IsNullOrEmpty(version) ? HttpConstants.Versions.CurrentVersion : version;

            bool enableCrossPartitionQuery = false;

            string enableCrossPartitionQueryHeader = request.Headers[HttpConstants.HttpHeaders.EnableCrossPartitionQuery];
            if (enableCrossPartitionQueryHeader != null)
            {
                if (!bool.TryParse(enableCrossPartitionQueryHeader, out enableCrossPartitionQuery))
                {
                    throw new BadRequestException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            RMResources.InvalidHeaderValue,
                            enableCrossPartitionQueryHeader,
                            HttpConstants.HttpHeaders.EnableCrossPartitionQuery));
                }
            }

            IReadOnlyList<Range<string>> providedRanges;
            if (!this.providedRangesCache.TryGetValue(collection.ResourceId, out providedRanges))
            {
                if (this.ShouldExecuteQueryRequest)
                {
                    QueryInfo queryInfo;
                    providedRanges = PartitionRoutingHelper.GetProvidedPartitionKeyRanges(
                        this.QuerySpec,
                        enableCrossPartitionQuery,
                        false,
                        isContinuationExpected,
                        collection.PartitionKey,
                        queryPartitionProvider,
                        version,
                        out queryInfo);
                }
                else
                {
                    providedRanges = new List<Range<string>>
                    {
                        new Range<string>(
                            PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                            PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                            true,
                            false)
                    };
                }

                this.providedRangesCache[collection.ResourceId] = providedRanges;
            }

            PartitionRoutingHelper.ResolvedRangeInfo resolvedRangeInfo = await this.partitionRoutingHelper.TryGetTargetRangeFromContinuationTokenRange(
                    providedRanges,
                    routingMapProvider,
                    collection.ResourceId,
                    rangeFromContinuationToken,
                    suppliedTokens);

            if (resolvedRangeInfo.ResolvedRange == null)
            {
                return null;
            }
            else
            {
                return Tuple.Create(resolvedRangeInfo, providedRanges);
            }
        }

        private async Task<DocumentServiceRequest> CreateRequestAsync()
        {
            INameValueCollection requestHeaders = await this.CreateCommonHeadersAsync(
                    this.GetFeedOptions(this.ContinuationToken));

            requestHeaders[HttpConstants.HttpHeaders.IsContinuationExpected] = isContinuationExpected.ToString();

            return this.CreateDocumentServiceRequest(
                requestHeaders,
                this.QuerySpec,
                this.PartitionKeyInternal);
        }
    }
}
