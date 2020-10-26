﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;

    internal sealed class ChangeFeedStateTime : ChangeFeedState
    {
        public ChangeFeedStateTime(DateTime time)
        {
            if (time.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentOutOfRangeException($"{nameof(time)}.{nameof(DateTime.Kind)} must be {nameof(DateTimeKind)}.{nameof(DateTimeKind.Utc)}");
            }

            this.StartTime = time;
        }

        public DateTime StartTime { get; }

        public override void Accept<TInput>(
            IChangeFeedStateVisitor<TInput> visitor,
            TInput input) => visitor.Visit(this, input);

        public override TOutput Accept<TInput, TOutput>(
            IChangeFeedStateVisitor<TInput, TOutput> visitor,
            TInput input) => visitor.Visit(this, input);

        public override TResult Accept<TResult>(
            IChangeFeedStateTransformer<TResult> visitor) => visitor.Transform(this);
    }
}
