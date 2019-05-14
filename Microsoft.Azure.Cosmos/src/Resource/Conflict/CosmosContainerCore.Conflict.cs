﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal partial class CosmosContainerCore
    {
        public override Task<CosmosResponseMessage> DeleteConflictAsync(
            object partitionKey,
            string id,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (partitionKey == null)
            {
                throw new ArgumentNullException(nameof(partitionKey));
            }

            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            Uri conflictLink = this.ClientContext.CreateLink(
                 parentLink: this.LinkUri.OriginalString,
                 uriPathSegment: Paths.ConflictsPathSegment,
                 id: id);

            return this.ClientContext.ProcessResourceOperationStreamAsync(
                resourceUri: conflictLink,
                resourceType: ResourceType.Conflict,
                operationType: OperationType.Delete,
                requestOptions: null,
                cosmosContainerCore: this,
                partitionKey: partitionKey,
                streamPayload: null,
                requestEnricher: null,
                cancellationToken: cancellationToken);
        }

        public override async Task<ItemResponse<T>> ReadConflictSourceItemAsync<T>(
            object partitionKey,
            CosmosConflictSettings cosmosConflict,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (partitionKey == null)
            {
                throw new ArgumentNullException(nameof(partitionKey));
            }

            if (cosmosConflict == null)
            {
                throw new ArgumentNullException(nameof(cosmosConflict));
            }

            // SourceResourceId is RID based on Conflicts, so we need to obtain the db and container rid
            CosmosDatabaseCore databaseCore = (CosmosDatabaseCore) this.Database;
            string databaseResourceId = await databaseCore.GetRID(cancellationToken);
            string containerResourceId = await this.GetRID(cancellationToken);

            Uri dbLink = this.ClientContext.CreateLink(
                parentLink: string.Empty,
                uriPathSegment: Paths.DatabasesPathSegment,
                id: databaseResourceId);

            Uri containerLink = this.ClientContext.CreateLink(
                parentLink: dbLink.OriginalString,
                uriPathSegment: Paths.CollectionsPathSegment,
                id: containerResourceId);
            
            Uri itemLink = this.ClientContext.CreateLink(
                parentLink: containerLink.OriginalString,
                uriPathSegment: Paths.DocumentsPathSegment,
                id: cosmosConflict.SourceResourceId);

            Task<CosmosResponseMessage> response = this.ClientContext.ProcessResourceOperationStreamAsync(
                resourceUri: itemLink,
                resourceType: ResourceType.Document,
                operationType: OperationType.Read,
                requestOptions: null,
                cosmosContainerCore: this,
                partitionKey: partitionKey,
                streamPayload: null,
                requestEnricher: null,
                cancellationToken: cancellationToken);

            return await this.ClientContext.ResponseFactory.CreateItemResponse<T>(response);
        }

        public override FeedIterator<CosmosConflictSettings> GetConflictsIterator(
            int? maxItemCount = null,
            string continuationToken = null)
        {
            return new FeedIteratorCore<CosmosConflictSettings>(
                maxItemCount,
                continuationToken,
                null,
                this.ConflictsFeedRequestExecutor);
        }

        public override FeedIterator GetConflictsStreamIterator(
            int? maxItemCount = null,
            string continuationToken = null)
        {
            return new FeedIteratorCore(
                maxItemCount,
                continuationToken,
                null,
                this.ConflictsFeedStreamRequestExecutor);
        }

        private Task<CosmosResponseMessage> ConflictsFeedStreamRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            RequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            return this.ClientContext.ProcessResourceOperationAsync<CosmosResponseMessage>(
                resourceUri: this.LinkUri,
                resourceType: Documents.ResourceType.Conflict,
                operationType: Documents.OperationType.ReadFeed,
                requestOptions: options,
                requestEnricher: request =>
                {
                    QueryRequestOptions.FillContinuationToken(request, continuationToken);
                    QueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => response,
                cosmosContainerCore: this,
                partitionKey: null,
                streamPayload: null,
                cancellationToken: cancellationToken);
        }

        private Task<FeedResponse<CosmosConflictSettings>> ConflictsFeedRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            RequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            return this.ClientContext.ProcessResourceOperationAsync<FeedResponse<CosmosConflictSettings>>(
                resourceUri: this.LinkUri,
                resourceType: Documents.ResourceType.Conflict,
                operationType: Documents.OperationType.ReadFeed,
                requestOptions: options,
                cosmosContainerCore: this,
                partitionKey: null,
                streamPayload: null,
                requestEnricher: request =>
                {
                    QueryRequestOptions.FillContinuationToken(request, continuationToken);
                    QueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => this.ClientContext.ResponseFactory.CreateResultSetQueryResponse<CosmosConflictSettings>(response),
                cancellationToken: cancellationToken);
        }
    }
}
