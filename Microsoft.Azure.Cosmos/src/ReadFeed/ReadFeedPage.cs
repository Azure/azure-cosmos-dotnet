﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal sealed class ReadFeedPage
    {
        public ReadFeedPage(
            CosmosArray documents,
            double requestCharge,  
            string activityId, 
            ReadFeedCrossFeedRangeState? state)
        {
            this.Documents = documents ?? throw new ArgumentNullException(nameof(documents));
            this.RequestCharge = requestCharge < 0 ? throw new ArgumentOutOfRangeException(nameof(requestCharge)) : requestCharge;
            this.ActivityId = activityId ?? throw new ArgumentNullException(nameof(activityId));
            this.State = state;
        }

        public CosmosArray Documents { get; }

        public double RequestCharge { get; }

        public string ActivityId { get; }

        public ReadFeedCrossFeedRangeState? State { get; }
    }
}
