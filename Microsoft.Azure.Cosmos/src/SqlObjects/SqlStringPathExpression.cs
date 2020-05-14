﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlStringPathExpression : SqlPathExpression
    {
        private SqlStringPathExpression(SqlPathExpression parentPath, SqlStringLiteral value)
            : base(parentPath)
        {
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public SqlStringLiteral Value { get; }

        public static SqlStringPathExpression Create(
            SqlPathExpression parentPath,
            SqlStringLiteral value) => new SqlStringPathExpression(parentPath, value);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlPathExpressionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlPathExpressionVisitor<TResult> visitor) => visitor.Visit(this);
    }
}
