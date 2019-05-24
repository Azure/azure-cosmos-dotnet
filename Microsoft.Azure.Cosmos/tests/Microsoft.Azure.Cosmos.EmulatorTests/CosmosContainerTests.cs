﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class CosmosContainerTests
    {
        private CosmosClient cosmosClient = null;
        private CosmosDatabase cosmosDatabase = null;
        private static long ToEpoch(DateTime dateTime) => (long)(dateTime - (new DateTime(1970, 1, 1))).TotalSeconds;

        [TestInitialize]
        public async Task TestInit()
        {
            this.cosmosClient = TestCommon.CreateCosmosClient();

            string databaseName = Guid.NewGuid().ToString();
            DatabaseResponse cosmosDatabaseResponse = await this.cosmosClient.Databases.CreateDatabaseIfNotExistsAsync(databaseName);
            this.cosmosDatabase = cosmosDatabaseResponse;
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (this.cosmosClient == null)
            {
                return;
            }

            if (this.cosmosDatabase != null)
            {
                await this.cosmosDatabase.DeleteAsync();
            }
            this.cosmosClient.Dispose();
        }

        [TestMethod]
        public async Task ContainerContractTest()
        {
            ContainerResponse response = await this.cosmosDatabase.Containers.CreateContainerAsync(new Guid().ToString(), "/id");
            Assert.IsNotNull(response);
            Assert.IsTrue(response.RequestCharge > 0);
            Assert.IsNotNull(response.Headers);
            Assert.IsNotNull(response.Headers.ActivityId);

            CosmosContainerSettings containerSettings = response.Resource;
            Assert.IsNotNull(containerSettings.Id);
            Assert.IsNotNull(containerSettings.ResourceId);
            Assert.IsNotNull(containerSettings.ETag);
            Assert.IsTrue(containerSettings.LastModified.HasValue);

            Assert.IsTrue(containerSettings.LastModified.Value > new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), containerSettings.LastModified.Value.ToString());
        }

        [Ignore]
        [TestMethod]
        public async Task PartitionedCRUDTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerAsync(containerName, partitionKeyPath);

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            CosmosContainerSettings settings = new CosmosContainerSettings(containerName, partitionKeyPath)
            {
                IndexingPolicy = new Cosmos.IndexingPolicy()
                {
                    IndexingMode = Cosmos.IndexingMode.None,
                    Automatic = false
                }
            };

            CosmosContainer cosmosContainer = containerResponse;
            containerResponse = await cosmosContainer.ReplaceAsync(settings);
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
            Assert.AreEqual(Cosmos.IndexingMode.None, containerResponse.Resource.IndexingPolicy.IndexingMode);
            Assert.IsFalse(containerResponse.Resource.IndexingPolicy.Automatic);

            containerResponse = await cosmosContainer.ReadAsync();
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(Cosmos.PartitionKeyDefinitionVersion.V2, containerResponse.Resource.PartitionKeyDefinitionVersion);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());
            Assert.AreEqual(Cosmos.IndexingMode.None, containerResponse.Resource.IndexingPolicy.IndexingMode);
            Assert.IsFalse(containerResponse.Resource.IndexingPolicy.Automatic);

            containerResponse = await containerResponse.Container.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task CreateHashV1Container()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            CosmosContainerSettings settings = new CosmosContainerSettings(containerName, partitionKeyPath);
            settings.PartitionKeyDefinitionVersion = Cosmos.PartitionKeyDefinitionVersion.V1;

            ContainerResponse cosmosContainerResponse = await this.cosmosDatabase.Containers.CreateContainerAsync(settings);

            Assert.AreEqual(HttpStatusCode.Created, cosmosContainerResponse.StatusCode);

            Assert.AreEqual(Cosmos.PartitionKeyDefinitionVersion.V1, cosmosContainerResponse.Resource.PartitionKeyDefinitionVersion);
        }

        [TestMethod]
        public async Task PartitionedCreateWithPathDelete()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add(partitionKeyPath);

            CosmosContainerSettings settings = new CosmosContainerSettings(containerName, partitionKeyDefinition);
            ContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerAsync(settings);

            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            containerResponse = await containerResponse.Container.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task StreamPartitionedCreateWithPathDelete()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add(partitionKeyPath);

            CosmosContainerSettings settings = new CosmosContainerSettings(containerName, partitionKeyDefinition);
            using (CosmosResponseMessage containerResponse = await this.cosmosDatabase.Containers.CreateContainerAsStreamAsync(settings))
            {
                Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            }

            using (CosmosResponseMessage containerResponse = await this.cosmosDatabase.Containers[containerName].DeleteAsStreamAsync())
            {
                Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(CosmosException))]
        public async Task NegativePartitionedCreateDelete()
        {
            string containerName = Guid.NewGuid().ToString();

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add("/users");
            partitionKeyDefinition.Paths.Add("/test");

            CosmosContainerSettings settings = new CosmosContainerSettings(containerName, partitionKeyDefinition);
            ContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerAsync(settings);

            Assert.Fail("Multiple partition keys should have caused an exception.");
        }

        [TestMethod]
        public async Task NoPartitionedCreateFail()
        {
            string containerName = Guid.NewGuid().ToString();
            try
            {
                new CosmosContainerSettings(id: containerName, partitionKeyPath: null);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }

            try
            {
                new CosmosContainerSettings(id: containerName, partitionKeyDefinition: null);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }

            CosmosContainerSettings settings = new CosmosContainerSettings() { Id = containerName };
            try
            {
                ContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerAsync(settings);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }

            try
            {
                ContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerIfNotExistsAsync(settings);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }

            try
            {
                ContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerAsync(id: containerName, partitionKeyPath: null);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }

            try
            {
                ContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerIfNotExistsAsync(id: containerName, partitionKeyPath: null);
                Assert.Fail("Create should throw null ref exception");
            }
            catch (ArgumentNullException ae)
            {
                Assert.IsNotNull(ae);
            }
        }

        [TestMethod]
        public async Task PartitionedCreateDeleteIfNotExists()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            containerResponse = await this.cosmosDatabase.Containers.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            containerResponse = await containerResponse.Container.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task IteratorTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            HashSet<string> containerIds = new HashSet<string>();
            FeedIterator<CosmosContainerSettings> resultSet = this.cosmosDatabase.Containers.GetContainersIterator();
            while (resultSet.HasMoreResults)
            {
                foreach (CosmosContainerSettings setting in await resultSet.FetchNextSetAsync())
                {
                    if (!containerIds.Contains(setting.Id))
                    {
                        containerIds.Add(setting.Id);
                    }
                }
            }

            Assert.IsTrue(containerIds.Count > 0, "The iterator did not find any containers.");
            Assert.IsTrue(containerIds.Contains(containerName), "The iterator did not find the created container");

            containerResponse = await containerResponse.Container.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task StreamIteratorTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            containerName = Guid.NewGuid().ToString();
            containerResponse = await this.cosmosDatabase.Containers.CreateContainerAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(containerName, containerResponse.Resource.Id);
            Assert.AreEqual(partitionKeyPath, containerResponse.Resource.PartitionKey.Paths.First());

            HashSet<string> containerIds = new HashSet<string>();
            FeedIterator resultSet = this.cosmosDatabase.Containers.GetContainersStreamIterator(
                    maxItemCount:1,
                    requestOptions: new QueryRequestOptions());
            while (resultSet.HasMoreResults)
            {
                using (CosmosResponseMessage message = await resultSet.FetchNextSetAsync())
                {
                    Assert.AreEqual(HttpStatusCode.OK, message.StatusCode);
                    CosmosJsonSerializerCore defaultJsonSerializer = new CosmosJsonSerializerCore();
                    dynamic containers = defaultJsonSerializer.FromStream<dynamic>(message.Content).DocumentCollections;
                    foreach (dynamic container in containers)
                    {
                        string id = container.id.ToString();
                        containerIds.Add(id);
                    }
                }
            }

            Assert.IsTrue(containerIds.Count > 0, "The iterator did not find any containers.");
            Assert.IsTrue(containerIds.Contains(containerName), "The iterator did not find the created container");

            containerResponse = await containerResponse.Container.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task DeleteNonExistingContainer()
        {
            string containerName = Guid.NewGuid().ToString();
            CosmosContainer cosmosContainer = this.cosmosDatabase.Containers[containerName];

            ContainerResponse containerResponse = await cosmosContainer.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NotFound, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task DefaultThroughputTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            CosmosContainer cosmosContainer = this.cosmosDatabase.Containers[containerName];

            int? readThroughput = await cosmosContainer.ReadProvisionedThroughputAsync();
            Assert.IsNotNull(readThroughput);

            containerResponse = await cosmosContainer.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task TimeToLiveTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";
            int timeToLiveInSeconds = 10;
            CosmosContainerSettings setting = new CosmosContainerSettings()
            {
                Id = containerName,
                PartitionKey = new PartitionKeyDefinition() { Paths = new Collection<string> { partitionKeyPath }, Kind = PartitionKind.Hash },
                DefaultTimeToLive = timeToLiveInSeconds,
            };

            ContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerIfNotExistsAsync(setting);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            CosmosContainer cosmosContainer = containerResponse;
            CosmosContainerSettings responseSettings = containerResponse;

            Assert.AreEqual(timeToLiveInSeconds, responseSettings.DefaultTimeToLive);

            ContainerResponse readResponse = await cosmosContainer.ReadAsync();
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            Assert.AreEqual(timeToLiveInSeconds, readResponse.Resource.DefaultTimeToLive);

            JObject itemTest = JObject.FromObject(new { id = Guid.NewGuid().ToString(), users = "testUser42" });
            ItemResponse<JObject> createResponse = await cosmosContainer.CreateItemAsync<JObject>(partitionKey: itemTest["users"].ToString(), item: itemTest);
            JObject responseItem = createResponse;
            Assert.IsNull(responseItem["ttl"]);

            containerResponse = await cosmosContainer.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task ReplaceThroughputTest()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.Containers.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            CosmosContainer cosmosContainer = this.cosmosDatabase.Containers[containerName];

            int? readThroughput = await cosmosContainer.ReadProvisionedThroughputAsync();
            Assert.IsNotNull(readThroughput);

            await cosmosContainer.ReplaceProvisionedThroughputAsync(readThroughput.Value + 1000);
            int? replaceThroughput = await cosmosContainer.ReadProvisionedThroughputAsync();
            Assert.IsNotNull(replaceThroughput);
            Assert.AreEqual(readThroughput.Value + 1000, replaceThroughput);

            containerResponse = await cosmosContainer.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }

        [TestMethod]
        [ExpectedException(typeof(AggregateException))]
        public async Task ThroughputNonExistingTest()
        {
            string containerName = Guid.NewGuid().ToString();
            CosmosContainer cosmosContainer = this.cosmosDatabase.Containers[containerName];

            await cosmosContainer.ReadProvisionedThroughputAsync();

            ContainerResponse containerResponse = await cosmosContainer.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NotFound, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task ImplicitConversion()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/users";

            ContainerResponse containerResponse = await this.cosmosDatabase.Containers[containerName].ReadAsync();
            CosmosContainer cosmosContainer = containerResponse;
            CosmosContainerSettings cosmosContainerSettings = containerResponse;

            Assert.AreEqual(HttpStatusCode.NotFound, containerResponse.StatusCode);
            Assert.IsNotNull(cosmosContainer);
            Assert.IsNull(cosmosContainerSettings);

            containerResponse = await this.cosmosDatabase.Containers.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath);
            cosmosContainer = containerResponse;
            cosmosContainerSettings = containerResponse;
            Assert.IsNotNull(cosmosContainer);
            Assert.IsNotNull(cosmosContainerSettings);

            containerResponse = await cosmosContainer.DeleteAsync();
            cosmosContainer = containerResponse;
            cosmosContainerSettings = containerResponse;
            Assert.IsNotNull(cosmosContainer);
            Assert.IsNull(cosmosContainerSettings);
        }

        /// <summary>
        /// This test verifies that we are able to set the ttl property path correctly using SDK.
        /// Also this test will successfully read active item based on its TimeToLivePropertyPath value.
        /// </summary>
        [TestMethod]
        public async Task TimeToLivePropertyPath()
        {
            string containerName = Guid.NewGuid().ToString();
            string partitionKeyPath = "/user";
            int timeToLivetimeToLiveInSeconds = 10;
            CosmosContainerSettings setting = new CosmosContainerSettings()
            {
                Id = containerName,
                PartitionKey = new PartitionKeyDefinition() { Paths = new Collection<string> { partitionKeyPath }, Kind = PartitionKind.Hash },
                TimeToLivePropertyPath = "/creationDate",
            };

            ContainerResponse containerResponse = null;
            try
            {
                containerResponse = await this.cosmosDatabase.Containers.CreateContainerIfNotExistsAsync(setting);
                Assert.Fail("CreateColleciton with TtlPropertyPath and with no DefaultTimeToLive should have failed.");
            }
            catch (CosmosException exeption)
            {
                // expected because DefaultTimeToLive was not specified
                Assert.AreEqual(HttpStatusCode.BadRequest, exeption.StatusCode);
            }

            // Verify the container content.
            setting.DefaultTimeToLive = timeToLivetimeToLiveInSeconds;
            containerResponse = await this.cosmosDatabase.Containers.CreateContainerIfNotExistsAsync(setting);
            CosmosContainer cosmosContainer = containerResponse;
            Assert.AreEqual(timeToLivetimeToLiveInSeconds, containerResponse.Resource.DefaultTimeToLive);
            Assert.AreEqual("/creationDate", containerResponse.Resource.TimeToLivePropertyPath);

            //verify removing the ttl property path
            setting.TimeToLivePropertyPath = null;
            containerResponse = await cosmosContainer.ReplaceAsync(setting);
            cosmosContainer = containerResponse;
            Assert.AreEqual(timeToLivetimeToLiveInSeconds, containerResponse.Resource.DefaultTimeToLive);
            Assert.IsNull(containerResponse.Resource.TimeToLivePropertyPath);

            //adding back the ttl property path
            setting.TimeToLivePropertyPath = "/creationDate";
            containerResponse = await cosmosContainer.ReplaceAsync(setting);
            cosmosContainer = containerResponse;
            Assert.AreEqual(containerResponse.Resource.TimeToLivePropertyPath, "/creationDate");

            //Creating an item and reading before expiration
            var payload = new { id = "testId", user = "testUser", creationDate = ToEpoch(DateTime.UtcNow) };
            ItemResponse<dynamic> createItemResponse = await cosmosContainer.CreateItemAsync<dynamic>(payload.user, payload);
            Assert.IsNotNull(createItemResponse.Resource);
            Assert.AreEqual(createItemResponse.StatusCode, HttpStatusCode.Created);
            ItemResponse<dynamic> readItemResponse = await cosmosContainer.ReadItemAsync<dynamic>(payload.user, payload.id);
            Assert.IsNotNull(readItemResponse.Resource);
            Assert.AreEqual(readItemResponse.StatusCode, HttpStatusCode.OK);

            containerResponse = await cosmosContainer.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, containerResponse.StatusCode);
        }
    }
}
