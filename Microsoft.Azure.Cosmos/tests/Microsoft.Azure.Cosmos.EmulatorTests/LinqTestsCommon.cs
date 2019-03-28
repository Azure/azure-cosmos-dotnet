﻿//-----------------------------------------------------------------------
// <copyright file="LinqAttributeContractTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Services.Management.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml;
    using Microsoft.Azure.Cosmos.Services.Management.Tests.BaselineTest;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class LinqTestsCommon
    {
        /// <summary>
        /// Compare two list of anonymous objects
        /// </summary>
        /// <param name="queryResults"></param>
        /// <param name="dataResults"></param>
        /// <returns></returns>
        private static bool compareListOfAnonymousType(List<object> queryResults, List<dynamic> dataResults)
        {
            return queryResults.SequenceEqual(dataResults);
        }

        /// <summary>
        /// Compare 2 IEnumerable which may contain IEnumerable themselves.
        /// </summary>
        /// <param name="queryResults">The query results from Cosmos DB</param>
        /// <param name="dataResults">The query results from actual data</param>
        /// <returns>True if the two IEbumerable equal</returns>
        private static bool NestedListsSequenceEqual(IEnumerable queryResults, IEnumerable dataResults)
        {
            IEnumerator queryIter, dataIter;
            for (queryIter = queryResults.GetEnumerator(), dataIter = dataResults.GetEnumerator();
                queryIter.MoveNext() && dataIter.MoveNext(); )
            {
                IEnumerable queryEnumerable = queryIter.Current as IEnumerable;
                IEnumerable dataEnumerable = dataIter.Current as IEnumerable;
                if (queryEnumerable == null && dataEnumerable == null)
                {
                    if (!(queryIter.Current.Equals(dataIter.Current))) return false;

                }

                else if (queryEnumerable == null || dataEnumerable == null)
                {
                    return false;
                } 

                else
                {
                    if (!(LinqTestsCommon.NestedListsSequenceEqual(queryEnumerable, dataEnumerable))) return false;
                }
            }

            return (!(queryIter.MoveNext() || dataIter.MoveNext()));
        }

        /// <summary>
        /// Compare the list of results from CosmosDB query and the list of results from LinQ query on the original data
        /// Similar to Collections.SequenceEqual with the assumption that these lists are non-empty
        /// </summary>
        /// <param name="queryResults">A list representing the query restuls from CosmosDB</param>
        /// <param name="dataResults">A list representing the linQ query results from the original data</param>
        /// <returns>true if the two </returns>
        private static bool compareListOfArrays(List<object> queryResults, List<dynamic> dataResults)
        {
            if (NestedListsSequenceEqual(queryResults, dataResults)) return true;

            bool resultMatched = true;

            // dataResults contains type ConcatIterator whereas queryResults may contain IEnumerable
            // therefore it's simpler to just cast them into List<INumerable<object>> manually for simplify the verification
            var l1 = new List<List<object>>();
            foreach (IEnumerable list in dataResults)
            {
                var l = new List<object>();
                var iterator = list.GetEnumerator();
                while (iterator.MoveNext())
                {
                    l.Add(iterator.Current);
                }

                l1.Add(l);
            }

            var l2 = new List<List<object>>();
            foreach (IEnumerable list in queryResults)
            {
                var l = new List<object>();
                var iterator = list.GetEnumerator();
                while (iterator.MoveNext())
                {
                    l.Add(iterator.Current);
                }

                l2.Add(l);
            }

            foreach (IEnumerable<object> list in l1)
            {
                if (!l2.Any(a => a.SequenceEqual(list)))
                {
                    resultMatched = false;
                    return false;
                }
            }

            foreach (IEnumerable<object> list in l2)
            {
                if (!l1.Any(a => a.SequenceEqual(list)))
                {
                    resultMatched = false;
                    break;
                }
            }

            return resultMatched;
        }

        private static bool IsNumber(dynamic value)
        {
            return value is sbyte
                || value is byte
                || value is short
                || value is ushort
                || value is int
                || value is uint
                || value is long
                || value is ulong
                || value is float
                || value is double
                || value is decimal;
        }

        public static Boolean IsAnonymousType(Type type)
        {
            Boolean hasCompilerGeneratedAttribute = type.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Count() > 0;
            Boolean nameContainsAnonymousType = type.FullName.Contains("AnonymousType");
            Boolean isAnonymousType = hasCompilerGeneratedAttribute && nameContainsAnonymousType;

            return isAnonymousType;
        }

        /// <summary>
        /// Validate the results of CosmosDB query and the results of LinQ query on the original data
        /// Using Assert, will fail the unit test if the two results list are not SequenceEqual
        /// </summary>
        /// <param name="queryResults"></param>
        /// <param name="dataResults"></param>
        public static void ValidateResults(IQueryable queryResults, IQueryable dataResults)
        {
            // execution validation
            IEnumerator queryEnumerator = queryResults.GetEnumerator();
            List<object> queryResultsList = new List<object>();
            while (queryEnumerator.MoveNext()) 
            {
                queryResultsList.Add(queryEnumerator.Current);
            }

            List<dynamic> dataResultsList = dataResults.Cast<dynamic>().ToList();
            bool resultMatched = true;
            string actualStr = null;
            string expectedStr = null;
            if (dataResultsList.Count == 0 || queryResultsList.Count == 0)
            {
                resultMatched &= dataResultsList.Count == queryResultsList.Count;
            }
            else
            {
                dynamic firstElem = dataResultsList.FirstOrDefault();
                if (firstElem is IEnumerable)
                {
                    resultMatched &= compareListOfArrays(queryResultsList, dataResultsList);
                }
                else if (LinqTestsCommon.IsAnonymousType(firstElem.GetType()))
                {
                    resultMatched &= compareListOfAnonymousType(queryResultsList, dataResultsList);
                }
                else if (LinqTestsCommon.IsNumber(firstElem))
                {
                    const double Epsilon = 1E-6;
                    Type dataType = firstElem.GetType();
                    List<dynamic> dataSortedList = dataResultsList.OrderBy(x => x).ToList();
                    List<object> querySortedList = queryResultsList.OrderBy(x => x).ToList();
                    if (dataSortedList.Count != querySortedList.Count)
                    {
                        resultMatched = false;
                    }
                    else
                    {
                        for (int i = 0; i < dataSortedList.Count; ++i)
                        {
                            if (Math.Abs(dataSortedList[i] - (dynamic)querySortedList[i]) > (dynamic)Convert.ChangeType(Epsilon, dataType))
                            {
                                resultMatched = false;
                                break;
                            }
                        }
                    }

                    if (!resultMatched)
                    {
                        actualStr = JsonConvert.SerializeObject(querySortedList);
                        expectedStr = JsonConvert.SerializeObject(dataSortedList);
                    }
                }
                else
                {
                    var dataNotQuery = dataResultsList.Except(queryResultsList).ToList();
                    var queryNotData = queryResultsList.Except(dataResultsList).ToList();
                    resultMatched &= !dataNotQuery.Any() && !queryNotData.Any();
                }
            }

            string assertMsg = string.Empty;
            if (!resultMatched)
            {
                if (actualStr == null) actualStr = JsonConvert.SerializeObject(queryResultsList);

                if (expectedStr == null) expectedStr = JsonConvert.SerializeObject(dataResultsList);

                resultMatched |= actualStr.Equals(expectedStr);
                if (!resultMatched)
                {
                    assertMsg = $"Expected: {expectedStr}, Actual: {actualStr}, RandomSeed: {LinqTestInput.RandomSeed}";
                }
            }

            Assert.IsTrue(resultMatched, assertMsg);
        }

        /// <summary>
        /// Generate a random string containing alphabetical characters
        /// </summary>
        /// <param name="random"></param>
        /// <param name="length"></param>
        /// <returns>a random string</returns>
        public static string RandomString(Random random, int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz ";
            return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        /// <summary>
        /// Generate a random DateTime object from a DateTime,
        /// with the variance of the time span between the provided DateTime to the current time
        /// </summary>
        /// <param name="random"></param>
        /// <param name="midDateTime"></param>
        /// <returns></returns>
        public static DateTime RandomDateTime(Random random, DateTime midDateTime)
        {
            TimeSpan timeSpan = DateTime.Now - midDateTime;
            TimeSpan newSpan = new TimeSpan(0, random.Next(0, (int)timeSpan.TotalMinutes * 2) - (int)timeSpan.TotalMinutes, 0);
            DateTime newDate = midDateTime + newSpan;
            return newDate;
        }

        /// <summary>
        /// Generate test data for most LinQ tests
        /// </summary>
        /// <typeparam name="T">the object type</typeparam>
        /// <param name="func">the lamda to create an instance of test data</param>
        /// <param name="count">number of test data to be created</param>
        /// <param name="client">the DocumentClient that is used to create the data</param>
        /// <param name="collection">the target collection</param>
        /// <returns>a lambda that takes a boolean which indicate where the query should run against CosmosDB or against original data, and return a query results as IQueryable</returns>
        internal static Func<bool, IQueryable<T>> GenerateTestData<T>(Func<Random, T> func, int count, DocumentClient client, DocumentCollection collection)
        {
            List<T> data = new List<T>();
            int seed = DateTime.Now.Millisecond;
            Random random = new Random(seed);
            LinqTestInput.RandomSeed = seed;
            for (int i = 0; i < count; ++i)
            {
                data.Add(func(random));
            }

            foreach (T obj in data)
            {
                client.CreateDocumentAsync(collection, obj).Wait();
            }

            var feedOptions = new FeedOptions() { EnableScanInQuery = true };
            var query = client.CreateDocumentQuery<T>(collection, feedOptions);

            // To cover both query against backend and queries on the original data using LINQ nicely, 
            // the LINQ expression should be written once and they should be compiled and executed against the two sources.
            // That is done by using Func that take a boolean Func. The parameter of the Func indicate whether the Cosmos DB query 
            // or the data list should be used. When a test is executed, the compiled LINQ expression would pass different values
            // to this getQuery method.
            Func<bool, IQueryable<T>> getQuery = useQuery => useQuery ? query : data.AsQueryable();

            return getQuery;
        }

        internal static Func<bool, IQueryable<Family>> GenerateFamilyData(
            DocumentClient client,
            Database testDb,
            out DocumentCollection testCollection)
        {
            // The test collection should have range index on string properties
            // for the orderby tests
            var newCol = new DocumentCollection()
            {
                Id = Guid.NewGuid().ToString(),
                IndexingPolicy = new IndexingPolicy()
                {
                    IncludedPaths = new System.Collections.ObjectModel.Collection<IncludedPath>()
                    {
                        new IncludedPath()
                        {
                            Path = "/*",
                            Indexes = new System.Collections.ObjectModel.Collection<Index>()
                            {
                                Index.Range(DataType.Number, -1),
                                Index.Range(DataType.String, -1)
                            }
                        }
                    }
                }
            };

            testCollection = client.CreateDocumentCollectionAsync(testDb, newCol).Result;
            const int Records = 100;
            const int MaxNameLength = 100;
            const int MaxThingStringLength = 50;
            const int MaxChild = 5;
            const int MaxPets = MaxChild;
            const int MaxThings = MaxChild;
            const int MaxGrade = 101;
            Func<Random, Family> createDataObj = random =>
            {
                var obj = new Family();
                obj.FamilyId = random.NextDouble() < 0.05 ? "some id" : Guid.NewGuid().ToString();
                obj.IsRegistered = random.NextDouble() < 0.5;
                obj.NullableInt = random.NextDouble() < 0.5 ? (int?)random.Next() : null;
                obj.Int = random.NextDouble() < 0.5 ? 5 : random.Next();
                obj.Parents = new Parent[random.Next(2) + 1];
                for (int i = 0; i < obj.Parents.Length; ++i)
                {
                    obj.Parents[i] = new Parent()
                    {
                        FamilyName = LinqTestsCommon.RandomString(random, random.Next(MaxNameLength)),
                        GivenName = LinqTestsCommon.RandomString(random, random.Next(MaxNameLength))
                    };
                }

                obj.Children = new Child[random.Next(MaxChild)];
                for (int i = 0; i < obj.Children.Length; ++i)
                {
                    obj.Children[i] = new Child()
                    {
                        Gender = random.NextDouble() < 0.5 ? "male" : "female",
                        FamilyName = obj.Parents[random.Next(obj.Parents.Length)].FamilyName,
                        GivenName = LinqTestsCommon.RandomString(random, random.Next(MaxNameLength)),
                        Grade = random.Next(MaxGrade)
                    };

                    obj.Children[i].Pets = new List<Pet>();
                    for (int j = 0; j < random.Next(MaxPets); ++j)
                    {
                        obj.Children[i].Pets.Add(new Pet()
                        {
                            GivenName = random.NextDouble() < 0.5 ?
                                LinqTestsCommon.RandomString(random, random.Next(MaxNameLength)) :
                                "Fluffy"
                        });
                    }

                    obj.Children[i].Things = new Dictionary<string, string>();
                    for (int j = 0; j < random.Next(MaxThings) + 1; ++j)
                    {
                        obj.Children[i].Things.Add(
                            j == 0 ? "A" : $"{j}-{random.Next().ToString()}",
                            LinqTestsCommon.RandomString(random, random.Next(MaxThingStringLength)));
                    }
                }
                return obj;
            };

            Func<bool, IQueryable<Family>> getQuery = LinqTestsCommon.GenerateTestData(createDataObj, Records, client, testCollection);
            return getQuery;
        }

        internal static Func<bool, IQueryable<Data>> GenerateSimpleData(
            DocumentClient client,
            Database testDb,
            out DocumentCollection testCollection)
        {
            const int DocumentCount = 10;

            testCollection = client.CreateDocumentCollectionAsync(
                testDb.GetLink(),
                new DocumentCollection() { Id = Guid.NewGuid().ToString() }).Result;

            Random random = new Random();
            List<Data> testData = new List<Data>();
            for (int index = 0; index < DocumentCount; index++)
            {
                Data dataEntry = new Data()
                {
                    Number = random.Next(-10000, 10000),
                    Flag = index % 2 == 0 ? true : false,
                    Multiples = new int[] { index, index * 2, index * 3, index * 4 }
                };

                client.CreateDocumentAsync(testCollection.GetLink(), dataEntry).Wait();
                testData.Add(dataEntry);
            }

            FeedOptions feedOptions = new FeedOptions() { EnableScanInQuery = true, EnableCrossPartitionQuery = true };
            var query = client.CreateDocumentQuery<Data>(testCollection.GetLink()).AsQueryable();

            // To cover both query against backend and queries on the original data using LINQ nicely, 
            // the LINQ expression should be written once and they should be compiled and executed against the two sources.
            // That is done by using Func that take a boolean Func. The parameter of the Func indicate whether the Cosmos DB query 
            // or the data list should be used. When a test is executed, the compiled LINQ expression would pass different values
            // to this getQuery method.
            Func<bool, IQueryable<Data>> getQuery = useQuery => useQuery ? query : testData.AsQueryable();
            return getQuery;
        }

        public static LinqTestOutput ExecuteTest(LinqTestInput input)
        {
            var querySqlStr = string.Empty;
            try
            {
                var compiledQuery = input.expression.Compile();

                var queryResults = compiledQuery(true);
                querySqlStr = JObject.Parse(queryResults.ToString()).GetValue("query", StringComparison.Ordinal).ToString();

                // we skip unordered query because the LinQ results vs actual query results are non-deterministic
                if (!input.skipVerification)
                {
                    var dataResults = compiledQuery(false);
                    LinqTestsCommon.ValidateResults(queryResults, dataResults);
                }

                string errorMsg = null;
                if (input.errorMessage != null)
                {
                    errorMsg = $"Expecting error containing message [[{input.errorMessage}]]. Actual: <No error>";
                }

                return new LinqTestOutput(querySqlStr, errorMsg, errorMsg != null);
            }
            catch (Exception e)
            {
                while (!(e is DocumentClientException) && e.InnerException != null)
                {
                    e = e.InnerException;
                }

                string message = e.ToString();
                bool hasFailed = false;
                if (input.errorMessage != null && !message.Contains(input.errorMessage))
                {
                    message = $"Expecting error containing message [[{input.errorMessage}]]. Actual: [[{message}]]";
                    hasFailed = true;
                }
                else if (input.errorMessage == null)
                {
                    hasFailed = true;
                }

                return new LinqTestOutput(querySqlStr, message, hasFailed);
            }
        }
    }

    public static class ErrorMessages
    {
        public const string OrderByCorrelatedCollectionNotSupported = "Order-by over correlated collections is not supported.";
        public const string TopInSubqueryNotSupported = "'TOP' is not supported in subqueries.";
        public const string OrderByInSubqueryNotSuppported = "'ORDER BY' is not supported in subqueries.";
        public const string OrderbyItemExpressionCouldNotBeMapped = "ORDER BY item expression could not be mapped to a document path.";
        public const string CrossPartitionQueriesOnlySupportValueAggregateFunc = "Cross partition query only supports 'VALUE <AggreateFunc>' for aggregates.";
        public const string MemberIndexerNotSupported = "The specified query includes 'member indexer' which is currently not supported.";
    }

    /// <summary>
    /// A base class that determines equality based on its json representation
    /// </summary>
    public class LinqTestObject
    {
        private string json;

        public override string ToString()
        {
            // simple cached serialization
            if (this.json == null)
            {
                this.json = JsonConvert.SerializeObject(this);
            }
            return this.json;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is LinqTestObject && 
                obj.GetType().IsAssignableFrom(this.GetType()) &&
                this.GetType().IsAssignableFrom(obj.GetType()))) return false;
            if (obj == null) return false;

            return this.ToString().Equals(obj.ToString());
        }

        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }
    }

    public class LinqTestInput : BaselineTestInput
    {
        internal static Regex classNameRegex = new Regex("(value\\(.+?\\+)?\\<\\>.+?__([A-Za-z]+)((\\d+_\\d+(`\\d+\\[.+?\\])?\\)(\\.value)?)|\\d+`\\d+)");
        internal static Regex invokeCompileRegex = new Regex("Invoke\\([^.]+\\.[^.,]+(\\.Compile\\(\\))?, b\\)(\\.Cast\\(\\))?");

        // As the tests are executed sequentially
        // We can store the random seed in a static variable for diagnostics
        internal static int RandomSeed = -1;

        internal int randomSeed = -1;
        internal Expression<Func<bool, IQueryable>> expression { get; }
        internal string errorMessage;
        internal string expressionStr;

        // We skip the verification between Cosmos DB and actual query restuls in the following cases
        //     - unordered query since the results are not deterministics for LinQ results and actual query results
        //     - scenarios not supported in LINQ, e.g. sequence doesn't contain element.
        internal bool skipVerification;

        internal LinqTestInput(string description, Expression<Func<bool, IQueryable>> expr, string errorMsg = null, bool skipVerification = false, string expressionStr = null)
            : base(description)
        {
            if (expr == null)
            {
                throw new ArgumentNullException($"{nameof(expr)} must not be null.");
            }

            this.expression = expr;
            this.errorMessage = errorMsg;
            this.skipVerification = skipVerification;
            this.expressionStr = expressionStr;
        }

        public static string CleanUpInputExpression(string input)
        {
            var expressionSb = new StringBuilder(input);
            // simplify full qualified class name
            // e.g. before: value(Microsoft.Azure.Documents.Services.Management.Tests.LinqSQLTranslationTest+<>c__DisplayClass7_0), after: DisplayClass
            // before: <>f__AnonymousType14`2(, after: AnonymousType(
            // value(Microsoft.Azure.Documents.Services.Management.Tests.LinqProviderTests.LinqTranslationBaselineTests +<> c__DisplayClass24_0`1[System.String]).value
            var match = classNameRegex.Match(expressionSb.ToString());
            while (match.Success)
            {
                expressionSb = expressionSb.Replace(match.Groups[0].Value, match.Groups[2].Value);
                match = match.NextMatch();
            }

            // remove the Invoke().Compile() string from the Linq scanning tests
            match = invokeCompileRegex.Match(expressionSb.ToString());
            while (match.Success)
            {
                expressionSb = expressionSb.Replace(match.Groups[0].Value, string.Empty);
                match = match.NextMatch();
            }

            expressionSb.Insert(0, "query");

            return expressionSb.ToString();
        }

        public override void SerializeAsXML(XmlWriter xmlWriter)
        {
            if (xmlWriter == null)
            {
                throw new ArgumentNullException($"{nameof(xmlWriter)} cannot be null.");
            }

            if (this.expressionStr == null)
            {
                this.expressionStr = LinqTestInput.CleanUpInputExpression(this.expression.Body.ToString());
            }

            if (this.expressionStr == null)
            {
                this.expressionStr = LinqTestInput.CleanUpInputExpression(this.expression.Body.ToString());
            }

            xmlWriter.WriteStartElement("Description");
            xmlWriter.WriteCData(this.Description);
            xmlWriter.WriteEndElement();
            xmlWriter.WriteStartElement("Expression");
            xmlWriter.WriteCData(expressionStr);
            xmlWriter.WriteEndElement();
            if (this.errorMessage != null)
            {
                xmlWriter.WriteStartElement("ErrorMessage");
                xmlWriter.WriteCData(this.errorMessage);
                xmlWriter.WriteEndElement();
            }
        }
    }

    public class LinqTestOutput : BaselineTestOutput
    {
        internal static Regex sdkVersion = new Regex("documentdb-dotnet-sdk[^]]+");
        internal static Regex activityId = new Regex("ActivityId:.+", RegexOptions.Multiline);
        internal static Regex newLine = new Regex("(\r\n|\r|\n)");

        internal string SqlQuery { get; }
        internal string errorMessage { get; private set; }
        internal bool failed { get; private set; }

        private static Dictionary<string, string> newlineKeywords = new Dictionary<string, string>() {
            { "SELECT", "\nSELECT" },
            { "FROM", "\nFROM" },
            { "WHERE", "\nWHERE" },
            { "JOIN", "\nJOIN" },
            { "ORDER BY", "\nORDER BY" },
            { " )", "\n)" }
        };

        public static string FormatErrorMessage(string msg)
        {
            msg = newLine.Replace(msg, string.Empty);

            // remove sdk version in the error message which can change in the future. 
            // e.g. <![CDATA[Method 'id' is not supported., documentdb-dotnet-sdk/2.0.0 Host/64-bit MicrosoftWindowsNT/6.3.9600.0]]>
            msg = sdkVersion.Replace(msg, string.Empty);

            // remove activity Id
            msg = activityId.Replace(msg, string.Empty);

            return msg;
        }

        internal LinqTestOutput(string sqlQuery, string errorMsg = null, bool hasFailed = false)
        {
            this.SqlQuery = FormatSql(sqlQuery);
            this.errorMessage = errorMsg;
            this.failed = hasFailed;
        }

        public static String FormatSql(string sqlQuery)
        {
            const string subqueryCue = "(SELECT";
            bool hasSubquery = sqlQuery.IndexOf(subqueryCue, StringComparison.OrdinalIgnoreCase) > 0;

            var sb = new StringBuilder(sqlQuery);
            foreach (KeyValuePair<string, string> kv in newlineKeywords)
            {
                sb.Replace(kv.Key, kv.Value);
            }

            if (!hasSubquery) return sb.ToString();

            const string oneTab = "    ";
            const string startCue = "SELECT";
            const string endCue = ")";

            var tokens = sb.ToString().Split('\n');
            bool firstSelect = true;
            sb.Length = 0;
            StringBuilder indentSb = new StringBuilder();
            for (int i = 0; i < tokens.Length; ++i)
            {
                if (tokens[i].StartsWith(startCue, StringComparison.OrdinalIgnoreCase))
                {
                    if (!firstSelect) indentSb.Append(oneTab); else firstSelect = false;

                }
                else if (tokens[i].StartsWith(endCue, StringComparison.OrdinalIgnoreCase))
                {
                    indentSb.Length = indentSb.Length - oneTab.Length;
                }

                sb.Append(indentSb).Append(tokens[i]).Append("\n");
            }

            return sb.ToString();
        }

        public override void SerializeAsXML(XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement(nameof(SqlQuery));
            xmlWriter.WriteCData(this.SqlQuery);
            xmlWriter.WriteEndElement();
            if (this.errorMessage != null)
            {
                xmlWriter.WriteStartElement("ErrorMessage");
                xmlWriter.WriteCData(LinqTestOutput.FormatErrorMessage(errorMessage));
                xmlWriter.WriteEndElement();
            }

            if (this.failed)
            {
                xmlWriter.WriteElementString("Failed", this.failed.ToString());
            }
        }
    }
}
