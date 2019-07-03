﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Util methods for batch requests.
    /// </summary>
    internal static class BatchExecUtils
    {
        /// <summary>
        /// Converts a Stream to a Memory{byte} wrapping a byte array honoring a provided maximum length for the returned Memory.
        /// </summary>
        /// <param name="stream">Stream to be converted to bytes.</param>
        /// <param name="maximumLength">Desired maximum length of the Memory{byte}.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> to cancel the operation.</param>
        /// <returns>A Memory{byte} with length at most maximumLength.</returns>
        /// <remarks>Throws RequestEntityTooLargeException if the input stream has more bytes than maximumLength.</remarks>
        public static async Task<Memory<byte>> StreamToMemoryAsync(Stream stream, int maximumLength, CancellationToken cancellationToken)
        {
            if (stream.CanSeek)
            {
                if (stream.Length > maximumLength)
                {
                    throw new RequestEntityTooLargeException(RMResources.RequestTooLarge);
                }

                // Some derived implementations of MemoryStream (such as versions of RecyclableMemoryStream prior to 1.2.2 that we may be using)
                // return an incorrect response from TryGetBuffer. Use TryGetBuffer only on the MemoryStream type and not derived types.
                MemoryStream memStream = stream as MemoryStream;
                if (memStream != null
                     && memStream.GetType() == typeof(MemoryStream)
                     && memStream.TryGetBuffer(out ArraySegment<byte> memBuffer))
                {
                    return memBuffer;
                }

                byte[] bytes = new byte[stream.Length];
                int sum = 0;
                int count;
                while ((count = await stream.ReadAsync(bytes, sum, bytes.Length - sum, cancellationToken)) > 0)
                {
                    sum += count;
                }

                return bytes;
            }
            else
            {
                int bufferSize = 81920; // Using the same buffer size as the Stream.DefaultCopyBufferSize
                byte[] buffer = new byte[bufferSize];

                using (MemoryStream memoryStream = new MemoryStream(bufferSize)) // using bufferSize as initial capacity as well
                {
                    int sum = 0;
                    int count;
                    while ((count = await stream.ReadAsync(buffer, 0, bufferSize, cancellationToken)) > 0)
                    {
                        sum += count;
                        if (sum > maximumLength)
                        {
                            throw new RequestEntityTooLargeException(RMResources.RequestTooLarge);
                        }

#pragma warning disable VSTHRD103 // Call async methods when in an async method
                        memoryStream.Write(buffer, 0, count);
#pragma warning restore VSTHRD103 // Call async methods when in an async method
                    }

                    return new Memory<byte>(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
                }
            }
        }

        public static void GetServerRequestLimits(out int maxServerRequestBodyLength, out int maxServerRequestOperationCount)
        {
            maxServerRequestBodyLength = Constants.MaxDirectModeBatchRequestBodySizeInBytes;
            maxServerRequestOperationCount = Constants.MaxOperationsInDirectModeBatchRequest;
        }

        public static CosmosResponseMessage Validate(
            IReadOnlyList<ItemBatchOperation> operations,
            RequestOptions batchOptions,
            int? maxOperationCount = null)
        {
            string errorMessage = null;

            if (operations.Count == 0)
            {
                errorMessage = ClientResources.BatchNoOperations;
            }

            if (maxOperationCount.HasValue && operations.Count > maxOperationCount.Value)
            {
                errorMessage = ClientResources.BatchTooLarge;
            }

            if (errorMessage == null && batchOptions != null)
            {
                if (batchOptions.IfMatchEtag != null || batchOptions.IfNoneMatchEtag != null)
                {
                    errorMessage = ClientResources.BatchRequestOptionNotSupported;
                }
            }

            if (errorMessage == null)
            {
                foreach (ItemBatchOperation operation in operations)
                {
                    if (operation.RequestOptions != null
                        && operation.RequestOptions.Properties != null
                        && (operation.RequestOptions.Properties.TryGetValue(WFConstants.BackendHeaders.EffectivePartitionKey, out object epkObj)
                            | operation.RequestOptions.Properties.TryGetValue(WFConstants.BackendHeaders.EffectivePartitionKeyString, out object epkStrObj)))
                    {
                        byte[] epk = epkObj as byte[];
                        string epkStr = epkStrObj as string;
                        if (epk == null || epkStr == null)
                        {
                            errorMessage = string.Format(
                                ClientResources.EpkPropertiesPairingExpected,
                                WFConstants.BackendHeaders.EffectivePartitionKey,
                                WFConstants.BackendHeaders.EffectivePartitionKeyString);
                        }

                        if (operation.PartitionKey != null)
                        {
                            errorMessage = ClientResources.PKAndEpkSetTogether;
                        }
                    }
                }
            }

            if (errorMessage != null)
            {
                return new CosmosResponseMessage(HttpStatusCode.BadRequest, errorMessage: errorMessage);
            }

            return new CosmosResponseMessage(HttpStatusCode.OK);
        }
    }
}