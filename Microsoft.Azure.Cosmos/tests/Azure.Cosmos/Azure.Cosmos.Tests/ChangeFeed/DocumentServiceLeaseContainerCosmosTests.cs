﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Cosmos.Fluent;
    using Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class DocumentServiceLeaseContainerCosmosTests
    {
        private static DocumentServiceLeaseStoreManagerOptions leaseStoreManagerSettings = new DocumentServiceLeaseStoreManagerOptions()
        {
            ContainerNamePrefix = "prefix",
            HostName = "host"
        };

        private static List<DocumentServiceLeaseCore> allLeases = new List<DocumentServiceLeaseCore>()
        {
            new DocumentServiceLeaseCore()
            {
                LeaseId = "1",
                Owner = "someone"
            },
            new DocumentServiceLeaseCore()
            {
                LeaseId = "2",
                Owner = "host"
            }
        };

        [TestMethod]
        public async Task GetAllLeasesAsync_ReturnsAllLeaseDocuments()
        {
            DocumentServiceLeaseContainerCosmos documentServiceLeaseContainerCosmos = new DocumentServiceLeaseContainerCosmos(
                DocumentServiceLeaseContainerCosmosTests.GetMockedContainer(),
                DocumentServiceLeaseContainerCosmosTests.leaseStoreManagerSettings);

            IEnumerable<DocumentServiceLease> readLeases = await documentServiceLeaseContainerCosmos.GetAllLeasesAsync();
            CollectionAssert.AreEqual(DocumentServiceLeaseContainerCosmosTests.allLeases, readLeases.ToList());
        }

        [TestMethod]
        public async Task GetOwnedLeasesAsync_ReturnsOnlyMatched()
        {
            DocumentServiceLeaseContainerCosmos documentServiceLeaseContainerCosmos = new DocumentServiceLeaseContainerCosmos(
                DocumentServiceLeaseContainerCosmosTests.GetMockedContainer(),
                DocumentServiceLeaseContainerCosmosTests.leaseStoreManagerSettings);

            IEnumerable<DocumentServiceLease> readLeases = await documentServiceLeaseContainerCosmos.GetOwnedLeasesAsync();
            CollectionAssert.AreEqual(DocumentServiceLeaseContainerCosmosTests.allLeases.Where(l => l.Owner == DocumentServiceLeaseContainerCosmosTests.leaseStoreManagerSettings.HostName).ToList(), readLeases.ToList());
        }

        private static CosmosContainer GetMockedContainer(string containerName = "myColl")
        {
            Headers headers = new Headers();
            headers.ContinuationToken = string.Empty;

            MockAsyncPageable<DocumentServiceLeaseCore> asyncPageable = new MockAsyncPageable<DocumentServiceLeaseCore>(DocumentServiceLeaseContainerCosmosTests.allLeases);

            Mock<CosmosContainer> mockedItems = new Mock<CosmosContainer>();
            mockedItems.Setup(i => i.GetItemQueryIterator<DocumentServiceLeaseCore>(
                // To make sure the SQL Query gets correctly created
                It.Is<string>(value => string.Equals("SELECT * FROM c WHERE STARTSWITH(c.id, '" + DocumentServiceLeaseContainerCosmosTests.leaseStoreManagerSettings.GetPartitionLeasePrefix() + "')", value)),
                It.IsAny<string>(), 
                It.IsAny<QueryRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns(()=>
                {
                    return asyncPageable;
                });

            return mockedItems.Object;
        }
    }
}
