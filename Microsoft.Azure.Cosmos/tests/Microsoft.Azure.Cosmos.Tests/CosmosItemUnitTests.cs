﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosItemUnitTests
    {
        [TestMethod]
        public async Task TestItemPartitionKeyTypes()
        {
            dynamic item = new
            {
                id = Guid.NewGuid().ToString(),
                pk = "FF627B77-568E-4541-A47E-041EAC10E46F"
            };
            await VerifyItemOperations(item.pk, "[\"FF627B77-568E-4541-A47E-041EAC10E46F\"]", item);

            item = new
            {
                id = Guid.NewGuid().ToString(),
                pk = 4567
            };
            await VerifyItemOperations(item.pk, "[4567.0]", item);

            item = new
            {
                id = Guid.NewGuid().ToString(),
                pk = 4567.1234
            };
            await VerifyItemOperations(item.pk, "[4567.1234]", item);

            item = new
            {
                id = Guid.NewGuid().ToString(),
                pk = true
            };
            await VerifyItemOperations(item.pk, "[true]", item);
        }

        [TestMethod]
        public async Task TestNullItemPartitionKeyFlag()
        {
            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString()
            };

            await VerifyItemOperations(Undefined.Value, "[{}]", testItem);
        }

        [TestMethod]
        public async Task TestNullItemPartitionKeyIsBlocked()
        {
            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString()
            };

            await VerifyItemNullExceptions(testItem, null);

            ItemRequestOptions requestOptions = new ItemRequestOptions();
            await VerifyItemNullExceptions(testItem, requestOptions);
        }

        private async Task VerifyItemNullExceptions(
            dynamic testItem,
            ItemRequestOptions requestOptions = null)
        {
            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                Assert.Fail("Null partition key should be blocked without the correct request option");
                return null;
            });

            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(
                (cosmosClientBuilder) => cosmosClientBuilder.AddCustomHandlers(testHandler));

            CosmosContainer container = client.Databases["testdb"]
                                        .Containers["testcontainer"];

            await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
            {
                await container.CreateItemAsync<dynamic>(
                    partitionKey: null,
                    item: testItem,
                    requestOptions: requestOptions);
            }, "CreateItemAsync should throw ArgumentNullException without the correct request option set.");

            await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
            {
                await container.ReadItemAsync<dynamic>(
                    partitionKey: null,
                    id: testItem.id,
                    requestOptions: requestOptions);
            }, "ReadItemAsync should throw ArgumentNullException without the correct request option set.");

            await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
            {
                await container.UpsertItemAsync<dynamic>(
                    partitionKey: null,
                    item: testItem,
                    requestOptions: requestOptions);
            }, "UpsertItemAsync should throw ArgumentNullException without the correct request option set.");

            await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
            {
                await container.ReplaceItemAsync<dynamic>(
                    partitionKey: null,
                    id: testItem.id,
                    item: testItem,
                    requestOptions: requestOptions);
            }, "ReplaceItemAsync should throw ArgumentNullException without the correct request option set.");

            await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
            {
                await container.DeleteItemAsync<dynamic>(
                    partitionKey: null,
                    id: testItem.id,
                    requestOptions: requestOptions);
            }, "DeleteItemAsync should throw ArgumentNullException without the correct request option set.");

            CosmosJsonSerializerCore jsonSerializer = new CosmosJsonSerializerCore();
            using (Stream itemStream = jsonSerializer.ToStream<dynamic>(testItem))
            {
                await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
                {
                    await container.CreateItemAsStreamAsync(
                        partitionKey: null,
                        streamPayload: itemStream,
                        requestOptions: requestOptions);
                }, "CreateItemAsync should throw ArgumentNullException without the correct request option set.");

                await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
                {
                    await container.ReadItemAsStreamAsync(
                        partitionKey: null,
                        id: testItem.id,
                        requestOptions: requestOptions);
                }, "ReadItemAsync should throw ArgumentNullException without the correct request option set.");

                await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
                {
                    await container.UpsertItemAsStreamAsync(
                        partitionKey: null,
                        streamPayload: itemStream,
                        requestOptions: requestOptions);
                }, "UpsertItemAsync should throw ArgumentNullException without the correct request option set.");

                await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
                {
                    await container.ReplaceItemAsStreamAsync(
                        partitionKey: null,
                        id: testItem.id,
                        streamPayload: itemStream,
                        requestOptions: requestOptions);
                }, "ReplaceItemAsync should throw ArgumentNullException without the correct request option set.");

                await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
                {
                    await container.DeleteItemAsStreamAsync(
                        partitionKey: null,
                        id: testItem.id,
                        requestOptions: requestOptions);
                }, "DeleteItemAsync should throw ArgumentNullException without the correct request option set.");
            }
        }

        private async Task VerifyItemOperations(
            object partitionKey,
            string partitionKeySerialized,
            dynamic testItem,
            ItemRequestOptions requestOptions = null)
        {
            CosmosResponseMessage response = null;
            HttpStatusCode httpStatusCode = HttpStatusCode.OK;
            int testHandlerHitCount = 0;
            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                Assert.IsTrue(request.RequestUri.OriginalString.StartsWith(@"/dbs/testdb/colls/testcontainer"));
                Assert.AreEqual(requestOptions, request.RequestOptions);
                Assert.AreEqual(ResourceType.Document, request.ResourceType);
                Assert.IsNotNull(request.Headers.PartitionKey);
                Assert.AreEqual(partitionKeySerialized, request.Headers.PartitionKey);
                testHandlerHitCount++;
                response = new CosmosResponseMessage(httpStatusCode, request, errorMessage: null);
                response.Content = request.Content;
                return Task.FromResult(response);
            });

            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient(
                (builder) => builder.AddCustomHandlers(testHandler));

            CosmosContainer container = client.Databases["testdb"]
                                        .Containers["testcontainer"];

            ItemResponse<dynamic> itemResponse = await container.CreateItemAsync<dynamic>(
                partitionKey: partitionKey,
                item: testItem,
                requestOptions: requestOptions);
            Assert.IsNotNull(itemResponse);
            Assert.AreEqual(httpStatusCode, itemResponse.StatusCode);

            itemResponse = await container.ReadItemAsync<dynamic>(
                partitionKey: partitionKey,
                id: testItem.id,
                requestOptions: requestOptions);
            Assert.IsNotNull(itemResponse);
            Assert.AreEqual(httpStatusCode, itemResponse.StatusCode);

            itemResponse = await container.UpsertItemAsync<dynamic>(
                partitionKey: partitionKey,
                item: testItem,
                requestOptions: requestOptions);
            Assert.IsNotNull(itemResponse);
            Assert.AreEqual(httpStatusCode, itemResponse.StatusCode);

            itemResponse = await container.ReplaceItemAsync<dynamic>(
                partitionKey: partitionKey,
                id: testItem.id,
                item: testItem,
                requestOptions: requestOptions);
            Assert.IsNotNull(itemResponse);
            Assert.AreEqual(httpStatusCode, itemResponse.StatusCode);

            itemResponse = await container.DeleteItemAsync<dynamic>(
                partitionKey: partitionKey,
                id: testItem.id,
                requestOptions: requestOptions);
            Assert.IsNotNull(itemResponse);
            Assert.AreEqual(httpStatusCode, itemResponse.StatusCode);

            Assert.AreEqual(5, testHandlerHitCount, "An operation did not make it to the handler");

            CosmosJsonSerializerCore jsonSerializer = new CosmosJsonSerializerCore();
            using (Stream itemStream = jsonSerializer.ToStream<dynamic>(testItem))
            {
                using (CosmosResponseMessage streamResponse = await container.CreateItemAsStreamAsync(
                    partitionKey: partitionKey,
                    streamPayload: itemStream))
                {
                    Assert.IsNotNull(streamResponse);
                    Assert.AreEqual(httpStatusCode, streamResponse.StatusCode);
                }
            }

            using (Stream itemStream = jsonSerializer.ToStream<dynamic>(testItem))
            {
                using (CosmosResponseMessage streamResponse = await container.ReadItemAsStreamAsync(
                    partitionKey: partitionKey,
                    id: testItem.id,
                    requestOptions: requestOptions))
                {
                    Assert.IsNotNull(streamResponse);
                    Assert.AreEqual(httpStatusCode, streamResponse.StatusCode);
                }
            }

            using (Stream itemStream = jsonSerializer.ToStream<dynamic>(testItem))
            {
                using (CosmosResponseMessage streamResponse = await container.UpsertItemAsStreamAsync(
                    partitionKey: partitionKey,
                    streamPayload: itemStream,
                    requestOptions: requestOptions))
                {
                    Assert.IsNotNull(streamResponse);
                    Assert.AreEqual(httpStatusCode, streamResponse.StatusCode);
                }
            }

            using (Stream itemStream = jsonSerializer.ToStream<dynamic>(testItem))
            {
                using (CosmosResponseMessage streamResponse = await container.ReplaceItemAsStreamAsync(
                    partitionKey: partitionKey,
                    id: testItem.id,
                    streamPayload: itemStream,
                    requestOptions: requestOptions))
                {
                    Assert.IsNotNull(streamResponse);
                    Assert.AreEqual(httpStatusCode, streamResponse.StatusCode);
                }
            }

            using (Stream itemStream = jsonSerializer.ToStream<dynamic>(testItem))
            {
                using (CosmosResponseMessage streamResponse = await container.DeleteItemAsStreamAsync(
                    partitionKey: partitionKey,
                    id: testItem.id,
                    requestOptions: requestOptions))
                {
                    Assert.IsNotNull(streamResponse);
                    Assert.AreEqual(httpStatusCode, streamResponse.StatusCode);
                }
            }

            Assert.AreEqual(10, testHandlerHitCount, "A stream operation did not make it to the handler");
        }
    }
}
