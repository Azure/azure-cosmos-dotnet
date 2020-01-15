﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    [TestClass]
    public class CosmosDiagnosticsTests : BaseCosmosClientHelper
    {
        private Container Container = null;
        private ContainerProperties containerSettings = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/status";
            this.containerSettings = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey);
            ContainerResponse response = await this.database.CreateContainerAsync(
                this.containerSettings,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task CustomHandlersDiagnostic()
        {
            TimeSpan delayTime = TimeSpan.FromSeconds(2);
            CosmosClient cosmosClient = TestCommon.CreateCosmosClient(builder =>
                builder.AddCustomHandlers(new RequestHandlerSleepHelper(delayTime)));

            DatabaseResponse databaseResponse = await cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());
            string diagnostics = databaseResponse.Diagnostics.ToString();
            Assert.IsNotNull(diagnostics);
            JObject jObject = JObject.Parse(diagnostics);
            JArray contextList = jObject["Context"].ToObject<JArray>();
            JObject customHandler = GetJObjectInContextList(contextList, typeof(RequestHandlerSleepHelper).FullName);
            Assert.IsNotNull(customHandler);
            TimeSpan elapsedTime = customHandler["ElapsedTime"].ToObject<TimeSpan>();
            Assert.IsTrue(elapsedTime.TotalSeconds > 1);

            customHandler = GetJObjectInContextList(contextList, typeof(RequestHandlerSleepHelper).FullName);
            Assert.IsNotNull(customHandler);
            elapsedTime = customHandler["ElapsedTime"].ToObject<TimeSpan>();
            Assert.IsTrue(elapsedTime > delayTime);

            await databaseResponse.Database.DeleteAsync();
        }

        [TestMethod]
        public async Task PointOperationDiagnostic()
        {
            //Checking point operation diagnostics on typed operations
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> createResponse = await this.Container.CreateItemAsync<ToDoActivity>(item: testItem);
            CosmosDiagnosticsTests.VerifyPointDiagnostics(createResponse.Diagnostics);

            ItemResponse<ToDoActivity> readResponse = await this.Container.ReadItemAsync<ToDoActivity>(id: testItem.id, partitionKey: new PartitionKey(testItem.status));
            Assert.IsNotNull(readResponse.Diagnostics);

            testItem.description = "NewDescription";
            ItemResponse<ToDoActivity> replaceResponse = await this.Container.ReplaceItemAsync<ToDoActivity>(item: testItem, id: testItem.id, partitionKey: new PartitionKey(testItem.status));
            Assert.AreEqual(replaceResponse.Resource.description, "NewDescription");
            CosmosDiagnosticsTests.VerifyPointDiagnostics(replaceResponse.Diagnostics);

            ItemResponse<ToDoActivity> deleteResponse = await this.Container.DeleteItemAsync<ToDoActivity>(partitionKey: new Cosmos.PartitionKey(testItem.status), id: testItem.id);
            Assert.IsNotNull(deleteResponse);
            CosmosDiagnosticsTests.VerifyPointDiagnostics(deleteResponse.Diagnostics);

            //Checking point operation diagnostics on stream operations
            ResponseMessage createStreamResponse = await this.Container.CreateItemStreamAsync(
                partitionKey: new PartitionKey(testItem.status),
                streamPayload: TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem));
            CosmosDiagnosticsTests.VerifyPointDiagnostics(createStreamResponse.Diagnostics);

            ResponseMessage readStreamResponse = await this.Container.ReadItemStreamAsync(
                id: testItem.id,
                partitionKey: new PartitionKey(testItem.status));
            CosmosDiagnosticsTests.VerifyPointDiagnostics(readStreamResponse.Diagnostics);

            ResponseMessage replaceStreamResponse = await this.Container.ReplaceItemStreamAsync(
               streamPayload: TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem),
               id: testItem.id,
               partitionKey: new PartitionKey(testItem.status));
            CosmosDiagnosticsTests.VerifyPointDiagnostics(replaceStreamResponse.Diagnostics);

            ResponseMessage deleteStreamResponse = await this.Container.DeleteItemStreamAsync(
               id: testItem.id,
               partitionKey: new PartitionKey(testItem.status));
            CosmosDiagnosticsTests.VerifyPointDiagnostics(deleteStreamResponse.Diagnostics);

            // Ensure diagnostics are set even on failed operations
            testItem.description = new string('x', Microsoft.Azure.Documents.Constants.MaxResourceSizeInBytes + 1);
            ResponseMessage createTooBigStreamResponse = await this.Container.CreateItemStreamAsync(
                partitionKey: new PartitionKey(testItem.status),
                streamPayload: TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem));
            Assert.IsFalse(createTooBigStreamResponse.IsSuccessStatusCode);
            CosmosDiagnosticsTests.VerifyPointDiagnostics(createTooBigStreamResponse.Diagnostics);
        }

        [TestMethod]
        public async Task QueryOperationDiagnostic()
        {
            int totalItems = 3;
            IList<ToDoActivity> itemList = await ToDoActivity.CreateRandomItems(
                this.Container,
                pkCount: totalItems,
                perPKItemCount: 1,
                randomPartitionKey: true);

            //Checking query metrics on typed query
            long totalOutputDocumentCount = await this.ExecuteQueryAndReturnOutputDocumentCount(
                queryText: "select * from ToDoActivity",
                expectedItemCount: totalItems);

            Assert.AreEqual(totalItems, totalOutputDocumentCount);

            totalOutputDocumentCount = await this.ExecuteQueryAndReturnOutputDocumentCount(
                queryText: "select * from ToDoActivity t ORDER BY t.cost",
                expectedItemCount: totalItems);

            Assert.AreEqual(totalItems, totalOutputDocumentCount);

            totalOutputDocumentCount = await this.ExecuteQueryAndReturnOutputDocumentCount(
                queryText: "select DISTINCT t.cost from ToDoActivity t",
                expectedItemCount: 1);

            Assert.IsTrue(totalOutputDocumentCount >= 1);

            totalOutputDocumentCount = await this.ExecuteQueryAndReturnOutputDocumentCount(
                queryText: "select * from ToDoActivity OFFSET 1 LIMIT 1",
                expectedItemCount: 1);

            Assert.IsTrue(totalOutputDocumentCount >= 1);
        }

        [TestMethod]
        public async Task NonDataPlaneDiagnosticTest()
        {
            DatabaseResponse databaseResponse = await this.cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());
            Assert.IsNotNull(databaseResponse.Diagnostics);
            string diagnostics = databaseResponse.Diagnostics.ToString();
            Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            Assert.IsTrue(diagnostics.Contains("StatusCode"));
            Assert.IsTrue(diagnostics.Contains("SubStatusCode"));
            Assert.IsTrue(diagnostics.Contains("RequestUri"));
        }

        public static void VerifyQueryDiagnostics(CosmosDiagnostics diagnostics, bool isFirstPage)
        {
            string info = diagnostics.ToString();
            Assert.IsNotNull(info);
            JObject jObject = JObject.Parse(info);
            JToken summary = jObject["Summary"];
            Assert.IsNotNull(summary["UserAgent"].ToString());
            Assert.IsNotNull(summary["StartUtc"].ToString());

            JArray contextList = jObject["Context"].ToObject<JArray>();
            Assert.IsTrue(contextList.Count > 0);

            // Find the PointOperationStatistics object
            JObject page = GetJObjectInContextList(
                contextList,
                "0",
                "PKRangeId");

            // First page will have a request
            // Query might use cache pages which don't have the following info. It was returned in the previous call.
            if(isFirstPage || page != null)
            {
                string queryMetrics = page["QueryMetric"].ToString();
                Assert.IsNotNull(queryMetrics);
                Assert.IsNotNull(page["IndexUtilization"].ToString());
                Assert.IsNotNull(page["PKRangeId"].ToString());
                JArray requestDiagnostics = page["Context"].ToObject<JArray>();
                Assert.IsNotNull(requestDiagnostics);
            }
        }

        public static void VerifyPointDiagnostics(CosmosDiagnostics diagnostics)
        {
            string info = diagnostics.ToString();
            Assert.IsNotNull(info);
            JObject jObject = JObject.Parse(info);
            JToken summary = jObject["Summary"];
            Assert.IsNotNull(summary["UserAgent"].ToString());
            Assert.IsNotNull(summary["StartUtc"].ToString());
            Assert.IsNotNull(summary["ElapsedTime"].ToString());

            Assert.IsNotNull(jObject["Context"].ToString());
            JArray contextList = jObject["Context"].ToObject<JArray>();
            Assert.IsTrue(contextList.Count > 3);

            // Find the PointOperationStatistics object
            JObject pointStatistics = GetJObjectInContextList(
                contextList,
                "PointOperationStatistics");

            Assert.IsNotNull(pointStatistics, $"Context list does not contain PointOperationStatistics in {contextList.ToString()}");
            int statusCode = pointStatistics["StatusCode"].ToObject<int>();
            Assert.IsNotNull(pointStatistics["ActivityId"].ToString());
            Assert.IsNotNull(pointStatistics["StatusCode"].ToString());
            Assert.IsNotNull(pointStatistics["RequestCharge"].ToString());
            Assert.IsNotNull(pointStatistics["RequestUri"].ToString());
            Assert.IsNotNull(pointStatistics["ClientRequestStats"].ToString());
            JObject clientJObject = pointStatistics["ClientRequestStats"].ToObject<JObject>();
            Assert.IsNotNull(clientJObject["RequestStartTimeUtc"].ToString()); 
            Assert.IsNotNull(clientJObject["ContactedReplicas"].ToString());
            Assert.IsNotNull(clientJObject["RequestLatency"].ToString());

            // Not all request have these fields. If the field exists then it should not be null
            if (clientJObject["EndpointToAddressResolutionStatistics"] != null)
            {
                Assert.IsNotNull(clientJObject["EndpointToAddressResolutionStatistics"].ToString());
            }

            if (clientJObject["SupplementalResponseStatisticsListLast10"] != null)
            {
                Assert.IsNotNull(clientJObject["SupplementalResponseStatisticsListLast10"].ToString());
            }

            if (clientJObject["FailedReplicas"] != null)
            {
                Assert.IsNotNull(clientJObject["FailedReplicas"].ToString());
            }

            // Session token only expected on success
            if (statusCode >= 200 && statusCode < 300)
            {
                Assert.IsNotNull(clientJObject["ResponseStatisticsList"].ToString());   
                Assert.IsNotNull(clientJObject["RegionsContacted"].ToString());
                Assert.IsNotNull(clientJObject["RequestEndTimeUtc"].ToString());
                Assert.IsNotNull(pointStatistics["ResponseSessionToken"].ToString());
            }
        }

        private static JObject GetJObjectInContextList(JArray contextList, string value, string key = "Id")
        {
            foreach (JObject tempJObject in contextList)
            {
                JToken jsonId = tempJObject[key];
                string name = jsonId?.Value<string>();
                if (string.Equals(value, name))
                {
                    return tempJObject;
                }
            }

            return null;
        }

        private static JObject GetPropertyInContextList(JArray contextList, string id)
        {
            JObject jObject = GetJObjectInContextList(contextList, id);
            if (jObject == null)
            {
                return null;
            }

            return jObject["Value"].ToObject<JObject>();
        }

        private async Task<long> ExecuteQueryAndReturnOutputDocumentCount(string queryText, int expectedItemCount)
        {
            QueryDefinition sql = new QueryDefinition(queryText);

            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxItemCount = 1,
                MaxConcurrency = 1,
            };

            // Verify the typed query iterator
            FeedIterator<ToDoActivity> feedIterator = this.Container.GetItemQueryIterator<ToDoActivity>(
                    sql,
                    requestOptions: requestOptions);

            List<ToDoActivity> results = new List<ToDoActivity>();
            long totalOutDocumentCount = 0;
            bool isFirst = true;
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> response = await feedIterator.ReadNextAsync();
                results.AddRange(response);
                VerifyQueryDiagnostics(response.Diagnostics, isFirst);
                isFirst = false;
            }

            Assert.AreEqual(expectedItemCount, results.Count);

            // Verify the stream query iterator
            FeedIterator streamIterator = this.Container.GetItemQueryStreamIterator(
                   sql,
                   requestOptions: requestOptions);

            List<ToDoActivity> streamResults = new List<ToDoActivity>();
            long streamTotalOutDocumentCount = 0;
            isFirst = true;
            while (streamIterator.HasMoreResults)
            {
                ResponseMessage response = await streamIterator.ReadNextAsync();
                Collection<ToDoActivity> result = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(response.Content).Data;
                streamResults.AddRange(result);
                VerifyQueryDiagnostics(response.Diagnostics, isFirst);
                isFirst = false;
            }

            Assert.AreEqual(expectedItemCount, streamResults.Count);
            Assert.AreEqual(totalOutDocumentCount, streamTotalOutDocumentCount);

            return results.Count;
        }

        private class RequestHandlerSleepHelper : RequestHandler
        {
            TimeSpan timeToSleep;

            public RequestHandlerSleepHelper(TimeSpan timeToSleep)
            {
                this.timeToSleep = timeToSleep;
            }

            public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
            {
                await Task.Delay(this.timeToSleep);
                return await base.SendAsync(request, cancellationToken);
            }
        }
    }
}
