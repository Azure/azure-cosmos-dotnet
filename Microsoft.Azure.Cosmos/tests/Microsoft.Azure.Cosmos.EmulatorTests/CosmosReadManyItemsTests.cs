﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosReadManyItemsTests : BaseCosmosClientHelper
    {
        private Container Container = null;
        private ContainerProperties containerSettings = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/pk";
            this.containerSettings = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey);
            ContainerResponse response = await this.database.CreateContainerAsync(
                this.containerSettings,
                throughput: 20000,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = response;

            // Create items with different pk values
            for (int i = 0; i < 500; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                item.pk = "pk" + i.ToString();
                item.id = i.ToString();
                ItemResponse<ToDoActivity> itemResponse = await this.Container.CreateItemAsync(item);
                Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);
            }
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task ReadManyTypedTest()
        {
            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
            for (int i=0; i<10; i++)
            {
                itemList.Add((i.ToString(), new PartitionKey("pk" + i.ToString())));
            }

            FeedResponse<ToDoActivity> feedResponse= await this.Container.ReadManyItemsAsync<ToDoActivity>(itemList);
            Assert.IsNotNull(feedResponse);
            Assert.AreEqual(feedResponse.Count, 10);
            Assert.IsTrue(feedResponse.Headers.RequestCharge > 0);
            Assert.IsNotNull(feedResponse.Diagnostics);
        }

        [TestMethod]
        public async Task ReadManyStreamTest()
        {
            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
            for (int i = 0; i < 5; i++)
            {
                itemList.Add((i.ToString(), new PartitionKey("pk" + i.ToString())));
            }

            using (ResponseMessage responseMessage = await this.Container.ReadManyItemsStreamAsync(itemList))
            {
                Assert.IsNotNull(responseMessage);
                Assert.IsTrue(responseMessage.Headers.RequestCharge > 0);
                Assert.IsNotNull(responseMessage.Diagnostics);

                ToDoActivity[] items = this.cosmosClient.ClientContext.SerializerCore.FromFeedStream<ToDoActivity>(
                                        CosmosFeedResponseSerializer.GetStreamWithoutServiceEnvelope(responseMessage.Content));
                Assert.AreEqual(items.Length, 5);
            }
        }

        [TestMethod]
        public async Task ReadManyDoesNotFetchQueryPlan()
        {
            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
            for (int i = 0; i < 5; i++)
            {
                itemList.Add((i.ToString(), new PartitionKey("pk" + i.ToString())));
            }

            using (ResponseMessage responseMessage = await this.Container.ReadManyItemsStreamAsync(itemList))
            {
                Assert.IsNotNull(responseMessage);
                Assert.IsTrue(responseMessage.Headers.RequestCharge > 0);
                Assert.IsNotNull(responseMessage.Diagnostics);
                Assert.IsFalse(responseMessage.Diagnostics.ToString().Contains("Gateway QueryPlan"));
            }
        }

        [TestMethod]
        public async Task ReadManyWithIdasPk()
        {
            string PartitionKey = "/id";
            ContainerProperties containerSettings = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey);
            Container container = await this.database.CreateContainerAsync(containerSettings);

            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
            for (int i = 0; i < 5; i++)
            {
                itemList.Add((i.ToString(), new PartitionKey(i.ToString())));
            }

            // Create items with different pk values
            for (int i = 0; i < 5; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                item.id = i.ToString();
                ItemResponse<ToDoActivity> itemResponse = await container.CreateItemAsync(item);
                Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);
            }

            using (ResponseMessage responseMessage = await container.ReadManyItemsStreamAsync(itemList))
            {
                Assert.IsNotNull(responseMessage);
                Assert.IsTrue(responseMessage.Headers.RequestCharge > 0);
                Assert.IsNotNull(responseMessage.Diagnostics);

                ToDoActivity[] items = this.cosmosClient.ClientContext.SerializerCore.FromFeedStream<ToDoActivity>(
                                        CosmosFeedResponseSerializer.GetStreamWithoutServiceEnvelope(responseMessage.Content));
                Assert.AreEqual(items.Length, 5);
            }

            FeedResponse<ToDoActivity> feedResponse = await container.ReadManyItemsAsync<ToDoActivity>(itemList);
            Assert.IsNotNull(feedResponse);
            Assert.AreEqual(feedResponse.Count, 5);
            Assert.IsTrue(feedResponse.Headers.RequestCharge > 0);
            Assert.IsNotNull(feedResponse.Diagnostics);
        }

        [TestMethod]
        public async Task ReadManyWithNestedPk()
        {
            string PartitionKey = "/NestedObject/pk";
            ContainerProperties containerSettings = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey);
            Container container = await this.database.CreateContainerAsync(containerSettings);

            // Create items with different pk values
            for (int i = 0; i < 5; i++)
            {
                NestedToDoActivity item = NestedToDoActivity.CreateRandomNestedToDoActivity("pk" + i.ToString(), i.ToString());
                await container.CreateItemAsync(item);
            }

            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
            for (int i = 0; i < 5; i++)
            {
                itemList.Add((i.ToString(), new PartitionKey("pk" + i.ToString())));
            }

            FeedResponse<ToDoActivity> feedResponse = await container.ReadManyItemsAsync<ToDoActivity>(itemList);
            Assert.IsNotNull(feedResponse);
            Assert.AreEqual(feedResponse.Count, 5);
            Assert.IsTrue(feedResponse.Headers.RequestCharge > 0);
            Assert.IsNotNull(feedResponse.Diagnostics);
        }

        [TestMethod]
        public async Task ValidateContainerRecreateScenario()
        {
            CosmosClient cc1 = TestCommon.CreateCosmosClient();
            CosmosClient cc2 = TestCommon.CreateCosmosClient();

            Database database = null;
            try
            {
                database = await cc1.CreateDatabaseAsync("ContainerRecreateScenarioDb");
                Container containerCC1 = await database.CreateContainerAsync("ContainerRecreateContainer", "/pk");

                // Create items with different pk values
                for (int i = 0; i < 5; i++)
                {
                    ItemResponse<ToDoActivity> itemResponse = await containerCC1.CreateItemAsync(
                        ToDoActivity.CreateRandomToDoActivity("pk" + i, i.ToString()));
                }

                List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
                for (int i = 0; i < 5; i++)
                {
                    itemList.Add((i.ToString(), new PartitionKey("pk" + i)));
                }

                FeedResponse<ToDoActivity> feedResponse = await containerCC1.ReadManyItemsAsync<ToDoActivity>(itemList);
                Assert.AreEqual(feedResponse.Count, 5);

                Database databaseCC2 = cc2.GetDatabase("ContainerRecreateScenarioDb");
                Container containerCC2 = cc2.GetContainer("ContainerRecreateScenarioDb", "ContainerRecreateContainer");
                await containerCC2.DeleteContainerAsync();

                // Recreate container 
                containerCC2 = await databaseCC2.CreateContainerAsync("ContainerRecreateContainer", "/pk");

                // Check if recreate scenario works
                feedResponse = await containerCC1.ReadManyItemsAsync<ToDoActivity>(itemList);
                Assert.AreEqual(feedResponse.Count, 0);
                Assert.IsTrue(feedResponse.StatusCode == HttpStatusCode.OK);
            }
            finally
            {
                await database.DeleteAsync();
                cc1.Dispose();
                cc2.Dispose();
            }
        }

        [TestMethod]
        public async Task MultipleQueriesToSamePartitionTest()
        {
            for (int i = 0; i < 2500; i++)
            {
                await this.Container.CreateItemAsync<ToDoActivity>(ToDoActivity.CreateRandomToDoActivity("pk", i.ToString()));
            }

            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
            for (int i = 0; i < 2500; i++)
            {
                itemList.Add((i.ToString(), new PartitionKey("pk")));
            }

            FeedResponse<ToDoActivity> feedResponse = await this.Container.ReadManyItemsAsync<ToDoActivity>(itemList);
            Assert.AreEqual(feedResponse.Count, 2500);
        }

#if PREVIEW
        [TestMethod]
        public async Task ReadManyMultiplePK()
        {
            IReadOnlyList<string> pkPaths = new List<string> { "/pk", "/description" };
            ContainerProperties containerSettings = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPaths: pkPaths);
            Container container = await this.database.CreateContainerAsync(this.containerSettings);

            for (int i = 0; i < 5; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                item.pk = "pk" + i.ToString();
                item.id = i.ToString();
                item.description = "description" + i;
                ItemResponse<ToDoActivity> itemResponse = await container.CreateItemAsync(item);
                Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);
            }

            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
            for (int i = 0; i < 5; i++)
            {
                PartitionKey partitionKey = new PartitionKeyBuilder()
                                                        .Add("pk" + i)
                                                        .Add("description" + i)
                                                        .Build();

                itemList.Add((i.ToString(), partitionKey));
            }

            FeedResponse<ToDoActivity> feedResponse = await container.ReadManyItemsAsync<ToDoActivity>(itemList);
            Assert.IsNotNull(feedResponse);
            Assert.AreEqual(feedResponse.Count, 5);
            Assert.IsTrue(feedResponse.Headers.RequestCharge > 0);
            Assert.IsNotNull(feedResponse.Diagnostics);
        }
#endif

        private class NestedToDoActivity
        {
            public ToDoActivity NestedObject { get; set; }
#pragma warning disable IDE1006 // Naming Styles
            public string id { get; set; }
#pragma warning restore IDE1006 // Naming Styles

            public static NestedToDoActivity CreateRandomNestedToDoActivity(string pk, string id)
            {
                return new NestedToDoActivity()
                {
                    id = id,
                    NestedObject = ToDoActivity.CreateRandomToDoActivity(pk: pk)
                };
            }
        }
    }
}
