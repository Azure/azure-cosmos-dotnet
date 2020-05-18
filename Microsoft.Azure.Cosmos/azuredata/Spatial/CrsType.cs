﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Spatial
{
    /// <summary>
    /// Type of Coordinate Reference System in the Azure Cosmos DB service.
    /// </summary>
    internal enum CrsType
    {
        /// <summary>
        /// Coordinate Reference System is specified by name.
        /// </summary>
        Named,

        /// <summary>
        /// Coordinate Reference System is specified by link.
        /// </summary>
        Linked,

        /// <summary>
        /// No Coordinate Reference System can be assumed for a geometry.
        /// </summary>
        Unspecified
    }
}
