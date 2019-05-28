﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;


    [TestClass]
    public class CosmosNotFoundTests
    {
        public const string DoesNotExist = "DoesNotExist-69E1BD04-EC99-449B-9365-34DA9F4D4ECE";
        private static CosmosClient client = null;
        private static CosmosJsonSerializer jsonSerializer = new CosmosJsonSerializerCore();

        [ClassInitialize]
        public static void Initialize(TestContext textContext)
        {
            CosmosNotFoundTests.client = TestCommon.CreateCosmosClient();
        }

        [TestMethod]
        public async Task ValidateDatabaseNotFoundResponse()
        {
            CosmosDatabase database = CosmosNotFoundTests.client.Databases[DoesNotExist];
            this.VerifyNotFoundResponse(await database.ReadAsStreamAsync());
            this.VerifyNotFoundResponse(await database.DeleteAsStreamAsync());
        }

        [TestMethod]
        public async Task ValidateContainerNotFoundResponse()
        {
            CosmosDatabase dbDoesNotExist = CosmosNotFoundTests.client.Databases[DoesNotExist];
            await this.ContainerOperations(database: dbDoesNotExist, dbNotExist: true);

            CosmosDatabase dbExists = await client.Databases.CreateDatabaseAsync("NotFoundTest" + Guid.NewGuid().ToString());
            await this.ContainerOperations(database: dbExists, dbNotExist: false);
        }

        [TestMethod]
        public async Task ValidateQueryNotFoundResponse()
        {
            CosmosDatabase db = await CosmosNotFoundTests.client.Databases.CreateDatabaseAsync("NotFoundTest" + Guid.NewGuid().ToString());
            CosmosContainer container = await db.Containers.CreateContainerAsync("NotFoundTest" + Guid.NewGuid().ToString(), "/pk", 500);

            dynamic randomItem = new { id = "test", pk = "testpk" };
            await container.CreateItemAsync(randomItem.pk, randomItem);

            await container.DeleteAsync();

            var crossPartitionQueryIterator = container.CreateItemQueryAsStream("select * from t where true", maxConcurrency: 2);
            var queryResponse = await crossPartitionQueryIterator.FetchNextSetAsync();
            Assert.IsNotNull(queryResponse);
            Assert.AreEqual(HttpStatusCode.Gone, queryResponse.StatusCode);

            var queryIterator = container.CreateItemQueryAsStream("select * from t where true", maxConcurrency: 1, partitionKey: "testpk");
            this.VerifyQueryNotFoundResponse(await queryIterator.FetchNextSetAsync());

            var crossPartitionQueryIterator2 = container.CreateItemQueryAsStream("select * from t where true", maxConcurrency: 2);
            this.VerifyQueryNotFoundResponse(await crossPartitionQueryIterator2.FetchNextSetAsync());

            await db.DeleteAsync();
        }

        private async Task ContainerOperations(CosmosDatabase database, bool dbNotExist)
        {
            // Create should fail if the database does not exist
            if (dbNotExist)
            {
                CosmosContainerSettings newcontainerSettings = new CosmosContainerSettings(id: DoesNotExist, partitionKeyPath: "/pk");
                this.VerifyNotFoundResponse(await database.Containers.CreateContainerAsStreamAsync(newcontainerSettings, throughput: 500));
            }

            CosmosContainer doesNotExistContainer = database.Containers[DoesNotExist];
            this.VerifyNotFoundResponse(await doesNotExistContainer.ReadAsStreamAsync());

            CosmosContainerSettings containerSettings = new CosmosContainerSettings(id: DoesNotExist, partitionKeyPath: "/pk");
            this.VerifyNotFoundResponse(await doesNotExistContainer.ReplaceAsStreamAsync(containerSettings));
            this.VerifyNotFoundResponse(await doesNotExistContainer.DeleteAsStreamAsync());

            // Validate Child resources
            await this.ItemOperations(doesNotExistContainer, true);

            // The database exists create a container and validate it's children
            if (!dbNotExist)
            {
                CosmosContainer containerExists = await database.Containers.CreateContainerAsync(
                    id: "NotFoundTest" + Guid.NewGuid().ToString(),
                    partitionKeyPath: "/pk");

                await this.ItemOperations(containerExists, false);
            }
        }

        private async Task ItemOperations(CosmosContainer container, bool containerNotExist)
        {
            if (containerNotExist)
            {
                dynamic randomItem = new { id = "test", pk = "doesnotexist" };
                Stream create = jsonSerializer.ToStream<dynamic>(randomItem);
                this.VerifyNotFoundResponse(await container.CreateItemAsStreamAsync(randomItem.pk, create));

                var queryIterator = container.CreateItemQueryAsStream("select * from t where true", maxConcurrency: 2);
                this.VerifyQueryNotFoundResponse(await queryIterator.FetchNextSetAsync());

                var feedIterator = container.GetItemsStreamIterator();
                this.VerifyNotFoundResponse(await feedIterator.FetchNextSetAsync());

                dynamic randomUpsertItem = new { id = DoesNotExist, pk = DoesNotExist, status = 42 };
                Stream upsert = jsonSerializer.ToStream<dynamic>(randomUpsertItem);
                this.VerifyNotFoundResponse(await container.UpsertItemAsStreamAsync(
                    partitionKey: randomUpsertItem.pk,
                    streamPayload: upsert));
            }

            this.VerifyNotFoundResponse(await container.ReadItemAsStreamAsync(partitionKey: DoesNotExist, id: DoesNotExist));
            this.VerifyNotFoundResponse(await container.DeleteItemAsStreamAsync(partitionKey: DoesNotExist, id: DoesNotExist));

            dynamic randomReplaceItem = new { id = "test", pk = "doesnotexist", status = 42 };
            Stream replace = jsonSerializer.ToStream<dynamic>(randomReplaceItem);
            this.VerifyNotFoundResponse(await container.ReplaceItemAsStreamAsync(
                partitionKey: randomReplaceItem.pk,
                id: randomReplaceItem.id,
                streamPayload: replace));
        }

        private void VerifyQueryNotFoundResponse(CosmosResponseMessage response)
        {
            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        }

        private void VerifyNotFoundResponse(CosmosResponseMessage response)
        {
            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}