﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// This class provides a way to configure basic
    /// serializer settings.
    /// </summary>
    public struct CosmosSerializationOptions
    {
        /// <summary>
        /// Gets or sets if the serializer should ignore null properties
        /// </summary>
        /// <remarks>
        /// The default value is false
        /// </remarks>
        public bool IgnoreNullValues { get; set; }

        /// <summary>
        /// Gets or sets if the serializer should use indentation
        /// </summary>
        /// <remarks>
        /// The default value is false
        /// </remarks>
        public bool Indented { get; set; }

        /// <summary>
        /// Gets or sets whether the naming policy used to convert a string-based name to another format,
        /// such as a camel-casing format.
        /// </summary>
        /// <remarks>
        /// The default value is CosmosPropertyNamingPolicy.Default
        /// </remarks>
        public CosmosPropertyNamingPolicy PropertyNamingPolicy { get; set; }
    }
}
