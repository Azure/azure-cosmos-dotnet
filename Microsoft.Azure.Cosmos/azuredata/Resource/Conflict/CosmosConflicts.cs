﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Operations for reading/querying conflicts in a Azure Cosmos container.
    /// </summary>
    public abstract class CosmosConflicts
    {
        /// <summary>
        /// Delete a conflict from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="conflict">The conflict to delete.</param>
        /// <param name="partitionKey">The partition key for the conflict.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        /// <seealso cref="ConflictProperties"/>
        public abstract Task<Response> DeleteAsync(
            ConflictProperties conflict,
            PartitionKey partitionKey,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Reads the item that originated the conflict.
        /// </summary>
        /// <param name="conflict">The conflict for which we want to read the item.</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The current state of the item associated with the conflict.</returns>
        /// <seealso cref="ConflictProperties"/>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator<ConflictProperties> conflictIterator = conflicts.GetConflictQueryIterator();
        /// while (conflictIterator.HasMoreResults)
        /// {
        ///     foreach(ConflictProperties item in await conflictIterator.ReadNextAsync())
        ///     {
        ///         MyClass intendedChanges = conflicts.ReadConflictContent<MyClass>(item);
        ///         ItemResponse<MyClass> currentState = await conflicts.ReadCurrentAsync<MyClass>(intendedChanges.MyPartitionKey, item);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ItemResponse<T>> ReadCurrentAsync<T>(
            ConflictProperties conflict,
            PartitionKey partitionKey,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Reads the content of the Conflict resource in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="conflict">The conflict for which we want to read the content of.</param>
        /// <returns>The content of the conflict.</returns>
        /// <seealso cref="ConflictProperties"/>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator<ConflictProperties> conflictIterator = conflicts.GetConflictQueryIterator();
        /// while (conflictIterator.HasMoreResults)
        /// {
        ///     foreach(ConflictProperties item in await conflictIterator.ReadNextAsync())
        ///     {
        ///         MyClass intendedChanges = conflicts.ReadConflictContent<MyClass>(item);
        ///         ItemResponse<MyClass> currentState = await conflicts.ReadCurrentAsync<MyClass>(intendedChanges.MyPartitionKey, item);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract T ReadConflictContent<T>(ConflictProperties conflict);

        /// <summary>
        /// Obtains an iterator to go through the <see cref="ConflictProperties"/> on an Azure Cosmos container.
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An iterator to go through the conflicts.</returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// await foreach(ConflictProperties item in conflicts.GetConflictQueryIterator())
        /// {
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract AsyncPageable<T> GetConflictQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets an iterator to go through all the conflicts for the container as the original ResponseMessage
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An iterator to go through the conflicts.</returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// await foreach(Response response in conflicts.GetConflictQueryStreamIterator())
        /// {
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract IAsyncEnumerable<Response> GetConflictQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Obtains an iterator to go through the <see cref="ConflictProperties"/> on an Azure Cosmos container.
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An iterator to go through the conflicts.</returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// await foreach(ConflictProperties item in conflicts.GetConflictQueryIterator())
        /// {
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract AsyncPageable<T> GetConflictQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets an iterator to go through all the conflicts for the container as the original ResponseMessage
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An iterator to go through the conflicts.</returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// await foreach (Response response in conflicts.GetConflictQueryStreamIterator())
        /// {
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract IAsyncEnumerable<Response> GetConflictQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}