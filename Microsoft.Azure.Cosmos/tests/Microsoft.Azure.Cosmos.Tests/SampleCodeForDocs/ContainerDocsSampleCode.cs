﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------


namespace Microsoft.Azure.Cosmos.SampleCodeForDocs
{
    using System.Threading.Tasks;

    internal class ContainerDocsSampleCode
    {
        private CosmosClient cosmosClient;

        internal void intitialize()
        {
            cosmosClient = new CosmosClient(
                accountEndpoint: "TestAccount",
                accountKey: "TestKey",
                clientOptions: new CosmosClientOptions());
        }

        public async Task ContainerCreateWithThroughput()
        {
            // <ContainerCreateWithThroughput>
            string containerName = "myContainerName";
            string partitionKeyPath = "/myPartitionKey";

            await this.cosmosClient.GetDatabase("myDatabase").CreateContainerAsync(
                id: containerName,
                partitionKeyPath: partitionKeyPath,
                throughput: 1000);
            // <ContainerCreateWithThroughput>
        }
    }
}
