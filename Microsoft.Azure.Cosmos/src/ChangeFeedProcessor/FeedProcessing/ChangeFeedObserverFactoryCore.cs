﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using static Microsoft.Azure.Cosmos.Container;

    internal sealed class ChangeFeedObserverFactoryCore : ChangeFeedObserverFactory
    {
        private readonly ChangesStreamHandler onChanges;

        public ChangeFeedObserverFactoryCore(ChangesStreamHandler onChanges)
        {
            this.onChanges = onChanges;
        }

        public override ChangeFeedObserver CreateObserver()
        {
            return new ChangeFeedObserverBase(this.onChanges);
        }
    }

    internal sealed class ChangeFeedObserverFactoryCore<T> : ChangeFeedObserverFactory
    {
        private readonly ChangesHandler<T> onChanges;
        private readonly CosmosSerializerCore serializerCore;

        public ChangeFeedObserverFactoryCore(ChangesHandler<T> onChanges, CosmosSerializerCore serializerCore)
        {
            this.onChanges = onChanges ?? throw new ArgumentNullException(nameof(onChanges));
            this.serializerCore = serializerCore ?? throw new ArgumentNullException(nameof(serializerCore));
        }

        public override ChangeFeedObserver CreateObserver()
        {
            return new ChangeFeedObserverBase(this.ChangesStreamHandlerAsync);
        }

        private async Task ChangesStreamHandlerAsync(Stream changes, CancellationToken cancellationToken)
        {
            IEnumerable<T> asEnumerable;
            try
            {
                asEnumerable = this.serializerCore.FromFeedResponseStream<T>(changes, Documents.ResourceType.Document);
            }
            catch (Exception serializationException)
            {
                // Error using custom serializer to parse stream
                throw new ObserverException(serializationException);
            }

            List<T> asReadOnlyList = asEnumerable as List<T> ?? new List<T>(asEnumerable);
            await this.onChanges(asReadOnlyList.AsReadOnly(), cancellationToken);
        }
    }
}