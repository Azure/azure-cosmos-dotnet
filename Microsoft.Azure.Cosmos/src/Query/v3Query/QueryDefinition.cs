﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Defines a Cosmos SQL query
    /// </summary>
    public class QueryDefinition
    {
        private IReadOnlyDictionary<string, object> parameters;

        private Dictionary<string, SqlParameter> SqlParameters { get; }

        /// <summary>
        /// Create a <see cref="QueryDefinition"/>
        /// </summary>
        /// <param name="query">A valid Cosmos SQL query "Select * from test t"</param>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// QueryDefinition query = new QueryDefinition(
        ///     "select * from t where t.Account = @account")
        ///     .WithParameter("@account", "12345");
        /// ]]>
        /// </code>
        /// </example>
        public QueryDefinition(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                throw new ArgumentNullException(nameof(query));
            }

            this.QueryText = query;
            this.SqlParameters = new Dictionary<string, SqlParameter>();
        }

        /// <summary>
        /// Gets the text of the Azure Cosmos DB SQL query.
        /// </summary>
        /// <value>The text of the SQL query.</value>
        public string QueryText { get; }

        internal QueryDefinition(SqlQuerySpec sqlQuery)
        {
            if (sqlQuery == null)
            {
                throw new ArgumentNullException(nameof(sqlQuery));
            }

            this.QueryText = sqlQuery.QueryText;
            this.SqlParameters = new Dictionary<string, SqlParameter>();
            foreach (SqlParameter sqlParameter in sqlQuery.Parameters)
            {
                this.SqlParameters.Add(sqlParameter.Name, sqlParameter);
            }
        }

        /// <summary>
        /// Add parameters to the SQL query
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="value">The value for the parameter.</param>
        /// <remarks>
        /// If the same name is added again it will replace the original value
        /// </remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// QueryDefinition query = new QueryDefinition(
        ///     "select * from t where t.Account = @account")
        ///     .WithParameter("@account", "12345");
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>An instance of <see cref="QueryDefinition"/>.</returns>
        public QueryDefinition WithParameter(string name, object value)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            this.SqlParameters[name] = new SqlParameter(name, value);
            return this;
        }

        /// <summary>
        /// Add parameters to the SQL query
        /// </summary>
        /// <param name="parameters">The parameters to add, in the form of a list of key-value pairs (can be a dictionary).</param>
        /// <remarks>
        /// If the same name is added again it will replace the original value
        /// </remarks>
        /// <returns>An instance of <see cref="QueryDefinition"/>.</returns>
        public QueryDefinition WithParameters(IEnumerable<KeyValuePair<string, object>> parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            foreach (KeyValuePair<string, object> keyValuePair in parameters)
            {
                this.SqlParameters[keyValuePair.Key] = new SqlParameter(keyValuePair.Key, keyValuePair.Value);
            }
            
            return this;
        }

        internal SqlQuerySpec ToSqlQuerySpec()
        {
            return new SqlQuerySpec(this.QueryText, new SqlParameterCollection(this.SqlParameters.Values));
        }

        /// <summary>
        /// Returns the query definition's parameters.
        /// </summary>
        /// <returns>A dictionary with the name and value of the parameters.</returns>
        public IReadOnlyDictionary<string, object> Parameters
        {
            get
            {
                return this.parameters ?? (this.parameters = new ParameterValuesDictionary(this.SqlParameters));
            }
        }

        private class ParameterValuesDictionary : IReadOnlyDictionary<string, object>
        {
            private Dictionary<string, SqlParameter> sqlParameters;
            public ParameterValuesDictionary(Dictionary<string, SqlParameter> sqlParameters)
            {
                this.sqlParameters = sqlParameters;
            }

            public object this[string key] => this.sqlParameters[key].Value;

            public IEnumerable<string> Keys => this.sqlParameters.Keys;

            public IEnumerable<object> Values => this.sqlParameters.Values.Select(p => p.Value);

            public int Count => this.sqlParameters.Count;

            public bool ContainsKey(string key) => this.sqlParameters.ContainsKey(key);

            public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
            {
                foreach (SqlParameter p in this.sqlParameters.Values)
                {
                    yield return new KeyValuePair<string, object>(p.Name, p.Value);
                }
            }

            public bool TryGetValue(string key, out object value)
            {
                if (this.sqlParameters.TryGetValue(key, out SqlParameter sqlParameter))
                {
                    value = sqlParameter.Value;
                    return true;
                }

                value = null;
                return false;
            }

            IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
    }
}
