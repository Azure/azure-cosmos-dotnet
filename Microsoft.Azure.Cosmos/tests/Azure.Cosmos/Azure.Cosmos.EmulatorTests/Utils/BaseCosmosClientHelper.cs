﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Data.Cosmos;

    public abstract class BaseCosmosClientHelper
    {
        protected CosmosClient cosmosClient = null;
        protected Database database = null;
        protected CancellationTokenSource cancellationTokenSource = null;
        protected CancellationToken cancellationToken;

        public async Task TestInit()
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.cancellationToken = this.cancellationTokenSource.Token;
            (string endpoint, string key) = TestCommon.GetAccountInfo();
            this.cosmosClient = new CosmosClient(endpoint, key);
            this.database = await this.cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString(),
                cancellationToken: this.cancellationToken);
        }

        public async Task TestCleanup()
        {
            if (this.cosmosClient == null)
            {
                return;
            }

            if (this.database != null)
            {
                using (Response response = await this.database.DeleteStreamAsync(
                    requestOptions: null,
                    cancellationToken: this.cancellationToken)) { }
            }

            this.cancellationTokenSource?.Cancel();

            this.cosmosClient.Dispose();
        }
    }
}
