﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Represents a unit of feed consumption that can be used as unit of parallelism.
    /// </summary>
    [Serializable]
#if PREVIEW
    public
#else
    internal
#endif
    abstract class FeedRange
    {
        /// <summary>
        /// Gets a string representation of the current range.
        /// </summary>
        /// <returns>A string representation of the current token.</returns>
        public abstract override string ToString();
    }
}
