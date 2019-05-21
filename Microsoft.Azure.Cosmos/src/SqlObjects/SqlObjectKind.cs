﻿//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal enum SqlObjectKind
    {
        AliasedCollectionExpression,
        ArrayCreateScalarExpression,
        ArrayIteratorCollectionExpression,
        ArrayScalarExpression,
        BetweenScalarExpression,
        BinaryScalarExpression,
        BooleanLiteral,
        CoalesceScalarExpression,
        ConditionalScalarExpression, // Used in Mongo
        ConversionScalarExpression,
        ExistsScalarExpression,
        FromClause,
        FunctionCallScalarExpression,
        GeoNearCallScalarExpression, // Used in Mongo
        GroupByClause,
        Identifier,
        IdentifierPathExpression,
        InScalarExpression,
        InputPathCollection,
        JoinCollectionExpression,
        LimitSpec,
        LiteralArrayCollection,
        LiteralScalarExpression,
        MemberIndexerScalarExpression,
        NullLiteral,
        NumberLiteral,
        NumberPathExpression,
        ObjectCreateScalarExpression,
        ObjectLiteral,
        ObjectProperty,
        OffsetLimitClause,
        OffsetSpec,
        OrderByClause,
        OrderByItem,
        Program,
        PropertyName,
        PropertyRefScalarExpression,
        Query,
        SelectClause,
        SelectItem,
        SelectListSpec,
        SelectStarSpec,
        SelectValueSpec,
        StringLiteral,
        StringPathExpression,
        SubqueryCollection,
        SubqueryCollectionExpression,
        SubqueryScalarExpression,
        TopSpec,
        UnaryScalarExpression,
        UndefinedLiteral,
        WhereClause,
    }
}
