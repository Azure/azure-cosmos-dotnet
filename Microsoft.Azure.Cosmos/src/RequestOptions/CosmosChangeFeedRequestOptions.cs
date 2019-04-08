//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The Cosmos Change Feed request options
    /// </summary>
    internal class CosmosChangeFeedRequestOptions : CosmosRequestOptions
    {
        internal const string IfNoneMatchAllHeaderValue = "*";

        /// <summary>
        /// Marks whether the change feed should be read from the start.
        /// </summary>
        /// <remarks>
        /// If this is specified, StartTime is ignored.
        /// </remarks>
        public virtual bool StartFromBeginning { get; set; }

        /// <summary>
        /// Specifies a particular point in time to start to read the change feed.
        /// </summary>
        public virtual DateTime? StartTime { get; set; }

        internal virtual string PartitionKeyRangeId { get; set; }

        /// <summary>
        /// Fill the CosmosRequestMessage headers with the set properties
        /// </summary>
        /// <param name="request">The <see cref="CosmosRequestMessage"/></param>
        public override void FillRequestOptions(CosmosRequestMessage request)
        {
            // Check if no Continuation Token is present
            if (string.IsNullOrEmpty(request.Headers.IfNoneMatch))
            {
                if (!this.StartFromBeginning && this.StartTime == null)
                {
                    request.Headers.IfNoneMatch = CosmosChangeFeedRequestOptions.IfNoneMatchAllHeaderValue;
                }
                else if (this.StartTime != null)
                {
                    request.Headers.Add(HttpConstants.HttpHeaders.IfModifiedSince, this.StartTime.Value.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture));
                }
            }

            request.Headers.Add(HttpConstants.HttpHeaders.A_IM, HttpConstants.A_IMHeaderValues.IncrementalFeed);

            if (!string.IsNullOrEmpty(this.PartitionKeyRangeId))
            {
                request.PartitionKeyRangeId = this.PartitionKeyRangeId;
            }

            base.FillRequestOptions(request);
        }

        internal void ValidateOptions(string providedContinuationToken)
        {
            int setOptions = 0;
            if (this.StartFromBeginning)
            {
                setOptions++;
            }

            if (providedContinuationToken != null)
            {
                setOptions++;
            }

            if (this.StartTime != null)
            {
                setOptions++;
            }

            if (setOptions > 1)
            {
                throw new ArgumentException("Cannot specify ContinuationToken, StartFromBeginning, and StartTime. Only one of them can be used");
            }
        }

        internal static void FillContinuationToken(CosmosRequestMessage request, string continuationToken)
        {
            Debug.Assert(request != null);

            if (!string.IsNullOrWhiteSpace(continuationToken))
            {
                // On REST level, change feed is using IfNoneMatch/ETag instead of continuation
                request.Headers.IfNoneMatch = continuationToken;
            }
        }

        internal static void FillMaxItemCount(CosmosRequestMessage request, int? maxItemCount)
        {
            Debug.Assert(request != null);

            if (maxItemCount.HasValue)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.PageSize, maxItemCount.Value.ToString(CultureInfo.InvariantCulture));
            }
        }
    }
}