﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CosmosClientResourceUnitTests
    {
        [TestMethod]
        public void ValidateUriGenerationForResources()
        {
            string databaseId = "db1234";
            string crId = "cr42";
            string spId = "sp9001";
            string trId = "tr9002";
            string udfId = "udf9003";

            CosmosClientContext context = new CosmosClientContextCore(
                client: null,
                clientConfiguration: null,
                cosmosJsonSerializer: null,
                cosmosResponseFactory: null,
                requestHandler: null,
                documentClient: null,
                documentQueryClient: new Mock<IDocumentQueryClient>().Object);

            CosmosDatabaseCore db = new CosmosDatabaseCore(context, databaseId);
            Assert.AreEqual(db.LinkUri.OriginalString, "/dbs/" + databaseId);

            CosmosContainerCore container = new CosmosContainerCore(context, db, crId);
            Assert.AreEqual(container.LinkUri.OriginalString, "/dbs/" + databaseId + "/colls/" + crId);

            CosmosStoredProcedureCore sp = new CosmosStoredProcedureCore(context, container, spId);
            Assert.AreEqual(sp.LinkUri.OriginalString, "/dbs/" + databaseId + "/colls/" + crId + "/sprocs/" + spId);

            CosmosTrigger tr = new CosmosTrigger(context, container, trId);
            Assert.AreEqual(tr.LinkUri.OriginalString, "/dbs/" + databaseId + "/colls/" + crId + "/triggers/" + trId);

            CosmosUserDefinedFunction udf = new CosmosUserDefinedFunction(context, container, udfId);
            Assert.AreEqual(udf.LinkUri.OriginalString, "/dbs/" + databaseId + "/colls/" + crId + "/udfs/" + udfId);
        }

        [TestMethod]
        public void ValidateItemRequestOptions()
        {
            ItemRequestOptions options = new ItemRequestOptions
            {
                PreTriggers = new List<string>()
                {
                    "preTrigger"
                },

                PostTriggers = new List<string>()
                {
                    "postTrigger"
                }
            };

            CosmosRequestMessage httpRequest = new CosmosRequestMessage(
                HttpMethod.Post,
                new Uri("/dbs/testdb/colls/testcontainer/docs/testId", UriKind.Relative));

            options.FillRequestOptions(httpRequest);

            Assert.IsTrue(httpRequest.Headers.TryGetValue(HttpConstants.HttpHeaders.PreTriggerInclude, out string preTriggerHeader));
            Assert.IsTrue(httpRequest.Headers.TryGetValue(HttpConstants.HttpHeaders.PostTriggerInclude, out string postTriggerHeader));
        }

        [TestMethod]
        public void ValidateItemRequestOptionsMultipleTriggers()
        {
            ItemRequestOptions options = new ItemRequestOptions
            {
                PreTriggers = new List<string>()
                {
                    "preTrigger",
                    "preTrigger2",
                    "preTrigger3",
                    "preTrigger4"
                },

                PostTriggers = new List<string>()
                {
                    "postTrigger",
                    "postTrigger2",
                    "postTrigger3",
                    "postTrigger4",
                    "postTrigger5"
                }
            };

            CosmosRequestMessage httpRequest = new CosmosRequestMessage(
                HttpMethod.Post,
                new Uri("/dbs/testdb/colls/testcontainer/docs/testId", UriKind.Relative));

            options.FillRequestOptions(httpRequest);

            Assert.IsTrue(httpRequest.Headers.TryGetValue(HttpConstants.HttpHeaders.PreTriggerInclude, out string preTriggerHeader));
            Assert.IsTrue(httpRequest.Headers.TryGetValue(HttpConstants.HttpHeaders.PostTriggerInclude, out string postTriggerHeader));
        }

        [TestMethod]
        public void ValidateSetItemRequestOptions()
        {
            ItemRequestOptions options = new ItemRequestOptions();
            options.PreTriggers = new List<string>() { "preTrigger" };
            options.PostTriggers = new List<string>() { "postTrigger" };

            CosmosRequestMessage httpRequest = new CosmosRequestMessage(
                HttpMethod.Post,
                new Uri("/dbs/testdb/colls/testcontainer/docs/testId", UriKind.Relative));

            options.FillRequestOptions(httpRequest);

            Assert.IsTrue(httpRequest.Headers.TryGetValue(HttpConstants.HttpHeaders.PreTriggerInclude, out string preTriggerHeader));
            Assert.IsTrue(httpRequest.Headers.TryGetValue(HttpConstants.HttpHeaders.PostTriggerInclude, out string postTriggerHeader));
        }

    }
}
