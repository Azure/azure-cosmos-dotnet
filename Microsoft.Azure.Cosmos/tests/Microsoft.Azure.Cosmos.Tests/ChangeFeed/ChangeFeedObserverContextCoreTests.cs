﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class ChangeFeedObserverContextCoreTests
    {
        [TestMethod]
        public void ExposesResponseProperties()
        {
            string leaseToken = Guid.NewGuid().ToString();
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK);
            responseMessage.Headers.RequestCharge = 10;

            ChangeFeedObserverContextCore changeFeedObserverContextCore = new ChangeFeedObserverContextCore(leaseToken, responseMessage, Mock.Of<PartitionCheckpointer>());

            Assert.AreEqual(leaseToken, changeFeedObserverContextCore.LeaseToken);
            Assert.ReferenceEquals(responseMessage.Headers, changeFeedObserverContextCore.Headers);
            Assert.ReferenceEquals(responseMessage.Diagnostics, changeFeedObserverContextCore.Diagnostics);
        }

        [TestMethod]
        public async Task TryCheckpoint_OnSuccess()
        {
            string leaseToken = Guid.NewGuid().ToString();
            string continuation = Guid.NewGuid().ToString();
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK);
            responseMessage.Headers.ContinuationToken = continuation;
            Mock<PartitionCheckpointer> checkpointer = new Mock<PartitionCheckpointer>();
            checkpointer.Setup(c => c.CheckpointPartitionAsync(It.Is<string>(s => s == continuation))).Returns(Task.CompletedTask);
            ChangeFeedObserverContextCore changeFeedObserverContextCore = new ChangeFeedObserverContextCore(leaseToken, responseMessage, checkpointer.Object);

            (bool isSuccess, CosmosException exception) = await changeFeedObserverContextCore.TryCheckpointAsync();
            Assert.IsTrue(isSuccess);
            Assert.IsNull(exception);
        }

        [TestMethod]
        public async Task TryCheckpoint_OnLeaseLost()
        {
            string leaseToken = Guid.NewGuid().ToString();
            string continuation = Guid.NewGuid().ToString();
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK);
            responseMessage.Headers.ContinuationToken = continuation;
            Mock<PartitionCheckpointer> checkpointer = new Mock<PartitionCheckpointer>();
            checkpointer.Setup(c => c.CheckpointPartitionAsync(It.Is<string>(s => s == continuation))).ThrowsAsync(new LeaseLostException());
            ChangeFeedObserverContextCore changeFeedObserverContextCore = new ChangeFeedObserverContextCore(leaseToken, responseMessage, checkpointer.Object);

            (bool isSuccess, CosmosException exception) = await changeFeedObserverContextCore.TryCheckpointAsync();
            Assert.IsFalse(isSuccess);
            Assert.IsNotNull(exception);
            Assert.AreEqual(HttpStatusCode.PreconditionFailed, exception.StatusCode);
        }

        [TestMethod]
        public async Task TryCheckpoint_OnCosmosException()
        {
            CosmosException cosmosException = CosmosExceptionFactory.CreateThrottledException("throttled", new Headers());
            string leaseToken = Guid.NewGuid().ToString();
            string continuation = Guid.NewGuid().ToString();
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK);
            responseMessage.Headers.ContinuationToken = continuation;
            Mock<PartitionCheckpointer> checkpointer = new Mock<PartitionCheckpointer>();
            checkpointer.Setup(c => c.CheckpointPartitionAsync(It.Is<string>(s => s == continuation))).ThrowsAsync(cosmosException);
            ChangeFeedObserverContextCore changeFeedObserverContextCore = new ChangeFeedObserverContextCore(leaseToken, responseMessage, checkpointer.Object);

            (bool isSuccess, CosmosException exception) = await changeFeedObserverContextCore.TryCheckpointAsync();
            Assert.IsFalse(isSuccess);
            Assert.IsNotNull(exception);
            Assert.ReferenceEquals(cosmosException, exception);
        }

        [TestMethod]
        public async Task TryCheckpoint_OnUnknownException()
        {
            NotImplementedException cosmosException = new NotImplementedException();
            string leaseToken = Guid.NewGuid().ToString();
            string continuation = Guid.NewGuid().ToString();
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK);
            responseMessage.Headers.ContinuationToken = continuation;
            Mock<PartitionCheckpointer> checkpointer = new Mock<PartitionCheckpointer>();
            checkpointer.Setup(c => c.CheckpointPartitionAsync(It.Is<string>(s => s == continuation))).ThrowsAsync(cosmosException);
            ChangeFeedObserverContextCore changeFeedObserverContextCore = new ChangeFeedObserverContextCore(leaseToken, responseMessage, checkpointer.Object);

            (bool isSuccess, CosmosException exception) = await changeFeedObserverContextCore.TryCheckpointAsync();
            Assert.IsFalse(isSuccess);
            Assert.IsNotNull(exception);
            Assert.AreEqual(HttpStatusCode.InternalServerError, exception.StatusCode);
            Assert.ReferenceEquals(cosmosException, exception.InnerException);
        }
    }
}
