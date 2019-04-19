﻿//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.DocDBErrors;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.Logging;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Documents;

    internal sealed class FeedProcessorCore<T> : FeedProcessor
    {
        private static readonly int DefaultMaxItemCount = 100;
        private readonly ILog logger = LogProvider.GetCurrentClassLogger();
        private readonly ProcessorSettings settings;
        private readonly PartitionCheckpointer checkpointer;
        private readonly ChangeFeedObserver<T> observer;
        private readonly ChangeFeedOptions options;
        private readonly CosmosResultSetIterator<T> resultSetIterator;

        public FeedProcessorCore(ChangeFeedObserver<T> observer, CosmosResultSetIterator<T> resultSetIterator, ProcessorSettings settings, PartitionCheckpointer checkpointer)
        {
            this.observer = observer;
            this.settings = settings;
            this.checkpointer = checkpointer;
            this.options = new ChangeFeedOptions
            {
                MaxItemCount = settings.MaxItemCount,
                PartitionKeyRangeId = settings.LeaseToken,
                SessionToken = settings.SessionToken,
                StartFromBeginning = settings.StartFromBeginning,
                RequestContinuation = settings.StartContinuation,
                StartTime = settings.StartTime,
            };

            this.resultSetIterator = resultSetIterator;
        }

        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            string lastContinuation = this.settings.StartContinuation;

            while (!cancellationToken.IsCancellationRequested)
            {
                TimeSpan delay = this.settings.FeedPollDelay;

                try
                {
                    do
                    {
                        CosmosQueryResponse<T> response = await this.resultSetIterator.FetchNextSetAsync(cancellationToken).ConfigureAwait(false);
                        lastContinuation = response.ContinuationToken;
                        if (response.GetHasMoreResults())
                        {
                            await this.DispatchChanges(response, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    while (this.resultSetIterator.HasMoreResults && !cancellationToken.IsCancellationRequested);

                    if (this.options.MaxItemCount != this.settings.MaxItemCount)
                    {
                        this.options.MaxItemCount = this.settings.MaxItemCount;   // Reset after successful execution.
                    }
                }
                catch (DocumentClientException clientException)
                {
                    this.logger.WarnException("exception: lease token '{0}'", clientException, this.settings.LeaseToken);
                    DocDbError docDbError = ExceptionClassifier.ClassifyClientException(clientException);
                    switch (docDbError)
                    {
                        case DocDbError.PartitionNotFound:
                            throw new FeedNotFoundException("Partition not found.", lastContinuation);
                        case DocDbError.PartitionSplit:
                            throw new FeedSplitException("Partition split.", lastContinuation);
                        case DocDbError.Undefined:
                            throw;
                        case DocDbError.TransientError:
                            // Retry on transient (429) errors
                            break;
                        case DocDbError.MaxItemCountTooLarge:
                            if (!this.options.MaxItemCount.HasValue)
                            {
                                this.options.MaxItemCount = DefaultMaxItemCount;
                            }
                            else if (this.options.MaxItemCount <= 1)
                            {
                                this.logger.ErrorFormat("Cannot reduce maxItemCount further as it's already at {0}.", this.options.MaxItemCount);
                                throw;
                            }

                            this.options.MaxItemCount /= 2;
                            this.logger.WarnFormat("Reducing maxItemCount, new value: {0}.", this.options.MaxItemCount);
                            break;
                        default:
                            this.logger.Fatal($"Unrecognized DocDbError enum value {docDbError}");
                            Debug.Fail($"Unrecognized DocDbError enum value {docDbError}");
                            throw;
                    }

                    if (clientException.RetryAfter != TimeSpan.Zero)
                        delay = clientException.RetryAfter;
                }
                catch (TaskCanceledException canceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                        throw;

                    this.logger.WarnException("exception: lease token '{0}'", canceledException, this.settings.LeaseToken);

                    // ignore as it is caused by DocumentDB client
                }

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        private Task DispatchChanges(CosmosQueryResponse<T> response, CancellationToken cancellationToken)
        {
            ChangeFeedObserverContext context = new ChangeFeedObserverContextCore<T>(this.settings.LeaseToken, response, this.checkpointer);
            return this.observer.ProcessChangesAsync(context, response, cancellationToken);
        }
    }
}