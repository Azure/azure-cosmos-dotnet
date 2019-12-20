﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Cosmos.Serialization;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;

    internal sealed class FeedProcessorCore<T> : FeedProcessor
    {
        private readonly ProcessorOptions options;
        private readonly PartitionCheckpointer checkpointer;
        private readonly ChangeFeedObserver<T> observer;
        private readonly FeedIterator resultSetIterator;
        private readonly CosmosSerializer cosmosJsonSerializer;

        public FeedProcessorCore(
            ChangeFeedObserver<T> observer,
            FeedIterator resultSetIterator,
            ProcessorOptions options,
            PartitionCheckpointer checkpointer,
            CosmosSerializer cosmosJsonSerializer)
        {
            this.observer = observer;
            this.options = options;
            this.checkpointer = checkpointer;
            this.resultSetIterator = resultSetIterator;
            this.cosmosJsonSerializer = cosmosJsonSerializer;
        }

        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            string lastContinuation = this.options.StartContinuation;

            while (!cancellationToken.IsCancellationRequested)
            {
                TimeSpan delay = this.options.FeedPollDelay;

                try
                {
                    do
                    {
                        Response response = await this.resultSetIterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
                        if (response.Status != (int)HttpStatusCode.NotModified && !response.IsSuccessStatusCode())
                        {
                            SubStatusCodes subStatusCode = response.Headers.GetSubStatusCode();
                            DefaultTrace.TraceWarning("unsuccessful feed read: lease token '{0}' status code {1}. substatuscode {2}", this.options.LeaseToken, response.Status, subStatusCode);
                            this.HandleFailedRequest((HttpStatusCode)response.Status, (int)subStatusCode, lastContinuation);

                            if (response.Headers.TryGetValue(HttpConstants.HttpHeaders.RetryAfterInMilliseconds, out string retryHeader))
                            {
                                TimeSpan? retryAfter = Headers.GetRetryAfter(retryHeader);
                                if (retryAfter.HasValue)
                                {
                                    delay = retryAfter.Value;
                                }
                            }

                            // Out of the loop for a retry
                            break;
                        }

                        lastContinuation = response.Headers.GetContinuationToken();
                        if (this.resultSetIterator.HasMoreResults)
                        {
                            await this.DispatchChangesAsync(response, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    while (this.resultSetIterator.HasMoreResults && !cancellationToken.IsCancellationRequested);
                }
                catch (TaskCanceledException canceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }

                    Extensions.TraceException(canceledException);
                    DefaultTrace.TraceWarning("exception: lease token '{0}'", this.options.LeaseToken);

                    // ignore as it is caused by Cosmos DB client when StopAsync is called
                }

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        private void HandleFailedRequest(
            HttpStatusCode statusCode,
            int subStatusCode,
            string lastContinuation)
        {
            DocDbError docDbError = ExceptionClassifier.ClassifyStatusCodes(statusCode, subStatusCode);
            switch (docDbError)
            {
                case DocDbError.PartitionSplit:
                    throw new FeedSplitException("Partition split.", lastContinuation);
                case DocDbError.Undefined:
                    throw new InvalidOperationException($"Undefined DocDbError for status code {statusCode} and substatus code {subStatusCode}");
                default:
                    DefaultTrace.TraceCritical($"Unrecognized DocDbError enum value {docDbError}");
                    Debug.Fail($"Unrecognized DocDbError enum value {docDbError}");
                    throw new InvalidOperationException($"Unrecognized DocDbError enum value {docDbError} for status code {statusCode} and substatus code {subStatusCode}");
            }
        }

        private async Task DispatchChangesAsync(Response response, CancellationToken cancellationToken)
        {
            ChangeFeedObserverContext context = new ChangeFeedObserverContextCore<T>(this.options.LeaseToken, response, this.checkpointer);
            Collection<T> asFeedResponse;
            try
            {
                asFeedResponse = (await this.cosmosJsonSerializer.FromStreamAsync<CosmosFeedResponseUtil<T>>(response.ContentStream, cancellationToken)).Data;
            }
            catch (Exception serializationException)
            {
                // Error using custom serializer to parse stream
                throw new ObserverException(serializationException);
            }

            // When StartFromBeginning is used, the first request returns OK but no content
            if (asFeedResponse.Count == 0)
            {
                return;
            }

            List<T> asReadOnlyList = new List<T>(asFeedResponse.Count);
            asReadOnlyList.AddRange(asFeedResponse);

            await this.observer.ProcessChangesAsync(context, asReadOnlyList, cancellationToken);
        }
    }
}