//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;

    /// <summary>
    /// Refreshed named cache in-case of Gone with NameCacheIsStale
    /// </summary>
    internal class NamedCacheRetryHandler : AbstractRetryHandler
    {
        private readonly CosmosClient client;

        public NamedCacheRetryHandler(CosmosClient client)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            this.client = client;
        }

        internal override Task<IDocumentClientRetryPolicy> GetRetryPolicyAsync(CosmosRequestMessage request)
        {
            return Task.FromResult<IDocumentClientRetryPolicy>(new InvalidPartitionExceptionRetryPolicy(this.client.DocumentClient, null));
        }
    }
}