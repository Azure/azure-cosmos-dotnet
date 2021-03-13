﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Serializer;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.AsyncEnumerable;

    internal sealed class ChangeFeedCrossFeedRangeAsyncEnumerable : ITraceableAsyncEnumerable<TryCatch<ChangeFeedPage>>
    {
        private readonly IDocumentContainer documentContainer;
        private readonly ChangeFeedPaginationOptions changeFeedPaginationOptions;
        private readonly ChangeFeedCrossFeedRangeState state;
        private readonly JsonSerializationFormatOptions jsonSerializationFormatOptions;
        private readonly ITrace trace;

        public ChangeFeedCrossFeedRangeAsyncEnumerable(
            IDocumentContainer documentContainer,
            ChangeFeedCrossFeedRangeState state,
            ChangeFeedPaginationOptions changeFeedPaginationOptions,
            JsonSerializationFormatOptions jsonSerializationFormatOptions = null,
            ITrace trace = null)
        {
            this.documentContainer = documentContainer ?? throw new ArgumentNullException(nameof(documentContainer));
            this.changeFeedPaginationOptions = changeFeedPaginationOptions ?? ChangeFeedPaginationOptions.Default;
            this.state = state;
            this.jsonSerializationFormatOptions = jsonSerializationFormatOptions;
            this.trace = trace ?? NoOpTrace.Singleton;
        }

        public IAsyncEnumerator<TryCatch<ChangeFeedPage>> GetAsyncEnumerator(
            CancellationToken cancellationToken = default)
        {
            return this.GetAsyncEnumerator(this.trace, cancellationToken);
        }

        public ITraceableAsyncEnumerator<TryCatch<ChangeFeedPage>> GetAsyncEnumerator(
            ITrace trace,
            CancellationToken cancellationToken)
        {
            CrossFeedRangeState<ChangeFeedState> innerState = new CrossFeedRangeState<ChangeFeedState>(this.state.FeedRangeStates);
            CrossPartitionChangeFeedAsyncEnumerator innerEnumerator = CrossPartitionChangeFeedAsyncEnumerator.Create(
                this.documentContainer,
                innerState,
                this.changeFeedPaginationOptions,
                trace,
                cancellationToken);

            return new ChangeFeedCrossFeedRangeAsyncEnumerator(
                innerEnumerator,
                this.jsonSerializationFormatOptions,
                trace);
        }
    }
}
