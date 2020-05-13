﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlSelectItem : SqlObject
    {
        private SqlSelectItem(
           SqlScalarExpression expression,
           SqlIdentifier alias)
        {
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            this.Alias = alias;
        }

        public SqlScalarExpression Expression { get; }

        public SqlIdentifier Alias { get; }

        public static SqlSelectItem Create(
            SqlScalarExpression expression,
            SqlIdentifier alias = null) => new SqlSelectItem(expression, alias);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
