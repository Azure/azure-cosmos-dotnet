//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Routing;

    /// <summary>
    /// Cosmos Stand-By Feed iterator implementing Composite Continuation Token
    /// </summary>
    internal class CosmosStandbyFeedResultSetIteratorCore : CosmosFeedResultSetIterator
    {

        private readonly PartitionRoutingHelper partitionRoutingHelper = new PartitionRoutingHelper();
        private readonly CosmosContainerCore cosmosContainer;
        private StandByFeedContinuationToken compositeContinuationToken;

        internal delegate Task<CosmosResponseMessage> NextResultSetDelegate(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken);

        internal readonly NextResultSetDelegate nextResultSetDelegate;

        internal CosmosStandbyFeedResultSetIteratorCore(
            CosmosContainerCore cosmosContainer,
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            NextResultSetDelegate nextDelegate,
            object state)
        {
            this.cosmosContainer = cosmosContainer;
            this.nextResultSetDelegate = nextDelegate;
            this.HasMoreResults = true;
            this.state = state;
            this.MaxItemCount = maxItemCount;
            if (!string.IsNullOrEmpty(continuationToken))
            {
                this.compositeContinuationToken = new StandByFeedContinuationToken(continuationToken);
            }

            this.queryOptions = options;
        }

        /// <summary>
        /// The Continuation Token
        /// </summary>
        protected string continuationToken => this.compositeContinuationToken?.NextToken;

        /// <summary>
        /// The query options for the result set
        /// </summary>
        protected readonly CosmosRequestOptions queryOptions;

        /// <summary>
        /// The state of the result set.
        /// </summary>
        protected readonly object state;

        /// <summary>
        /// The max item count to return as part of the query
        /// </summary>
        protected int? MaxItemCount;

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override async Task<CosmosResponseMessage> FetchNextSetAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (this.compositeContinuationToken == null)
            {
                PartitionKeyRangeCache pkRangeCache = await this.cosmosContainer.Client.DocumentClient.GetPartitionKeyRangeCacheAsync();
                string containerRid = await this.cosmosContainer.GetRID(cancellationToken);
                IReadOnlyList<Documents.PartitionKeyRange> keyRanges = await pkRangeCache.TryGetOverlappingRangesAsync(containerRid, new Documents.Routing.Range<string>(
                    Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                    Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                    true,
                    false));

                this.compositeContinuationToken = new StandByFeedContinuationToken(keyRanges);
            }

            ChangeFeedState iterationState = (ChangeFeedState)this.state;
            iterationState.StartEffectivePartitionKeyString = this.compositeContinuationToken.MinInclusiveRange;
            iterationState.EndEffectivePartitionKeyString = this.compositeContinuationToken.MaxExclusiveRange;

            return await this.nextResultSetDelegate(this.MaxItemCount, this.continuationToken, this.queryOptions, iterationState, cancellationToken)
                .ContinueWith(task =>
                {
                    CosmosResponseMessage response = task.Result;
                    
                    // Change Feed read uses Etag
                    string responseContinuationToken = response.Headers.ETag;
                    this.HasMoreResults = GetHasMoreResults(responseContinuationToken, response.StatusCode);
                    if (!this.HasMoreResults)
                    {
                        // Current Range is done, push it to the end
                        response.Headers.Continuation = this.compositeContinuationToken.PushCurrentToBack();
                        this.HasMoreResults = true;
                    }
                    else
                    {
                        response.Headers.Continuation = this.compositeContinuationToken.UpdateCurrentToken(responseContinuationToken);
                    }

                    return response;
                }, cancellationToken);
        }

        internal static bool GetHasMoreResults(string continuationToken, HttpStatusCode statusCode)
        {
            return continuationToken != null && statusCode != HttpStatusCode.NotModified;
        }
    }
}