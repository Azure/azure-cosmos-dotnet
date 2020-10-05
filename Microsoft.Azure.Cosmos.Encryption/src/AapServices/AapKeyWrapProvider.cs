﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Encryption.Cryptography;

    /// <summary>
    /// Provides functionality to wrap (encrypt) and unwrap (decrypt) data encryption keys using Key Encryption Keys (KEKs) via EncryptionKeyStoreProvider.
    /// </summary>
    internal sealed class AapKeyWrapProvider : EncryptionKeyWrapProvider
    {
        public EncryptionKeyStoreProvider EncryptionKeyStoreProvider { get; }

        public AapKeyWrapProvider(EncryptionKeyStoreProvider encryptionKeyStoreProvider)
        {
            this.EncryptionKeyStoreProvider = encryptionKeyStoreProvider ?? throw new ArgumentNullException(nameof(encryptionKeyStoreProvider));
        }

        public override Task<EncryptionKeyUnwrapResult> UnwrapKeyAsync(
            byte[] wrappedKey,
            EncryptionKeyWrapMetadata metadata,
            CancellationToken cancellationToken)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            KeyEncryptionKey masterKey = KeyEncryptionKey.GetOrCreate(
                metadata.Name,
                metadata.Value,
                this.EncryptionKeyStoreProvider);

            byte[] result = masterKey.DecryptEncryptionKey(wrappedKey);
            return Task.FromResult(new EncryptionKeyUnwrapResult(result, TimeSpan.Zero));
        }

        public override Task<EncryptionKeyWrapResult> WrapKeyAsync(
            byte[] key,
            EncryptionKeyWrapMetadata metadata,
            CancellationToken cancellationToken)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            KeyEncryptionKey masterKey = KeyEncryptionKey.GetOrCreate(
                metadata.Name,
                metadata.Value,
                this.EncryptionKeyStoreProvider);

            byte[] result = masterKey.EncryptEncryptionKey(key);
            return Task.FromResult(new EncryptionKeyWrapResult(result, metadata));
        }
    }
}