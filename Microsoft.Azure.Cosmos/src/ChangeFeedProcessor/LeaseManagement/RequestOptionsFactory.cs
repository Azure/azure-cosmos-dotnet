﻿//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement
{
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Defines request options for lease requests to use with <see cref="DocumentServiceLeaseStoreManagerCosmos"/> and <see cref="DocumentServiceLeaseStoreCosmos"/>.
    /// </summary>
    internal abstract class RequestOptionsFactory
    {
        public abstract string GetPartitionKey(string itemId);

        public abstract FeedOptions CreateFeedOptions();
    }
}
