﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Linq;

    /// <summary>
    /// This class provides extension methods for cosmos LINQ code.
    /// </summary>
    public static class CosmosLinqExtensions
    {
        /// <summary>
        /// Determines if a certain property is defined or not.
        /// This method is to be used in LINQ expressions only and will be evaluated on server.
        /// There's no implementation provided in the client library.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>Returns true if this property is defined otherwise returns false.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var isDefinedQuery = documents.Where(document => document.Name.IsDefined());
        /// ]]>
        /// </code>
        /// </example>
        public static bool IsDefined(this object obj)
        {
            throw new NotImplementedException(ClientResources.TypeCheckExtensionFunctionsNotImplemented);
        }

        /// <summary>
        /// Determines if a certain property is null or not.
        /// This method is to be used in LINQ expressions only and will be evaluated on server.
        /// There's no implementation provided in the client library.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>Returns true if this property is null otherwise returns false.</returns>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var isNullQuery = documents.Where(document => document.Name.IsNull());
        /// ]]>
        /// </code>
        /// </example>s>
        public static bool IsNull(this object obj)
        {
            throw new NotImplementedException(ClientResources.TypeCheckExtensionFunctionsNotImplemented);
        }

        /// <summary>
        /// Determines if a certain property is of primitive JSON type.
        /// This method is to be used in LINQ expressions only and will be evaluated on server.
        /// There's no implementation provided in the client library.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>Returns true if this property is null otherwise returns false.</returns>
        /// <remarks>
        /// Primitive JSON types (Double, String, Boolean and Null)
        /// </remarks>
        /// <example>
        /// <code>
        /// <![CDATA[
        /// var isPrimitiveQuery = documents.Where(document => document.Name.IsPrimitive());
        /// ]]>
        /// </code>
        /// </example>s>
        public static bool IsPrimitive(this object obj)
        {
            throw new NotImplementedException(ClientResources.TypeCheckExtensionFunctionsNotImplemented);
        }

        /// <summary>
        /// This method generate query definition from LINQ query.
        /// </summary>
        /// <typeparam name="T">the type of object to query.</typeparam>
        /// <param name="query">the IQueryable{T} to be converted.</param>
        /// <returns>The queryDefinition which can be used in query execution.</returns>
        /// <example>
        /// This example shows how to generate query definition from LINQ.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// IQueryable<T> queryable = container.GetItemsQueryIterator<T>(allowSynchronousQueryExecution = true)
        ///                      .Where(t => b.id.contains("test"));
        /// QueryDefinition queryDefinition = queryable.ToQueryDefinition();
        /// ]]>
        /// </code>
        /// </example>
        public static QueryDefinition ToQueryDefinition<T>(this IQueryable<T> query)
        {
            CosmosLinqQuery<T> linqQuery = query as CosmosLinqQuery<T>;

            if (linqQuery == null)
            {
                throw new ArgumentOutOfRangeException(nameof(linqQuery), "ToSqlQueryText is only supported on cosmos LINQ query operations");
            }

            return linqQuery.ToQueryDefinition();
        }

        /// <summary>
        /// This extension method gets the FeedIterator from LINQ IQueryable to execute query asynchronously.
        /// This will create the fresh new FeedIterator when called.
        /// </summary>
        /// <typeparam name="T">the type of object to query.</typeparam>
        /// <param name="query">the IQueryable{T} to be converted.</param>
        /// <returns>An iterator to go through the items.</returns>
        /// <example>
        /// This example shows how to get FeedIterator from LINQ.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// IOrderedQueryable<ToDoActivity> linqQueryable = this.Container.GetItemLinqQueryable<ToDoActivity>();
        /// FeedIterator<ToDoActivity> setIterator = linqQueryable.Where(item => (item.taskNum < 100)).ToFeedIterator()
        /// ]]>
        /// </code>
        /// </example>
        public static FeedIterator<T> ToFeedIterator<T>(this IQueryable<T> query)
        {
            CosmosLinqQuery<T> linqQuery = query as CosmosLinqQuery<T>;

            if (linqQuery == null)
            {
                throw new ArgumentOutOfRangeException(nameof(linqQuery), "ToFeedIterator is only supported on cosmos LINQ query operations");
            }

            return linqQuery.ToFeedIterator();
        }
    }
}