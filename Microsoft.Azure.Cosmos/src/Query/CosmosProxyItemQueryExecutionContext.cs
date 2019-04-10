﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Query.ParallelQuery;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary>
    /// This class is used as a proxy to wrap the DefaultDocumentQueryExecutionContext which is needed 
    /// for sending the query to Gateway first and then uses PipelinedDocumentQueryExecutionContext after
    /// it gets the necessary info. This has been added since we
    /// haven't produced Linux/Mac version of the ServiceInterop native binary which holds the logic for
    /// parsing the query without having this extra hop to Gateway
    /// </summary>
    internal sealed class CosmosProxyItemQueryExecutionContext : IDocumentQueryExecutionContext
    {
        private IDocumentQueryExecutionContext innerExecutionContext;

        CosmosQueryContext queryContext;

        private readonly CosmosContainerSettings collection;

        private CosmosProxyItemQueryExecutionContext(
            IDocumentQueryExecutionContext innerExecutionContext,
            CosmosQueryContext queryContext,
            CosmosContainerSettings collection)
        {
            this.innerExecutionContext = innerExecutionContext;

            this.queryContext = queryContext;

            this.collection = collection;
        }

        public static CosmosProxyItemQueryExecutionContext CreateAsync(
            CosmosQueryContext queryContext,
            CancellationToken token,
            CosmosContainerSettings collection)
        {
            token.ThrowIfCancellationRequested();
            IDocumentQueryExecutionContext innerExecutionContext =
             new CosmosGatewayQueryExecutionContext(queryContext);

            return new CosmosProxyItemQueryExecutionContext(
                innerExecutionContext,
                queryContext,
                collection);
        }

        public bool IsDone
        {
            get { return this.innerExecutionContext.IsDone; }
        }

        public void Dispose()
        {
            this.innerExecutionContext.Dispose();
        }

        public async Task<FeedResponse<CosmosElement>> ExecuteNextAsync(CancellationToken token)
        {
            if (this.IsDone)
            {
                throw new InvalidOperationException(RMResources.DocumentQueryExecutionContextIsDone);
            }

            Error error = null;

            try
            {
                return await this.innerExecutionContext.ExecuteNextAsync(token);
            }
            catch (CosmosException ex)
            {
                if (ex.StatusCode != HttpStatusCode.BadRequest || ex.SubStatusCode != (int)SubStatusCodes.CrossPartitionQueryNotServable)
                {
                    throw;
                }

                error = ex.Error;
            }

            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo =
                    JsonConvert.DeserializeObject<PartitionedQueryExecutionInfo>(error.AdditionalErrorInfo);

            string rewrittenQuery = partitionedQueryExecutionInfo.QueryInfo.RewrittenQuery;
            if (!string.IsNullOrEmpty(rewrittenQuery))
            {
                this.queryContext.SqlQuerySpec.QueryText = rewrittenQuery;
            }

            List<PartitionKeyRange> partitionKeyRanges =
                await this.queryContext.QueryClient.GetTargetPartitionKeyRanges(
                    this.queryContext.ResourceLink.OriginalString,
                    this.collection.ResourceId,
                    partitionedQueryExecutionInfo.QueryRanges);

            this.innerExecutionContext = await CosmosQueryExecutionContextFactory.CreateSpecializedDocumentQueryExecutionContext(
                this.queryContext,
                partitionedQueryExecutionInfo,
                partitionKeyRanges,
                this.collection.ResourceId,
                token);

            return await this.innerExecutionContext.ExecuteNextAsync(token);
        }
    }
}
