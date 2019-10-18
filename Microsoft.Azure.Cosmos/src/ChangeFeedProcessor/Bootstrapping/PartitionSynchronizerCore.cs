﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if AZURECORE
namespace Azure.Cosmos.ChangeFeed
#else
namespace Microsoft.Azure.Cosmos.ChangeFeed
#endif
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    internal sealed class PartitionSynchronizerCore : PartitionSynchronizer
    {
#pragma warning disable SA1401 // Fields should be private
        internal static int DefaultDegreeOfParallelism = 25;
#pragma warning restore SA1401 // Fields should be private

        private readonly ContainerCore container;
        private readonly DocumentServiceLeaseContainer leaseContainer;
        private readonly DocumentServiceLeaseManager leaseManager;
        private readonly int degreeOfParallelism;
        private readonly int maxBatchSize;

        public PartitionSynchronizerCore(
            ContainerCore container,
            DocumentServiceLeaseContainer leaseContainer,
            DocumentServiceLeaseManager leaseManager,
            int degreeOfParallelism,
            int maxBatchSize)
        {
            this.container = container;
            this.leaseContainer = leaseContainer;
            this.leaseManager = leaseManager;
            this.degreeOfParallelism = degreeOfParallelism;
            this.maxBatchSize = maxBatchSize;
        }

        public override async Task CreateMissingLeasesAsync()
        {
            IReadOnlyList<PartitionKeyRange> ranges = await this.EnumPartitionKeyRangesAsync().ConfigureAwait(false);
            HashSet<string> partitionIds = new HashSet<string>(ranges.Select(range => range.Id));
            DefaultTrace.TraceInformation("Source collection: '{0}', {1} partition(s)", this.container.LinkUri.ToString(), partitionIds.Count);
            await this.CreateLeasesAsync(partitionIds).ConfigureAwait(false);
        }

        public override async Task<IEnumerable<DocumentServiceLease>> SplitPartitionAsync(DocumentServiceLease lease)
        {
            if (lease == null)
            {
                throw new ArgumentNullException(nameof(lease));
            }

            string partitionId = lease.CurrentLeaseToken;
            string lastContinuationToken = lease.ContinuationToken;

            DefaultTrace.TraceInformation("Partition {0} is gone due to split", partitionId);

            // After split the childs are either all or none available
            IReadOnlyList<PartitionKeyRange> ranges = await this.EnumPartitionKeyRangesAsync().ConfigureAwait(false);
            List<string> addedPartitionIds = ranges.Where(range => range.Parents.Contains(partitionId)).Select(range => range.Id).ToList();
            if (addedPartitionIds.Count == 0)
            {
                DefaultTrace.TraceError("Partition {0} had split but we failed to find at least one child partition", partitionId);
                throw new InvalidOperationException();
            }

            ConcurrentQueue<DocumentServiceLease> newLeases = new ConcurrentQueue<DocumentServiceLease>();
            await addedPartitionIds.ForEachAsync(
                async addedRangeId =>
                {
                    DocumentServiceLease newLease = await this.leaseManager.CreateLeaseIfNotExistAsync(addedRangeId, lastContinuationToken).ConfigureAwait(false);
                    if (newLease != null)
                    {
                        newLeases.Enqueue(newLease);
                    }
                },
                this.degreeOfParallelism).ConfigureAwait(false);

            DefaultTrace.TraceInformation("partition {0} split into {1}", partitionId, string.Join(", ", newLeases.Select(l => l.CurrentLeaseToken)));

            return newLeases;
        }

        private async Task<IReadOnlyList<PartitionKeyRange>> EnumPartitionKeyRangesAsync()
        {
            PartitionKeyRangeCache pkRangeCache = await this.container.ClientContext.DocumentClient.GetPartitionKeyRangeCacheAsync();
            string containerRid = await this.container.GetRIDAsync(default(CancellationToken));
            return await pkRangeCache.TryGetOverlappingRangesAsync(
                containerRid,
                new Range<string>(
                    PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                    PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                    true,
                    false));
        }

        /// <summary>
        /// Creates leases if they do not exist. This might happen on initial start or if some lease was unexpectedly lost.
        /// Leases are created without the continuation token. It means partitions will be read according to 'From Beginning' or
        /// 'From current time'.
        /// Same applies also to split partitions. We do not search for parent lease and take continuation token since this might end up
        /// of reprocessing all the events since the split.
        /// </summary>
        private async Task CreateLeasesAsync(HashSet<string> partitionIds)
        {
            // Get leases after getting ranges, to make sure that no other hosts checked in continuation token for split partition after we got leases.
            IEnumerable<DocumentServiceLease> leases = await this.leaseContainer.GetAllLeasesAsync().ConfigureAwait(false);
            HashSet<string> existingPartitionIds = new HashSet<string>(leases.Select(lease => lease.CurrentLeaseToken));
            HashSet<string> addedPartitionIds = new HashSet<string>(partitionIds);
            addedPartitionIds.ExceptWith(existingPartitionIds);

            await addedPartitionIds.ForEachAsync(
                async addedRangeId => { await this.leaseManager.CreateLeaseIfNotExistAsync(addedRangeId, continuationToken: null).ConfigureAwait(false); },
                this.degreeOfParallelism).ConfigureAwait(false);
        }
    }
}