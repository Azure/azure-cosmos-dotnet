﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;

    [TestClass]
    public class CosmosConflictTests
    {
        [TestMethod]
        public async Task ConflictsFeedSetsPartitionKeyRangeIdentity()
        {
            ContainerCore container = CosmosConflictTests.GetMockedContainer((request, cancellationToken) => {
                Assert.IsNotNull(request.ToDocumentServiceRequest().PartitionKeyRangeIdentity);
                return TestHandler.ReturnSuccess();
            });

            IAsyncEnumerable<Response> iterator = container.Conflicts.GetConflictQueryStreamIterator();
            await foreach (Response response in iterator)
            {
            }
        }

        [TestMethod]
        public async Task ReadCurrentGetsCorrectRID()
        {
            const string expectedRID = "something";
            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey("pk");
            // Using "test" as container name because the Mocked DocumentClient has it hardcoded
            Uri expectedRequestUri = new Uri($"dbs/conflictsDb/colls/test/docs/{expectedRID}", UriKind.Relative);
            ContainerCore container = CosmosConflictTests.GetMockedContainer((request, cancellationToken) => {
                Assert.AreEqual(OperationType.Read, request.OperationType);
                Assert.AreEqual(ResourceType.Document, request.ResourceType);
                Assert.AreEqual(expectedRequestUri, request.RequestUri);
                return TestHandler.ReturnSuccess();
            });

            ConflictProperties conflictSettings = new ConflictProperties();
            conflictSettings.SourceResourceId = expectedRID;

            await container.Conflicts.ReadCurrentAsync<JObject>(conflictSettings, partitionKey);
        }

        [TestMethod]
        public void ReadConflictContentDeserializesContent()
        {
            ContainerCore container = CosmosConflictTests.GetMockedContainer((request, cancellationToken) => {
                return TestHandler.ReturnSuccess();
            });

            JObject someJsonObject = new JObject();
            someJsonObject["id"] = Guid.NewGuid().ToString();
            someJsonObject["someInt"] = 2;

            ConflictProperties conflictSettings = new ConflictProperties();
            conflictSettings.Content = someJsonObject.ToString();

            Assert.AreEqual(someJsonObject.ToString(), container.Conflicts.ReadConflictContent<JObject>(conflictSettings).ToString());
        }

        [TestMethod]
        public async Task DeleteSendsCorrectPayload()
        {
            const string expectedId = "something";
            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey("pk");
            Uri expectedRequestUri = new Uri($"/dbs/conflictsDb/colls/conflictsColl/conflicts/{expectedId}", UriKind.Relative);
            ContainerCore container = CosmosConflictTests.GetMockedContainer((request, cancellationToken) => {
                Assert.AreEqual(OperationType.Delete, request.OperationType);
                Assert.AreEqual(ResourceType.Conflict, request.ResourceType);
                Assert.AreEqual(expectedRequestUri, request.RequestUri);
                return TestHandler.ReturnSuccess();
            });

            ConflictProperties conflictSettings = new ConflictProperties();
            conflictSettings.Id = expectedId;

            await container.Conflicts.DeleteAsync(conflictSettings, partitionKey);
        }

        private static ContainerCore GetMockedContainer(Func<RequestMessage,
            CancellationToken, Task<ResponseMessage>> handlerFunc)
        {
            return new ContainerCore(CosmosConflictTests.GetMockedClientContext(handlerFunc), MockCosmosUtil.CreateMockDatabase("conflictsDb").Object, "conflictsColl");
        }

        private static CosmosClientContext GetMockedClientContext(
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> handlerFunc)
        {
            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            Mock<PartitionRoutingHelper> partitionRoutingHelperMock = MockCosmosUtil.GetPartitionRoutingHelperMock("0");
            PartitionKeyRangeHandler partitionKeyRangeHandler = new PartitionKeyRangeHandler(new CosmosClientDriverContext(client), partitionRoutingHelperMock.Object);

            TestHandler testHandler = new TestHandler(handlerFunc);
            partitionKeyRangeHandler.InnerHandler = testHandler;

            RequestHandler handler = client.RequestHandler.InnerHandler;
            while (handler != null)
            {
                if (handler.InnerHandler is RouterHandler)
                {
                    handler.InnerHandler = new RouterHandler(partitionKeyRangeHandler, testHandler);
                    break;
                }

                handler = handler.InnerHandler;
            }

            CosmosResponseFactory responseFactory = new CosmosResponseFactory(MockCosmosUtil.Serializer, MockCosmosUtil.Serializer);

            return new ClientContextCore(
                client: client,
                clientOptions: new CosmosClientOptions(),
                userJsonSerializer: MockCosmosUtil.Serializer,
                defaultJsonSerializer: MockCosmosUtil.Serializer,
                sqlQuerySpecSerializer: MockCosmosUtil.Serializer,
                cosmosResponseFactory: responseFactory,
                requestHandler: client.RequestHandler,
                documentClient: new MockDocumentClient());
        }
    }
}
