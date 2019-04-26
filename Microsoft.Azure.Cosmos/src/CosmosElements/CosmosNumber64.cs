﻿//-----------------------------------------------------------------------
// <copyright file="CosmosNumber.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using Microsoft.Azure.Cosmos.Json;

    internal abstract partial class CosmosNumber64 : CosmosNumber
    {
        protected CosmosNumber64()
            : base(CosmosNumberType.Number64)
        {
        }

        public static CosmosNumber Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new LazyCosmosNumber64(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosNumber Create(Number64 number)
        {
            return new EagerCosmosNumber64(number);
        }
    }
}
