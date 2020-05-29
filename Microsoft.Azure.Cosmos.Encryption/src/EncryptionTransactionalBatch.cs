﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    internal sealed class EncryptionTransactionalBatch : TransactionalBatch
    {
        private readonly Encryptor encryptor;
        private readonly CosmosSerializer cosmosSerializer;
        private TransactionalBatch transactionalBatch;

        public EncryptionTransactionalBatch(
            TransactionalBatch transactionalBatch,
            Encryptor encryptor,
            CosmosSerializer cosmosSerializer)
        {
            this.transactionalBatch = transactionalBatch ?? throw new ArgumentNullException(nameof(transactionalBatch));
            this.encryptor = encryptor ?? throw new ArgumentNullException(nameof(encryptor));
            this.cosmosSerializer = cosmosSerializer ?? throw new ArgumentNullException(nameof(cosmosSerializer));
        }

        public override TransactionalBatch CreateItem<T>(
            T item,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            if (!(requestOptions is EncryptionTransactionalBatchItemRequestOptions encryptionItemRequestOptions) ||
                encryptionItemRequestOptions.EncryptionOptions == null)
            {
                this.transactionalBatch = this.transactionalBatch.CreateItem(
                    item,
                    requestOptions);

                return this;
            }

            using (Stream itemStream = this.cosmosSerializer.ToStream<T>(item))
            {
                return this.CreateItemStream(
                    itemStream,
                    requestOptions);
            }
        }

        public override TransactionalBatch CreateItemStream(
            Stream streamPayload,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            if (requestOptions is EncryptionTransactionalBatchItemRequestOptions encryptionItemRequestOptions &&
                encryptionItemRequestOptions.EncryptionOptions != null)
            {
                CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
                using (diagnosticsContext.CreateScope("EncryptItemStream"))
                {
                    streamPayload = EncryptionProcessor.EncryptAsync(
                        streamPayload,
                        this.encryptor,
                        encryptionItemRequestOptions.EncryptionOptions,
                        diagnosticsContext,
                        cancellationToken: default).Result;
                }
            }

            this.transactionalBatch = this.transactionalBatch.CreateItemStream(
                streamPayload,
                requestOptions);

            return this;
        }

        public override TransactionalBatch DeleteItem(
            string id,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            this.transactionalBatch = this.transactionalBatch.DeleteItem(
                id,
                requestOptions);

            return this;
        }

        public override TransactionalBatch ReadItem(
            string id,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            this.transactionalBatch = this.transactionalBatch.ReadItem(
                id,
                requestOptions);

            return this;
        }

        public override TransactionalBatch ReplaceItem<T>(
            string id,
            T item,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            if (!(requestOptions is EncryptionTransactionalBatchItemRequestOptions encryptionItemRequestOptions) ||
                encryptionItemRequestOptions.EncryptionOptions == null)
            {
                this.transactionalBatch = this.transactionalBatch.ReplaceItem(
                    id,
                    item,
                    requestOptions);

                return this;
            }

            using (Stream itemStream = this.cosmosSerializer.ToStream<T>(item))
            {
                return this.ReplaceItemStream(
                    id,
                    itemStream,
                    requestOptions);
            }
        }

        public override TransactionalBatch ReplaceItemStream(
            string id,
            Stream streamPayload,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            if (requestOptions is EncryptionTransactionalBatchItemRequestOptions encryptionItemRequestOptions &&
                encryptionItemRequestOptions.EncryptionOptions != null)
            {
                CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
                using (diagnosticsContext.CreateScope("EncryptItemStream"))
                {
                    streamPayload = EncryptionProcessor.EncryptAsync(
                        streamPayload,
                        this.encryptor,
                        encryptionItemRequestOptions.EncryptionOptions,
                        diagnosticsContext,
                        cancellationToken: default).Result;
                }
            }

            this.transactionalBatch = this.transactionalBatch.ReplaceItemStream(
                id,
                streamPayload,
                requestOptions);

            return this;
        }

        public override TransactionalBatch UpsertItem<T>(
            T item,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            if (!(requestOptions is EncryptionTransactionalBatchItemRequestOptions encryptionItemRequestOptions) ||
                encryptionItemRequestOptions.EncryptionOptions == null)
            {
                this.transactionalBatch = this.transactionalBatch.UpsertItem(
                    item,
                    requestOptions);

                return this;
            }

            using (Stream itemStream = this.cosmosSerializer.ToStream<T>(item))
            {
                return this.UpsertItemStream(
                    itemStream,
                    requestOptions);
            }
        }

        public override TransactionalBatch UpsertItemStream(
            Stream streamPayload,
            TransactionalBatchItemRequestOptions requestOptions = null)
        {
            if (requestOptions is EncryptionTransactionalBatchItemRequestOptions encryptionItemRequestOptions &&
                encryptionItemRequestOptions.EncryptionOptions != null)
            {
                CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
                using (diagnosticsContext.CreateScope("EncryptItemStream"))
                {
                    streamPayload = EncryptionProcessor.EncryptAsync(
                        streamPayload,
                        this.encryptor,
                        encryptionItemRequestOptions.EncryptionOptions,
                        diagnosticsContext,
                        cancellationToken: default).Result;
                }
            }

            this.transactionalBatch = this.transactionalBatch.UpsertItemStream(
                streamPayload,
                requestOptions);

            return this;
        }

        public override async Task<TransactionalBatchResponse> ExecuteAsync(
            CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            using (diagnosticsContext.CreateScope("TransactionalBatch.ExecuteAsync"))
            {
                TransactionalBatchResponse response = await this.transactionalBatch.ExecuteAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    for (int index = 0; index < response.Count; index++)
                    {
                        TransactionalBatchOperationResult result = response[index];

                        if (result.ResourceStream != null)
                        {
                            result.ResourceStream = await EncryptionProcessor.DecryptAsync(
                                result.ResourceStream,
                                this.encryptor,
                                diagnosticsContext,
                                cancellationToken);
                        }
                    }                    
                }

                return response;
            }
        }
    }
}
