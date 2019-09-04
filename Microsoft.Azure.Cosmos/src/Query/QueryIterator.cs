﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;

    internal class QueryIterator : FeedIterator
    {
        private readonly CosmosQueryExecutionContextFactory cosmosQueryExecutionContext;

        internal QueryIterator(
            CosmosQueryClient client,
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            QueryRequestOptions queryRequestOptions,
            Uri resourceLink,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery)
        {
            this.cosmosQueryExecutionContext = new CosmosQueryExecutionContextFactory(
                client: client,
                resourceTypeEnum: Documents.ResourceType.Document,
                operationType: Documents.OperationType.Query,
                resourceType: typeof(QueryResponseCore),
                sqlQuerySpec: sqlQuerySpec,
                continuationToken: continuationToken,
                queryRequestOptions: queryRequestOptions,
                resourceLink: resourceLink,
                isContinuationExpected: isContinuationExpected,
                allowNonValueAggregateQuery: allowNonValueAggregateQuery,
                correlatedActivityId: Guid.NewGuid());
        }

        public override bool HasMoreResults => !this.cosmosQueryExecutionContext.IsDone;

        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            // This catches exception thrown by the pipeline and converts it to QueryResponse
            ResponseMessage response;
            try
            {
                QueryResponseCore responseCore = await this.cosmosQueryExecutionContext.ExecuteNextAsync(cancellationToken);
                CosmosQueryContext cosmosQueryContext = this.cosmosQueryExecutionContext.CosmosQueryContext;
                QueryResponse queryResponse;
                if (responseCore.IsSuccess)
                {
                    queryResponse = QueryResponse.CreateSuccess(
                        result: responseCore.CosmosElements,
                        count: responseCore.CosmosElements.Count,
                        responseLengthBytes: responseCore.ResponseLengthBytes,
                        queryMetrics: responseCore.QueryMetrics,
                        responseHeaders: new CosmosQueryResponseMessageHeaders(
                            responseCore.ContinuationToken,
                            responseCore.DisallowContinuationTokenMessage,
                            cosmosQueryContext.ResourceTypeEnum,
                            cosmosQueryContext.ContainerResourceId)
                        {
                            RequestCharge = responseCore.RequestCharge,
                            ActivityId = responseCore.ActivityId
                        });
                }
                else
                {
                    queryResponse = QueryResponse.CreateFailure(
                        statusCode: responseCore.StatusCode,
                        error: null,
                        errorMessage: responseCore.ErrorMessage,
                        requestMessage: null,
                        responseHeaders: new CosmosQueryResponseMessageHeaders(
                            responseCore.ContinuationToken,
                            responseCore.DisallowContinuationTokenMessage,
                            cosmosQueryContext.ResourceTypeEnum,
                            cosmosQueryContext.ContainerResourceId)
                        {
                            RequestCharge = responseCore.RequestCharge,
                            ActivityId = responseCore.ActivityId
                        });
                }
                
                if (responseCore.QueryMetrics != null && responseCore.QueryMetrics.Count > 0)
                {
                    queryResponse.Diagnostics = new QueryOperationStatistics(responseCore.QueryMetrics);
                }

                queryResponse.CosmosSerializationOptions = cosmosQueryContext.QueryRequestOptions.CosmosSerializationFormatOptions;

                response = queryResponse;
            }
            catch (Documents.DocumentClientException exception)
            {
                response = exception.ToCosmosResponseMessage(request: null);
            }
            catch (CosmosException exception)
            {
                response = exception.ToCosmosResponseMessage(request: null);
            }
            catch (AggregateException ae)
            {
                response = TransportHandler.AggregateExceptionConverter(ae, null);
                if (response == null)
                {
                    throw;
                }
            }

            return response;
        }
    }
}
