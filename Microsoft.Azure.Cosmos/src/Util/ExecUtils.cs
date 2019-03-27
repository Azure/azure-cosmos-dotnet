﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;

    internal static class ExecUtils
    {
        internal static Task<T> ProcessResourceOperationAsync<T>(
            CosmosClient client,
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            Action<CosmosRequestMessage> requestEnricher,
            Func<CosmosResponseMessage, T> responseCreator,
            CancellationToken cancellationToken)
        {
            return ExecUtils.ProcessResourceOperationAsync(
                client,
                resourceUri,
                resourceType,
                operationType,
                requestOptions,
                partitionKey: null,
                streamPayload: null,
                requestEnricher: requestEnricher,
                responseCreator: responseCreator,
                cancellationToken: cancellationToken);
        }

        internal static Task<T> ProcessResourceOperationAsync<T>(
            CosmosClient client,
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            Func<CosmosResponseMessage, T> responseCreator,
            CancellationToken cancellationToken)
        {
            return ExecUtils.ProcessResourceOperationAsync(
                client,
                resourceUri,
                resourceType,
                operationType,
                requestOptions,
                partitionKey: null,
                streamPayload: null,
                requestEnricher: null,
                responseCreator: responseCreator,
                cancellationToken: cancellationToken);
        }

        internal static Task<T> ProcessResourceOperationAsync<T>(
            CosmosClient client,
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            Stream streamPayload,
            Func<CosmosResponseMessage, T> responseCreator,
            CancellationToken cancellationToken)
        {
            return ExecUtils.ProcessResourceOperationAsync(
                client,
                resourceUri,
                resourceType,
                operationType,
                requestOptions,
                partitionKey: null,
                streamPayload: streamPayload,
                requestEnricher: null,
                responseCreator: responseCreator,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Used internally by friends ensrue robust argument and 
        /// exception-less handling
        /// </summary>
        internal static Task<T> ProcessResourceOperationAsync<T>(
            CosmosClient client,
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            Object partitionKey,
            Stream streamPayload,
            Action<CosmosRequestMessage> requestEnricher,
            Func<CosmosResponseMessage, T> responseCreator,
            CancellationToken cancellationToken)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (client.RequestHandler == null)
            {
                throw new ArgumentException(nameof(client));
            }

            if (resourceUri == null)
            {
                throw new ArgumentNullException(nameof(resourceUri));
            }

            if (responseCreator == null)
            {
                throw new ArgumentNullException(nameof(responseCreator));
            }

            CosmosRequestMessage request = ExecUtils.GenerateCosmosRequestMessage(
                resourceUri,
                resourceType,
                operationType,
                requestOptions,
                partitionKey,
                streamPayload,
                requestEnricher);

            return client.RequestHandler.SendAsync(request, cancellationToken)
                     .ContinueWith(task => responseCreator(task.Result), cancellationToken);
        }

        internal static Task<CosmosResponseMessage> ProcessResourceOperationStreamAsync(
            CosmosClient client,
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            Object partitionKey,
            Stream streamPayload,
            Action<CosmosRequestMessage> requestEnricher,
            CancellationToken cancellationToken)
        {
            CosmosRequestMessage request = ExecUtils.GenerateCosmosRequestMessage(
                resourceUri,
                resourceType,
                operationType,
                requestOptions,
                partitionKey,
                streamPayload,
                requestEnricher);

            return client.RequestHandler.SendAsync(request, cancellationToken);
        }

        private static CosmosRequestMessage GenerateCosmosRequestMessage(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            Object partitionKey,
            Stream streamPayload,
            Action<CosmosRequestMessage> requestEnricher)
        {
            HttpMethod method = ExecUtils.GetHttpMethod(operationType);

            CosmosRequestMessage request = new CosmosRequestMessage(method, resourceUri);
            request.OperationType = operationType;
            request.ResourceType = resourceType;
            request.RequestOptions = requestOptions;
            request.Content = streamPayload;

            if (partitionKey != null)
            {
                PartitionKey pk = new PartitionKey(partitionKey);
                request.Headers.PartitionKey = pk.InternalKey.ToJsonString();
            }

            if (operationType == OperationType.Upsert)
            {
                request.Headers.IsUpsert = bool.TrueString;
            }

            requestEnricher?.Invoke(request);

            return request;
        }

        internal static HttpMethod GetHttpMethod(
            OperationType operationType)
        {
            HttpMethod httpMethod = HttpMethod.Head;
            if (operationType == OperationType.Create ||
                operationType == OperationType.Upsert ||
                operationType == OperationType.Query ||
                operationType == OperationType.SqlQuery ||
                operationType == OperationType.Batch ||
                operationType == OperationType.ExecuteJavaScript)
            {
                return HttpMethod.Post;
            }
            else if (operationType == OperationType.Read ||
                operationType == OperationType.ReadFeed)
            {
                return HttpMethod.Get;
            }
            else if (operationType == OperationType.Replace)
            {
                return HttpMethod.Put;
            }
            else if (operationType == OperationType.Delete)
            {
                return HttpMethod.Delete;
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
