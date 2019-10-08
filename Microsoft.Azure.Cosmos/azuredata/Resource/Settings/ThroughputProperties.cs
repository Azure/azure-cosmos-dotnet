﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Azure.Data.Cosmos
{
    using System;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents a throughput of the resources in the Azure Cosmos DB service.
    /// It is the standard pricing for the resource in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// It contains provisioned container throughput in measurement of request units per second in the Azure Cosmos service.
    /// Refer to http://azure.microsoft.com/documentation/articles/documentdb-performance-levels/ for details on provision offer throughput.
    /// </remarks>
    /// <example>
    /// The example below fetch the ThroughputProperties on testContainer.
    /// <code language="c#">
    /// <![CDATA[ 
    /// ThroughputProperties throughputProperties = await testContainer.ReadThroughputAsync().Resource;
    /// ]]>
    /// </code>
    /// </example>
    public class ThroughputProperties
    {
        /// <summary>
        /// Gets the entity tag associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The entity tag associated with the resource.
        /// </value>
        /// <remarks>
        /// ETags are used for concurrency checking when updating resources.
        /// </remarks>
        [JsonPropertyName(Constants.Properties.ETag)]
        public string ETag { get; /*private*/ set; }

        /// <summary>
        /// Gets the last modified time stamp associated with <see cref="DatabaseProperties" /> from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The last modified time stamp associated with the resource.</value>
        [JsonConverter(typeof(UnixDateTimeConverter))]
        [JsonPropertyName(Constants.Properties.LastModified)]
        public DateTime LastModified { get; /*private*/ set; }

        /// <summary>
        /// Gets the provisioned throughput for a resource in measurement of request units per second in the Azure Cosmos service.
        /// </summary>
        public int? Throughput
        {
            get => this.Content.OfferThroughput;
            private set => this.Content = new OfferContentV2(value.Value);
        }

        /// <summary>
        /// Gets the offer rid.
        /// </summary>
        [JsonPropertyName(Constants.Properties.RId)]
        /*internal*/ public string OfferRID { get; /*private*/ set; }

        /// <summary>
        /// Gets the resource rid.
        /// </summary>
        [JsonPropertyName(Constants.Properties.OfferResourceId)]
        /*internal*/ public string ResourceRID { get; /*private*/ set; }

        [JsonPropertyName("content")]
        private OfferContentV2 Content { get; set; }
    }
}
