﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     ANTLR Version: 4.7.2
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

// Generated from C:\CosmosSQLANTLR\CosmosSqlAntlr\CosmosSqlAntlr\sql.g4 by ANTLR 4.7.2

// Unreachable code detected
#pragma warning disable 0162
// The variable '...' is assigned but its value is never used
#pragma warning disable 0219
// Missing XML comment for publicly visible type or member '...'
#pragma warning disable 1591
// Ambiguous reference in cref attribute
#pragma warning disable 419

namespace Microsoft.Azure.Cosmos.Query.Core.Parser
{
    using Antlr4.Runtime.Misc;
    using Antlr4.Runtime.Tree;

    /// <summary>
    /// This class provides an empty implementation of <see cref="IsqlVisitor{Result}"/>,
    /// which can be extended to create a visitor which only needs to handle a subset
    /// of the available methods.
    /// </summary>
    /// <typeparam name="Result">The return type of the visit operation.</typeparam>
    [System.CodeDom.Compiler.GeneratedCode("ANTLR", "4.7.2")]
    [System.CLSCompliant(false)]
#pragma warning disable CS3021 // Type or member does not need a CLSCompliant attribute because the assembly does not have a CLSCompliant attribute
    internal partial class sqlBaseVisitor<Result> : AbstractParseTreeVisitor<Result>, IsqlVisitor<Result>
#pragma warning restore CS3021 // Type or member does not need a CLSCompliant attribute because the assembly does not have a CLSCompliant attribute
    {
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.program"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitProgram([NotNull] AntlrSqlParser.ProgramContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.sql_query"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitSql_query([NotNull] AntlrSqlParser.Sql_queryContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.select_clause"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitSelect_clause([NotNull] AntlrSqlParser.Select_clauseContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.top_spec"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitTop_spec([NotNull] AntlrSqlParser.Top_specContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.selection"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitSelection([NotNull] AntlrSqlParser.SelectionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.select_star_spec"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitSelect_star_spec([NotNull] AntlrSqlParser.Select_star_specContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.select_value_spec"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitSelect_value_spec([NotNull] AntlrSqlParser.Select_value_specContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.select_list_spec"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitSelect_list_spec([NotNull] AntlrSqlParser.Select_list_specContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.select_item"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitSelect_item([NotNull] AntlrSqlParser.Select_itemContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.from_clause"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitFrom_clause([NotNull] AntlrSqlParser.From_clauseContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>JoinCollectionExpression</c>
        /// labeled alternative in <see cref="AntlrSqlParser.collection_expression"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitJoinCollectionExpression([NotNull] AntlrSqlParser.JoinCollectionExpressionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>AliasedCollectionExpression</c>
        /// labeled alternative in <see cref="AntlrSqlParser.collection_expression"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitAliasedCollectionExpression([NotNull] AntlrSqlParser.AliasedCollectionExpressionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>ArrayIteratorCollectionExpression</c>
        /// labeled alternative in <see cref="AntlrSqlParser.collection_expression"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitArrayIteratorCollectionExpression([NotNull] AntlrSqlParser.ArrayIteratorCollectionExpressionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>InputPathCollection</c>
        /// labeled alternative in <see cref="AntlrSqlParser.collection"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitInputPathCollection([NotNull] AntlrSqlParser.InputPathCollectionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>SubqueryCollection</c>
        /// labeled alternative in <see cref="AntlrSqlParser.collection"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitSubqueryCollection([NotNull] AntlrSqlParser.SubqueryCollectionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>StringPathExpression</c>
        /// labeled alternative in <see cref="AntlrSqlParser.path_expression"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitStringPathExpression([NotNull] AntlrSqlParser.StringPathExpressionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>EpsilonPathExpression</c>
        /// labeled alternative in <see cref="AntlrSqlParser.path_expression"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitEpsilonPathExpression([NotNull] AntlrSqlParser.EpsilonPathExpressionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>IdentifierPathExpression</c>
        /// labeled alternative in <see cref="AntlrSqlParser.path_expression"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitIdentifierPathExpression([NotNull] AntlrSqlParser.IdentifierPathExpressionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>NumberPathExpression</c>
        /// labeled alternative in <see cref="AntlrSqlParser.path_expression"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitNumberPathExpression([NotNull] AntlrSqlParser.NumberPathExpressionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.where_clause"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitWhere_clause([NotNull] AntlrSqlParser.Where_clauseContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.group_by_clause"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitGroup_by_clause([NotNull] AntlrSqlParser.Group_by_clauseContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.order_by_clause"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitOrder_by_clause([NotNull] AntlrSqlParser.Order_by_clauseContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.order_by_items"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitOrder_by_items([NotNull] AntlrSqlParser.Order_by_itemsContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.order_by_item"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitOrder_by_item([NotNull] AntlrSqlParser.Order_by_itemContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.sort_order"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitSort_order([NotNull] AntlrSqlParser.Sort_orderContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.offset_limit_clause"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitOffset_limit_clause([NotNull] AntlrSqlParser.Offset_limit_clauseContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.offset_count"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitOffset_count([NotNull] AntlrSqlParser.Offset_countContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.limit_count"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitLimit_count([NotNull] AntlrSqlParser.Limit_countContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>LiteralScalarExpression</c>
        /// labeled alternative in <see cref="AntlrSqlParser.scalar_expression"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitLiteralScalarExpression([NotNull] AntlrSqlParser.LiteralScalarExpressionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>BetweenScalarExpression</c>
        /// labeled alternative in <see cref="AntlrSqlParser.scalar_expression"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitBetweenScalarExpression([NotNull] AntlrSqlParser.BetweenScalarExpressionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>ObjectCreateScalarExpression</c>
        /// labeled alternative in <see cref="AntlrSqlParser.scalar_expression"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitObjectCreateScalarExpression([NotNull] AntlrSqlParser.ObjectCreateScalarExpressionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>InScalarExpression</c>
        /// labeled alternative in <see cref="AntlrSqlParser.scalar_expression"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitInScalarExpression([NotNull] AntlrSqlParser.InScalarExpressionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>ArrayCreateScalarExpression</c>
        /// labeled alternative in <see cref="AntlrSqlParser.scalar_expression"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitArrayCreateScalarExpression([NotNull] AntlrSqlParser.ArrayCreateScalarExpressionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>MemberIndexerScalarExpression</c>
        /// labeled alternative in <see cref="AntlrSqlParser.scalar_expression"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitMemberIndexerScalarExpression([NotNull] AntlrSqlParser.MemberIndexerScalarExpressionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>SubqueryScalarExpression</c>
        /// labeled alternative in <see cref="AntlrSqlParser.scalar_expression"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitSubqueryScalarExpression([NotNull] AntlrSqlParser.SubqueryScalarExpressionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>PropertyRefScalarExpressionBase</c>
        /// labeled alternative in <see cref="AntlrSqlParser.scalar_expression"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitPropertyRefScalarExpressionBase([NotNull] AntlrSqlParser.PropertyRefScalarExpressionBaseContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>CoalesceScalarExpression</c>
        /// labeled alternative in <see cref="AntlrSqlParser.scalar_expression"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitCoalesceScalarExpression([NotNull] AntlrSqlParser.CoalesceScalarExpressionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>ConditionalScalarExpression</c>
        /// labeled alternative in <see cref="AntlrSqlParser.scalar_expression"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitConditionalScalarExpression([NotNull] AntlrSqlParser.ConditionalScalarExpressionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>FunctionCallScalarExpression</c>
        /// labeled alternative in <see cref="AntlrSqlParser.scalar_expression"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitFunctionCallScalarExpression([NotNull] AntlrSqlParser.FunctionCallScalarExpressionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>ArrayScalarExpression</c>
        /// labeled alternative in <see cref="AntlrSqlParser.scalar_expression"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitArrayScalarExpression([NotNull] AntlrSqlParser.ArrayScalarExpressionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>ExistsScalarExpression</c>
        /// labeled alternative in <see cref="AntlrSqlParser.scalar_expression"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitExistsScalarExpression([NotNull] AntlrSqlParser.ExistsScalarExpressionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>UnaryScalarExpression</c>
        /// labeled alternative in <see cref="AntlrSqlParser.scalar_expression"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitUnaryScalarExpression([NotNull] AntlrSqlParser.UnaryScalarExpressionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>BinaryScalarExpression</c>
        /// labeled alternative in <see cref="AntlrSqlParser.scalar_expression"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitBinaryScalarExpression([NotNull] AntlrSqlParser.BinaryScalarExpressionContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by the <c>PropertyRefScalarExpressionRecursive</c>
        /// labeled alternative in <see cref="AntlrSqlParser.scalar_expression"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitPropertyRefScalarExpressionRecursive([NotNull] AntlrSqlParser.PropertyRefScalarExpressionRecursiveContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.scalar_expression_list"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitScalar_expression_list([NotNull] AntlrSqlParser.Scalar_expression_listContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.binary_operator"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitBinary_operator([NotNull] AntlrSqlParser.Binary_operatorContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.unary_operator"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitUnary_operator([NotNull] AntlrSqlParser.Unary_operatorContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.object_propertty_list"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitObject_propertty_list([NotNull] AntlrSqlParser.Object_propertty_listContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.object_property"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitObject_property([NotNull] AntlrSqlParser.Object_propertyContext context) { return VisitChildren(context); }
        /// <summary>
        /// Visit a parse tree produced by <see cref="AntlrSqlParser.literal"/>.
        /// <para>
        /// The default implementation returns the result of calling <see cref="AbstractParseTreeVisitor{Result}.VisitChildren(IRuleNode)"/>
        /// on <paramref name="context"/>.
        /// </para>
        /// </summary>
        /// <param name="context">The parse tree.</param>
        /// <return>The visitor result.</return>
        public virtual Result VisitLiteral([NotNull] AntlrSqlParser.LiteralContext context) { return VisitChildren(context); }
    }
}