﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Maintains a batch of operations and dispatches the batch through an executor. Maps results into the original operation contexts.
    /// </summary>
    /// <seealso cref="BatchAsyncOperationContext"/>
    internal class BatchAsyncBatcher : IDisposable
    {
        private readonly SemaphoreSlim tryAddLimiter;
        private readonly CosmosSerializer CosmosSerializer;
        private readonly List<BatchAsyncOperationContext> batchOperations;
        private readonly Func<IReadOnlyList<BatchAsyncOperationContext>, CancellationToken, Task<PartitionKeyBatchResponse>> executor;
        private readonly int maxBatchByteSize;
        private readonly int maxBatchOperationCount;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private long currentSize = 0;

        public bool IsEmpty => this.batchOperations.Count == 0;

        public BatchAsyncBatcher(
            int maxBatchOperationCount,
            int maxBatchByteSize,
            CosmosSerializer cosmosSerializer,
            Func<IReadOnlyList<BatchAsyncOperationContext>, CancellationToken, Task<PartitionKeyBatchResponse>> executor)
        {
            if (maxBatchOperationCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBatchOperationCount));
            }

            if (maxBatchByteSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBatchByteSize));
            }

            if (executor == null)
            {
                throw new ArgumentNullException(nameof(executor));
            }

            if (cosmosSerializer == null)
            {
                throw new ArgumentNullException(nameof(cosmosSerializer));
            }

            this.batchOperations = new List<BatchAsyncOperationContext>(maxBatchOperationCount);
            this.tryAddLimiter = new SemaphoreSlim(1, 1);
            this.executor = executor;
            this.maxBatchByteSize = maxBatchByteSize;
            this.maxBatchOperationCount = maxBatchOperationCount;
            this.CosmosSerializer = cosmosSerializer;
        }

        public async Task<bool> TryAddAsync(BatchAsyncOperationContext batchAsyncOperation)
        {
            if (batchAsyncOperation == null)
            {
                throw new ArgumentNullException(nameof(batchAsyncOperation));
            }

            using (await this.tryAddLimiter.UsingWaitAsync(this.cancellationTokenSource.Token))
            {
                if (this.batchOperations.Count == this.maxBatchOperationCount)
                {
                    return false;
                }

                int itemByteSize = batchAsyncOperation.Operation.GetApproximateSerializedLength();

                if (itemByteSize + this.currentSize > this.maxBatchByteSize)
                {
                    return false;
                }

                this.currentSize += itemByteSize;

                // Operation index is in the scope of the current batch
                batchAsyncOperation.Operation.OperationIndex = this.batchOperations.Count;
                this.batchOperations.Add(batchAsyncOperation);
                return true;
            }
        }

        public async Task DispatchAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                using (PartitionKeyBatchResponse batchResponse = await this.executor(this.batchOperations, cancellationToken))
                {
                    // If the batch was not successful, we need to set all the responses
                    if (!batchResponse.IsSuccessStatusCode)
                    {
                        BatchOperationResult errorResult = new BatchOperationResult(batchResponse.StatusCode);
                        foreach (BatchAsyncOperationContext operation in this.batchOperations)
                        {
                            operation.Complete(errorResult);
                        }

                        return;
                    }

                    for (int index = 0; index < this.batchOperations.Count; index++)
                    {
                        BatchAsyncOperationContext operation = this.batchOperations[index];
                        try
                        {
                            BatchOperationResult response = batchResponse[operation.Operation.OperationIndex];
                            operation.Complete(response);
                        }
                        catch (KeyNotFoundException)
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Exceptions happening during execution fail all the Tasks
                foreach (BatchAsyncOperationContext operation in this.batchOperations)
                {
                    operation.Fail(ex);
                }
            }
        }

        public void Dispose()
        {
            this.cancellationTokenSource.Cancel();
            this.cancellationTokenSource.Dispose();
            this.tryAddLimiter.Dispose();
        }
    }
}
