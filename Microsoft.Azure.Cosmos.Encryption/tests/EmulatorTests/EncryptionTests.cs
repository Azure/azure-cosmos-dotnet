﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.EmulatorTests
{
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    [TestClass]
    public class EncryptionTests
    {
        private static readonly EncryptionKeyWrapMetadata metadata1 = new EncryptionKeyWrapMetadata("metadata1");
        private static readonly EncryptionKeyWrapMetadata metadata2 = new EncryptionKeyWrapMetadata("metadata2");
        private const string metadataUpdateSuffix = "updated";
        private static TimeSpan cacheTTL = TimeSpan.FromDays(1);

        private const string dekId = "mydek";

        private static CosmosClient client;

        private static Database database;
        private static DataEncryptionKeyProperties dekProperties;
        private static Container itemContainer;
        private static Container keyContainer;
        private static CosmosDataEncryptionKeyProvider dekProvider;
        private static TestEncryptor encryptor;

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            EncryptionTests.dekProvider = new CosmosDataEncryptionKeyProvider(new TestKeyWrapProvider());
            EncryptionTests.encryptor = new TestEncryptor(EncryptionTests.dekProvider);

            EncryptionTests.client = EncryptionTests.GetClient();
            EncryptionTests.database = await EncryptionTests.client.CreateDatabaseAsync(Guid.NewGuid().ToString());

            EncryptionTests.keyContainer = await EncryptionTests.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id", 400);
            await EncryptionTests.dekProvider.InitializeAsync(EncryptionTests.database, EncryptionTests.keyContainer.Id);

            EncryptionTests.itemContainer = await EncryptionTests.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/PK", 400);

            EncryptionTests.dekProperties = await EncryptionTests.CreateDekAsync(EncryptionTests.dekProvider, EncryptionTests.dekId);
        }

        [ClassCleanup]
        public static async Task ClassCleanup()
        {
            if (EncryptionTests.database != null)
            {
                using (await EncryptionTests.database.DeleteStreamAsync()) { }
            }

            if (EncryptionTests.client != null)
            {
                EncryptionTests.client.Dispose();
            }
        }

        [TestMethod]
        public async Task EncryptionCreateDek()
        {
            string dekId = "anotherDek";
            DataEncryptionKeyProperties dekProperties = await EncryptionTests.CreateDekAsync(EncryptionTests.dekProvider, dekId);
            Assert.IsNotNull(dekProperties);
            Assert.IsNotNull(dekProperties.CreatedTime);
            Assert.IsNotNull(dekProperties.LastModified);
            Assert.IsNotNull(dekProperties.SelfLink);
            // Assert.IsNotNull(dekProperties.ResourceId);

            // Assert.AreEqual(dekProperties.LastModified, dekProperties.CreatedTime);
            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(EncryptionTests.metadata1.Value + EncryptionTests.metadataUpdateSuffix),
                dekProperties.EncryptionKeyWrapMetadata);

            // Use different DEK provider to avoid (unintentional) cache impact
            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestKeyWrapProvider());
            await dekProvider.InitializeAsync(EncryptionTests.database, EncryptionTests.keyContainer.Id);
            DataEncryptionKeyProperties readProperties = await dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(dekId);
            Assert.AreEqual(dekProperties, readProperties);
        }

        [TestMethod]
        public async Task EncryptionDekReadFeed()
        {
            Container newKeyContainer = await EncryptionTests.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id", 400);
            try
            {
                CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestKeyWrapProvider());
                await dekProvider.InitializeAsync(EncryptionTests.database, newKeyContainer.Id);

                string contosoV1 = "Contoso_v001";
                string contosoV2 = "Contoso_v002";
                string fabrikamV1 = "Fabrikam_v001";
                string fabrikamV2 = "Fabrikam_v002";

                await EncryptionTests.CreateDekAsync(dekProvider, contosoV1);
                await EncryptionTests.CreateDekAsync(dekProvider, contosoV2);
                await EncryptionTests.CreateDekAsync(dekProvider, fabrikamV1);
                await EncryptionTests.CreateDekAsync(dekProvider, fabrikamV2);

                // Test getting all keys
                await EncryptionTests.IterateDekFeedAsync(
                    dekProvider,
                    new List<string> { contosoV1, contosoV2, fabrikamV1, fabrikamV2 },
                    isExpectedDeksCompleteSetForRequest: true,
                    isResultOrderExpected: false,
                    "SELECT * from c");

                // Test getting specific subset of keys
                await EncryptionTests.IterateDekFeedAsync(
                    dekProvider,
                    new List<string> { contosoV2 },
                    isExpectedDeksCompleteSetForRequest: false,
                    isResultOrderExpected: true,
                    "SELECT TOP 1 * from c where c.id >= 'Contoso_v000' and c.id <= 'Contoso_v999' ORDER BY c.id DESC");

                // Ensure only required results are returned
                await EncryptionTests.IterateDekFeedAsync(
                    dekProvider,
                    new List<string> { contosoV1, contosoV2 },
                    isExpectedDeksCompleteSetForRequest: true,
                    isResultOrderExpected: true,
                    "SELECT * from c where c.id >= 'Contoso_v000' and c.id <= 'Contoso_v999' ORDER BY c.id ASC");

                // Test pagination
                await EncryptionTests.IterateDekFeedAsync(
                    dekProvider,
                    new List<string> { contosoV1, contosoV2, fabrikamV1, fabrikamV2 },
                    isExpectedDeksCompleteSetForRequest: true,
                    isResultOrderExpected: false,
                    "SELECT * from c",
                    itemCountInPage: 3);
            }
            finally
            {
                await newKeyContainer.DeleteContainerStreamAsync();
            }
        }

        [TestMethod]
        public async Task EncryptionCreateItemWithoutEncryptionOptions()
        {
            TestDoc testDoc = TestDoc.Create();
            ItemResponse<TestDoc> createResponse = await EncryptionTests.itemContainer.CreateItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK));
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.AreEqual(testDoc, createResponse.Resource);
        }

        [TestMethod]
        public async Task EncryptionCreateItem()
        {
            TestDoc testDoc = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainer, EncryptionTests.dekId, TestDoc.PathsToEncrypt);

            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainer, testDoc);

            await EncryptionTests.VerifyItemByReadStreamAsync(EncryptionTests.itemContainer, testDoc);

            TestDoc expectedDoc = new TestDoc(testDoc);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.itemContainer,
                "SELECT * FROM c",
                expectedDoc);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.itemContainer,
                string.Format(
                    "SELECT * FROM c where c.PK = '{0}' and c.id = '{1}' and c.NonSensitive = '{2}'",
                    expectedDoc.PK,
                    expectedDoc.Id,
                    expectedDoc.NonSensitive),
                expectedDoc);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.itemContainer,
                string.Format("SELECT * FROM c where c.Sensitive = '{0}'", testDoc.Sensitive),
                expectedDoc: null);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.itemContainer,
                queryDefinition: new QueryDefinition(
                    "select * from c where c.id = @theId and c.PK = @thePK")
                         .WithParameter("@theId", expectedDoc.Id)
                         .WithParameter("@thePK", expectedDoc.PK),
                expectedDoc: expectedDoc);

            expectedDoc.Sensitive = null;

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.itemContainer,
                "SELECT c.id, c.PK, c.Sensitive, c.NonSensitive FROM c",
                expectedDoc);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.itemContainer,
                "SELECT c.id, c.PK, c.NonSensitive FROM c",
                expectedDoc);

            await EncryptionTests.ValidateSprocResultsAsync(
                EncryptionTests.itemContainer,
                expectedDoc);
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryResultMultipleDocs()
        {
            TestDoc testDoc1 = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainer, EncryptionTests.dekId, TestDoc.PathsToEncrypt);
            TestDoc testDoc2 = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainer, EncryptionTests.dekId, TestDoc.PathsToEncrypt);

            string query = $"SELECT * FROM c WHERE c.PK in ('{testDoc1.PK}', '{testDoc2.PK}')";
            await EncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(EncryptionTests.itemContainer, testDoc1, testDoc2, query);

            // ORDER BY query
            query = query + " ORDER BY c._ts";
            await EncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(EncryptionTests.itemContainer, testDoc1, testDoc2, query);
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryResultDifferentDeks()
        {
            string dekId1 = "mydek1";
            await EncryptionTests.CreateDekAsync(EncryptionTests.dekProvider, dekId1);

            TestDoc testDoc1 = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainer, EncryptionTests.dekId, TestDoc.PathsToEncrypt);
            TestDoc testDoc2 = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainer, dekId1, TestDoc.PathsToEncrypt);
            string query = $"SELECT * FROM c WHERE c.PK in ('{testDoc1.PK}', '{testDoc2.PK}')";

            await EncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(EncryptionTests.itemContainer, testDoc1, testDoc2, query);
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryResultMultipleEncryptedProperties()
        {
            TestDoc testDoc = await EncryptionTests.CreateItemAsync(
                EncryptionTests.itemContainer,
                EncryptionTests.dekId,
                new List<string>() { "/Sensitive", "/NonSensitive" });

            TestDoc expectedDoc = new TestDoc(testDoc);

            await EncryptionTests.ValidateQueryResultsAsync(
                EncryptionTests.itemContainer,
                "SELECT * FROM c",
                expectedDoc);
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryValueResponse()
        {
            TestDoc testDoc = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainer, EncryptionTests.dekId, TestDoc.PathsToEncrypt);
            string query = "SELECT VALUE COUNT(1) FROM c";

            await EncryptionTests.ValidateQueryResponseAsync(EncryptionTests.itemContainer, query);
        }

        [TestMethod]
        public async Task DecryptGroupByQueryResultTest()
        {
            string partitionKey = Guid.NewGuid().ToString();

            TestDoc testDoc1 = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainer, EncryptionTests.dekId, TestDoc.PathsToEncrypt, partitionKey);
            TestDoc testDoc2 = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainer, EncryptionTests.dekId, TestDoc.PathsToEncrypt, partitionKey);

            string query = $"SELECT COUNT(c.Id), c.PK " +
                           $"FROM c WHERE c.PK = '{partitionKey}' " +
                           $"GROUP BY c.PK ";

            await EncryptionTests.ValidateQueryResponseAsync(EncryptionTests.itemContainer, query);
        }

        [TestMethod]
        public async Task EncryptionRudItem()
        {
            TestDoc testDoc = await EncryptionTests.UpsertItemAsync(
                EncryptionTests.itemContainer,
                TestDoc.Create(),
                EncryptionTests.dekId,
                TestDoc.PathsToEncrypt,
                HttpStatusCode.Created);

            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainer, testDoc);

            testDoc.NonSensitive = Guid.NewGuid().ToString();
            testDoc.Sensitive = Guid.NewGuid().ToString();

            ItemResponse<TestDoc> upsertResponse = await EncryptionTests.UpsertItemAsync(
                EncryptionTests.itemContainer,
                testDoc,
                EncryptionTests.dekId,
                TestDoc.PathsToEncrypt,
                HttpStatusCode.OK);
            TestDoc updatedDoc = upsertResponse.Resource;

            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainer, updatedDoc);

            updatedDoc.NonSensitive = Guid.NewGuid().ToString();
            updatedDoc.Sensitive = Guid.NewGuid().ToString();

            TestDoc replacedDoc = await EncryptionTests.ReplaceItemAsync(
                EncryptionTests.itemContainer,
                updatedDoc,
                EncryptionTests.dekId,
                TestDoc.PathsToEncrypt,
                upsertResponse.ETag);

            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainer, replacedDoc);

            await EncryptionTests.DeleteItemAsync(EncryptionTests.itemContainer, replacedDoc);
        }

        [TestMethod]
        public async Task EncryptionResourceTokenAuthRestricted()
        {
            TestDoc testDoc = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainer, EncryptionTests.dekId, TestDoc.PathsToEncrypt);

            User restrictedUser = EncryptionTests.database.GetUser(Guid.NewGuid().ToString());
            await EncryptionTests.database.CreateUserAsync(restrictedUser.Id);

            PermissionProperties restrictedUserPermission = await restrictedUser.CreatePermissionAsync(
                new PermissionProperties(Guid.NewGuid().ToString(), PermissionMode.All, EncryptionTests.itemContainer));

            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestKeyWrapProvider());
            TestEncryptor encryptor = new TestEncryptor(dekProvider);

            CosmosClient clientForRestrictedUser = TestCommon.CreateCosmosClient(
                restrictedUserPermission.Token,
                builder => builder
                .WithEncryptor(encryptor));

            Database databaseForRestrictedUser = clientForRestrictedUser.GetDatabase(EncryptionTests.database.Id);
            Container containerForRestrictedUser = databaseForRestrictedUser.GetContainer(EncryptionTests.itemContainer.Id);

            await EncryptionTests.PerformForbiddenOperationAsync(() =>
                dekProvider.InitializeAsync(databaseForRestrictedUser, EncryptionTests.keyContainer.Id), "CosmosDekProvider.InitializeAsync");

            await EncryptionTests.PerformOperationOnUninitializedDekProviderAsync(() =>
                dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(EncryptionTests.dekId), "DEK.ReadAsync");

            await EncryptionTests.PerformOperationOnUninitializedDekProviderAsync(() =>
                containerForRestrictedUser.ReadItemAsync<TestDoc>(testDoc.Id, new PartitionKey(testDoc.PK)), "ReadItemAsync");

            await EncryptionTests.PerformOperationOnUninitializedDekProviderAsync(() =>
                containerForRestrictedUser.ReadItemStreamAsync(testDoc.Id, new PartitionKey(testDoc.PK)), "ReadItemStreamAsync");
        }

        [TestMethod]
        public async Task EncryptionResourceTokenAuthAllowed()
        {
            User keyManagerUser = EncryptionTests.database.GetUser(Guid.NewGuid().ToString());
            await EncryptionTests.database.CreateUserAsync(keyManagerUser.Id);

            PermissionProperties keyManagerUserPermission = await keyManagerUser.CreatePermissionAsync(
                new PermissionProperties(Guid.NewGuid().ToString(), PermissionMode.All, EncryptionTests.keyContainer));

            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestKeyWrapProvider());
            TestEncryptor encryptor = new TestEncryptor(dekProvider);
            CosmosClient clientForKeyManagerUser = TestCommon.CreateCosmosClient(
                keyManagerUserPermission.Token,
                builder => builder
                .WithEncryptor(encryptor));

            Database databaseForKeyManagerUser = clientForKeyManagerUser.GetDatabase(EncryptionTests.database.Id);

            await dekProvider.InitializeAsync(databaseForKeyManagerUser, EncryptionTests.keyContainer.Id);

            DataEncryptionKeyProperties readDekProperties = await dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(EncryptionTests.dekId);
            Assert.AreEqual(EncryptionTests.dekProperties, readDekProperties);
        }

        [TestMethod]
        public async Task EncryptionRestrictedProperties()
        {
            TestDoc testDoc = TestDoc.Create();

            try
            {
                await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainer, EncryptionTests.dekId, new List<string>() { "/id" });
                Assert.Fail("Expected item creation with id specified to be encrypted to fail.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
            }

            try
            {
                await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainer, EncryptionTests.dekId, new List<string>() { "/PK" });
                Assert.Fail("Expected item creation with PK specified to be encrypted to fail.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
            }
        }

        [TestMethod]
        public async Task EncryptionBulkCrud()
        {
            TestDoc docToReplace = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainer, EncryptionTests.dekId, TestDoc.PathsToEncrypt);
            docToReplace.NonSensitive = Guid.NewGuid().ToString();
            docToReplace.Sensitive = Guid.NewGuid().ToString();

            TestDoc docToUpsert = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainer, EncryptionTests.dekId, TestDoc.PathsToEncrypt);
            docToUpsert.NonSensitive = Guid.NewGuid().ToString();
            docToUpsert.Sensitive = Guid.NewGuid().ToString();

            TestDoc docToDelete = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainer, EncryptionTests.dekId, TestDoc.PathsToEncrypt);

            CosmosClient clientWithBulk = TestCommon.CreateCosmosClient(builder => builder
               .WithEncryptor(EncryptionTests.encryptor)
               .WithBulkExecution(true));

            Database databaseWithBulk = clientWithBulk.GetDatabase(EncryptionTests.database.Id);
            Container containerWithBulk = databaseWithBulk.GetContainer(EncryptionTests.itemContainer.Id);

            List<Task> tasks = new List<Task>
            {
                EncryptionTests.CreateItemAsync(containerWithBulk, EncryptionTests.dekId, TestDoc.PathsToEncrypt),
                EncryptionTests.UpsertItemAsync(containerWithBulk, TestDoc.Create(), EncryptionTests.dekId, TestDoc.PathsToEncrypt, HttpStatusCode.Created),
                EncryptionTests.ReplaceItemAsync(containerWithBulk, docToReplace, EncryptionTests.dekId, TestDoc.PathsToEncrypt),
                EncryptionTests.UpsertItemAsync(containerWithBulk, docToUpsert, EncryptionTests.dekId, TestDoc.PathsToEncrypt, HttpStatusCode.OK),
                EncryptionTests.DeleteItemAsync(containerWithBulk, docToDelete)
            };
            await Task.WhenAll(tasks);
        }

        [TestMethod]
        public async Task EncryptionTransactionBatchCrud()
        {
            string partitionKey = "thePK";
            string dek1 = EncryptionTests.dekId;
            string dek2 = "dek2Forbatch";
            await EncryptionTests.CreateDekAsync(EncryptionTests.dekProvider, dek2);

            TestDoc doc1ToCreate = TestDoc.Create(partitionKey);
            TestDoc doc2ToCreate = TestDoc.Create(partitionKey);
            TestDoc doc3ToCreate = TestDoc.Create(partitionKey);

            ItemResponse<TestDoc> doc1ToReplaceCreateResponse = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainer, dek1, TestDoc.PathsToEncrypt, partitionKey);
            TestDoc doc1ToReplace = doc1ToReplaceCreateResponse.Resource;
            doc1ToReplace.NonSensitive = Guid.NewGuid().ToString();
            doc1ToReplace.Sensitive = Guid.NewGuid().ToString();

            TestDoc doc2ToReplace = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainer, dek2, TestDoc.PathsToEncrypt, partitionKey);
            doc2ToReplace.NonSensitive = Guid.NewGuid().ToString();
            doc2ToReplace.Sensitive = Guid.NewGuid().ToString();

            TestDoc doc1ToUpsert = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainer, dek2, TestDoc.PathsToEncrypt, partitionKey);
            doc1ToUpsert.NonSensitive = Guid.NewGuid().ToString();
            doc1ToUpsert.Sensitive = Guid.NewGuid().ToString();

            TestDoc doc2ToUpsert = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainer, dek1, TestDoc.PathsToEncrypt, partitionKey);
            doc2ToUpsert.NonSensitive = Guid.NewGuid().ToString();
            doc2ToUpsert.Sensitive = Guid.NewGuid().ToString();

            TestDoc docToDelete = await EncryptionTests.CreateItemAsync(EncryptionTests.itemContainer, dek1, TestDoc.PathsToEncrypt, partitionKey);

            TransactionalBatchResponse batchResponse = await EncryptionTests.itemContainer.CreateTransactionalBatch(new Cosmos.PartitionKey(partitionKey))
                .CreateItem(doc1ToCreate, EncryptionTests.GetBatchItemRequestOptions(EncryptionTests.itemContainer, dek1, TestDoc.PathsToEncrypt))
                .CreateItemStream(doc2ToCreate.ToStream(), EncryptionTests.GetBatchItemRequestOptions(EncryptionTests.itemContainer, dek2, TestDoc.PathsToEncrypt))
                .ReplaceItem(doc1ToReplace.Id, doc1ToReplace, EncryptionTests.GetBatchItemRequestOptions(EncryptionTests.itemContainer, dek2, TestDoc.PathsToEncrypt, doc1ToReplaceCreateResponse.ETag))
                .CreateItem(doc3ToCreate)
                .ReplaceItemStream(doc2ToReplace.Id, doc2ToReplace.ToStream(), EncryptionTests.GetBatchItemRequestOptions(EncryptionTests.itemContainer, dek2, TestDoc.PathsToEncrypt))
                .UpsertItem(doc1ToUpsert, EncryptionTests.GetBatchItemRequestOptions(EncryptionTests.itemContainer, dek1, TestDoc.PathsToEncrypt))
                .DeleteItem(docToDelete.Id)
                .UpsertItemStream(doc2ToUpsert.ToStream(), EncryptionTests.GetBatchItemRequestOptions(EncryptionTests.itemContainer, dek2, TestDoc.PathsToEncrypt))
                .ExecuteAsync();

            Assert.AreEqual(HttpStatusCode.OK, batchResponse.StatusCode);

            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainer, doc1ToCreate);
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainer, doc2ToCreate);
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainer, doc3ToCreate);
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainer, doc1ToReplace);
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainer, doc2ToReplace);
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainer, doc1ToUpsert);
            await EncryptionTests.VerifyItemByReadAsync(EncryptionTests.itemContainer, doc2ToUpsert);

            ResponseMessage readResponseMessage = await EncryptionTests.itemContainer.ReadItemStreamAsync(docToDelete.Id, new PartitionKey(docToDelete.PK));
            Assert.AreEqual(HttpStatusCode.NotFound, readResponseMessage.StatusCode);
        }

        private static async Task ValidateSprocResultsAsync(Container container, TestDoc expectedDoc)
        {
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = @"function(docId) {
                var context = getContext();
                var collection = context.getCollection();
                var docUri =  collection.getAltLink() + '/docs/' + docId;
                var response = context.getResponse();

                collection.readDocument(docUri, { },
                    function(error, resource, options) {
                        response.setBody(resource);
                    });
            }";

            StoredProcedureResponse storedProcedureResponse =
                await container.Scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);

            StoredProcedureExecuteResponse<TestDoc> sprocResponse = await container.Scripts.ExecuteStoredProcedureAsync<TestDoc>(
                sprocId,
                new PartitionKey(expectedDoc.PK),
                parameters: new dynamic[] { expectedDoc.Id });

            Assert.AreEqual(expectedDoc, sprocResponse.Resource);
        }

        // One of query or queryDefinition is to be passed in non-null
        private static async Task ValidateQueryResultsAsync(
            Container container,
            string query = null,
            TestDoc expectedDoc = null,
            QueryDefinition queryDefinition = null)
        {
            QueryRequestOptions requestOptions = expectedDoc != null ? new QueryRequestOptions() { PartitionKey = new PartitionKey(expectedDoc.PK) } : null;
            FeedIterator<TestDoc> queryResponseIterator;
            if (query != null)
            {
                queryResponseIterator = container.GetItemQueryIterator<TestDoc>(query, requestOptions: requestOptions);
            }
            else
            {
                queryResponseIterator = container.GetItemQueryIterator<TestDoc>(queryDefinition, requestOptions: requestOptions);
            }

            FeedResponse<TestDoc> readDocs = await queryResponseIterator.ReadNextAsync();
            Assert.AreEqual(null, readDocs.ContinuationToken);

            if (expectedDoc != null)
            {
                Assert.AreEqual(1, readDocs.Count);
                TestDoc readDoc = readDocs.Single();
                Assert.AreEqual(expectedDoc, readDoc);
            }
            else
            {
                Assert.AreEqual(0, readDocs.Count);
            }
        }

        private static async Task ValidateQueryResultsMultipleDocumentsAsync(
            Container container,
            TestDoc testDoc1,
            TestDoc testDoc2,
            string query)
        {
            FeedIterator<TestDoc> queryResponseIterator = container.GetItemQueryIterator<TestDoc>(query);
            FeedResponse<TestDoc> readDocs = await queryResponseIterator.ReadNextAsync();
            Assert.AreEqual(null, readDocs.ContinuationToken);
            Assert.AreEqual(2, readDocs.Count);
            foreach (TestDoc readDoc in readDocs)
            {
                Assert.AreEqual(readDoc, readDoc.Id.Equals(testDoc1.Id) ? testDoc1 : testDoc2);
            }
        }

        private static async Task ValidateQueryResponseAsync(Container container, string query)
        {
            FeedIterator feedIterator = container.GetItemQueryStreamIterator(query);
            while (feedIterator.HasMoreResults)
            {
                ResponseMessage response = await feedIterator.ReadNextAsync();
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.IsNull(response.ErrorMessage);
            }
        }

        private static CosmosClient GetClient()
        {
            return TestCommon.CreateCosmosClient(
                builder => builder.WithEncryptor(encryptor));
        }

        private static async Task IterateDekFeedAsync(
            CosmosDataEncryptionKeyProvider dekProvider,
            List<string> expectedDekIds,
            bool isExpectedDeksCompleteSetForRequest,
            bool isResultOrderExpected,
            string query,
            int? itemCountInPage = null)
        {
            int remainingItemCount = expectedDekIds.Count;
            QueryRequestOptions requestOptions = null;
            if (itemCountInPage.HasValue)
            {
                requestOptions = new QueryRequestOptions()
                {
                    MaxItemCount = itemCountInPage
                };
            }

            FeedIterator<DataEncryptionKeyProperties> dekIterator = dekProvider.DataEncryptionKeyContainer
                .GetDataEncryptionKeyQueryIterator<DataEncryptionKeyProperties>(
                    query,
                    requestOptions: requestOptions);

            Assert.IsTrue(dekIterator.HasMoreResults);

            List<string> readDekIds = new List<string>();
            while (remainingItemCount > 0)
            {
                FeedResponse<DataEncryptionKeyProperties> page = await dekIterator.ReadNextAsync();
                if (itemCountInPage.HasValue)
                {
                    // last page
                    if (remainingItemCount < itemCountInPage.Value)
                    {
                        Assert.AreEqual(remainingItemCount, page.Count);
                    }
                    else
                    {
                        Assert.AreEqual(itemCountInPage.Value, page.Count);
                    }
                }
                else
                {
                    Assert.AreEqual(expectedDekIds.Count, page.Count);
                }

                remainingItemCount -= page.Count;
                if (isExpectedDeksCompleteSetForRequest)
                {
                    Assert.AreEqual(remainingItemCount > 0, dekIterator.HasMoreResults);
                }

                foreach (DataEncryptionKeyProperties dek in page.Resource)
                {
                    readDekIds.Add(dek.Id);
                }
            }

            if (isResultOrderExpected)
            {
                Assert.IsTrue(expectedDekIds.SequenceEqual(readDekIds));
            }
            else
            {
                Assert.IsTrue(expectedDekIds.ToHashSet().SetEquals(readDekIds));
            }
        }

        private static async Task<ItemResponse<TestDoc>> UpsertItemAsync(
            Container container,
            TestDoc testDoc,
            string dekId,
            List<string> pathsToEncrypt,
            HttpStatusCode expectedStatusCode)
        {
            ItemResponse<TestDoc> upsertResponse = await container.UpsertItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK),
                EncryptionTests.GetRequestOptions(container, dekId, pathsToEncrypt));
            Assert.AreEqual(expectedStatusCode, upsertResponse.StatusCode);
            Assert.AreEqual(testDoc, upsertResponse.Resource);
            return upsertResponse;
        }

        private static async Task<ItemResponse<TestDoc>> CreateItemAsync(
            Container container,
            string dekId,
            List<string> pathsToEncrypt,
            string partitionKey = null)
        {
            TestDoc testDoc = TestDoc.Create(partitionKey);
            ItemResponse<TestDoc> createResponse = await container.CreateItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK),
                EncryptionTests.GetRequestOptions(container, dekId, pathsToEncrypt));
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.AreEqual(testDoc, createResponse.Resource);
            return createResponse;
        }

        private static async Task<ItemResponse<TestDoc>> ReplaceItemAsync(
            Container container,
            TestDoc testDoc,
            string dekId,
            List<string> pathsToEncrypt,
            string etag = null)
        {
            ItemResponse<TestDoc> replaceResponse = await container.ReplaceItemAsync(
                testDoc,
                testDoc.Id,
                new PartitionKey(testDoc.PK),
                EncryptionTests.GetRequestOptions(container, dekId, pathsToEncrypt, etag));

            Assert.AreEqual(HttpStatusCode.OK, replaceResponse.StatusCode);
            Assert.AreEqual(testDoc, replaceResponse.Resource);
            return replaceResponse;
        }

        private static async Task<ItemResponse<TestDoc>> DeleteItemAsync(
            Container container,
            TestDoc testDoc)
        {
            ItemResponse<TestDoc> deleteResponse = await container.DeleteItemAsync<TestDoc>(
                testDoc.Id,
                new PartitionKey(testDoc.PK));

            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
            Assert.IsNull(deleteResponse.Resource);
            return deleteResponse;
        }

        private static ItemRequestOptions GetRequestOptions(
            Container container,
            string dekId,
            List<string> pathsToEncrypt,
            string ifMatchEtag = null)
        {
            return new ItemRequestOptions
            {
                EncryptionOptions = new EncryptionOptions
                {
                    DataEncryptionKeyId = dekId,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                    PathsToEncrypt = pathsToEncrypt
                },
                IfMatchEtag = ifMatchEtag
            };
        }

        private static TransactionalBatchItemRequestOptions GetBatchItemRequestOptions(
            Container container,
            string dekId,
            List<string> pathsToEncrypt,
            string ifMatchEtag = null)
        {
            return new TransactionalBatchItemRequestOptions
            {
                EncryptionOptions = new EncryptionOptions
                {
                    DataEncryptionKeyId = dekId,
                    EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                    PathsToEncrypt = pathsToEncrypt
                },
                IfMatchEtag = ifMatchEtag
            };
        }

        private static async Task VerifyItemByReadStreamAsync(Container container, TestDoc testDoc)
        {
            ResponseMessage readResponseMessage = await container.ReadItemStreamAsync(testDoc.Id, new PartitionKey(testDoc.PK));
            Assert.AreEqual(HttpStatusCode.OK, readResponseMessage.StatusCode);
            Assert.IsNotNull(readResponseMessage.Content);
            TestDoc readDoc = TestCommon.FromStream<TestDoc>(readResponseMessage.Content);
            Assert.AreEqual(testDoc, readDoc);
        }

        private static async Task VerifyItemByReadAsync(Container container, TestDoc testDoc)
        {
            ItemResponse<TestDoc> readResponse = await container.ReadItemAsync<TestDoc>(testDoc.Id, new PartitionKey(testDoc.PK));
            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            Assert.AreEqual(testDoc, readResponse.Resource);
        }

        private static async Task<DataEncryptionKeyProperties> CreateDekAsync(CosmosDataEncryptionKeyProvider dekProvider, string dekId)
        {
            ItemResponse<DataEncryptionKeyProperties> dekResponse = await dekProvider.DataEncryptionKeyContainer.CreateDataEncryptionKeyAsync(
                dekId,
                CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                EncryptionTests.metadata1);

            Assert.AreEqual(HttpStatusCode.Created, dekResponse.StatusCode);
            Assert.IsTrue(dekResponse.RequestCharge > 0);
            Assert.IsNotNull(dekResponse.ETag);

            DataEncryptionKeyProperties dekProperties = dekResponse.Resource;
            Assert.AreEqual(dekResponse.ETag, dekProperties.ETag);
            Assert.AreEqual(dekId, dekProperties.Id);
            return dekProperties;
        }

        private static async Task PerformForbiddenOperationAsync(Func<Task> func, string operationName)
        {
            try
            {
                await func();
                Assert.Fail($"Expected resource token based client to not be able to perform {operationName}");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
            {
            }
        }

        private static async Task PerformOperationOnUninitializedDekProviderAsync(Func<Task> func, string operationName)
        {
            try
            {
                await func();
                Assert.Fail($"Expected {operationName} to not work on uninitialized CosmosDataEncryptionKeyProvider.");
            }
            catch (InvalidOperationException ex)
            {
                Assert.IsTrue(ex.Message.Contains("The CosmosDataEncryptionKeyProvider was not initialized."));
            }
        }

        public class TestDoc
        {
            public static List<string> PathsToEncrypt { get; } = new List<string>() { "/Sensitive" };

            [JsonProperty("id")]
            public string Id { get; set; }

            public string PK { get; set; }

            public string NonSensitive { get; set; }

            public string Sensitive { get; set; }

            public TestDoc()
            {
            }

            public TestDoc(TestDoc other)
            {
                this.Id = other.Id;
                this.PK = other.PK;
                this.NonSensitive = other.NonSensitive;
                this.Sensitive = other.Sensitive;
            }

            public override bool Equals(object obj)
            {
                return obj is TestDoc doc
                       && this.Id == doc.Id
                       && this.PK == doc.PK
                       && this.NonSensitive == doc.NonSensitive
                       && this.Sensitive == doc.Sensitive;
            }

            public override int GetHashCode()
            {
                int hashCode = 1652434776;
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Id);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.PK);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.NonSensitive);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Sensitive);
                return hashCode;
            }

            public static TestDoc Create(string partitionKey = null)
            {
                return new TestDoc()
                {
                    Id = Guid.NewGuid().ToString(),
                    PK = partitionKey ?? Guid.NewGuid().ToString(),
                    NonSensitive = Guid.NewGuid().ToString(),
                    Sensitive = Guid.NewGuid().ToString()
                };
            }

            public Stream ToStream()
            {
                return TestCommon.ToStream(this);
            }
        }

        private class TestKeyWrapProvider : EncryptionKeyWrapProvider
        {
            public override Task<EncryptionKeyUnwrapResult> UnwrapKeyAsync(byte[] wrappedKey, EncryptionKeyWrapMetadata metadata, CancellationToken cancellationToken)
            {
                int moveBy = metadata.Value == EncryptionTests.metadata1.Value + EncryptionTests.metadataUpdateSuffix ? 1 : 2;
                return Task.FromResult(new EncryptionKeyUnwrapResult(wrappedKey.Select(b => (byte)(b - moveBy)).ToArray(), EncryptionTests.cacheTTL));
            }

            public override Task<EncryptionKeyWrapResult> WrapKeyAsync(byte[] key, EncryptionKeyWrapMetadata metadata, CancellationToken cancellationToken)
            {
                EncryptionKeyWrapMetadata responseMetadata = new EncryptionKeyWrapMetadata(metadata.Value + EncryptionTests.metadataUpdateSuffix);
                int moveBy = metadata.Value == EncryptionTests.metadata1.Value ? 1 : 2;
                return Task.FromResult(new EncryptionKeyWrapResult(key.Select(b => (byte)(b + moveBy)).ToArray(), responseMetadata));
            }
        }

        // This class is same as CosmosEncryptor but copied since the emulator tests don't
        // have internal visibility into Cosmos.Encryption assembly.
        private class TestEncryptor : Encryptor
        {
            public DataEncryptionKeyProvider DataEncryptionKeyProvider { get; }

            public TestEncryptor(DataEncryptionKeyProvider dataEncryptionKeyProvider)
            {
                this.DataEncryptionKeyProvider = dataEncryptionKeyProvider;
            }

            public override async Task<byte[]> DecryptAsync(
                byte[] cipherText,
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                DataEncryptionKey dek = await this.DataEncryptionKeyProvider.FetchDataEncryptionKeyAsync(
                    dataEncryptionKeyId,
                    encryptionAlgorithm,
                    cancellationToken);

                if (dek == null)
                {
                    throw new InvalidOperationException($"Null {nameof(DataEncryptionKey)} returned from {nameof(this.DataEncryptionKeyProvider.FetchDataEncryptionKeyAsync)}.");
                }

                return dek.DecryptData(cipherText);
            }

            public override async Task<byte[]> EncryptAsync(
                byte[] plainText,
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                DataEncryptionKey dek = await this.DataEncryptionKeyProvider.FetchDataEncryptionKeyAsync(
                    dataEncryptionKeyId,
                    encryptionAlgorithm,
                    cancellationToken);

                return dek.EncryptData(plainText);
            }
        }
    }
}