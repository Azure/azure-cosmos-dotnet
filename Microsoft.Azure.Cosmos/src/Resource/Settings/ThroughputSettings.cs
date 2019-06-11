﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Represents a throughput of the resources in the Azure Cosmos DB service.
    /// It is the standard pricing for the resource in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// It contains provisioned container throughput in measurement of Requests-per-Unit in the Azure Cosmos service.
    /// Refer to http://azure.microsoft.com/documentation/articles/documentdb-performance-levels/ for details on provision offer throughput.
    /// </remarks>
    /// <example>
    /// The example below fetch the ThroughputSettings for resource RID "testRID".
    /// <code language="c#">
    /// <![CDATA[ 
    /// add example here
    /// ]]>
    /// </code>
    /// </example>
    public class ThroughputSettings
    {
        /// <summary>
        /// Gets the entity tag associated with the resource throughput from the Azure Cosmos DB service.
        /// </summary>
        public string ETag { get; }

        /// <summary>
        /// Gets the last modified timestamp associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the offer rid.
        /// </summary>
        public string OfferRID { get; }

        /// <summary>
        /// Gets the resource rid.
        /// </summary>
        public string ResoruceRID { get; }
    }
}
