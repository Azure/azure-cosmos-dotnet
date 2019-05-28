﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Utils
{
    using System.Globalization;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;

    internal static class CosmosContainerExtensions
    {
        public static async Task<T> TryGetItemAsync<T>(
            this CosmosContainer container,
            object partitionKey,
            string itemId)
        {
            var response = await container.ReadItemAsync<T>(
                    partitionKey,
                    itemId)
                    .ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return default(T);
            }

            return response;
        }

        public static async Task<ItemResponse<T>> TryCreateItemAsync<T>(
            this CosmosContainer container, 
            object partitionKey, 
            T item)
        {
            var response = await container.CreateItemAsync<T>(item, new ItemRequestOptions { PartitionKey = partitionKey }).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                // Ignore-- document already exists.
                return null;
            }

            return response;
        }

        public static async Task<T> TryDeleteItemAsync<T>(
            this CosmosContainer container,
            object partitionKey,
            string itemId,
            ItemRequestOptions cosmosItemRequestOptions = null)
        {
            var response = await container.DeleteItemAsync<T>(partitionKey, itemId, cosmosItemRequestOptions).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return default(T);
            }

            return response.Resource;
        }

        public static async Task<bool> ItemExistsAsync(
            this CosmosContainer container,
            object partitionKey,
            string itemId)
        {
            var response = await container.ReadItemAsStreamAsync(
                        partitionKey,
                        itemId)
                        .ConfigureAwait(false);

            return response.IsSuccessStatusCode;
        }

        public static async Task<string> GetMonitoredContainerRidAsync(
            this CosmosContainer monitoredContainer,
            string suggestedMonitoredRid,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!string.IsNullOrEmpty(suggestedMonitoredRid))
            {
                return suggestedMonitoredRid;
            }

            string containerRid = await ((CosmosContainerCore)monitoredContainer).GetRIDAsync(cancellationToken);
            string databaseRid = await ((CosmosDatabaseCore)((CosmosContainerCore)monitoredContainer).Database).GetRIDAsync(cancellationToken);
            return $"{databaseRid}_{containerRid}";
        }

        public static string GetLeasePrefix(
            this CosmosContainer monitoredContainer,
            ChangeFeedLeaseOptions changeFeedLeaseOptions,
            string monitoredContainerRid)
        {
            string optionsPrefix = changeFeedLeaseOptions.LeasePrefix ?? string.Empty;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}{1}_{2}",
                optionsPrefix,
                ((CosmosContainerCore)monitoredContainer).ClientContext.ClientOptions.AccountEndPoint.Host,
                monitoredContainerRid);
        }
    }
}