﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using global::Azure.Data.Cosmos;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    internal static class NonPartitionedContainerHelper
    {
        private static readonly string PreNonPartitionedMigrationApiVersion = "2018-08-31";
        private static readonly string utc_date = DateTime.UtcNow.ToString("r");

        internal static async Task<ContainerCore> CreateNonPartitionedContainer(
            global::Azure.Data.Cosmos.Database database,
            string containerId,
            string indexingPolicy = null)
        {
            DocumentCollection documentCollection = new DocumentCollection()
            {
                Id = containerId
            };

            if (indexingPolicy != null)
            {
                documentCollection.IndexingPolicy = JsonSerializer.Deserialize<Documents.IndexingPolicy>(indexingPolicy);
            }

            await NonPartitionedContainerHelper.CreateNonPartitionedContainer(
                database,
                documentCollection);

            return (ContainerCore)database.GetContainer(containerId);
        }

        internal static async Task<DocumentCollection> CreateNonPartitionedContainer(
            global::Azure.Data.Cosmos.Database database,
            DocumentCollection documentCollection)
        {
            (string endpoint, string authKey) accountInfo = TestCommon.GetAccountInfo();

            //Creating non partition Container, rest api used instead of .NET SDK api as it is not supported anymore.
            HttpClient client = new System.Net.Http.HttpClient();
            Uri baseUri = new Uri(accountInfo.endpoint);
            string verb = "POST";
            string resourceType = "colls";
            string resourceId = string.Format("dbs/{0}", database.Id);
            string resourceLink = string.Format("dbs/{0}/colls", database.Id);
            client.DefaultRequestHeaders.Add("x-ms-date", utc_date);
            client.DefaultRequestHeaders.Add("x-ms-version", NonPartitionedContainerHelper.PreNonPartitionedMigrationApiVersion);

            string authHeader = NonPartitionedContainerHelper.GenerateMasterKeyAuthorizationSignature(
                verb,
                resourceId,
                resourceType,
                accountInfo.authKey,
                "master",
                "1.0");

            client.DefaultRequestHeaders.Add("authorization", authHeader);
            string containerDefinition = documentCollection.ToString();
            StringContent containerContent = new StringContent(containerDefinition);
            Uri requestUri = new Uri(baseUri, resourceLink);

            DocumentCollection responseCollection = null;
            using (HttpResponseMessage response = await client.PostAsync(requestUri.ToString(), containerContent))
            {
                response.EnsureSuccessStatusCode();
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode, response.ToString());
                responseCollection = JsonSerializer.Deserialize<DocumentCollection>(await response.Content.ReadAsStringAsync());
            }

            return responseCollection;
        }

        internal static async Task CreateUndefinedPartitionItem(
            ContainerCore container,
            string itemId)
        {
            (string endpoint, string authKey) accountInfo = TestCommon.GetAccountInfo();
            //Creating undefined partition key  item, rest api used instead of .NET SDK api as it is not supported anymore.
            HttpClient client = new System.Net.Http.HttpClient();
            Uri baseUri = new Uri(accountInfo.endpoint);
            client.DefaultRequestHeaders.Add("x-ms-date", utc_date);
            client.DefaultRequestHeaders.Add("x-ms-version", NonPartitionedContainerHelper.PreNonPartitionedMigrationApiVersion);
            client.DefaultRequestHeaders.Add("x-ms-documentdb-partitionkey", "[{}]");

            //Creating undefined partition Container item.
            string verb = "POST";
            string resourceType = "docs";
            string resourceId = container.LinkUri.OriginalString;
            string resourceLink = string.Format("dbs/{0}/colls/{1}/docs", container.Database.Id, container.Id);
            string authHeader = NonPartitionedContainerHelper.GenerateMasterKeyAuthorizationSignature(
                verb,
                resourceId,
                resourceType,
                accountInfo.authKey,
                "master",
                "1.0");

            client.DefaultRequestHeaders.Remove("authorization");
            client.DefaultRequestHeaders.Add("authorization", authHeader);

            var payload = new { id = itemId, user = itemId };
            string itemDefinition = JsonSerializer.Serialize(payload);
            StringContent itemContent = new StringContent(itemDefinition);
            Uri requestUri = new Uri(baseUri, resourceLink);
            await client.PostAsync(requestUri.ToString(), itemContent);
        }

        internal static async Task CreateItemInNonPartitionedContainer(
            ContainerCore container,
            string itemId)
        {
            (string endpoint, string authKey) accountInfo = TestCommon.GetAccountInfo();
            //Creating non partition Container item.
            HttpClient client = new System.Net.Http.HttpClient();
            Uri baseUri = new Uri(accountInfo.endpoint);
            string verb = "POST";
            string resourceType = "docs";
            string resourceLink = string.Format("dbs/{0}/colls/{1}/docs", container.Database.Id, container.Id);
            string authHeader = NonPartitionedContainerHelper.GenerateMasterKeyAuthorizationSignature(
                verb, container.LinkUri.OriginalString,
                resourceType,
                accountInfo.authKey,
                "master",
                "1.0");

            client.DefaultRequestHeaders.Add("x-ms-date", utc_date);
            client.DefaultRequestHeaders.Add("x-ms-version", NonPartitionedContainerHelper.PreNonPartitionedMigrationApiVersion);
            client.DefaultRequestHeaders.Add("authorization", authHeader);

            string itemDefinition = JsonSerializer.Serialize(ToDoActivity.CreateRandomToDoActivity(id: itemId));
            {
                StringContent itemContent = new StringContent(itemDefinition);
                Uri requestUri = new Uri(baseUri, resourceLink);
                HttpResponseMessage response = await client.PostAsync(requestUri.ToString(), itemContent);
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode, response.ToString());
            }
        }

        private static string GenerateMasterKeyAuthorizationSignature(
            string verb,
            string resourceId,
            string resourceType,
            string key,
            string keyType,
            string tokenVersion)
        {
            System.Security.Cryptography.HMACSHA256 hmacSha256 = new System.Security.Cryptography.HMACSHA256 { Key = Convert.FromBase64String(key) };

            string payLoad = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}\n{1}\n{2}\n{3}\n{4}\n",
                    verb.ToLowerInvariant(),
                    resourceType.ToLowerInvariant(),
                    resourceId,
                    utc_date.ToLowerInvariant(),
                    ""
            );

            byte[] hashPayLoad = hmacSha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payLoad));
            string signature = Convert.ToBase64String(hashPayLoad);

            return System.Web.HttpUtility.UrlEncode(string.Format(System.Globalization.CultureInfo.InvariantCulture, "type={0}&ver={1}&sig={2}",
                keyType,
                tokenVersion,
                signature));
        }

    }
}
