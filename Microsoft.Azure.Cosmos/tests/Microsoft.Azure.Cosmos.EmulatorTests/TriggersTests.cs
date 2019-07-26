﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class TriggersTests : BaseCosmosClientHelper
    {
        private ContainerCore container = null;
        private Scripts scripts = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/status";
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.container = (ContainerCore)response;
            this.scripts = this.container.Scripts;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task CRUDTest()
        {
            TriggerProperties settings = new TriggerProperties
            {
                Id = Guid.NewGuid().ToString(),
                Body = TriggersTests.GetTriggerFunction(".05"),
                TriggerOperation = Cosmos.Scripts.TriggerOperation.Create,
                TriggerType = Cosmos.Scripts.TriggerType.Pre
            };

            TriggerResponse triggerResponse =
                await this.scripts.CreateTriggerAsync(settings);
            double reqeustCharge = triggerResponse.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.Created, triggerResponse.StatusCode);
            TriggersTests.ValidateTriggerSettings(settings, triggerResponse);

            triggerResponse = await this.scripts.ReadTriggerAsync(settings.Id);
            reqeustCharge = triggerResponse.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.OK, triggerResponse.StatusCode);
            TriggersTests.ValidateTriggerSettings(settings, triggerResponse);

            TriggerProperties updatedSettings = triggerResponse.Resource;
            updatedSettings.Body = TriggersTests.GetTriggerFunction(".42");

            TriggerResponse replaceResponse = await this.scripts.ReplaceTriggerAsync(updatedSettings);
            TriggersTests.ValidateTriggerSettings(updatedSettings, replaceResponse);
            reqeustCharge = replaceResponse.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.OK, replaceResponse.StatusCode);

            replaceResponse = await this.scripts.DeleteTriggerAsync(updatedSettings.Id);
            reqeustCharge = replaceResponse.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.NoContent, replaceResponse.StatusCode);
        }

        [TestMethod]
        public async Task ValidateTriggersTest()
        {
            // Prevent failures if previous test did not clean up correctly 
            try
            {
                await this.scripts.DeleteTriggerAsync("addTax");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                //swallow
            }
            
            ToDoActivity item = new ToDoActivity()
            {
                id = Guid.NewGuid().ToString(),
                cost = 9001,
                description = "trigger_test_item",
                status = "Done",
                taskNum = 1
            };

            TriggerProperties cosmosTrigger = await this.scripts.CreateTriggerAsync(
                new TriggerProperties
                {
                    Id = "addTax",
                    Body = TriggersTests.GetTriggerFunction(".20"),
                    TriggerOperation = Cosmos.Scripts.TriggerOperation.All,
                    TriggerType = Cosmos.Scripts.TriggerType.Pre
                });

            ItemRequestOptions options = new ItemRequestOptions()
            {
                PreTriggers = new List<string>() { cosmosTrigger.Id },
            };

            ItemResponse<dynamic> createdItem = await this.container.CreateItemAsync<dynamic>(item, requestOptions: options);

            double itemTax = createdItem.Resource.tax;
            Assert.AreEqual(item.cost * .20, itemTax);
            // Delete existing user defined functions.
            await this.scripts.DeleteTriggerAsync("addTax");
        }

        [TestMethod]
        public async Task TriggersIteratorTest()
        {
            TriggerProperties cosmosTrigger = await CreateRandomTrigger();

            HashSet<string> settings = new HashSet<string>();
            FeedIterator<TriggerProperties> iter = this.scripts.GetTriggerQueryIterator<TriggerProperties>(); ;
            while (iter.HasMoreResults)
            {
                foreach (TriggerProperties storedProcedureSettingsEntry in await iter.ReadNextAsync())
                {
                    settings.Add(storedProcedureSettingsEntry.Id);
                }
            }

            Assert.IsTrue(settings.Contains(cosmosTrigger.Id), "The iterator did not return the user defined function definition.");

            // Delete existing user defined functions.
            await this.scripts.DeleteTriggerAsync(cosmosTrigger.Id);
        }

        private static string GetTriggerFunction(string taxPercentage)
        {
            return @"function AddTax() {
                var item = getContext().getRequest().getBody();

                // Validate/calculate the tax.
                item.tax = calculateTax(item.cost);
                
                // Insert auto-created field 'createdTime'.
                item.createdTime = new Date();

                // Update the request -- this is what is going to be inserted.
                getContext().getRequest().setBody(item);
                function calculateTax(amt) {
                    // Simple input validation.

                    return amt * " + taxPercentage + @";
                }
            }";
        }

        private static void ValidateTriggerSettings(TriggerProperties triggerSettings, TriggerResponse cosmosResponse)
        {
            TriggerProperties settings = cosmosResponse.Resource;
            Assert.AreEqual(triggerSettings.Body, settings.Body,
                "Trigger function do not match");
            Assert.AreEqual(triggerSettings.Id, settings.Id,
                "Trigger id do not match");
            Assert.IsTrue(cosmosResponse.RequestCharge > 0);
            Assert.IsNotNull(cosmosResponse.MaxResourceQuota);
            Assert.IsNotNull(cosmosResponse.CurrentResourceQuotaUsage);
        }

        private async Task<TriggerResponse> CreateRandomTrigger()
        {
            string id = Guid.NewGuid().ToString();
            string function = GetTriggerFunction(".05");

            TriggerProperties settings = new TriggerProperties
            {
                Id = id,
                Body = function,
                TriggerOperation = Cosmos.Scripts.TriggerOperation.Create,
                TriggerType = Cosmos.Scripts.TriggerType.Pre
            };

            //Create a user defined function 
            TriggerResponse createResponse = await this.scripts.CreateTriggerAsync(
                triggerProperties: settings,
                cancellationToken: this.cancellationToken);

            ValidateTriggerSettings(settings, createResponse);

            return createResponse;
        }
    }
}
