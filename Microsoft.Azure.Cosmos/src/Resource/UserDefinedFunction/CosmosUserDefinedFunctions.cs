﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Operations for creating new user defined function, and reading/querying all user defined functions
    ///
    /// <see cref="CosmosUserDefinedFunction"/> for reading, replacing, or deleting an existing user defined functions.
    /// </summary>
    internal class CosmosUserDefinedFunctions
    {
        private readonly CosmosContainerCore container;
        private readonly CosmosClientContext clientContext;

        /// <summary>
        /// Create a <see cref="CosmosUserDefinedFunctions"/>
        /// </summary>
        /// <param name="clientContext">The client context.</param>
        /// <param name="container">The <see cref="CosmosContainer"/> the user defined function set is related to.</param>
        protected internal CosmosUserDefinedFunctions(
            CosmosClientContext clientContext,
            CosmosContainerCore container)
        {
            this.container = container;
            this.clientContext = clientContext;
        }

        /// <summary>
        /// Creates a user defined function as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="userDefinedFunctionSettings">The <see cref="CosmosUserDefinedFunctionSettings"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the user defined function request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A task object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="userDefinedFunctionSettings"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occurred during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a user defined function are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an Id was not supplied for the new user defined function or that the Body was malformed.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - You have reached your quota of user defined functions for the collection supplied. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="CosmosUserDefinedFunctionSettings"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the body of the <see cref="CosmosUserDefinedFunctionSettings"/> you tried to create was too large.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///  This creates a user defined function then uses the function in an item query.
        /// <code language="c#">
        /// <![CDATA[
        /// 
        /// await this.container.UserDefinedFunctions.CreateUserDefinedFunctionAsync(
        ///     new CosmosUserDefinedFunctionSettings 
        ///     { 
        ///         Id = "calculateTax", 
        ///         Body = @"function(amt) { return amt * 0.05; }" 
        ///     });
        ///
        /// CosmosSqlQueryDefinition sqlQuery = new CosmosSqlQueryDefinition(
        ///     "SELECT VALUE udf.calculateTax(t.cost) FROM toDoActivity t where t.cost > @expensive and t.status = @status")
        ///     .UseParameter("@expensive", 9000)
        ///     .UseParameter("@status", "Done");
        ///
        /// FeedIterator<double> feedIterator = this.container.Items.CreateItemQuery<double>(
        ///     sqlQueryDefinition: sqlQuery,
        ///     partitionKey: "Done");
        ///
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     foreach (var tax in await feedIterator.FetchNextSetAsync())
        ///     {
        ///         Console.WriteLine(tax);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<UserDefinedFunctionResponse> CreateUserDefinedFunctionAsync(
            CosmosUserDefinedFunctionSettings userDefinedFunctionSettings,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.clientContext.ProcessResourceOperationStreamAsync(
                resourceUri: this.container.LinkUri,
                resourceType: ResourceType.UserDefinedFunction,
                operationType: OperationType.Create,
                requestOptions: requestOptions,
                cosmosContainerCore: this.container,
                partitionKey: null,
                streamPayload: CosmosResource.ToStream(userDefinedFunctionSettings),
                requestEnricher: null,
                cancellationToken: cancellationToken);

            return this.clientContext.ResponseFactory.CreateUserDefinedFunctionResponse(this[userDefinedFunctionSettings.Id], response);
        }

        /// <summary>
        /// Gets an iterator to go through all the user defined functions for the container
        /// </summary>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <example>
        /// Get an iterator for all the triggers under the cosmos container
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator<CosmosUserDefinedFunctionSettings> feedIterator = this.container.UserDefinedFunctions.GetUserDefinedFunctionsIterator();
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     foreach(CosmosUserDefinedFunctionSettings settings in await feedIterator.FetchNextSetAsync())
        ///     {
        ///          Console.WriteLine(settings.Id); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public FeedIterator<CosmosUserDefinedFunctionSettings> GetUserDefinedFunctionsIterator(
            int? maxItemCount = null,
            string continuationToken = null)
        {
            return new FeedIteratorCore<CosmosUserDefinedFunctionSettings>(
                maxItemCount,
                continuationToken,
                null,
                this.ContainerFeedRequestExecutor);
        }

        /// <summary>
        /// Returns a reference to a user defined functions object. 
        /// </summary>
        /// <param name="id">The cosmos user defined functions id.</param>
        /// <remarks>
        /// Note that the user defined functions must be explicitly created, if it does not already exist, before
        /// you can read from it or write to it.
        /// </remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosUserDefinedFunction userDefinedFunction = this.cosmosContainer.UserDefinedFunction["myUserDefinedFunctionId"];
        /// UserDefinedFunctionResponse response = await userDefinedFunction.ReadAsync();
        /// ]]>
        /// </code>
        /// </example>
        public CosmosUserDefinedFunction this[string id] => new CosmosUserDefinedFunction(
            this.clientContext,
            this.container, 
            id);

        private Task<FeedResponse<CosmosUserDefinedFunctionSettings>> ContainerFeedRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            RequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            Debug.Assert(state == null);

            return this.clientContext.ProcessResourceOperationAsync<FeedResponse<CosmosUserDefinedFunctionSettings>>(
                resourceUri: this.container.LinkUri,
                resourceType: ResourceType.UserDefinedFunction,
                operationType: OperationType.ReadFeed,
                requestOptions: options,
                cosmosContainerCore: null,
                partitionKey: null,
                streamPayload: null,
                requestEnricher: request =>
                {
                    QueryRequestOptions.FillContinuationToken(request, continuationToken);
                    QueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => this.clientContext.ResponseFactory.CreateResultSetQueryResponse<CosmosUserDefinedFunctionSettings>(response),
                cancellationToken: cancellationToken);
        }
    }
}
