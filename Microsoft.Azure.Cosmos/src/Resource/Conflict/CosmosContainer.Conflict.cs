﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    public abstract partial class CosmosContainer
    {
        /// <summary>
        /// Delete a conflict from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="id">The conflict id.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <seealso cref="CosmosConflictSettings"/>
        public abstract Task<CosmosResponseMessage> DeleteConflictAsync(
            object partitionKey,
            string id,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Reads the item that originated the conflict.
        /// </summary>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="cosmosConflict">The conflict for which we want to read the item.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <seealso cref="CosmosConflictSettings"/>
        public abstract Task<ItemResponse<T>> ReadConflictSourceItemAsync<T>(
            object partitionKey,
            CosmosConflictSettings cosmosConflict,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Obtains an iterator to go through the <see cref="CosmosConflictSettings"/> on an Azure Cosmos container.
        /// </summary>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator conflictIterator = await cosmosContainer.GetConflictsIterator();
        /// do
        /// {
        ///     QueryResponse<CosmosConflict> conflicts = await conflictIterator.FetchNextAsync();
        /// }
        /// while (conflictIterator.HasMoreResults);
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator<CosmosConflictSettings> GetConflictsIterator(
            int? maxItemCount = null,
            string continuationToken = null);

        /// <summary>
        /// Gets an iterator to go through all the conflicts for the container as the original CosmosResponseMessage
        /// </summary>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator conflictIterator = await cosmosContainer.Conflicts.GetConflictsIterator();
        /// do
        /// {
        ///     QueryResponse<CosmosConflict> conflicts = await conflictIterator.FetchNextAsync();
        /// }
        /// while (conflictIterator.HasMoreResults);
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator GetConflictsStreamIterator(
            int? maxItemCount = null,
            string continuationToken = null);
    }
}
