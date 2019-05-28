﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Operations for reading or deleting an existing database.
    ///
    /// <see cref="CosmosDatabases"/> for or creating new databases, and reading/querying all databases; use `client.Databases`.
    /// </summary>
    internal class CosmosDatabaseCore : CosmosDatabase
    {
        /// <summary>
        /// Only used for unit testing
        /// </summary>
        internal CosmosDatabaseCore()
        {
        }

        internal CosmosDatabaseCore(
            CosmosClientContext clientContext,
            string databaseId)
        {
            this.Id = databaseId;
            this.ClientContext = clientContext;
            this.LinkUri = clientContext.CreateLink(
                parentLink: null,
                uriPathSegment: Paths.DatabasesPathSegment,
                id: databaseId);

            this.Containers = new CosmosContainersCore(clientContext, this);
        }

        public override string Id { get; }
        public override CosmosContainers Containers { get; }

        internal virtual Uri LinkUri { get; }

        internal CosmosClientContext ClientContext { get; }

        public override Task<DatabaseResponse> ReadAsync(
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.ReadAsStreamAsync(
                        requestOptions: requestOptions,
                        cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateDatabaseResponseAsync(this, response);
        }

        public override Task<DatabaseResponse> DeleteAsync(
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.DeleteAsStreamAsync(
                        requestOptions: requestOptions,
                        cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateDatabaseResponseAsync(this, response);
        }

        public override async Task<int?> ReadProvisionedThroughputAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosOfferResult offerResult = await this.ReadProvisionedThroughputIfExistsAsync(cancellationToken);
            if (offerResult.StatusCode == HttpStatusCode.OK || offerResult.StatusCode == HttpStatusCode.NotFound)
            {
                return offerResult.Throughput;
            }

            throw offerResult.CosmosException;
        }

        public override async Task ReplaceProvisionedThroughputAsync(
            int throughput,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosOfferResult offerResult = await this.ReplaceProvisionedThroughputIfExistsAsync(throughput, cancellationToken);
            if (offerResult.StatusCode != HttpStatusCode.OK)
            {
                throw offerResult.CosmosException;
            }
        }

        public override Task<CosmosResponseMessage> ReadAsStreamAsync(
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessAsync(
                OperationType.Read,
                requestOptions,
                cancellationToken);
        }

        public override Task<CosmosResponseMessage> DeleteAsStreamAsync(
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessAsync(
                OperationType.Delete,
                requestOptions,
                cancellationToken);
        }

        internal Task<CosmosOfferResult> ReadProvisionedThroughputIfExistsAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.GetRIDAsync(cancellationToken)
                .ContinueWith(task => this.ClientContext.Client.Offers.ReadProvisionedThroughputIfExistsAsync(task.Result, cancellationToken), cancellationToken)
                .Unwrap();
        }

        internal Task<CosmosOfferResult> ReplaceProvisionedThroughputIfExistsAsync(
            int targetThroughput,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<string> rid = this.GetRIDAsync(cancellationToken);
            return rid.ContinueWith(task => this.ClientContext.Client.Offers.ReplaceThroughputIfExistsAsync(task.Result, targetThroughput, cancellationToken), cancellationToken)
                .Unwrap();
        }

        internal virtual Task<string> GetRIDAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ReadAsync(cancellationToken: cancellationToken)
                .ContinueWith(task =>
                {
                    DatabaseResponse response = task.Result;
                    return response.Resource.ResourceId;
                }, cancellationToken);
        }

        private Task<CosmosResponseMessage> ProcessAsync(
            OperationType operationType,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ClientContext.ProcessResourceOperationAsStreamAsync(
                resourceUri: this.LinkUri,
                resourceType: ResourceType.Database,
                operationType: operationType,
                requestOptions: requestOptions,
                cosmosContainerCore: null,
                partitionKey: null,
                streamPayload: null,
                requestEnricher: null,
                cancellationToken: cancellationToken);
        }
    }
}
