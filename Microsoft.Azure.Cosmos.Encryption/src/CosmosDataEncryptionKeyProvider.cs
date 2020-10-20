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
    /// Default implementation for a provider to get a data encryption key - wrapped keys are stored in a Cosmos DB container.
    /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public sealed class CosmosDataEncryptionKeyProvider : DataEncryptionKeyProvider
    {
        private const string ContainerPartitionKeyPath = "/id";

        private readonly DataEncryptionKeyContainerCore dataEncryptionKeyContainerCore;

        private Container container;

        internal DekCache DekCache { get; }

        /* MDE's Protected Data Encryption key Cache TTL*/
        internal TimeSpan? PdekCacheTimeToLive { get; }

        internal Container Container
        {
            get
            {
                if (this.container != null)
                {
                    return this.container;
                }

                throw new InvalidOperationException($"The {nameof(CosmosDataEncryptionKeyProvider)} was not initialized.");
            }
        }

        /// <summary>
        /// Gets a provider of type EncryptionKeyWrapProvider that will be used to wrap (encrypt) and unwrap (decrypt) data encryption keys for envelope based encryption.
        /// </summary>
        public EncryptionKeyWrapProvider EncryptionKeyWrapProvider { get; }

        /// <summary>
        /// Gets a provider of type EncryptionKeyStoreProvider that will be used to wrap (encrypt) and unwrap (decrypt) data encryption keys for envelope based encryption.
        /// </summary>
        public EncryptionKeyWrapProvider EncryptionKeyStoreWrapProvider { get; }

        /// <summary>
        /// Gets Container for data encryption keys.
        /// </summary>
        public DataEncryptionKeyContainer DataEncryptionKeyContainer => this.dataEncryptionKeyContainerCore;

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosDataEncryptionKeyProvider"/> class.
        /// </summary>
        /// <param name="encryptionKeyWrapProvider">A provider that will be used to wrap (encrypt) and unwrap (decrypt) data encryption keys for envelope based encryption</param>
        /// <param name="dekPropertiesTimeToLive">Time to live for DEK properties before having to refresh.</param>
        public CosmosDataEncryptionKeyProvider(
            EncryptionKeyWrapProvider encryptionKeyWrapProvider,
            TimeSpan? dekPropertiesTimeToLive = null)
        {
            this.EncryptionKeyWrapProvider = encryptionKeyWrapProvider ?? throw new ArgumentNullException(nameof(encryptionKeyWrapProvider));
            this.dataEncryptionKeyContainerCore = new DataEncryptionKeyContainerCore(this);
            this.DekCache = new DekCache(dekPropertiesTimeToLive);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosDataEncryptionKeyProvider"/> class.
        /// </summary>
        /// <param name="encryptionKeyStoreProvider"> MDE EncryptionKeyStoreProvider for Wrapping/UnWrapping services. </param>
        /// <param name="cacheTimeToLive">Time to live for EncryptionKeyStoreProvider's ProtectedDataEncryptionKey before having to refresh. 0 results in no Caching.</param>
        /// <param name="dekPropertiesTimeToLive">Time to live for DEK properties before having to refresh.</param>
        public CosmosDataEncryptionKeyProvider(
            EncryptionKeyStoreProvider encryptionKeyStoreProvider,
            TimeSpan? cacheTimeToLive = null,
            TimeSpan? dekPropertiesTimeToLive = null)
        {
            this.EncryptionKeyStoreWrapProvider = new MdeKeyWrapProvider(
                encryptionKeyStoreProvider
                ?? throw new ArgumentNullException(nameof(encryptionKeyStoreProvider)));
            this.dataEncryptionKeyContainerCore = new DataEncryptionKeyContainerCore(this);
            this.DekCache = new DekCache(dekPropertiesTimeToLive);
            this.PdekCacheTimeToLive = cacheTimeToLive;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosDataEncryptionKeyProvider"/> class.
        /// </summary>
        /// <param name="encryptionKeyWrapProvider">A provider that will be used to wrap (encrypt) and unwrap (decrypt) data encryption keys for envelope based encryption</param>
        /// <param name="encryptionKeyStoreProvider"> MDE EncryptionKeyStoreProvider for Wrapping/UnWrapping services. </param>
        /// <param name="cacheTimeToLive">Time to live for EncryptionKeyStoreProvider ProtectedDataEncryptionKey before having to refresh. 0 results in no Caching.</param>
        /// <param name="dekPropertiesTimeToLive">Time to live for DEK properties before having to refresh.</param>
        public CosmosDataEncryptionKeyProvider(
            EncryptionKeyWrapProvider encryptionKeyWrapProvider,
            EncryptionKeyStoreProvider encryptionKeyStoreProvider,
            TimeSpan? cacheTimeToLive = null,
            TimeSpan? dekPropertiesTimeToLive = null)
        {
            this.EncryptionKeyWrapProvider = encryptionKeyWrapProvider ?? throw new ArgumentNullException(nameof(encryptionKeyWrapProvider));
            this.EncryptionKeyStoreWrapProvider = new MdeKeyWrapProvider(
                encryptionKeyStoreProvider
                ?? throw new ArgumentNullException(nameof(encryptionKeyStoreProvider)));
            this.dataEncryptionKeyContainerCore = new DataEncryptionKeyContainerCore(this);
            this.DekCache = new DekCache(dekPropertiesTimeToLive);
            this.PdekCacheTimeToLive = cacheTimeToLive;
        }

        /// <summary>
        /// Initialize Cosmos DB container for CosmosDataEncryptionKeyProvider to store wrapped DEKs
        /// </summary>
        /// <param name="database">Database</param>
        /// <param name="containerId">Container id</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task to await on.</returns>
        public async Task InitializeAsync(
            Database database,
            string containerId,
            CancellationToken cancellationToken = default)
        {
            if (this.container != null)
            {
                throw new InvalidOperationException($"{nameof(CosmosDataEncryptionKeyProvider)} has already been initialized.");
            }

            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            ContainerResponse containerResponse = await database.CreateContainerIfNotExistsAsync(
                containerId,
                partitionKeyPath: CosmosDataEncryptionKeyProvider.ContainerPartitionKeyPath);

            if (containerResponse.Resource.PartitionKeyPath != CosmosDataEncryptionKeyProvider.ContainerPartitionKeyPath)
            {
                throw new ArgumentException(
                    $"Provided container {containerId} did not have the appropriate partition key definition. " +
                    $"The container needs to be created with PartitionKeyPath set to {CosmosDataEncryptionKeyProvider.ContainerPartitionKeyPath}.",
                    nameof(containerId));
            }

            this.container = containerResponse.Container;
        }

        /// <inheritdoc/>
        public override async Task<DataEncryptionKey> FetchDataEncryptionKeyAsync(
            string id,
            string encryptionAlgorithm,
            CancellationToken cancellationToken)
        {
            DataEncryptionKeyProperties dataEncryptionKeyProperties = await this.dataEncryptionKeyContainerCore.FetchDataEncryptionKeyPropertiesAsync(
                id,
                diagnosticsContext: CosmosDiagnosticsContext.Create(null),
                cancellationToken: cancellationToken);

            InMemoryRawDek inMemoryRawDek = await this.dataEncryptionKeyContainerCore.FetchUnwrappedAsync(
                dataEncryptionKeyProperties,
                encryptionAlgorithm,
                diagnosticsContext: CosmosDiagnosticsContext.Create(null),
                cancellationToken: cancellationToken);

            return inMemoryRawDek.DataEncryptionKey;
        }
    }
}
