﻿namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.Query
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// A helper that forces the SDK to use the gateway or the service interop for the query plan
    /// </summary>
    internal class MockCosmosQueryClient : CosmosQueryClientCore
    {
        /// <summary>
        /// True it will use the gateway query plan.
        /// False it will use the service interop
        /// </summary>
        private readonly bool forceQueryPlanGatewayElseServiceInterop;

        public MockCosmosQueryClient(
            CosmosClientContext clientContext,
            ContainerCore cosmosContainerCore,
            bool forceQueryPlanGatewayElseServiceInterop) : base(
                clientContext,
                cosmosContainerCore)
        {
            this.forceQueryPlanGatewayElseServiceInterop = forceQueryPlanGatewayElseServiceInterop;
        }

        public int QueryPlanCalls { get; private set; }

        internal override bool ByPassQueryParsing()
        {
            return this.forceQueryPlanGatewayElseServiceInterop;
        }

        internal override Task<PartitionedQueryExecutionInfo> ExecuteQueryPlanRequestAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            SqlQuerySpec sqlQuerySpec,
            Cosmos.PartitionKey? partitionKey,
            string supportedQueryFeatures,
            CancellationToken cancellationToken)
        {
            this.QueryPlanCalls++;
            return base.ExecuteQueryPlanRequestAsync(
                resourceUri,
                resourceType,
                operationType,
                sqlQuerySpec,
                partitionKey,
                supportedQueryFeatures,
                cancellationToken);
        }

        internal override Task<QueryResponseCore> ExecuteItemQueryAsync<RequestOptionType>(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptionType requestOptions,
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            PartitionKeyRangeIdentity partitionKeyRange,
            bool isContinuationExpected,
            int pageSize,
            SchedulingStopwatch schedulingStopwatch,
            CancellationToken cancellationToken)
        {
            Assert.IsFalse(
                this.forceQueryPlanGatewayElseServiceInterop && this.QueryPlanCalls == 0,
                "Query Plan is force gateway mode, but no ExecuteQueryPlanRequestAsync have been called");
            return base.ExecuteItemQueryAsync(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                sqlQuerySpec: sqlQuerySpec,
                continuationToken: continuationToken,
                partitionKeyRange: partitionKeyRange,
                isContinuationExpected: isContinuationExpected,
                pageSize: pageSize,
                schedulingStopwatch: schedulingStopwatch,
                cancellationToken: cancellationToken);
        }
    }
}
