﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary>
    /// The DatabaseAccountLocation class represents an Azure Cosmos DB database account in a specific region.
    /// </summary>
    public class CosmosAccountLocation 
    {
        /// <summary>
        /// Gets the name of the database account location in the Azure Cosmos DB service. For example,
        /// "West US" as the name of the database account location in the West US region.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Name)]
        public string Name { get; set; }

        /// <summary>
        /// Gets the Url of the database account location in the Azure Cosmos DB service. For example,
        /// "https://contoso-WestUS.documents.azure.com:443/" as the Url of the 
        /// database account location in the West US region.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.DatabaseAccountEndpoint)]
        public string DatabaseAccountEndpoint { get; set; }
    }
}
