﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlOffsetLimitClause : SqlObject
    {
        private SqlOffsetLimitClause(SqlOffsetSpec offsetSpec, SqlLimitSpec limitSpec)
        {
            this.OffsetSpec = offsetSpec ?? throw new ArgumentNullException(nameof(offsetSpec));
            this.LimitSpec = limitSpec ?? throw new ArgumentNullException(nameof(limitSpec));
        }

        public SqlOffsetSpec OffsetSpec { get; }

        public SqlLimitSpec LimitSpec { get; }

        public static SqlOffsetLimitClause Create(
            SqlOffsetSpec offsetSpec,
            SqlLimitSpec limitSpec) => new SqlOffsetLimitClause(offsetSpec, limitSpec);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
