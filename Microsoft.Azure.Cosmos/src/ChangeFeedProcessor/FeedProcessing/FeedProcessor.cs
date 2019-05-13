﻿//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides an API to run continuous processing on a single processing unit of some resource.
    /// Created by <see cref="FeedProcessorFactory{T}.Create"/> after some lease is acquired by the current host.
    /// Processing can perform the following tasks in a loop:
    ///   1. Read some data from the resource feed.
    ///   2. Handle possible problems with the read.
    ///   3. Pass the obtained data to an observer by calling <see cref="ChangeFeedObserver{T}.ProcessChangesAsync"/> with the context <see cref="ChangeFeedObserverContext"/>.
    /// </summary>
    internal abstract class FeedProcessor
    {
        /// <summary>
        /// Perform feed processing.
        /// </summary>
        /// <param name="cancellation">Cancellation token to be used to stop processing</param>
        /// <returns>A <see cref="Task"/>.</returns>
        public abstract Task RunAsync(CancellationToken cancellation);
    }
}