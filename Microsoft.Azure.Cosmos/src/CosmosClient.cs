﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Provides a client-side logical representation of the Azure Cosmos DB database account.
    /// This client can be used to configure and execute requests in the Azure Cosmos DB database service.
    /// 
    /// CosmosClient is thread-safe. Its recommended to maintain a single instance of CosmosClient per lifetime 
    /// of the application which enables efficient connection management and performance.
    /// </summary>
    /// <example>
    /// This example create a <see cref="CosmosClient"/>, <see cref="CosmosDatabase"/>, and a <see cref="CosmosContainer"/>.
    /// The CosmosClient uses the <see cref="CosmosClientOptions"/> to get all the configuration values.
    /// <code language="c#">
    /// <![CDATA[
    /// CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(
    ///     accountEndPoint: "https://testcosmos.documents.azure.com:443/",
    ///     accountKey: "SuperSecretKey")
    ///     .WithApplicationRegion(LocationNames.EastUS2);
    /// 
    /// using (CosmosClient cosmosClient = cosmosClientBuilder.Build())
    /// {
    ///     CosmosDatabase db = await client.CreateDatabaseAsync(Guid.NewGuid().ToString())
    ///     CosmosContainer container = await db.CreateContainerAsync(Guid.NewGuid().ToString());
    /// }
    /// ]]>
    /// </code>
    /// </example>
    /// 
    /// <example>
    /// This example create a <see cref="CosmosClient"/>, <see cref="CosmosDatabase"/>, and a <see cref="CosmosContainer"/>.
    /// The CosmosClient is created with the AccountEndpoint and AccountKey.
    /// <code language="c#">
    /// <![CDATA[
    /// using (CosmosClient cosmosClient = new CosmosClient(
    ///     accountEndPoint: "https://testcosmos.documents.azure.com:443/",
    ///     accountKey: "SuperSecretKey"))
    /// {
    ///     CosmosDatabase db = await client.CreateDatabaseAsync(Guid.NewGuid().ToString())
    ///     CosmosContainer container = await db.Containers.CreateContainerAsync(Guid.NewGuid().ToString());
    /// }
    /// ]]>
    /// </code>
    /// </example>
    /// 
    /// <example>
    /// This example create a <see cref="CosmosClient"/>, <see cref="CosmosDatabase"/>, and a <see cref="CosmosContainer"/>.
    /// The CosmosClient is created with the connection string.
    /// <code language="c#">
    /// <![CDATA[
    /// using (CosmosClient cosmosClient = new CosmosClient(
    ///     connectionString: "AccountEndpoint=https://testcosmos.documents.azure.com:443/;AccountKey=SuperSecretKey;"))
    /// {
    ///     CosmosDatabase db = await client.CreateDatabaseAsync(Guid.NewGuid().ToString())
    ///     CosmosContainer container = await db.Containers.CreateContainerAsync(Guid.NewGuid().ToString());
    /// }
    /// ]]>
    /// </code>
    /// </example>
    public partial class CosmosClient : IDisposable
    {
        private Lazy<CosmosOffers> offerSet;

        static CosmosClient()
        {
            HttpConstants.Versions.CurrentVersion = HttpConstants.Versions.v2018_12_31;
            HttpConstants.Versions.CurrentVersionUTF8 = Encoding.UTF8.GetBytes(HttpConstants.Versions.CurrentVersion);

            // V3 always assumes assemblies exists
            // Shall revisit on feedback
            ServiceInteropWrapper.AssembliesExist = new Lazy<bool>(() => true);
        }

        /// <summary>
        /// Create a new CosmosClient used for mock testing
        /// </summary>
        protected CosmosClient()
        {
        }

        /// <summary>
        /// Create a new CosmosClient with the connection
        /// </summary>
        /// <param name="connectionString">The connection string to the cosmos account. Example: https://mycosmosaccount.documents.azure.com:443/;AccountKey=SuperSecretKey;</param>
        /// <example>
        /// This example creates a CosmosClient
        /// <code language="c#">
        /// <![CDATA[
        /// using (CosmosClient cosmosClient = new CosmosClient(
        ///     connectionString: "https://testcosmos.documents.azure.com:443/;AccountKey=SuperSecretKey;"))
        /// {
        ///     // Create a database and other CosmosClient operations
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public CosmosClient(string connectionString)
            : this(new CosmosClientOptions(connectionString))
        {
        }

        /// <summary>
        /// Create a new CosmosClient with the account endpoint URI string and account key
        /// </summary>
        /// <param name="accountEndPoint">The cosmos service endpoint to use to create the client.</param>
        /// <param name="accountKey">The cosmos account key to use to create the client.</param>
        /// <example>
        /// This example creates a CosmosClient
        /// <code language="c#">
        /// <![CDATA[
        /// using (CosmosClient cosmosClient = new CosmosClient(
        ///     accountEndPoint: "https://testcosmos.documents.azure.com:443/",
        ///     accountKey: "SuperSecretKey"))
        /// {
        ///     // Create a database and other CosmosClient operations
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public CosmosClient(
            string accountEndPoint,
            string accountKey)
            : this(new CosmosClientOptions(accountEndPoint, accountKey))
        {
        }

        /// <summary>
        /// Create a new CosmosClient with the cosmosClientOption
        /// </summary>
        /// <param name="clientOptions">The <see cref="CosmosClientOptions"/> used to initialize the cosmos client.</param>
        /// <example>
        /// This example creates a CosmosClient through explicit CosmosClientOptions
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClientOptions clientOptions = new CosmosClientOptions("accountEndpoint", "accountkey");
        /// clientOptions.ApplicationRegion = "East US 2";
        /// clientOptions.ConnectionMode = ConnectionMode.Direct;
        /// clientOptions.RequestTimeout = TimeSpan.FromSeconds(5);
        /// 
        /// using (CosmosClient client = new CosmosClient(clientOptions))
        /// {
        ///     // Create a database and other CosmosClient operations
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// This example creates a CosmosClient through builder
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder("accountEndpoint", "accountkey")
        /// .UseConsistencyLevel(ConsistencyLevel.Eventual)
        /// .WithApplicationRegion("East US 2");
        /// CosmosClient client = cosmosClientBuilder.Build();
        /// ]]>
        /// </code>
        /// </example>
        public CosmosClient(CosmosClientOptions clientOptions)
        {
            if (clientOptions == null)
            {
                throw new ArgumentNullException(nameof(clientOptions));
            }

            CosmosClientOptions clientOptionsClone = clientOptions.Clone();

            DocumentClient documentClient = new DocumentClient(
                clientOptionsClone.AccountEndPoint,
                clientOptionsClone.AccountKey,
                apitype: clientOptionsClone.ApiType,
                sendingRequestEventArgs: clientOptionsClone.SendingRequestEventArgs,
                transportClientHandlerFactory: clientOptionsClone.TransportClientHandlerFactory,
                connectionPolicy: clientOptionsClone.GetConnectionPolicy(),
                enableCpuMonitor: clientOptionsClone.EnableCpuMonitor,
                storeClientFactory: clientOptionsClone.StoreClientFactory);

            this.Init(
                clientOptionsClone,
                documentClient);
        }

        /// <summary>
        /// Used for unit testing only.
        /// </summary>
        internal CosmosClient(
            CosmosClientOptions cosmosClientOptions,
            DocumentClient documentClient)
        {
            if (cosmosClientOptions == null)
            {
                throw new ArgumentNullException(nameof(cosmosClientOptions));
            }

            if (documentClient == null)
            {
                throw new ArgumentNullException(nameof(documentClient));
            }

            this.Init(cosmosClientOptions, documentClient);
        }

        /// <summary>
        /// The <see cref="Cosmos.CosmosClientOptions"/> used initialize CosmosClient
        /// </summary>
        public virtual CosmosClientOptions ClientOptions { get; private set; }

        internal CosmosOffers Offers => this.offerSet.Value;
        internal DocumentClient DocumentClient { get; set; }
        internal RequestInvokerHandler RequestHandler { get; private set; }
        internal ConsistencyLevel AccountConsistencyLevel { get; private set; }
        internal CosmosResponseFactory ResponseFactory { get; private set; }

        /// <summary>
        /// Read the <see cref="Microsoft.Azure.Cosmos.CosmosAccountSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <returns>
        /// A <see cref="CosmosAccountSettings"/> wrapped in a <see cref="System.Threading.Tasks.Task"/> object.
        /// </returns>
        public virtual Task<CosmosAccountSettings> GetAccountSettingsAsync()
        {
            return ((IDocumentClientInternal)this.DocumentClient).GetDatabaseAccountInternalAsync(this.ClientOptions.AccountEndPoint);
        }

        /// <summary>
        /// Get cosmos container proxy. 
        /// </summary>
        /// <remarks>Proxy existence doesn't guarantee either database or container existence.</remarks>
        /// <param name="databaseId">cosmos database name</param>
        /// <param name="containerId">comsos container name</param>
        /// <returns>Cosmos container proxy</returns>
        public virtual CosmosContainer GetContainer(string databaseId, string containerId)
        {
            if (string.IsNullOrEmpty(databaseId))
            {
                throw new ArgumentNullException(nameof(databaseId));
            }

            if (string.IsNullOrEmpty(containerId))
            {
                throw new ArgumentNullException(nameof(containerId));
            }

            return this.GetDatabase(databaseId).GetContainer(containerId);
        }

        internal void Init(
            CosmosClientOptions clientOptions,
            DocumentClient documentClient)
        {
            this.ClientOptions = clientOptions;
            this.DocumentClient = documentClient;

            //Request pipeline 
            ClientPipelineBuilder clientPipelineBuilder = new ClientPipelineBuilder(
                this,
                this.ClientOptions.CustomHandlers);

            // DocumentClient is not initialized with any consistency overrides so default is backend consistency
            this.AccountConsistencyLevel = (ConsistencyLevel)this.DocumentClient.ConsistencyLevel;

            this.RequestHandler = clientPipelineBuilder.Build();

            this.ResponseFactory = new CosmosResponseFactory(
                defaultJsonSerializer: this.ClientOptions.SettingsSerializer,
                userJsonSerializer: this.ClientOptions.CosmosSerializerWithWrapperOrDefault);

            this.ClientContext = new CosmosClientContextCore(
                client: this,
                clientOptions: this.ClientOptions,
                userJsonSerializer: this.ClientOptions.CosmosSerializerWithWrapperOrDefault,
                defaultJsonSerializer: this.ClientOptions.SettingsSerializer,
                cosmosResponseFactory: this.ResponseFactory,
                requestHandler: this.RequestHandler,
                documentClient: this.DocumentClient,
                documentQueryClient: new DocumentQueryClient(this.DocumentClient));

            this.offerSet = new Lazy<CosmosOffers>(() => new CosmosOffers(this.DocumentClient), LazyThreadSafetyMode.PublicationOnly);
        }

        /// <summary>
        /// Dispose of cosmos client
        /// </summary>
        /// <param name="disposing">True if disposing</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.DocumentClient != null)
            {
                this.DocumentClient.Dispose();
                this.DocumentClient = null;
            }
        }

        /// <summary>
        /// Dispose of cosmos client
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
