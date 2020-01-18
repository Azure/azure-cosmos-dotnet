﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements.Numbers
{
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    interface ICosmosNumberVisitor<TInput, TOutput>
    {
        TOutput Visit(CosmosFloat32 cosmosFloat32, TInput input);
        TOutput Visit(CosmosFloat64 cosmosFloat64, TInput input);
        TOutput Visit(CosmosInt16 cosmosInt16, TInput input);
        TOutput Visit(CosmosInt32 cosmosInt32, TInput input);
        TOutput Visit(CosmosInt64 cosmosInt64, TInput input);
        TOutput Visit(CosmosInt8 cosmosInt8, TInput input);
        TOutput Visit(CosmosNumber64 cosmosNumber64, TInput input);
        TOutput Visit(CosmosUInt32 cosmosUInt32, TInput input);
    }
}
