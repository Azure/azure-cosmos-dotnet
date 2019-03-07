﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.Azure.Cosmos.Services.Management.Tests;
    using Microsoft.CSharp.RuntimeBinder;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class DirectRESTTests
    {
        internal readonly Uri baseUri;
        internal readonly string masterKey;
        internal readonly Random random;

        public DirectRESTTests()
        {
            this.baseUri = new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]);
            this.masterKey = ConfigurationManager.AppSettings["MasterKey"];
            this.random = new Random();
        }

        private static HttpClient CreateHttpClient(string apiVersion)
        {
            HttpClient client = new HttpClient();

            CacheControlHeaderValue cacheControl = new CacheControlHeaderValue();
            cacheControl.NoCache = true;
            client.DefaultRequestHeaders.CacheControl = cacheControl;

            if (apiVersion != null)
            {
                client.DefaultRequestHeaders.Remove(HttpConstants.HttpHeaders.Version);
                client.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Version, apiVersion);
            }

            return client;
        }

        [ClassCleanup]
        public static void ClassCleanUp()
        {
            using (var client = TestCommon.CreateClient(true))
            {
                TestCommon.DeleteAllDatabasesAsync(client).Wait();
            }
        }

        [TestMethod]
        public void ValidateDatabaseCrud()
        {
            using (HttpClient client = CreateHttpClient(HttpConstants.Versions.CurrentVersion))
            {
                INameValueCollection headers = new StringKeyValueCollection();

                Logger.LogLine("Listing Databases");
                Uri uri = new Uri(baseUri, "dbs");
                client.AddMasterAuthorizationHeader("get", "", "dbs", headers, this.masterKey);
                ICollection<CosmosDatabaseSettings> databaseCollection1 = client.ListAllAsync<CosmosDatabaseSettings>(uri).Result;

                string databaseName = Guid.NewGuid().ToString("N");
                CosmosDatabaseSettings database = new CosmosDatabaseSettings
                {
                    Id = databaseName,
                };

                try
                {
                    Logger.LogLine("Try expanding database");
                    dynamic dynamicDatabase = database;
                    dynamicDatabase.Test = 100;
                    Assert.Fail("Should have thrown exception in previous statement");
                }
                catch (RuntimeBinderException)
                {
                    Logger.LogLine("Received expected exception");
                }

                Logger.LogLine("Creating Database");
                client.AddMasterAuthorizationHeader("post", "", "dbs", headers, this.masterKey);
                var retrievedTask = client.PostAsync(new Uri(baseUri, "dbs"), database.AsHttpContent());
                CosmosDatabaseSettings retrieved = retrievedTask.Result.ToResourceAsync<CosmosDatabaseSettings>().Result;

                Logger.LogLine("Creating Database with same name");
                try
                {
                    retrievedTask = client.PostAsync(new Uri(baseUri, "dbs"), database.AsHttpContent());
                    retrieved = retrievedTask.Result.ToResourceAsync<CosmosDatabaseSettings>().Result;
                    Assert.Fail("Should have thrown exception in previous statement");
                }
                catch (Exception)
                {
                    Logger.LogLine("Received expected exception");
                }

                Logger.LogLine("Listing Databases");
                client.AddMasterAuthorizationHeader("get", "", "dbs", headers, this.masterKey);
                ICollection<CosmosDatabaseSettings> databaseCollection2 = client.ListAllAsync<CosmosDatabaseSettings>(
                    new Uri(baseUri, "dbs")).Result;
                Assert.AreEqual(databaseCollection1.Count + 1, databaseCollection2.Count, "Collection count dont match");

                Logger.LogLine("Reading Database");

                client.AddMasterAuthorizationHeader("get", retrieved.ResourceId, "dbs", headers, this.masterKey);
                retrievedTask = client.GetAsync(new Uri(baseUri, retrieved.SelfLink));
                retrieved = retrievedTask.Result.ToResourceAsync<CosmosDatabaseSettings>().Result;

                Logger.LogLine("Reading Database with same etag");
                var ifNoneMatchHdr = new StringKeyValueCollection();
                ifNoneMatchHdr.Add("If-None-Match", retrieved.ETag);
                retrievedTask = client.GetAsync(new Uri(baseUri, retrieved.SelfLink), ifNoneMatchHdr);
                retrievedTask.Wait();
                Assert.IsTrue(retrievedTask.Result.StatusCode == HttpStatusCode.NotModified);

                if (retrievedTask.Result.StatusCode == HttpStatusCode.OK)
                {
                    Logger.LogLine("Warning!!! - Xstore does not return Content not modified. If this is not xstore then its bug");
                }

                Logger.LogLine("Updating Database");
                try
                {
                    using (var dbContent = database.AsHttpContent())
                    {
                        client.AddMasterAuthorizationHeader("put", retrieved.ResourceId, "dbs", headers, this.masterKey);
                        retrievedTask = client.PutAsync(new Uri(baseUri, retrieved.SelfLink), dbContent);
                        retrievedTask.Wait();
                    }
                    retrieved = retrievedTask.Result.ToResourceAsync<CosmosDatabaseSettings>().Result;
                    Assert.Fail("FAIL - Update database should fail");
                }
                catch (AggregateException agg)
                {
                    Assert.IsNotNull(agg.InnerException);
                    DocumentClientException e = agg.InnerException as DocumentClientException;
                    Assert.IsTrue(e.Error.Code == "MethodNotAllowed");
                }

                Logger.LogLine("Deleting Database");
                client.AddMasterAuthorizationHeader("delete", retrieved.ResourceId, "dbs", headers, this.masterKey);
                using (HttpResponseMessage deleteResponse = client.DeleteAsync(new Uri(baseUri, retrieved.SelfLink)).Result)
                {
                    Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode, "Delete Status code dont match");

                    Logger.LogLine("Listing Databases");
                    client.AddMasterAuthorizationHeader("get", "", "dbs", headers, this.masterKey);
                    databaseCollection2 = client.ListAllAsync<CosmosDatabaseSettings>(
                        new Uri(baseUri, "dbs")).Result;
                    Assert.AreEqual(databaseCollection1.Count, databaseCollection2.Count, "Collection count dont match");

                    try
                    {
                        Logger.LogLine("Try getting deleted database");
                        client.AddMasterAuthorizationHeader("get", retrieved.ResourceId, "dbs", headers, this.masterKey);
                        retrievedTask = client.GetAsync(new Uri(baseUri, retrieved.SelfLink));
                        retrieved = retrievedTask.Result.ToResourceAsync<CosmosDatabaseSettings>().Result;

                        Assert.Fail("Should have thrown exception in previous statement");
                    }
                    catch (AggregateException aggregatedException)
                    {
                        DocumentClientException documentClientException = aggregatedException.InnerException as DocumentClientException;
                        Assert.IsNotNull(documentClientException);
                        TestCommon.AssertException(documentClientException, HttpStatusCode.NotFound);
                        Logger.LogLine("Received expected exception.");
                        //Got expected exception.
                    }
                }

                client.AddMasterAuthorizationHeader("delete", retrieved.ResourceId, "dbs", headers, this.masterKey);

                using (HttpResponseMessage deleteResponse = client.DeleteAsync(new Uri(baseUri, retrieved.SelfLink)).Result)
                {
                    Assert.AreEqual(HttpStatusCode.NotFound, deleteResponse.StatusCode, "Delete Status code dont match");
                }
            }
        }

        [TestMethod]
        public void ValidateNegativeTestsForErrorMessages()
        {
            using (HttpClient client = CreateHttpClient(HttpConstants.Versions.CurrentVersion))
            {

                string databaseName = Guid.NewGuid().ToString("N");
                CosmosDatabaseSettings database = new CosmosDatabaseSettings
                {
                    Id = databaseName,
                };

                Task<HttpResponseMessage> retrievedTask = null;
                CosmosDatabaseSettings retrieved = null;

                Logger.LogLine("Reading Database");
                try
                {
                    Uri getUri = new Uri(baseUri, @"dbs/ZzJwAA==");
                    client.AddMasterAuthorizationHeader("get", "ZzJwAA==", "dbs", new StringKeyValueCollection(), masterKey);
                    retrievedTask = client.GetAsync(getUri);
                    retrieved = retrievedTask.Result.ToResourceAsync<CosmosDatabaseSettings>().Result;
                    Assert.Fail("FAIL - Exception exception trying to retrieve ZzJwAA==");
                }
                catch (AggregateException agg)
                {
                    Assert.IsNotNull(agg.InnerException);
                    DocumentClientException e = agg.InnerException as DocumentClientException;
                    Assert.IsTrue(e.Error.Code == "NotFound");
                    Logger.LogLine("Expected exception trying to retrieve ZzJwAA");
                    Logger.LogLine("Message : " + e.Error.Message);
                }


                // Update bad ID
                try
                {
                    Logger.LogLine("Updating Database");
                    using (var dbContent = database.AsHttpContent())
                    {
                        client.AddMasterAuthorizationHeader("put", "ZzJwAA==", "dbs", new StringKeyValueCollection(), masterKey);
                        retrievedTask = client.PutAsync(new Uri(baseUri, @"dbs/ZzJwAA=="), dbContent);
                        retrievedTask.Wait();
                    }
                    retrieved = retrievedTask.Result.ToResourceAsync<CosmosDatabaseSettings>().Result;
                    Assert.Fail("FAIL - Exception exception trying to update ZzJwAA");
                }
                catch (AggregateException agg)
                {
                    Assert.IsNotNull(agg.InnerException);
                    DocumentClientException e = agg.InnerException as DocumentClientException;
                    Assert.IsTrue(e.Error.Code == "MethodNotAllowed");
                    Logger.LogLine("Expected exception trying to retrieve ZzJwAA");
                    Logger.LogLine("Message : " + e.Error.Message);
                }

                // Delete bad ID
                try
                {
                    Logger.LogLine("Deleting Database");
                    client.AddMasterAuthorizationHeader("delete", "ZzJwAA==", "dbs", new StringKeyValueCollection(), masterKey);
                    retrievedTask = client.DeleteAsync(new Uri(baseUri, @"dbs/ZzJwAA=="));
                    retrievedTask.Wait();
                    retrieved = retrievedTask.Result.ToResourceAsync<CosmosDatabaseSettings>().Result;
                    Assert.Fail("FAIL - Exception exception trying to delete ZzJwAA");
                }
                catch (AggregateException agg)
                {
                    Assert.IsNotNull(agg.InnerException);
                    DocumentClientException e = agg.InnerException as DocumentClientException;
                    Assert.IsTrue(e.Error.Code == "NotFound");
                    Logger.LogLine("Expected exception trying to delete ZzJwAA");
                    Logger.LogLine("Message : " + e.Error.Message);
                }

                // Validate for few error messages - more detailed testing is done @ backend::ValidationTests.cpp
                // 1. DB  -- incorrect Name
                try
                {
                    Logger.LogLine("Creating Database with longer name");
                    database.Id = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

                    INameValueCollection headers = new StringKeyValueCollection();
                    client.AddMasterAuthorizationHeader("post", "", "dbs", headers, this.masterKey);
                    retrievedTask = client.PostAsync(new Uri(baseUri, "dbs"), database.AsHttpContent());
                    retrieved = retrievedTask.Result.ToResourceAsync<CosmosDatabaseSettings>().Result;
                    Assert.Fail("FAIL - Exception exception trying to create DB with longer name");
                }
                catch (AggregateException ae)
                {
                    DocumentClientException e = ae.InnerException as DocumentClientException;
                    Assert.IsNotNull(e);
                    Assert.IsTrue(e.Error.Code == HttpStatusCode.BadRequest.ToString(), "Wrong status code");
                    Logger.LogLine("Expected exception trying to create DB with negative maxsize");
                    Logger.LogLine("{0}", e.Error.Message);
                }


                // 2. DB  -- Missing Content
                try
                {
                    Logger.LogLine("Creating Database with empty content");
                    INameValueCollection headers = new StringKeyValueCollection();
                    client.AddMasterAuthorizationHeader("post", "", "dbs", headers, this.masterKey);
                    using (MemoryStream emptyContentStream = new MemoryStream())
                    {
                        using (StreamContent emptyContent = new StreamContent(emptyContentStream))
                        {
                            retrievedTask = client.PostAsync(new Uri(baseUri, "dbs"), emptyContent);
                            retrieved = retrievedTask.Result.ToResourceAsync<CosmosDatabaseSettings>().Result;
                            Assert.Fail("FAIL - Exception exception trying to create DB with empty content");
                        }
                    }
                }
                catch (AggregateException ae)
                {
                    DocumentClientException e = ae.InnerException as DocumentClientException;
                    Assert.IsNotNull(e);
                    Assert.IsTrue(e.Error.Code == HttpStatusCode.BadRequest.ToString(), "Wrong status code: {0}", e.ToString());
                    Logger.LogLine("Expected exception trying to create DB with with empty content");
                    Logger.LogLine("{0}", e.Error.Message);
                }

                // 3. DB  -- Bad Content
                try
                {
                    Logger.LogLine("Creating Database with empty content");
                    INameValueCollection headers = new StringKeyValueCollection();
                    client.AddMasterAuthorizationHeader("post", "", "dbs", headers, this.masterKey);
                    //byte[] contentBuffer = Encoding.UTF8.GetBytes(@"{""name"":""NAME"", ""content"":Name: NAME}");
                    byte[] contentBuffer = Encoding.UTF8.GetBytes(@"{""name"":""NAME"",{""a"":""b""}}");
                    using (MemoryStream badContentStream = new MemoryStream(contentBuffer))
                    {
                        using (StreamContent badContent = new StreamContent(badContentStream))
                        {
                            retrievedTask = client.PostAsync(new Uri(baseUri, "dbs"), badContent);
                            retrieved = retrievedTask.Result.ToResourceAsync<CosmosDatabaseSettings>().Result;
                            Assert.Fail("FAIL - Exception exception trying to create DB with empty content");
                        }
                    }
                }
                catch (AggregateException ae)
                {
                    DocumentClientException e = ae.InnerException as DocumentClientException;
                    Assert.IsNotNull(e);
                    Assert.IsTrue(e.Error.Code == HttpStatusCode.BadRequest.ToString(), "Wrong status code");
                    Logger.LogLine("Expected exception trying to create DB with with empty content");
                    Logger.LogLine("{0}", e.Error.Message);
                }
            }
        }

        [TestMethod]
        public async Task ValidateSessionTokenForREST()
        {
            try
            {
                using (HttpClient client = CreateHttpClient(HttpConstants.Versions.CurrentVersion))
                {
                    client.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.ConsistencyLevel, "Session");
                    INameValueCollection headers;
                    HttpResponseMessage message;

                    string databaseName1 = Guid.NewGuid().ToString("N");
                    CosmosDatabaseSettings database1 = new CosmosDatabaseSettings { Id = databaseName1, };

                    Logger.LogLine("Creating Database #1");
                    headers = new StringKeyValueCollection();
                    client.AddMasterAuthorizationHeader("post", "", "dbs", headers, this.masterKey);
                    var retrievedTask = client.PostAsync(new Uri(this.baseUri, "dbs"), database1.AsHttpContent());
                    CosmosDatabaseSettings retrievedDatabase1 = retrievedTask.Result.ToResourceAsync<CosmosDatabaseSettings>().Result;

                    string databaseName2 = Guid.NewGuid().ToString("N");
                    CosmosDatabaseSettings database2 = new CosmosDatabaseSettings { Id = databaseName2, };

                    Logger.LogLine("Creating Database #2");
                    headers = new StringKeyValueCollection();
                    client.AddMasterAuthorizationHeader("post", "", "dbs", headers, this.masterKey);
                    retrievedTask = client.PostAsync(new Uri(this.baseUri, "dbs"), database2.AsHttpContent());
                    CosmosDatabaseSettings retrievedDatabase2 = retrievedTask.Result.ToResourceAsync<CosmosDatabaseSettings>().Result;

                    string collectionName1 = "coll1";
                    CosmosContainerSettings collection1 = new CosmosContainerSettings { Id = collectionName1 };

                    Uri uri;

                    Logger.LogLine("Creating collection #1");
                    headers = new StringKeyValueCollection();
                    client.AddMasterAuthorizationHeader(
                        "post",
                        retrievedDatabase1.ResourceId,
                        "colls",
                        headers,
                        this.masterKey);
                    uri = new Uri(this.baseUri, retrievedDatabase1.SelfLink + "colls");

                    CosmosContainerSettings collection2 = null;
                    CosmosContainerSettings retrievedCollection1 = null;
                    using (message = client.PostAsync(uri, collection1.AsHttpContent()).Result)
                    {

                        Assert.IsTrue(message.IsSuccessStatusCode, "Collection #1 create failed");

                        retrievedCollection1 = message.ToResourceAsync<CosmosContainerSettings>().Result;

                        string collectionName2 = "coll2";
                        PartitionKeyDefinition partitionKey = new PartitionKeyDefinition
                        {
                            Paths = new Collection<string> { "/a" },
                            Kind = PartitionKind.Hash
                        };
                        collection2 = new CosmosContainerSettings { Id = collectionName2, PartitionKey = partitionKey };

                        Logger.LogLine("Creating collection #2");
                        headers = new StringKeyValueCollection();
                        client.AddMasterAuthorizationHeader(
                            "post",
                            retrievedDatabase2.ResourceId,
                            "colls",
                            headers,
                            this.masterKey);
                        client.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.OfferThroughput, "12000");
                        uri = new Uri(this.baseUri, retrievedDatabase2.SelfLink + "colls");
                    }

                    CosmosContainerSettings retrievedCollection2 = null;
                    Document retrievedDocument1 = null;
                    using (message = client.PostAsync(uri, collection2.AsHttpContent()).Result)
                    {

                        Assert.IsTrue(message.IsSuccessStatusCode, "Collection #2 create failed");

                        retrievedCollection2 = message.ToResourceAsync<CosmosContainerSettings>().Result;

                        Logger.LogLine("Creating document #1");

                        String document1 = "{\"id\":\"user1\",\"_rid\":100}";
                        headers = new StringKeyValueCollection();
                        client.AddMasterAuthorizationHeader(
                            "post",
                            retrievedCollection1.ResourceId,
                            "docs",
                            headers,
                            this.masterKey);


                        using (MemoryStream documentStream1 = new MemoryStream())
                        {
                            using (StreamWriter writer1 = new StreamWriter(documentStream1))
                            {
                                writer1.WriteLine(document1);
                                writer1.Flush();
                                documentStream1.Position = 0;

                                using (StreamContent documentContent1 = new StreamContent(documentStream1))
                                {

                                    uri = new Uri(this.baseUri, retrievedCollection1.SelfLink + "docs");
                                    message = client.PostAsync(uri, documentContent1).Result;

                                    Assert.IsTrue(message.IsSuccessStatusCode, "Doc#1 create failed");

                                    // For session reads, there had better be a session token.
                                    Assert.IsTrue(message.Headers.Contains(HttpConstants.HttpHeaders.SessionToken));

                                    retrievedDocument1 = message.ToResourceAsync<Document>().Result;
                                }
                            }
                        }

                    }
                    Logger.LogLine("Creating document #2");

                    String document2 = "{\"id\":\"user2\",\"_rid\":200, \"a\":1}";
                    headers = new StringKeyValueCollection();
                    client.AddMasterAuthorizationHeader(
                        "post",
                        retrievedCollection2.ResourceId,
                        "docs",
                        headers,
                        this.masterKey);
                    client.DefaultRequestHeaders.Remove(HttpConstants.HttpHeaders.PartitionKey);
                    client.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.PartitionKey, "[1]");


                    Document retrievedDocument2 = null;

                    using (MemoryStream documentStream2 = new MemoryStream())
                    {
                        using (StreamWriter writer2 = new StreamWriter(documentStream2))
                        {
                            writer2.WriteLine(document2);
                            writer2.Flush();
                            documentStream2.Position = 0;

                            using (StreamContent documentContent2 = new StreamContent(documentStream2))
                            {

                                uri = new Uri(this.baseUri, retrievedCollection2.SelfLink + "docs");

                                using (message = client.PostAsync(uri, documentContent2).Result)
                                {
                                    using (var ms = new MemoryStream())
                                    {
                                        await message.Content.CopyToAsync(ms);
                                        string s = Encoding.UTF8.GetString(ms.ToArray());
                                    }

                                    Assert.IsTrue(message.IsSuccessStatusCode, "Doc#2 create failed");

                                    // For session reads, there had better be a session token.
                                    Assert.IsTrue(message.Headers.Contains(HttpConstants.HttpHeaders.SessionToken));

                                    retrievedDocument2 = message.ToResourceAsync<Document>().Result;

                                }
                            }
                        }
                    }

                    String document3 = "{\"id\":\"user3\",\"_rid\":200, \"a\":4}";
                    headers = new StringKeyValueCollection();
                    client.AddMasterAuthorizationHeader(
                        "post",
                        retrievedCollection2.ResourceId,
                        "docs",
                        headers,
                        this.masterKey);
                    client.DefaultRequestHeaders.Remove(HttpConstants.HttpHeaders.PartitionKey);
                    client.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.PartitionKey, "[4]");


                    Document retrievedDocument3 = null;

                    using (MemoryStream documentStream3 = new MemoryStream())
                    {
                        using (StreamWriter writer3 = new StreamWriter(documentStream3))
                        {
                            writer3.WriteLine(document3);
                            writer3.Flush();
                            documentStream3.Position = 0;

                            using (StreamContent documentContent3 = new StreamContent(documentStream3))
                            {

                                uri = new Uri(this.baseUri, retrievedCollection2.SelfLink + "docs");

                                using (message = client.PostAsync(uri, documentContent3).Result)
                                {

                                    Assert.IsTrue(message.IsSuccessStatusCode, "Doc#3 create failed");

                                    // For session reads, there had better be a session token.
                                    Assert.IsTrue(message.Headers.Contains(HttpConstants.HttpHeaders.SessionToken));

                                    retrievedDocument3 = message.ToResourceAsync<Document>().Result;

                                }
                            }
                        }
                    }

                    // add the high version to validate the session check is ignored
                    client.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.SessionToken, "0:999999");
                    client.DefaultRequestHeaders.Remove(HttpConstants.HttpHeaders.Version);
                    client.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Version, HttpConstants.Versions.v2015_12_16);

                    Logger.LogLine("Reading document #1");
                    headers = new StringKeyValueCollection();
                    client.AddMasterAuthorizationHeader(
                        "get",
                        retrievedDocument1.ResourceId,
                        "docs",
                        headers,
                        this.masterKey);
                    client.DefaultRequestHeaders.Remove(HttpConstants.HttpHeaders.PartitionKey);
                    for (int i = 0; i < 100; i++)
                    {
                        Logger.LogLine("Reading document #1 {0} iteration", i);

                        uri = new Uri(this.baseUri, retrievedDocument1.SelfLink);
                        retrievedTask = client.GetAsync(uri);
                        using (message = retrievedTask.Result)
                        {

                            Assert.IsTrue(message.IsSuccessStatusCode, "Document #1 read failed");

                            // For session reads, there had better be a session token.
                            Assert.IsTrue(message.Headers.Contains(HttpConstants.HttpHeaders.SessionToken));

                            String receivedDocument1 = message.Content.ReadAsStringAsync().Result;

                            Assert.IsTrue(receivedDocument1.Length != 0, "Query failed to retrieve document #1");
                        }
                    }

                    Logger.LogLine("Reading document #2");
                    headers = new StringKeyValueCollection();
                    client.AddMasterAuthorizationHeader(
                        "get",
                        retrievedDocument2.ResourceId,
                        "docs",
                        headers,
                        this.masterKey);
                    client.DefaultRequestHeaders.Remove(HttpConstants.HttpHeaders.PartitionKey);
                    client.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.PartitionKey, "[1]");
                    uri = new Uri(this.baseUri, retrievedDocument2.SelfLink);
                    retrievedTask = client.GetAsync(uri);
                    using (message = retrievedTask.Result)
                    {
                        using (var ms = new MemoryStream())
                        {
                            await message.Content.CopyToAsync(ms);
                            string s = Encoding.UTF8.GetString(ms.ToArray());
                        }

                        Assert.IsTrue(message.IsSuccessStatusCode, "Document #2 read failed");

                        // For session reads, there had better be a session token.
                        Assert.IsTrue(message.Headers.Contains(HttpConstants.HttpHeaders.SessionToken));

                        String receivedDocument2 = message.Content.ReadAsStringAsync().Result;

                        Assert.IsTrue(receivedDocument2.Length != 0, "Query failed to retrieve the document");
                    }
                }
            }
            catch (Exception e)
            {
                Assert.Fail("Unexpected exception : " + e.ToString());
            }
        }

        [TestMethod]
        public void ValidateAPIVersionCheck()
        {
            Uri uri = new Uri(baseUri, new Uri("dbs", UriKind.Relative));

            // Positive test: create HttpClient with default params, which will
            // use add a proper version header to the request
            using (HttpClient client = CreateHttpClient(HttpConstants.Versions.CurrentVersion))
            {
                INameValueCollection headers = new StringKeyValueCollection();

                Logger.LogLine("Making request with valid API version");
                client.AddMasterAuthorizationHeader("get", "", "dbs", headers, this.masterKey);
                ICollection<CosmosDatabaseSettings> databaseCollection1 = client.ListAllAsync<CosmosDatabaseSettings>(uri).Result;
            }

            // Negative test: create HttpClient with invalid versions, which should
            // result in failed request.
            string[] invalidVersions = { "2000-01-01", "foobar", "", null };

            foreach (string version in invalidVersions)
            {
                using (HttpClient client = CreateHttpClient(version))
                {
                    INameValueCollection headers = new StringKeyValueCollection();
                    bool expectedException = false;

                    Logger.LogLine("Making request with invalid API version: " + version);
                    client.AddMasterAuthorizationHeader("get", "", "dbs", headers, this.masterKey);
                    try
                    {
                        ICollection<CosmosDatabaseSettings> databaseCollection1 = client.ListAllAsync<CosmosDatabaseSettings>(uri).Result;
                    }
                    catch (AggregateException e)
                    {
                        expectedException = true;
                        DocumentClientException de = e.InnerException as DocumentClientException;
                        Assert.IsNotNull(de, "Unexpected Exception");
                        Assert.AreEqual(HttpStatusCode.BadRequest.ToString(), de.Error.Code);
                    }

                    Assert.IsTrue(expectedException, "Expected exception not reached!");
                }
            }
        }

        [TestMethod]
        public void LoadBalancerProbeTest()
        {
            Uri uri = new Uri(baseUri, new Uri("probe", UriKind.Relative));

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // Plain HTTP GET should respond with 200
                    HttpResponseMessage response = client.GetAsync(uri).Result;

                    Assert.AreEqual(response.StatusCode, HttpStatusCode.OK);
                }
                catch (AggregateException e)
                {
                    Assert.Fail("Failed load balancer probe request. Exception {0}.", e.InnerException);
                }
            }
        }

        [TestMethod]
        public void ValidateContentType()
        {
            try
            {
                using (HttpClient client = CreateHttpClient(HttpConstants.Versions.CurrentVersion))
                {
                    Document retrievedDocument = null;
                    CreateItemsForContentType(client, out retrievedDocument);

                    ValidateContentTypeForAcceptTypes(client, retrievedDocument, null);
                    ValidateContentTypeForAcceptTypes(client, retrievedDocument, "application/json");
                    ValidateContentTypeForAcceptTypes(client, retrievedDocument, "*/*");
                    ValidateContentTypeForAcceptTypes(client, retrievedDocument, "application/*");
                    ValidateContentTypeForAcceptTypes(client, retrievedDocument, "text/html, application/json; q=0.1");
                    ValidateContentTypeForAcceptTypes(client, retrievedDocument, "text/html");
                    ValidateContentTypeForAcceptTypes(client, retrievedDocument, "application/**");
                    ValidateContentTypeForAcceptTypes(client, retrievedDocument, "*/json");
                    ValidateContentTypeForAcceptTypes(client, retrievedDocument, "abc/def");
                    ValidateContentTypeForAcceptTypes(client, retrievedDocument, "text/html, application/xml");
                }
            }
            catch (Exception e)
            {
                Logger.LogLine("Unexpected exception : " + GatewayTests.DumpFullExceptionMessage(e));
                throw;
            }
        }

        [TestMethod]
        public async Task ValidateUpdateCollectionIndexingPolicy_BadRequest()
        {
            DocumentClient client = TestCommon.CreateClient(true);

            try
            {
                await TestCommon.DeleteAllDatabasesAsync(client);

                string uniqDatabaseName = "ValidateUpdateCollectionIndexingPolicy_DB_" + Guid.NewGuid().ToString("N");
                CosmosDatabaseSettings database = await client.CreateDatabaseAsync(new CosmosDatabaseSettings { Id = uniqDatabaseName });

                string uniqCollectionName = "ValidateUpdateCollectionIndexingPolicy_COLL_" + Guid.NewGuid().ToString("N");
                CosmosContainerSettings collection = await client.CreateDocumentCollectionAsync(database, new CosmosContainerSettings { Id = uniqCollectionName });

                Logger.LogLine("Replace the collection with an invalid json object.");
                HttpResponseMessage response = await ReplaceDocumentCollectionAsync(collection, "I am not a valid json object");
                Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

                Logger.LogLine("Replace the collection with an invalid indexing mode.");
                response = await ReplaceDocumentCollectionAsync(collection,
                    string.Format("{{ \"id\": \"{0}\", \"indexingPolicy\": {{ \"indexingMode\": \"not a valid indexing mode\" }} }}", collection.Id));
                Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            }
            finally
            {
                TestCommon.DeleteAllDatabasesAsync(client).Wait();
            }
        }

        private async Task<HttpResponseMessage> ReplaceDocumentCollectionAsync(CosmosContainerSettings collection, string newCollectionContent)
        {
            using (HttpClient client = CreateHttpClient(HttpConstants.Versions.CurrentVersion))
            {
                INameValueCollection headers = new StringKeyValueCollection();
                client.AddMasterAuthorizationHeader("put", collection.ResourceId, "colls", headers, this.masterKey);
                Uri uri = new Uri(this.baseUri, collection.SelfLink);
                HttpContent httpContent = new StringContent(newCollectionContent);
                return await client.PutAsync(uri, httpContent);
            }
        }

        private void ValidateContentTypeForAcceptTypes(HttpClient client, Document retrievedDocument, string acceptTypes)
        {
            Logger.LogLine("Reading document for Accept Types '{0}'", acceptTypes);
            INameValueCollection headers = new StringKeyValueCollection();

            client.DefaultRequestHeaders.Remove(HttpConstants.HttpHeaders.Accept);

            if (acceptTypes != null)
            {
                client.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Accept, acceptTypes);
            }

            client.AddMasterAuthorizationHeader("get", retrievedDocument.ResourceId, "docs", headers, this.masterKey);
            Uri uri = new Uri(this.baseUri, retrievedDocument.SelfLink);
            var retrievedTask = client.GetAsync(uri);
            using (HttpResponseMessage message = retrievedTask.Result)
            {

                Assert.IsTrue(message.IsSuccessStatusCode, "Document read failed with status code {0}", message.StatusCode);

                String receivedDocument1 = message.Content.ReadAsStringAsync().Result;
                Assert.IsTrue(receivedDocument1.Length != 0, "Query failed to retrieve document");

                Assert.AreEqual("application/json", message.Content.Headers.ContentType.MediaType, "Document read returned unexpected content type");
            }
        }

        private void CreateItemsForContentType(HttpClient client, out Document retrievedDocument)
        {
            client.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.ConsistencyLevel, "Session");
            INameValueCollection headers;
            HttpResponseMessage message;

            string databaseName = Guid.NewGuid().ToString("N");
            CosmosDatabaseSettings database = new CosmosDatabaseSettings
            {
                Id = databaseName,
            };

            Logger.LogLine("Creating Database");
            headers = new StringKeyValueCollection();
            client.AddMasterAuthorizationHeader("post", "", "dbs", headers, this.masterKey);
            var retrievedTask = client.PostAsync(new Uri(this.baseUri, "dbs"), database.AsHttpContent());
            CosmosDatabaseSettings retrievedDatabase = retrievedTask.Result.ToResourceAsync<CosmosDatabaseSettings>().Result;

            string collectionName = "coll1";
            CosmosContainerSettings collection = new CosmosContainerSettings
            {
                Id = collectionName
            };

            Uri uri;

            Logger.LogLine("Creating collection");
            headers = new StringKeyValueCollection();
            client.AddMasterAuthorizationHeader("post", retrievedDatabase.ResourceId, "colls", headers, this.masterKey);
            uri = new Uri(this.baseUri, retrievedDatabase.SelfLink + "colls");

            CosmosContainerSettings retrievedCollection = null;
            using (message = client.PostAsync(uri, collection.AsHttpContent()).Result)
            {

                Assert.IsTrue(message.IsSuccessStatusCode, "Collection create failed");

                retrievedCollection = message.ToResourceAsync<CosmosContainerSettings>().Result;
            }

            Logger.LogLine("Creating document");

            String document = "{\"id\":\"user1\",\"_rid\":100}";
            headers = new StringKeyValueCollection();
            client.AddMasterAuthorizationHeader("post", retrievedCollection.ResourceId, "docs", headers, this.masterKey);

            using (MemoryStream documentStream = new MemoryStream())
            {
                using (StreamWriter writer = new StreamWriter(documentStream))
                {
                    writer.WriteLine(document);
                    writer.Flush();
                    documentStream.Position = 0;

                    using (StreamContent documentContent = new StreamContent(documentStream))
                    {

                        uri = new Uri(this.baseUri, retrievedCollection.SelfLink + "docs");
                        message = client.PostAsync(uri, documentContent).Result;

                        Assert.IsTrue(message.IsSuccessStatusCode, "Doc#1 create failed");

                        // For session reads, there had better be a session token.
                        Assert.IsTrue(message.Headers.Contains(HttpConstants.HttpHeaders.SessionToken));

                        retrievedDocument = message.ToResourceAsync<Document>().Result;
                    }
                }
            }
        }

        [TestMethod]
        public async Task TestBadPartitionKeyDefinition()
        {
            using (HttpClient client = CreateHttpClient(HttpConstants.Versions.CurrentVersion))
            {
                client.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.ConsistencyLevel, "Session");
                INameValueCollection headers;
                HttpResponseMessage message;

                string databaseName = Guid.NewGuid().ToString("N");
                CosmosDatabaseSettings database = new CosmosDatabaseSettings
                {
                    Id = databaseName,
                };

                Logger.LogLine("Creating Database");
                headers = new StringKeyValueCollection();
                client.AddMasterAuthorizationHeader("post", "", "dbs", headers, this.masterKey);
                var response = await client.PostAsync(new Uri(this.baseUri, new Uri("dbs", UriKind.Relative)), database.AsHttpContent());
                CosmosDatabaseSettings retrievedDatabase = await response.ToResourceAsync<CosmosDatabaseSettings>();

                Uri uri;

                Logger.LogLine("Creating collection");
                headers = new StringKeyValueCollection();
                client.AddMasterAuthorizationHeader("post", retrievedDatabase.ResourceId, "colls", headers, this.masterKey);
                headers[HttpConstants.HttpHeaders.OfferThroughput] = Convert.ToString(6000, CultureInfo.InvariantCulture);
                uri = new Uri(this.baseUri, new Uri(retrievedDatabase.SelfLink + "colls", UriKind.Relative));

                using (message = await client.PostAsync(uri, new StringContent("{\"id\":\"coll1\",\"partitionKey\":{\"paths\":[\"/id\"],\"kind\":\"Hash\"}}")))
                {
                    Assert.AreEqual(HttpStatusCode.Created, message.StatusCode);
                }

                using (message = await client.PostAsync(uri, new StringContent("{\"id\":\"coll1\",\"partitionKey\":{\"paths\":\"/id\",\"kind\":\"Hash\"}}")))
                {
                    Assert.AreEqual(HttpStatusCode.BadRequest, message.StatusCode);
                }
            }
        }

        [TestMethod]
        public async Task TestBadQueryHeaders()
        {
            DocumentClient client = TestCommon.CreateClient(true);

            try
            {
                await TestCommon.DeleteAllDatabasesAsync(client);

                string uniqDatabaseName = "DB_" + Guid.NewGuid().ToString("N");
                CosmosDatabaseSettings database = await client.CreateDatabaseAsync(new CosmosDatabaseSettings { Id = uniqDatabaseName });

                string uniqCollectionName = "COLL_" + Guid.NewGuid().ToString("N");
                CosmosContainerSettings collection = await client.CreateDocumentCollectionAsync(
                    database.SelfLink,
                    new CosmosContainerSettings
                    {
                        Id = uniqCollectionName,
                        PartitionKey = new PartitionKeyDefinition
                        {
                            Paths = new Collection<string> { "/key" }
                        }
                    },
                    new RequestOptions { OfferThroughput = 10000 });

                var uri = new Uri(this.baseUri, new Uri(collection.SelfLink + "docs", UriKind.Relative));
                SqlQuerySpec querySpec = new SqlQuerySpec("SELECT * FROM r");

                foreach (string header in new[] { HttpConstants.HttpHeaders.EnableCrossPartitionQuery, HttpConstants.HttpHeaders.ParallelizeCrossPartitionQuery })
                {
                    using (HttpClient httpClient = CreateHttpClient(HttpConstants.Versions.CurrentVersion))
                    {
                        var headers = new StringKeyValueCollection();
                        httpClient.AddMasterAuthorizationHeader("post", collection.ResourceId, "docs", headers, this.masterKey);
                        httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.IsQuery, bool.TrueString);
                        httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.EnableScanInQuery, bool.TrueString);
                        httpClient.DefaultRequestHeaders.Add(header, "bad");

                        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, uri);
                        var stringContent = new StringContent(JsonConvert.SerializeObject(querySpec), Encoding.UTF8, "application/query+json");
                        stringContent.Headers.ContentType.CharSet = null;
                        using (HttpResponseMessage message = await httpClient.PostAsync(uri, stringContent))
                        {
                            Assert.AreEqual(HttpStatusCode.BadRequest, message.StatusCode);
                            string responseContent = await message.Content.ReadAsStringAsync();
                            dynamic json = JsonConvert.DeserializeObject<dynamic>(responseContent);
                            string errorMessage = (string)json.message;
                            string expectedErrorMessage = string.Format(CultureInfo.InvariantCulture, RMResources.InvalidHeaderValue, "bad", header);
                            Assert.IsTrue(errorMessage.StartsWith(expectedErrorMessage));
                        }
                    }
                }
            }
            finally
            {
                TestCommon.DeleteAllDatabasesAsync(client).Wait();
            }
        }

        [TestMethod]
        public async Task TestPartitionedQueryExecutionInfoInDocumentClientExceptionWithIsContinuationExpectedHeader()
        {
            DocumentClient client = TestCommon.CreateClient(true);

            try
            {
                await TestCommon.DeleteAllDatabasesAsync(client);

                string uniqDatabaseName = "DB_" + Guid.NewGuid().ToString("N");
                CosmosDatabaseSettings database = await client.CreateDatabaseAsync(new CosmosDatabaseSettings { Id = uniqDatabaseName });

                string uniqCollectionName = "COLL_" + Guid.NewGuid().ToString("N");
                CosmosContainerSettings collection = await client.CreateDocumentCollectionAsync(
                    database.SelfLink,
                    new CosmosContainerSettings
                    {
                        Id = uniqCollectionName,
                        PartitionKey = new PartitionKeyDefinition
                        {
                            Paths = new Collection<string> { "/key" }
                        }
                    },
                    new RequestOptions { OfferThroughput = 10000 });

                string uniqSinglePartitionCollectionName = "COLL_" + Guid.NewGuid().ToString("N");
                CosmosContainerSettings singlePartitionCollection = await client.CreateDocumentCollectionAsync(
                    database.SelfLink,
                    new CosmosContainerSettings
                    {
                        Id = uniqSinglePartitionCollectionName,
                    },
                    new RequestOptions { OfferThroughput = 10000 });

                var partitionedCollectionUri = new Uri(this.baseUri, new Uri(collection.SelfLink + "docs", UriKind.Relative));

                var singlePartitionCollectionUri = new Uri(this.baseUri, new Uri(singlePartitionCollection.SelfLink + "docs", UriKind.Relative));

                SqlQuerySpec querySpec = new SqlQuerySpec
                {
                    QueryText = string.Format(CultureInfo.InvariantCulture, "SELECT VALUE AVG(r) FROM r WHERE r.key = {0}", 1)
                };

                PartitionedQueryExecutionInfo partitionedQueryExecutionInfo = new PartitionedQueryExecutionInfo
                {
                    QueryInfo = new QueryInfo
                    {
                        Top = null,
                        OrderBy = new SortOrder[] { },
                        OrderByExpressions = new string[] { },
                        Aggregates = new AggregateOperator[] { AggregateOperator.Average },
                        RewrittenQuery = string.Format(CultureInfo.InvariantCulture, "SELECT VALUE [{{\"item\": {{\"sum\": SUM(r), \"count\": COUNT(r)}}}}]\nFROM r\nWHERE (r.key = {0})", 1),
                    },
                    QueryRanges = new List<Range<string>>()
                    {
                        Range<string>.GetPointRange(PartitionKeyInternal.FromObjectArray(new object[] { 1 }, true).GetEffectivePartitionKeyString(collection.PartitionKey)),
                    },
                };

                PartitionedQueryExecutionInfo partitionedQueryExecutionInfo2 = new PartitionedQueryExecutionInfo
                {
                    QueryInfo = new QueryInfo
                    {
                        Top = null,
                        OrderBy = new SortOrder[] { },
                        OrderByExpressions = new string[] { },
                        Aggregates = new AggregateOperator[] { AggregateOperator.Average },
                        RewrittenQuery = string.Format(CultureInfo.InvariantCulture, "SELECT VALUE [{{\"item\": {{\"sum\": SUM(r), \"count\": COUNT(r)}}}}]\nFROM r\nWHERE (r.key = {0})", 1),
                    },
                    QueryRanges = new List<Range<string>>()
                    {
                        new Range<string>(PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey, PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey, true, false)
                    },
                };

                SqlQuerySpec querySpec2 = new SqlQuerySpec
                {
                    QueryText = string.Format(CultureInfo.InvariantCulture, "SELECT AVG(r) FROM r WHERE r.key = {0}", 1)
                };

                foreach (var queryTuple in new[] {
                    Tuple.Create(querySpec, true, partitionedQueryExecutionInfo, partitionedQueryExecutionInfo2),
                    Tuple.Create(querySpec2, false, (PartitionedQueryExecutionInfo)null, (PartitionedQueryExecutionInfo)null),
                })
                {
                    foreach (var collectionTuple in new[] {
                        Tuple.Create(collection, partitionedCollectionUri, true),
                        Tuple.Create(singlePartitionCollection, singlePartitionCollectionUri, false),
                    })
                    {
                        foreach (var versionTuple in new[] {
                            Tuple.Create(HttpConstants.Versions.v2016_07_11, false),
                            Tuple.Create(HttpConstants.Versions.v2016_11_14, true),
                        })
                        {
                            SqlQuerySpec query = queryTuple.Item1;
                            bool canBeAccumulated = queryTuple.Item2;
                            PartitionedQueryExecutionInfo queryInfoForPartitionedCollection = queryTuple.Item3;
                            PartitionedQueryExecutionInfo queryInfoForSinglePartitionCollection = queryTuple.Item4;

                            CosmosContainerSettings currentCollection = collectionTuple.Item1;
                            Uri uri = collectionTuple.Item2;
                            bool isPartitionedCollectionUri = collectionTuple.Item3;

                            string version = versionTuple.Item1;
                            bool isAggregateSupportedVersion = versionTuple.Item2;

                            foreach (bool isContinuationExpected in new[] { true, false })
                            {
                                using (HttpClient httpClient = CreateHttpClient(version))
                                {
                                    var headers = new StringKeyValueCollection();
                                    httpClient.AddMasterAuthorizationHeader("post", currentCollection.ResourceId, "docs", headers, this.masterKey);
                                    httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.IsQuery, bool.TrueString);
                                    httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.EnableScanInQuery, bool.TrueString);
                                    httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.EnableCrossPartitionQuery, bool.TrueString);
                                    httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.ParallelizeCrossPartitionQuery, bool.FalseString);
                                    httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.IsContinuationExpected, isContinuationExpected.ToString());

                                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, uri);
                                    var stringContent = new StringContent(JsonConvert.SerializeObject(query), Encoding.UTF8, "application/query+json");
                                    stringContent.Headers.ContentType.CharSet = null;
                                    using (HttpResponseMessage message = await httpClient.PostAsync(uri, stringContent))
                                    {
                                        if (!isContinuationExpected)
                                        {
                                            Assert.AreEqual(HttpStatusCode.BadRequest, message.StatusCode);

                                            string responseContent = await message.Content.ReadAsStringAsync();
                                            dynamic json = JsonConvert.DeserializeObject<dynamic>(responseContent);
                                            if (isAggregateSupportedVersion && canBeAccumulated)
                                            {
                                                IEnumerable<string> subStatusValues;
                                                Assert.AreEqual(true, message.Headers.TryGetValues(WFConstants.BackendHeaders.SubStatus, out subStatusValues));
                                                Assert.AreEqual((int)SubStatusCodes.CrossPartitionQueryNotServable, int.Parse(subStatusValues.Single(), CultureInfo.InvariantCulture));

                                                Assert.IsTrue(((string)json.message).StartsWith(RMResources.UnsupportedQueryWithFullResultAggregate));

                                                PartitionedQueryExecutionInfo responseQueryInfo = (PartitionedQueryExecutionInfo) JsonConvert.DeserializeObject<PartitionedQueryExecutionInfo>(json.additionalErrorInfo.ToString());
                                                if (isPartitionedCollectionUri)
                                                {
                                                    Assert.AreEqual(JsonConvert.SerializeObject(queryInfoForPartitionedCollection), JsonConvert.SerializeObject(responseQueryInfo));
                                                }
                                                else
                                                {
                                                    Assert.AreEqual(JsonConvert.SerializeObject(queryInfoForSinglePartitionCollection), JsonConvert.SerializeObject(responseQueryInfo));
                                                }
                                            }

                                            System.Diagnostics.Trace.TraceInformation(message.ToString());
                                            System.Diagnostics.Trace.TraceInformation(responseContent);
                                        }
                                        else
                                        {
                                            Assert.AreEqual(HttpStatusCode.OK, message.StatusCode);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                TestCommon.DeleteAllDatabasesAsync(client).Wait();
            }
        }

        [TestMethod]
        public async Task TestPartitionedQueryExecutionInfoInDocumentClientException()
        {
            /* Sample response:

               StatusCode: 400, ReasonPhrase: 'BadRequest', Version: 1.1, Content: System.Net.Http.StreamContent, Headers:
			   {
			     Transfer-Encoding: chunked
			     x-ms-activity-id: d4a17a7a-1f67-4a75-b37f-59f539842b26
			     x-ms-substatus: 1004
			     Strict-Transport-Security: max-age=31536000
			     x-ms-gatewayversion: version=1.9.0.0
			     Date: Wed, 29 Jun 2016 00:02:18 GMT
			     Server: Microsoft-HTTPAPI/2.0
			     Content-Type: application/json
			   }
			   {"code":"BadRequest","message":"Cross partition query with TOP and/or ORDER BY is not supported.\r\nActivityId: d4a17a7a-1f67-4a75-b37f-59f539842b26","additionalErrorInfo":{"partitionedQueryExecutionInfoVersion":1,"queryInfo":{"top":null,"orderBy":["Ascending"],"rewrittenQuery":"SELECT [{\"item\": r.key}] AS orderByItems, r AS payload\nFROM r\nWHERE (r.key IN (0, 1))\nORDER BY r.key"},"queryRanges":[{"min":"05C1BF6DA11560058000","max":"05C1BF6DA11560058000","isMinInclusive":true,"isMaxInclusive":true},{"min":"05C1ED172B6B2605BFF0","max":"05C1ED172B6B2605BFF0","isMinInclusive":true,"isMaxInclusive":true}]}}
             */
            DocumentClient client = TestCommon.CreateClient(true);

            try
            {
                await TestCommon.DeleteAllDatabasesAsync(client);

                string uniqDatabaseName = "DB_" + Guid.NewGuid().ToString("N");
                CosmosDatabaseSettings database = await client.CreateDatabaseAsync(new CosmosDatabaseSettings { Id = uniqDatabaseName });

                string uniqCollectionName = "COLL_" + Guid.NewGuid().ToString("N");
                CosmosContainerSettings collection = await client.CreateDocumentCollectionAsync(
                    database.SelfLink,
                    new CosmosContainerSettings
                    {
                        Id = uniqCollectionName,
                        PartitionKey = new PartitionKeyDefinition
                        {
                            Paths = new Collection<string> { "/key" }
                        }
                    },
                    new RequestOptions { OfferThroughput = 10000 });

                string[] documents = new[]
                {
                    @"{""id"":""1"",""key"":1}",
                    @"{""id"":""2"",""key"":2}",
                    @"{""id"":""3"",""key"":3}",
                    @"{""id"":""4"",""key"":4}",
                    @"{""id"":""5"",""key"":5}",
                    @"{""id"":""6"",""key"":6}",
                    @"{""id"":""7"",""key"":7}",
                };

                foreach (var doc in documents)
                {
                    using (MemoryStream stream = new MemoryStream(UnicodeEncoding.UTF8.GetBytes(doc)))
                    {
                        await client.CreateDocumentAsync(collection.SelfLink, JsonSerializable.LoadFrom<Document>(stream));
                    }
                }

                IEnumerable<int> ints = Enumerable.Range(0, 100);
                var queryRanges = new List<Range<string>>();
                var uri = new Uri(this.baseUri, new Uri(collection.SelfLink + "docs", UriKind.Relative));
                foreach (int i in ints)
                {
                    queryRanges.Add(Range<string>.GetPointRange(PartitionKeyInternal.FromObjectArray(new object[] { i }, true).GetEffectivePartitionKeyString(collection.PartitionKey)));
                }
                queryRanges.Sort(Range<string>.MinComparer.Instance);

                SqlQuerySpec querySpec1 = new SqlQuerySpec
                {
                    QueryText = string.Format(CultureInfo.InvariantCulture, "SELECT * FROM r WHERE r.key IN ({0})", string.Join(",", ints))
                };

                PartitionedQueryExecutionInfo partitionedQueryExecutionInfo1 = new PartitionedQueryExecutionInfo
                {
                    QueryInfo = new QueryInfo
                    {
                        Top = null,
                        OrderBy = new SortOrder[] { },
                        OrderByExpressions = new string[] { },
                        Aggregates = new AggregateOperator[] { },
                        RewrittenQuery = string.Empty
                    },
                    QueryRanges = queryRanges,
                };

                SqlQuerySpec querySpec2 = new SqlQuerySpec
                {
                    QueryText = string.Format(CultureInfo.InvariantCulture, "SELECT * FROM r WHERE r.key IN ({0}) ORDER BY r.key", string.Join(",", ints))
                };

                PartitionedQueryExecutionInfo partitionedQueryExecutionInfo2 = new PartitionedQueryExecutionInfo
                {
                    QueryInfo = new QueryInfo
                    {
                        Top = null,
                        OrderBy = new SortOrder[] { SortOrder.Ascending },
                        OrderByExpressions = new[] { "r.key" },
                        Aggregates = new AggregateOperator[] { },
                        RewrittenQuery = string.Format(CultureInfo.InvariantCulture, "SELECT r._rid, [{{\"item\": r.key}}] AS orderByItems, r AS payload\nFROM r\nWHERE (r.key IN ({0}))\nORDER BY r.key", string.Join(", ", ints))
                    },
                    QueryRanges = queryRanges,
                };


                SqlQuerySpec querySpec3 = querySpec2;

                PartitionedQueryExecutionInfo partitionedQueryExecutionInfo3 = new PartitionedQueryExecutionInfo
                {
                    QueryInfo = new QueryInfo
                    {
                        Top = null,
                        OrderBy = new SortOrder[] { SortOrder.Ascending },
                        OrderByExpressions = new[] { "r.key" },
                        Aggregates = new AggregateOperator[] { },
                        RewrittenQuery = string.Format(CultureInfo.InvariantCulture, "SELECT r._rid, [{{\"item\": r.key}}] AS orderByItems, r AS payload\nFROM r\nWHERE ((r.key IN ({0})) AND ({{documentdb-formattableorderbyquery-filter}}))\nORDER BY r.key", string.Join(", ", ints)),
                    },
                    QueryRanges = queryRanges,
                };

                SqlQuerySpec querySpec4 = new SqlQuerySpec
                {
                    QueryText = string.Format(CultureInfo.InvariantCulture, "SELECT VALUE AVG(r) FROM r WHERE r.key IN ({0})", string.Join(",", ints))
                };

                PartitionedQueryExecutionInfo partitionedQueryExecutionInfo4 = new PartitionedQueryExecutionInfo
                {
                    QueryInfo = new QueryInfo
                    {
                        Top = null,
                        OrderBy = new SortOrder[] { },
                        OrderByExpressions = new string[] { },
                        Aggregates = new AggregateOperator[] { AggregateOperator.Average },
                        RewrittenQuery = string.Format(CultureInfo.InvariantCulture, "SELECT VALUE [{{\"item\": {{\"sum\": SUM(r), \"count\": COUNT(r)}}}}]\nFROM r\nWHERE (r.key IN ({0}))", string.Join(", ", ints)),
                    },
                    QueryRanges = queryRanges,
                };

                foreach (var query in new List<Tuple<SqlQuerySpec, bool, PartitionedQueryExecutionInfo, bool>>
                {
                    Tuple.Create(querySpec1, false, partitionedQueryExecutionInfo1, false),
                    Tuple.Create(querySpec1, false, partitionedQueryExecutionInfo1, true),
                    Tuple.Create(querySpec2, true, partitionedQueryExecutionInfo2, false),
                    Tuple.Create(querySpec2, true, partitionedQueryExecutionInfo3, true),
                    Tuple.Create(querySpec4, true, partitionedQueryExecutionInfo4, false),
                    Tuple.Create(querySpec4, true, partitionedQueryExecutionInfo4, true),
                })
                {
                    foreach (var parallelizeCrossPartitionQuery in new[] { false, true })
                    {
                        foreach (var version in new List<Tuple<string, bool>>
                {
                    Tuple.Create(HttpConstants.Versions.v2015_08_06, false),
                    Tuple.Create(HttpConstants.Versions.v2015_12_16, false),
                    Tuple.Create(HttpConstants.Versions.v2016_05_30, false),
                    Tuple.Create(HttpConstants.Versions.v2016_07_11, true),
                    Tuple.Create(HttpConstants.Versions.v2016_11_14, true),
                    Tuple.Create(HttpConstants.Versions.CurrentVersion, true),
                })
                        {
                            SqlQuerySpec querySpec = query.Item1;
                            bool queryHasTopOrderBy = query.Item2;
                            bool isVersionSupported = version.Item2;
                            bool isBadRequest = parallelizeCrossPartitionQuery || queryHasTopOrderBy;
                            bool queryHasAggregate = query.Item3.QueryInfo.HasAggregates;
                            bool isVersionSupportedForAggregate = VersionUtility.IsLaterThan(version.Item1, HttpConstants.Versions.v2016_11_14);
                            bool isAdditionalInfoExpected = isVersionSupported && isBadRequest && (!queryHasAggregate || isVersionSupportedForAggregate);

                            using (HttpClient httpClient = CreateHttpClient(version.Item1))
                            {
                                var headers = new StringKeyValueCollection();
                                httpClient.AddMasterAuthorizationHeader("post", collection.ResourceId, "docs", headers, this.masterKey);
                                httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.IsQuery, bool.TrueString);
                                httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.EnableScanInQuery, bool.TrueString);
                                httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.EnableCrossPartitionQuery, bool.TrueString);
                                httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.ParallelizeCrossPartitionQuery, parallelizeCrossPartitionQuery.ToString());

                                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, uri);
                                var stringContent = new StringContent(JsonConvert.SerializeObject(querySpec), Encoding.UTF8, "application/query+json");
                                stringContent.Headers.ContentType.CharSet = null;
                                using (HttpResponseMessage message = await httpClient.PostAsync(uri, stringContent))
                                {
                                    if (isBadRequest)
                                    {
                                        Assert.AreEqual(HttpStatusCode.BadRequest, message.StatusCode);
                                        string responseContent = await message.Content.ReadAsStringAsync();
                                        dynamic json = JsonConvert.DeserializeObject<dynamic>(responseContent);
                                        string errorMessage = (string)json.message;
                                        Assert.IsTrue(
                                            errorMessage.Contains(RMResources.UnsupportedCrossPartitionQuery) ||
                                            errorMessage.Contains(RMResources.UnsupportedCrossPartitionQueryWithAggregate) || 
                                            errorMessage.Contains("Cross partition query with TOP/ORDER BY or aggregate functions is not supported"),
                                            errorMessage);

                                        IEnumerable<string> subStatusValues;
                                        Assert.AreEqual(isAdditionalInfoExpected, message.Headers.TryGetValues(WFConstants.BackendHeaders.SubStatus, out subStatusValues));

                                        if (isAdditionalInfoExpected)
                                        {
                                            if (query.Item4 == VersionUtility.IsLaterThan(version.Item1, HttpConstants.Versions.v2016_11_14))
                                            {
                                                Assert.AreEqual((int)SubStatusCodes.CrossPartitionQueryNotServable, int.Parse(subStatusValues.Single(), CultureInfo.InvariantCulture));
                                                // TODO: DistinctType is not being returned in additionalErrorInfo
                                                // Assert.AreEqual(JsonConvert.SerializeObject(query.Item3), json.additionalErrorInfo.ToString());
                                            }
                                        }

                                        System.Diagnostics.Trace.TraceInformation(message.ToString());
                                        System.Diagnostics.Trace.TraceInformation(responseContent);
                                    }
                                    else
                                    {
                                        Assert.AreEqual(HttpStatusCode.OK, message.StatusCode);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                TestCommon.DeleteAllDatabasesAsync(client).Wait();
            }
        }

        [TestMethod]
        public async Task TestQueryRequestWithEmptyBody()
        {
            DocumentClient client = TestCommon.CreateClient(true);

            try
            {
                await TestCommon.DeleteAllDatabasesAsync(client);

                string uniqDatabaseName = "DB_" + Guid.NewGuid().ToString("N");
                CosmosDatabaseSettings database = await client.CreateDatabaseAsync(new CosmosDatabaseSettings { Id = uniqDatabaseName });

                string uniqCollectionName = "COLL_" + Guid.NewGuid().ToString("N");
                CosmosContainerSettings collection = await client.CreateDocumentCollectionAsync(
                    database.SelfLink,
                    new CosmosContainerSettings
                    {
                        Id = uniqCollectionName,
                        PartitionKey = new PartitionKeyDefinition
                        {
                            Paths = new Collection<string> { "/key" }
                        }
                    },
                    new RequestOptions { OfferThroughput = 10000 });

                var uri = new Uri(this.baseUri, new Uri(collection.SelfLink + "docs", UriKind.Relative));

                using (HttpClient httpClient = CreateHttpClient(HttpConstants.Versions.CurrentVersion))
                {
                    var headers = new StringKeyValueCollection();
                    httpClient.AddMasterAuthorizationHeader("post", collection.ResourceId, "docs", headers, this.masterKey);
                    httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.IsQuery, bool.TrueString);
                    httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.EnableScanInQuery, bool.TrueString);

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, uri);
                    var stringContent = new StringContent(string.Empty, Encoding.UTF8, "application/query+json");
                    stringContent.Headers.ContentType.CharSet = null;
                    using (HttpResponseMessage message = await httpClient.PostAsync(uri, stringContent))
                    {
                        Assert.AreEqual(
                            HttpStatusCode.BadRequest,
                            message.StatusCode,
                            string.Format(CultureInfo.InvariantCulture, "Unexpected status code. Message Content: {0}", await message.Content.ReadAsStringAsync()));
                    }
                }
            }
            finally
            {
                TestCommon.DeleteAllDatabasesAsync(client).Wait();
            }
        }

        [TestMethod]
        public async Task TestQueryWithDatePartitionKey()
        {
            DocumentClient client = TestCommon.CreateClient(true);

            try
            {
                await TestCommon.DeleteAllDatabasesAsync(client);

                string uniqDatabaseName = "DB_" + Guid.NewGuid().ToString("N");
                CosmosDatabaseSettings database = await client.CreateDatabaseAsync(new CosmosDatabaseSettings { Id = uniqDatabaseName });

                string uniqCollectionName = "COLL_" + Guid.NewGuid().ToString("N");
                CosmosContainerSettings collection = await client.CreateDocumentCollectionAsync(
                    database.SelfLink,
                    new CosmosContainerSettings
                    {
                        Id = uniqCollectionName,
                        PartitionKey = new PartitionKeyDefinition
                        {
                            Paths = new Collection<string> { "/key" }
                        }
                    },
                    new RequestOptions { OfferThroughput = 10000 });

                var uri = new Uri(this.baseUri, new Uri(collection.SelfLink + "docs", UriKind.Relative));
                SqlQuerySpec querySpec = new SqlQuerySpec(@"SELECT * FROM r WHERE r.key = '\/Date(1198908717056)\/'");

                using (HttpClient httpClient = CreateHttpClient(HttpConstants.Versions.CurrentVersion))
                {
                    var headers = new StringKeyValueCollection();
                    httpClient.AddMasterAuthorizationHeader("post", collection.ResourceId, "docs", headers, this.masterKey);
                    httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.IsQuery, bool.TrueString);
                    httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.EnableScanInQuery, bool.TrueString);

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, uri);
                    var stringContent = new StringContent(JsonConvert.SerializeObject(querySpec), Encoding.UTF8, "application/query+json");
                    stringContent.Headers.ContentType.CharSet = null;
                    using (HttpResponseMessage message = await httpClient.PostAsync(uri, stringContent))
                    {
                        Assert.AreEqual(
                            HttpStatusCode.OK,
                            message.StatusCode,
                            string.Format(CultureInfo.InvariantCulture, "Unexpected status code. Message Content: {0}", await message.Content.ReadAsStringAsync()));
                    }
                }
            }
            finally
            {
                TestCommon.DeleteAllDatabasesAsync(client).Wait();
            }
        }

    }
}
