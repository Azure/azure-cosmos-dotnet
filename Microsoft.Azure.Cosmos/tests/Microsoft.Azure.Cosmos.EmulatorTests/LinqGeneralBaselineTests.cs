﻿//-----------------------------------------------------------------------
// <copyright file="LinqAttributeContractTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Services.Management.Tests.LinqProviderTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Dynamic;
    using System.Runtime.Serialization;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using BaselineTest;
    using Microsoft.Azure.Documents;

    [Ignore]
    [TestClass]
    [TestCategory("Quarantine")]
    public class LinqGeneralBaselineTests : BaselineTests<LinqTestInput, LinqTestOutput>
    {
        private static DocumentClient client;
        private static Database testDb;
        private static DocumentCollection testCollection;
        private static Func<bool, IQueryable<Family>> getQuery;

        [ClassInitialize]
        public static void Initialize(TestContext textContext)
        {
            client = TestCommon.CreateClient(true, defaultConsistencyLevel: ConsistencyLevel.Session);
            DocumentClientSwitchLinkExtension.Reset("LinqTests");
            var query = new DocumentQuery<Family>(client, ResourceType.Document, typeof(Document), null, null);
            CleanUp();

            testDb = client.CreateDatabaseAsync(new Database() { Id = Guid.NewGuid().ToString() }).Result;
            getQuery = LinqTestsCommon.GenerateFamilyData(client, testDb, out testCollection);
        }

        [ClassCleanup]
        public static void CleanUp()
        {
            if (testDb != null)
            {
                client.DeleteDatabaseAsync(testDb.SelfLink).Wait();
            }
        }

        public class Address
        {
            public string State;
            public string County;
            public string City;
        }

        public class GuidClass : LinqTestObject
        {
            [JsonProperty(PropertyName = "id")]
            public Guid Id;
        }

        public class ListArrayClass : LinqTestObject
        {
            [JsonProperty(PropertyName = "id")]
            public string Id;

            public int[] ArrayField;
            public List<int> ListField;
        }

        [DataContract]
        public class Sport : LinqTestObject
        {
            [DataMember(Name = "id")]
            public string SportName;

            [JsonProperty(PropertyName = "json")]
            [DataMember(Name = "data")]
            public string SportType;
        }

        public class Sport2 : LinqTestObject
        {
            [DataMember(Name = "data")]
            public string id;
        }

        [TestMethod]
        public void TestSelectMany()
        {
            var inputs = new List<LinqTestInput>();

            inputs.Add(new LinqTestInput("SelectMany(SelectMany(Where -> Select))",
                b => getQuery(b)
                .SelectMany(family => family.Children
                    .SelectMany(child => child.Pets
                        .Where(pet => pet.GivenName == "Fluffy")
                        .Select(pet => pet)))));

            inputs.Add(new LinqTestInput("SelectMany(Where -> SelectMany(Where -> Select))",
                b => getQuery(b)
                .SelectMany(family => family.Children.Where(c => c.Grade > 10)
                    .SelectMany(child => child.Pets
                        .Where(pet => pet.GivenName == "Fluffy")
                        .Select(pet => pet)))));

            inputs.Add(new LinqTestInput("SelectMany(SelectMany(Where -> Select new {}))",
                b => getQuery(b)
                .SelectMany(family => family.Children
                    .SelectMany(child => child.Pets
                        .Where(pet => pet.GivenName == "Fluffy")
                        .Select(pet => new
                        {
                            family = family.FamilyId,
                            child = child.GivenName,
                            pet = pet.GivenName
                        })))));

            inputs.Add(new LinqTestInput(
                "SelectMany(Select)",
                b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.FamilyName))));

            inputs.Add(new LinqTestInput(
                "SelectMany(Select -> Select)",
                b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.FamilyName).Select(n => n.Count()))));

            inputs.Add(new LinqTestInput(
                "SelectMany(Select -> Where)",
                b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.FamilyName).Where(n => n.Count() > 10))));

            inputs.Add(new LinqTestInput(
                "SelectMany(Select) -> Select",
                b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.FamilyName)).Select(n => n.Count())));

            inputs.Add(new LinqTestInput("SelectMany()", b => getQuery(b).SelectMany(root => root.Children)));

            inputs.Add(new LinqTestInput("SelectMany -> SelectMany", b => getQuery(b).SelectMany(f => f.Children).SelectMany(c => c.Pets)));

            inputs.Add(new LinqTestInput("SelectMany -> Where -> SelectMany(Select)", b => getQuery(b).SelectMany(f => f.Children).Where(c => c.Pets.Count() > 0).SelectMany(c => c.Pets.Select(p => p.GivenName))));

            inputs.Add(new LinqTestInput("SelectMany -> Where -> SelectMany(Select new)", b => getQuery(b)
                .SelectMany(f => f.Children)
                .Where(c => c.Pets.Count() > 0)
                .SelectMany(c => c.Pets.Select(p => new { PetName = p.GivenName, OwnerName = c.GivenName }))));

            inputs.Add(new LinqTestInput("Where -> SelectMany", b => getQuery(b).Where(f => f.Children.Count() > 0).SelectMany(f => f.Children)));

            inputs.Add(new LinqTestInput("SelectMany -> Select", b => getQuery(b).SelectMany(f => f.Children).Select(c => c.FamilyName)));

            inputs.Add(new LinqTestInput("SelectMany(Select)", b => getQuery(b).SelectMany(f => f.Children.Select(c => c.Pets.Count()))));

            inputs.Add(new LinqTestInput("SelectMany(Select)", b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.FamilyName))));

            inputs.Add(new LinqTestInput("SelectMany(Select -> Select)", b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.FamilyName).Select(n => n.Count()))));

            inputs.Add(new LinqTestInput("SelectMany(Select -> Where)", b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.FamilyName).Where(n => n.Count() > 10))));

            inputs.Add(new LinqTestInput("SelectMany(Take -> Where)", b => getQuery(b).SelectMany(f => f.Children.Take(2).Where(c => c.FamilyName.Count() > 10)), ErrorMessages.TopInSubqueryNotSupported));

            inputs.Add(new LinqTestInput("SelectMany(OrderBy -> Take -> Where)", b => getQuery(b).SelectMany(f => f.Children.OrderBy(c => c.Grade).Take(2).Where(c => c.FamilyName.Count() > 10)), ErrorMessages.TopInSubqueryNotSupported));

            inputs.Add(new LinqTestInput("SelectMany(Distinct -> Where)", b => getQuery(b).SelectMany(f => f.Children.Distinct().Where(c => c.FamilyName.Count() > 10))));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestSimpleSubquery()
        {
            var inputs = new List<LinqTestInput>();

            inputs.Add(new LinqTestInput("Select -> Select", b => getQuery(b).Select(f => f.FamilyId).Select(n => n.Count())));

            inputs.Add(new LinqTestInput("Select -> Where", b => getQuery(b).Select(f => f.FamilyId).Where(id => id.Count() > 10)));

            inputs.Add(new LinqTestInput("Select -> OrderBy -> Take -> Select -> Orderby -> Take", b => getQuery(b).Select(x => x).OrderBy(x => x).Take(10).Select(f => f.FamilyId).OrderBy(n => n.Count()).Take(5), ErrorMessages.OrderByInSubqueryNotSuppported));

            inputs.Add(new LinqTestInput("Select -> Orderby -> Take -> Select -> Orderby -> Take", b => getQuery(b).Select(f => f).OrderBy(f => f.Children.Count()).Take(3).Select(x => x).OrderBy(f => f.Parents.Count()).Take(2), ErrorMessages.OrderByInSubqueryNotSuppported));

            inputs.Add(new LinqTestInput("Orderby -> Take -> Orderby -> Take", b => getQuery(b).OrderBy(f => f.Children.Count()).Take(3).OrderBy(f => f.Parents.Count()).Take(2), ErrorMessages.OrderByInSubqueryNotSuppported));

            inputs.Add(new LinqTestInput("Take -> Orderby -> Take", b => getQuery(b).Take(10).OrderBy(f => f.FamilyId).Take(1), ErrorMessages.TopInSubqueryNotSupported));

            inputs.Add(new LinqTestInput("Take -> Where -> Take -> Where -> Take -> Where", b => getQuery(b).Take(10).Where(f => f.Children.Count() > 0).Take(9).Where(f => f.Parents.Count() > 0).Take(8).Where(f => f.FamilyId.Count() > 10), ErrorMessages.TopInSubqueryNotSupported));

            inputs.Add(new LinqTestInput("Take -> Where -> Distinct -> Select -> Take -> Where", b => getQuery(b).Take(10).Where(f => f.Children.Count() > 0).Distinct().Select(f => new { f }).Take(8).Where(f => f.f.FamilyId.Count() > 10), ErrorMessages.TopInSubqueryNotSupported));

            inputs.Add(new LinqTestInput("Distinct -> Select -> Take -> Where -> Take -> Where", b => getQuery(b).Distinct().Select(f => new { f }).Take(10).Where(f => f.f.Children.Count() > 0).Take(9).Where(f => f.f.Parents.Count() > 0), ErrorMessages.TopInSubqueryNotSupported));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestQueryFlattening()
        {
            // these queries should make more sense when combined with where and orderby
            // these tests verify the flattening part
            var inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("array create", b => getQuery(b).Select(f => f.Int).Select(i => new int[] { i })));
            inputs.Add(new LinqTestInput("unary operation", b => getQuery(b).Select(f => f.Int).Select(i => -i)));
            inputs.Add(new LinqTestInput("binary operation", b => getQuery(b).Select(f => f).Select(i => (i.Int % 10) * i.Children.Count())));
            inputs.Add(new LinqTestInput("literal", b => getQuery(b).Select(f => f.Int).Select(i => 0)));
            inputs.Add(new LinqTestInput("function call", b => getQuery(b).Select(f => f.Parents).Select(p => p.Count())));
            inputs.Add(new LinqTestInput("object create", b => getQuery(b).Select(f => f.Parents).Select(p => new { parentCount = p.Count() })));
            inputs.Add(new LinqTestInput("conditional", b => getQuery(b).Select(f => f.Children).Select(c => c.Count() > 0 ? "have kids" : "no kids")));
            inputs.Add(new LinqTestInput("property ref + indexer", b => getQuery(b).Select(f => f)
                .Where(f => f.Children.Count() > 0 && f.Children[0].Pets.Count() > 0)
                .Select(f => f.Children[0].Pets[0].GivenName)));

            inputs.Add(new LinqTestInput("array creation -> indexer", b => getQuery(b).Select(f => new int[] { f.Int }).Select(array => array[0])));
            inputs.Add(new LinqTestInput("unary, indexer, property, function call -> function call", b => getQuery(b)
                .Where(f => f.Children.Count() > 0)
                .Select(f => -f.Children[0].Pets.Count()).Select(i => Math.Abs(i))));
            inputs.Add(new LinqTestInput("binary operation, function call -> conditional", b => getQuery(b).Select(i => (i.Int % 10) * i.Children.Count()).Select(i => i > 0 ? new int[] { i } : new int[] { })));
            inputs.Add(new LinqTestInput("object creation -> conditional", b => getQuery(b)
                .Select(f => new { parentCount = f.Parents.Count(), childrenCount = f.Children.Count() })
                .Select(r => r.parentCount > 0 ? Math.Floor((double)r.childrenCount / r.parentCount) : 0)));
            inputs.Add(new LinqTestInput("indexer -> function call", b => getQuery(b).Select(f => f.Parents[0]).Select(p => string.Concat(p.FamilyName, p.GivenName))));
            inputs.Add(new LinqTestInput("conditional -> object creation", b => getQuery(b).Select(f => f.Parents.Count() > 0 ? f.Parents : new Parent[0]).Select(p => new { parentCount = p.Count() })));
            inputs.Add(new LinqTestInput("object creation -> conditional", b => getQuery(b).Select(f => new { children = f.Children }).Select(c => c.children.Count() > 0 ? c.children[0].GivenName : "no kids")));
            inputs.Add(new LinqTestInput("object creation -> conditional", b => getQuery(b).Select(f => new { family = f, children = f.Children.Count() }).Select(f => f.children > 0 && f.family.Children[0].Pets.Count() > 0 ? f.family.Children[0].Pets[0].GivenName : "no kids")));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestSubquery()
        {
            var inputs = new List<LinqTestInput>();

            // --------------------------------------
            // Subquery lambdas
            // --------------------------------------

            inputs.Add(new LinqTestInput(
                "Select(Select)", b => getQuery(b)
                .Select(f => f.Children.Select(c => c.Pets.Count()))));

            inputs.Add(new LinqTestInput(
                "Select(OrderBy)", b => getQuery(b)
                .Select(f => f.Children.OrderBy(c => c.Pets.Count())), 
                ErrorMessages.OrderByInSubqueryNotSuppported));
                
            inputs.Add(new LinqTestInput(
                "Select(Take)", b => getQuery(b)
                .Select(f => f.Children.Take(2)), 
                ErrorMessages.TopInSubqueryNotSupported));
                
            inputs.Add(new LinqTestInput(
                "Select(Where)", b => getQuery(b)
                .Select(f => f.Children.Where(c => c.Pets.Count() > 0))));
                
            inputs.Add(new LinqTestInput(
                "Select(Distinct)", b => getQuery(b)
                .Select(f => f.Children.Distinct())));
                
            inputs.Add(new LinqTestInput(
                "Select(Count)", b => getQuery(b)
                .Select(f => f.Children.Count(c => c.Grade > 80))));

            inputs.Add(new LinqTestInput(
                "Select(Sum)", b => getQuery(b)
                .Select(f => f.Children.Sum(c => c.Grade))));

            inputs.Add(new LinqTestInput(
                "Where(Count)", b => getQuery(b)
                .Where(f => f.Children.Count(c => c.Pets.Count() > 0) > 0)));

            inputs.Add(new LinqTestInput(
                "Where(Sum)", b => getQuery(b)
                .Where(f => f.Children.Sum(c => c.Grade) > 100)));

            inputs.Add(new LinqTestInput(
                "OrderBy(Select)", b => getQuery(b)
                .OrderBy(f => f.Children.Select(c => c.Pets.Count())), 
                ErrorMessages.OrderbyItemExpressionCouldNotBeMapped));

            inputs.Add(new LinqTestInput(
                "OrderBy(Sum)", b => getQuery(b)
                .OrderBy(f => f.Children.Sum(c => c.Grade)), 
                ErrorMessages.OrderbyItemExpressionCouldNotBeMapped));

            inputs.Add(new LinqTestInput(
                "OrderBy(Count)", b => getQuery(b)
                .OrderBy(f => f.Children.Count(c => c.Grade > 90)), 
                ErrorMessages.OrderbyItemExpressionCouldNotBeMapped));

            // -------------------------------------------------------------
            // Mutilpe-transformations subquery lambdas
            // -------------------------------------------------------------

            inputs.Add(new LinqTestInput(
                "Select(Select -> Distinct -> Count)", b => getQuery(b)
                .Select(f => f.Children.Select(c => c.Gender).Distinct().Count())));

            inputs.Add(new LinqTestInput(
                "Select(Select -> Sum)", b => getQuery(b)
                .Select(f => f.Children.Select(c => c.Grade).Sum())));

            inputs.Add(new LinqTestInput(
                "Select(Select -> OrderBy -> Take)", b => getQuery(b)
                .Select(f => f.Children.Select(c => c.GivenName).OrderBy(n => n.Length).Take(1)),
                ErrorMessages.OrderByInSubqueryNotSuppported));

            inputs.Add(new LinqTestInput(
                "Select(SelectMany -> Select)", b => getQuery(b)
                .Select(f => f.Children.SelectMany(c => c.Pets).Select(c => c.GivenName.Count()))));

            inputs.Add(new LinqTestInput(
                "Select(Where -> Count)", b => getQuery(b)
                .Select(f => f.Children.Where(c => c.Grade > 50).Count())));

            inputs.Add(new LinqTestInput(
                "Select(Where -> OrderBy -> Take)", b => getQuery(b)
                .Select(f => f.Children.Where(c => c.Grade > 50).OrderBy(c => c.Pets.Count()).Take(3)),
                ErrorMessages.OrderByInSubqueryNotSuppported));

            inputs.Add(new LinqTestInput(
                "Select(Where -> Select -> Take)", b => getQuery(b)
                .Select(f => f.Children.Where(c => c.Grade > 50).Select(c => c.Pets.Count()).Take(3)),
                ErrorMessages.TopInSubqueryNotSupported));

            inputs.Add(new LinqTestInput(
                "Select(Where -> Select(array) -> Take)", b => getQuery(b)
                .Select(f => f.Children.Where(c => c.Grade > 50).Select(c => c.Pets).Take(3)),
                ErrorMessages.TopInSubqueryNotSupported));

            inputs.Add(new LinqTestInput(
                "Select(where -> Select -> Distinct)", b => getQuery(b)
                .Select(f => f.Children.Where(c => c.Grade > 50 && c.Pets.Count() > 0).Select(c => c.Gender).Distinct())));

            inputs.Add(new LinqTestInput(
                "Select(OrderBy -> Take -> Select)", b => getQuery(b)
                .Select(f => f.Children.OrderBy(c => c.Grade).Take(1).Select(c => c.Gender)),
                ErrorMessages.TopInSubqueryNotSupported));

            inputs.Add(new LinqTestInput(
                "Select(OrderBy -> Take -> Select -> Average)", b => getQuery(b)
                .Select(f => f.Children.OrderBy(c => c.Pets.Count()).Take(2).Select(c => c.Grade).Average()),
                ErrorMessages.OrderByInSubqueryNotSuppported));

            inputs.Add(new LinqTestInput(
                "Where(Select -> Count)", b => getQuery(b)
                .Where(f => f.Children.Select(c => c.Pets.Count()).Count() > 0)));

            inputs.Add(new LinqTestInput(
                "Where(Where -> Count)", b => getQuery(b)
                .Where(f => f.Children.Where(c => c.Pets.Count() > 0).Count() > 0)));

            inputs.Add(new LinqTestInput(
                "Where(Where -> Sum)", b => getQuery(b)
                .Where(f => f.Children.Where(c => c.Pets.Count() > 0).Sum(c => c.Grade) < 200)));

            inputs.Add(new LinqTestInput(
                "Where(Where -> OrderBy -> Take -> Select)", b => getQuery(b)
                .Where(f => f.Children.Where(c => c.Pets.Count() > 0).OrderBy(c => c.Grade).Take(1).Where(c => c.Grade > 80).Count() > 0),
                ErrorMessages.OrderByInSubqueryNotSuppported));

            inputs.Add(new LinqTestInput(
                "OrderBy(Select -> Where)", b => getQuery(b)
                .OrderBy(f => f.Children.Select(c => c.Pets.Count()).Where(x => x > 1)), 
                ErrorMessages.OrderbyItemExpressionCouldNotBeMapped));

            inputs.Add(new LinqTestInput(
                "OrderBy(Where -> Count)", b => getQuery(b)
                .OrderBy(f => f.Children.Where(c => c.Pets.Count() > 3).Count()), 
                ErrorMessages.OrderbyItemExpressionCouldNotBeMapped));

            inputs.Add(new LinqTestInput(
                "OrderBy(Select -> Sum)", b => getQuery(b)
                .OrderBy(f => f.Children.Select(c => c.Grade).Sum()), 
                ErrorMessages.OrderbyItemExpressionCouldNotBeMapped));

            inputs.Add(new LinqTestInput(
                "OrderBy(OrderBy -> Take -> Sum)", b => getQuery(b)
                .OrderBy(f => f.Children.OrderBy(c => c.Pets.Count()).Take(2).Sum(c => c.Grade)), 
                ErrorMessages.OrderByInSubqueryNotSuppported));

            // ---------------------------------------------------------
            // Scalar and Built-in expressions with subquery lambdas
            // ---------------------------------------------------------

            // Unary

            inputs.Add(new LinqTestInput(
                "Where(unary (Where -> Count))", b => getQuery(b)
                .Where(f => -f.Children.Where(c => c.Grade < 20).Count() == 0)));

            // Binary

            inputs.Add(new LinqTestInput(
                "Select(binary with Count)", b => getQuery(b)
                .Select(f => 5 + f.Children.Count(c => c.Pets.Count() > 0))));
                
            inputs.Add(new LinqTestInput(
                "Select(constant + Where -> Count)", b => getQuery(b)
                .Select(f => 5 + f.Children.Where(c => c.Pets.Count() > 0).Count())));

            inputs.Add(new LinqTestInput(
                "Where((Where -> Count) % constant)", b => getQuery(b)
                .Where(f => f.Children.Where(c => c.Pets.Count() > 0).Count() % 2 == 1)));

            // Conditional

            inputs.Add(new LinqTestInput(
                "Select(conditional Any ? Select : Select)", b => getQuery(b)
                .Select(f => f.Children.Any() ? f.Children.Select(c => c.GivenName) : f.Parents.Select(p => p.GivenName))));

            inputs.Add(new LinqTestInput(
                "Select(conditional Any(filter) ? Max : Sum)", b => getQuery(b)
                .Select(f => f.Children.Any(c => c.Grade > 97) ? f.Children.Max(c => c.Grade) : f.Children.Sum(c => c.Grade))));

            // New array

            inputs.Add(new LinqTestInput(
                "Select(new array)", b => getQuery(b)
                .Select(f => new int[] { f.Children.Count(), f.Children.Sum(c => c.Grade) })));

            // New + member init

            inputs.Add(new LinqTestInput(
                "Select(new)", b => getQuery(b)
                .Select(f => new int[] { f.Children.Count(), f.Children.Sum(c => c.Grade) })));

            inputs.Add(new LinqTestInput(
               "Select(Select new)", b => getQuery(b)
               .Select(f => new { f.FamilyId, ChildrenPetCount = f.Children.Select(c => c.Pets.Count()) })));

            inputs.Add(new LinqTestInput(
                "Select(new Where)", b => getQuery(b)
                .Select(f => new { f.FamilyId, ChildrenWithPets = f.Children.Where(c => c.Pets.Count() > 3) })));

            inputs.Add(new LinqTestInput(
                "Select(new Where)", b => getQuery(b)
                .Select(f => new { f.FamilyId, GoodChildren = f.Children.Where(c => c.Grade > 90) })));

            inputs.Add(new LinqTestInput(
                "Select(new Where -> Select)", b => getQuery(b)
                .Select(f => new { f.FamilyId, ChildrenWithPets = f.Children.Where(c => c.Pets.Count() > 3).Select(c => c.GivenName) })));

            inputs.Add(new LinqTestInput(
                "Select(new Where -> Count) -> Where", b => getQuery(b)
                .Select(f => new { Family = f, ChildrenCount = f.Children.Where(c => c.Grade > 0).Count() }).Where(f => f.ChildrenCount > 0)));

            // Array builtin functions

            inputs.Add(new LinqTestInput(
                "Select(Where -> Concat(Where))", b => getQuery(b)
                .Select(f => f.Children.Where(c => c.Grade > 90).Concat(f.Children.Where(c => c.Grade < 10)))));

            inputs.Add(new LinqTestInput(
                "Select(Select -> Contains(Sum))", b => getQuery(b)
                .Select(f => f.Children.Select(c => c.Grade).Contains(f.Children.Sum(c => c.Pets.Count())))));

            inputs.Add(new LinqTestInput(
                "Select(Select -> Contains(Where -> Count))", b => getQuery(b)
                .Select(f => f.Children.Select(c => c.Grade).Contains(f.Children.Where(c => c.Grade > 50).Count()))));

            inputs.Add(new LinqTestInput(
                "Where -> Select(array indexer)", b => getQuery(b)
                .Where(f => f.Children.Where(c => c.Grade > 20).Count() >= 2)
                .Select(f => f.Children.Where(c => c.Grade > 20).ToArray()[1])));

            inputs.Add(new LinqTestInput(
                "Where -> Select(array indexer)", b => getQuery(b)
                .Where(f => f.Children.Where(c => c.Grade > 20).Count() >= 2)
                .Select(f => f.Children.Where(c => c.Grade > 20).ToArray()[f.Children.Count() % 2]),
                ErrorMessages.MemberIndexerNotSupported));

            // Math builtin functions

            inputs.Add(new LinqTestInput(
                "Select(Floor(sum(map), sum(map)))", b => getQuery(b)
                .Select(f => Math.Floor(1.0 * f.Children.Sum(c => c.Grade) / (f.Children.Sum(c => c.Pets.Count()) + 1)))));

            inputs.Add(new LinqTestInput(
                "Select(Pow(Sum(map), Count(Any)))", b => getQuery(b)
                .Select(f => Math.Pow(f.Children.Sum(c => c.Pets.Count()), f.Children.Count(c => c.Pets.Any(p => p.GivenName.Count() == 0 || p.GivenName.Substring(0, 1) == "A"))))));

            inputs.Add(new LinqTestInput(
                "OrderBy(Log(Where -> Count))", b => getQuery(b)
                .OrderBy(f => Math.Log(f.Children.Where(c => c.Pets.Count() > 0).Count())),
                ErrorMessages.OrderbyItemExpressionCouldNotBeMapped));

            // ------------------------------------------------------------------
            // Expression with subquery lambdas -> more transformations
            // ------------------------------------------------------------------

            inputs.Add(new LinqTestInput(
                "Select(Select) -> Where", b => getQuery(b).Select(f => f.Children.Select(c => c.Pets.Count())).Where(x => x.Count() > 0)));
            
            // Customer requested scenario
            inputs.Add(new LinqTestInput(
                "Select(new w/ Where) -> Where -> OrderBy -> Take", b => getQuery(b)
                .Select(f => new { f.FamilyId, ChildrenCount = f.Children.Count(), SmartChildren = f.Children.Where(c => c.Grade > 90) })
                .Where(f => f.FamilyId.CompareTo("ABC") > 0 && f.SmartChildren.Count() > 0)
                .OrderBy(f => f.ChildrenCount)
                .Take(10), 
                ErrorMessages.OrderbyItemExpressionCouldNotBeMapped));
                
            inputs.Add(new LinqTestInput(
                "Select(new w/ Where) -> Where -> OrderBy -> Take", b => getQuery(b)
                .Select(f => new { f.FamilyId, ChildrenCount = f.Children.Count(), SmartChildren = f.Children.Where(c => c.Grade > 90) })
                .Where(f => f.ChildrenCount > 2 && f.SmartChildren.Count() > 1)
                .OrderBy(f => f.FamilyId)
                .Take(10)));

            inputs.Add(new LinqTestInput(
                "Select(new { Select(Select), conditional Count Take }) -> Where -> Select(Select(Any))", b => getQuery(b)
                .Select(f => new
                {
                    f.FamilyId,
                    ChildrenPetFirstChars = f.Children.Select(c => c.Pets.Select(p => p.GivenName.Substring(0, 1))),
                    FirstChild = f.Children.Count() > 0 ? f.Children.Take(1) : null
                })
                .Where(f => f.FirstChild != null)
                .Select(f => f.ChildrenPetFirstChars.Select(chArray => chArray.Any(a => f.FamilyId.StartsWith(a)))),
                ErrorMessages.TopInSubqueryNotSupported));

            inputs.Add(new LinqTestInput(
                "Select(new (Select(new (Select, Select))))", b => getQuery(b)
                .Select(f => new
                {
                    f.FamilyId,
                    ChildrenProfile = f.Children.Select(c => new
                    {
                        Fullname = c.GivenName + " " + c.FamilyName,
                        PetNames = c.Pets.Select(p => p.GivenName),
                        ParentNames = f.Parents.Select(p => p.GivenName)
                    })
                })));

            // ------------------------------------------------
            // Subquery lambda -> subquery lamda
            // ------------------------------------------------

            inputs.Add(new LinqTestInput(
                "Select(array) -> Where(Sum(map))", b => getQuery(b)
                .Select(f => f.Children).Where(children => children.Sum(c => c.Grade) > 100)));

            inputs.Add(new LinqTestInput(
                "Select(Select) -> Select(Sum())", b => getQuery(b)
                .Select(f => f.Children.Select(c => c.Grade)).Select(children => children.Sum())));

            inputs.Add(new LinqTestInput(
                "Select(Select) -> Select(Sum(map))", b => getQuery(b)
                .Select(f => f.Children).Select(children => children.Sum(c => c.Grade))));

            inputs.Add(new LinqTestInput(
                "Where(Any binary) -> Select(Sum(map))", b => getQuery(b)
                .Where(f => f.Children.Any(c => c.Grade > 90) && f.IsRegistered)
                .Select(f => f.Children.Sum(c => c.Pets.Count()))));

            inputs.Add(new LinqTestInput(
                "Where(Any binary) -> OrderBy(Count(filter)) -> Select(Sum(map))", b => getQuery(b)
                .Where(f => f.Children.Any(c => c.Grade > 90) && f.IsRegistered)
                .OrderBy(f => f.Children.Count(c => c.Things.Count() > 3))
                .Select(f => f.Children.Sum(c => c.Pets.Count())),
                ErrorMessages.OrderbyItemExpressionCouldNotBeMapped));

            // ------------------------------
            // Nested subquery lambdas
            // ------------------------------

            inputs.Add(new LinqTestInput(
                "Select(Select(Select))", b => getQuery(b)
                .Select(f => f.Children.Select(c => c.Pets.Select(p => p.GivenName.Count())))));

            inputs.Add(new LinqTestInput(
                "Select(Select(Select))", b => getQuery(b)
                .Select(f => f.Children.Select(c => c.Pets.Select(p => p)))));

            inputs.Add(new LinqTestInput(
                "Select(Select(new Count))", b => getQuery(b)
                .Select(f => f.Children
                    .Select(c => new
                    {
                        HasSiblingWithSameStartingChar = f.Children.Count(child => (child.GivenName + " ").Substring(0, 1) == (c.GivenName + " ").Substring(0, 1)) > 1
                    }))));

            inputs.Add(new LinqTestInput(
                "Where -> Select(conditional ? Take : OrderBy -> Array indexer)", b => getQuery(b)
                .Where(f => f.Children.Count() > 0)
                .Select(f => f.Children.Count() == 1 ? f.Children.Take(1).ToArray()[0] : f.Children.OrderBy(c => c.Grade).ToArray()[1]),
                ErrorMessages.OrderByInSubqueryNotSuppported));

            inputs.Add(new LinqTestInput(
                "Select(Where(Where -> Count) -> Select(new Count))", b => getQuery(b)
                .Select(f => f.Children
                    .Where(c => c.Pets
                        .Where(p => p.GivenName.Count() > 10 && p.GivenName.Substring(0, 1) == "A")
                        .Count() > 0)
                    .Select(c => new
                    {
                        HasSiblingWithSameStartingChar = f.Children.Count(child => (child.GivenName + " ").Substring(0, 1) == (c.GivenName + " ").Substring(0, 1)) > 1
                    }))));

            inputs.Add(new LinqTestInput(
                "SelectMany(Select(Select))", b => getQuery(b)
                .SelectMany(f => f.Children.Select(c => c.Pets.Select(p => p.GivenName.Count())))));

            inputs.Add(new LinqTestInput(
                "SelectMany(Where(Any))", b => getQuery(b)
                .SelectMany(f => f.Children.Where(c => c.Pets.Any(p => p.GivenName.Count() > 10)))));

            inputs.Add(new LinqTestInput(
                "Where(Where(Count) -> Count)", b => getQuery(b)
                .Where(f => f.Parents.Where(p => p.FamilyName.Count() > 10).Count() > 1)));

            inputs.Add(new LinqTestInput(
                "Where(Where(Where -> Count) -> Count)", b => getQuery(b)
                .Where(f => f.Children.Where(c => c.Pets.Where(p => p.GivenName.Count() > 15).Count() > 0).Count() > 0)));

            inputs.Add(new LinqTestInput(
                "Where(Select(Select -> Any))", b => getQuery(b)
                .Where(f => f.Children.Select(c => c.Pets.Select(p => p.GivenName)).Any(t => t.Count() > 3))));

            // -------------------------------------
            // Expression -> Subquery lambdas
            // -------------------------------------

            inputs.Add(new LinqTestInput(
                "Select(new) -> Select(Select)", b => getQuery(b)
                .Select(f => new { f.FamilyId, Family = f }).Select(f => f.Family.Children.Select(c => c.Pets.Count()))));

            inputs.Add(new LinqTestInput(
                "SelectMany -> Select(Select)", b => getQuery(b)
                .SelectMany(f => f.Children).Select(c => c.Pets.Select(p => p.GivenName.Count()))));

            inputs.Add(new LinqTestInput(
                "SelectMany(Where) -> Where(Any) -> Select(Select)", b => getQuery(b)
                .SelectMany(f => f.Children.Where(c => c.Grade > 80))
                .Where(c => c.Pets.Any(p => p.GivenName.Count() > 20))
                .Select(c => c.Pets.Select(p => p.GivenName.Count()))));

            inputs.Add(new LinqTestInput(
                "Distinct -> Select(new) -> Where(Select(Select -> Any))", b => getQuery(b)
                .Distinct()
                .Select(f => new { f.FamilyId, ChildrenCount = f.Children.Count(), Family = f })
                .Where(f => f.Family.Children.Select(c => c.Pets.Select(p => p.GivenName)).Any(t => t.Count() > 3))));

            inputs.Add(new LinqTestInput(
                "Where -> Select(Select)", b => getQuery(b)
                .Where(f => f.Children.Count() > 0).Select(f => f.Children.Select(c => c.Pets.Count()))));

            inputs.Add(new LinqTestInput(
                "Distinct -> Select(Select)", b => getQuery(b)
                .Distinct().Select(f => f.Children.Select(c => c.Pets.Count()))));

            inputs.Add(new LinqTestInput(
                "Take -> Select(Select)", b => getQuery(b)
                .Take(10).Select(f => f.Children.Select(c => c.Pets.Count()))));
            
            // ------------------
            // Any in lambda
            // ------------------

            inputs.Add(new LinqTestInput(
                "Select(Any w const array)", b => getQuery(b)
                .Select(f => new int[] { 1, 2, 3 }.Any()), "Input is not of type IDocumentQuery."));
                
            inputs.Add(new LinqTestInput(
                "Select(Any)", b => getQuery(b)
                .Select(f => f.Children.Any())));
                
            inputs.Add(new LinqTestInput(
                "Select(Any w lambda)", b => getQuery(b)
                .Select(f => f.Children.Any(c => c.Grade > 80))));
                
            inputs.Add(new LinqTestInput(
                "Select(new Any)", b => getQuery(b)
                .Select(f => new { f.FamilyId, HasGoodChildren = f.Children.Any(c => c.Grade > 80) })));
                
            inputs.Add(new LinqTestInput(
                "Select(new 2 Any)", b => getQuery(b)
                .Select(f => new { HasChildrenWithPets = f.Children.Any(c => c.Pets.Count() > 0), HasGoodChildren = f.Children.Any(c => c.Grade > 80) })));
                
            inputs.Add(new LinqTestInput(
                "Select(Nested Any)", b => getQuery(b)
                .Select(f => f.Children.Any(c => c.Pets.Any(p => p.GivenName.Count() > 10)))));

            inputs.Add(new LinqTestInput(
                "Where(Any)", b => getQuery(b)
                .Where(f => f.Children.Any(c => c.Pets.Count() > 0))));

            // Customer requested scenario
            inputs.Add(new LinqTestInput(
                "Where(simple expr && Any)", b => getQuery(b)
                .Where(f => f.FamilyId.Contains("a") && f.Children.Any(c => c.Pets.Count() > 0))));

            inputs.Add(new LinqTestInput(
                "OrderBy(Any)", b => getQuery(b)
                .OrderBy(f => f.Children.Any(c => c.Pets.Count() > 3)), 
                ErrorMessages.OrderbyItemExpressionCouldNotBeMapped));
            
            // ------------------------------------------------
            // SelectMany with Take and OrderBy in lambda
            // ------------------------------------------------

            inputs.Add(new LinqTestInput(
                "SelectMany(Take)", b => getQuery(b)
                .SelectMany(f => f.Children.Take(2)), 
                ErrorMessages.TopInSubqueryNotSupported));

            inputs.Add(new LinqTestInput(
                "SelectMany(OrderBy)", b => getQuery(b)
                .SelectMany(f => f.Children.OrderBy(c => c.Grade)), 
                ErrorMessages.OrderByInSubqueryNotSuppported));

            inputs.Add(new LinqTestInput(
                "SelectMany(Where -> Take)", b => getQuery(b)
                .SelectMany(f => f.Children.Where(c => c.FamilyName.Count() > 10).Take(2)), 
                ErrorMessages.TopInSubqueryNotSupported));

            inputs.Add(new LinqTestInput(
                "SelectMany(Where -> Take -> Select)", b => getQuery(b)
                .SelectMany(f => f.Children.Where(c => c.FamilyName.Count() > 10).Take(2).Select(c => c.Grade)), 
                ErrorMessages.TopInSubqueryNotSupported));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Ignore]
        public void DebuggingTest()
        {
            
        }

        [TestMethod]
        [Ignore]
        public void TestUnsupportedScenarios()
        {
            var inputs = new List<LinqTestInput>();

            // --------------------
            // Dictionary type
            // --------------------

            // Iterating through Dictionary type
            inputs.Add(new LinqTestInput("Iterating through Dictionary type", b => getQuery(b).Select(f => f.Children.Select(c => c.Things.Select(t => t.Key.Count() + t.Value.Count())))));

            // Get a count of a Dictionary type
            inputs.Add(new LinqTestInput("Getting Dictionary count", b => getQuery(b).Select(f => f.Children.Select(c => c.Things.Count()))));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestOrderByTranslation()
        {
            var inputs = new List<LinqTestInput>();

            // Ascending
            inputs.Add(new LinqTestInput("Select -> order by", b => getQuery(b).Select(family => family.FamilyId).OrderBy(id => id)));
            inputs.Add(new LinqTestInput("Select -> order by -> Select", b => getQuery(b).Select(family => family.FamilyId).OrderBy(id => id).Select(x => x.Count())));
            inputs.Add(new LinqTestInput("Where -> OrderBy -> Select query",
                b => from f in getQuery(b)
                     where f.Int == 5 && f.NullableInt != null
                     orderby f.IsRegistered
                     select f.FamilyId));
            inputs.Add(new LinqTestInput("Where -> OrderBy -> Select", b => getQuery(b).Where(f => f.Int == 5 && f.NullableInt != null).OrderBy(f => f.IsRegistered).Select(f => f.FamilyId)));
            inputs.Add(new LinqTestInput("OrderBy query",
                b => from f in getQuery(b)
                     orderby f.FamilyId
                     select f));
            inputs.Add(new LinqTestInput("OrderBy", b => getQuery(b).OrderBy(f => f.FamilyId)));
            inputs.Add(new LinqTestInput("OrderBy -> Select query",
                b => from f in getQuery(b)
                     orderby f.FamilyId
                     select f.FamilyId));
            inputs.Add(new LinqTestInput("OrderBy -> Select", b => getQuery(b).OrderBy(f => f.FamilyId).Select(f => f.FamilyId)));
            inputs.Add(new LinqTestInput("OrderBy -> Select -> Take", b => getQuery(b).OrderBy(f => f.FamilyId).Select(f => f.FamilyId).Take(10)));
            inputs.Add(new LinqTestInput("OrderBy -> Select -> Select", b => getQuery(b).OrderBy(f => f.FamilyId).Select(f => f.FamilyId).Select(x => x)));

            // Descending
            inputs.Add(new LinqTestInput("Select -> order by", b => getQuery(b).Select(family => family.FamilyId).OrderByDescending(id => id)));
            inputs.Add(new LinqTestInput("Select -> order by -> Select", b => getQuery(b).Select(family => family.FamilyId).OrderByDescending(id => id).Select(x => x.Count())));
            inputs.Add(new LinqTestInput("Where -> OrderBy Desc -> Select query",
                b => from f in getQuery(b)
                     where f.Int == 5 && f.NullableInt != null
                     orderby f.IsRegistered descending
                     select f.FamilyId));
            inputs.Add(new LinqTestInput("Where -> OrderBy Desc -> Select", b => getQuery(b).Where(f => f.Int == 5 && f.NullableInt != null).OrderByDescending(f => f.IsRegistered).Select(f => f.FamilyId)));
            inputs.Add(new LinqTestInput("OrderBy Desc query",
                b => from f in getQuery(b)
                     orderby f.FamilyId descending
                     select f));
            inputs.Add(new LinqTestInput("OrderBy Desc", b => getQuery(b).OrderByDescending(f => f.FamilyId)));
            inputs.Add(new LinqTestInput("OrderBy Desc -> Select query",
                b => from f in getQuery(b)
                     orderby f.FamilyId descending
                     select f.FamilyId));
            inputs.Add(new LinqTestInput("OrderBy Desc -> Select", b => getQuery(b).OrderByDescending(f => f.FamilyId).Select(f => f.FamilyId)));
            inputs.Add(new LinqTestInput("OrderBy -> Select -> Take", b => getQuery(b).OrderByDescending(f => f.FamilyId).Select(f => f.FamilyId).Take(10)));
            inputs.Add(new LinqTestInput("OrderBy -> Select -> Select", b => getQuery(b).OrderByDescending(f => f.FamilyId).Select(f => f.FamilyId).Select(x => x)));
            // orderby multiple expression is not supported yet
            inputs.Add(new LinqTestInput("OrderBy multiple expressions",
                b => from f in getQuery(b)
                     orderby f.FamilyId, f.Int
                     select f.FamilyId, "Method 'ThenBy' is not supported."));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Ignore]
        public void TestDistinctSelectManyIssues()
        {
            var inputs = new List<LinqTestInput>();

            // these tests need a fix in the ServiceInterop
            inputs.Add(new LinqTestInput(
                "Distinct -> OrderBy -> Take",
                b => getQuery(b).Select(f => f.Int).Distinct().OrderBy(x => x).Take(10)));

            inputs.Add(new LinqTestInput(
                "OrderBy -> Distinct -> Take",
                b => getQuery(b).Select(f => f.Int).OrderBy(x => x).Distinct().Take(10)));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestDistinctTranslation()
        {
            var inputs = new List<LinqTestInput>();

            // Simple distinct
            // Select -> Distinct for all data types
            inputs.Add(new LinqTestInput(
                "Distinct string",
                b => getQuery(b).Select(f => f.FamilyId).Distinct()));

            inputs.Add(new LinqTestInput(
                "Distinct int",
                b => getQuery(b).Select(f => f.Int).Distinct()));

            inputs.Add(new LinqTestInput(
                "Distinct bool",
                b => getQuery(b).Select(f => f.IsRegistered).Distinct()));

            inputs.Add(new LinqTestInput(
                "Distinct nullable int",
                b => getQuery(b).Where(f => f.NullableInt != null).Select(f => f.NullableInt).Distinct()));

            inputs.Add(new LinqTestInput(
                "Distinct null",
                b => getQuery(b).Where(f => f.NullObject != null).Select(f => f.NullObject).Distinct()));

            inputs.Add(new LinqTestInput(
                "Distinct children",
                b => getQuery(b).SelectMany(f => f.Children).Distinct()));

            inputs.Add(new LinqTestInput(
                "Distinct parent",
                b => getQuery(b).SelectMany(f => f.Parents).Distinct()));

            inputs.Add(new LinqTestInput(
                "Distinct family",
                b => getQuery(b).Distinct()));

            inputs.Add(new LinqTestInput(
                "Multiple distincts",
                b => getQuery(b).Distinct().Distinct()));

            inputs.Add(new LinqTestInput(
                "Distinct new obj",
                b => getQuery(b).Select(f => new { Parents = f.Parents.Count(), Children = f.Children.Count() }).Distinct()));

            inputs.Add(new LinqTestInput(
                "Distinct new obj",
                b => getQuery(b).Select(f => new { Parents = f.Parents.Count(), Children = f.Children.Count() }).Select(f => f.Parents).Distinct()));

            // Distinct + Take
            inputs.Add(new LinqTestInput(
                "Distinct -> Take",
                b => getQuery(b).Select(f => f.Int).Distinct().Take(10)));

            inputs.Add(new LinqTestInput(
                "Take -> Distinct",
                b => getQuery(b).Select(f => f.Int).Take(10).Distinct(), ErrorMessages.TopInSubqueryNotSupported));

            // Distinct + Order By
            inputs.Add(new LinqTestInput(
                "Distinct -> OrderBy",
                b => getQuery(b).Select(f => f.Int).Distinct().OrderBy(x => x)));

            inputs.Add(new LinqTestInput(
                "OrderBy -> Distinct",
                b => getQuery(b).OrderBy(f => f.Int).Distinct()));

            // Distinct + Order By + Take
            inputs.Add(new LinqTestInput(
                "Distinct -> Take -> OrderBy",
                b => getQuery(b).Select(f => f.Int).Distinct().Take(10).OrderBy(x => x), ErrorMessages.TopInSubqueryNotSupported));

            inputs.Add(new LinqTestInput(
                "OrderBy -> Take -> Distinct",
                b => getQuery(b).Select(f => f.Int).OrderBy(x => x).Take(10).Distinct(), ErrorMessages.OrderByInSubqueryNotSuppported));

            // Distinct + Where
            inputs.Add(new LinqTestInput(
                "Where -> Distinct",
                b => getQuery(b).Select(f => f.Int).Where(x => x > 10).Distinct()));

            inputs.Add(new LinqTestInput(
                "Distinct -> Where",
                b => getQuery(b).Select(f => f.Int).Distinct().Where(x => x > 10)));

            // SelectMany w Distinct
            inputs.Add(new LinqTestInput(
                "SelectMany(Select obj) -> Distinct",
                b => getQuery(b).SelectMany(f => f.Parents).Distinct()));

            inputs.Add(new LinqTestInput("SelectMany(SelectMany(Where -> Select)) -> Distinct",
                b => getQuery(b)
                .SelectMany(family => family.Children
                    .SelectMany(child => child.Pets
                        .Where(pet => pet.GivenName == "Fluffy")
                        .Select(pet => pet)))
                    .Distinct()));

            inputs.Add(new LinqTestInput("SelectMany(SelectMany(Where -> Select -> Distinct))",
                b => getQuery(b)
                .SelectMany(family => family.Children
                    .SelectMany(child => child.Pets
                        .Where(pet => pet.GivenName == "Fluffy")
                        .Select(pet => pet)
                    .Distinct()))));

            inputs.Add(new LinqTestInput("SelectMany(SelectMany(Where -> Select -> Select) -> Distinct)",
                b => getQuery(b)
                .SelectMany(family => family.Children
                    .SelectMany(child => child.Pets
                        .Where(pet => pet.GivenName == "Fluffy")
                        .Select(pet => pet.GivenName)
                        .Select(name => name.Count())))
                    .Distinct()));

            inputs.Add(new LinqTestInput("SelectMany(SelectMany(Where -> Select new {} -> Select) -> Distinct)",
                b => getQuery(b)
                .SelectMany(family => family.Children
                    .SelectMany(child => child.Pets
                        .Where(pet => pet.GivenName == "Fluffy")
                        .Select(pet => new
                        {
                            family = family.FamilyId,
                            child = child.GivenName,
                            pet = pet.GivenName
                        }).Select(p => p.child))
                    .Distinct())));

            inputs.Add(new LinqTestInput("SelectMany(SelectMany(Where -> Select new {}) -> Distinct)",
                b => getQuery(b)
                .SelectMany(family => family.Children
                    .SelectMany(child => child.Pets
                        .Where(pet => pet.GivenName == "Fluffy")
                        .Select(pet => new
                        {
                            family = family.FamilyId,
                            child = child.GivenName,
                            pet = pet.GivenName
                        })))
                    .Distinct()));

            inputs.Add(new LinqTestInput("SelectMany(SelectMany(Where -> Select new {} -> Distinct))",
                b => getQuery(b)
                .SelectMany(family => family.Children
                    .SelectMany(child => child.Pets
                        .Where(pet => pet.GivenName == "Fluffy")
                        .Select(pet => new
                        {
                            family = family.FamilyId,
                            child = child.GivenName,
                            pet = pet.GivenName
                        })
                        .Distinct()))));

            // SelectMany(Distinct)
            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Distinct",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Distinct()));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10)));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> Distinct",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10).Distinct()));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> Distinct -> OrderBy",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10).Distinct()
                    .OrderBy(f => f), ErrorMessages.OrderByCorrelatedCollectionNotSupported));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> Distinct -> OrderBy -> Where",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10).Distinct()
                    .OrderBy(f => f).Where(f => f.FamilyName.Count() < 20), ErrorMessages.OrderByCorrelatedCollectionNotSupported));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> Distinct -> OrderBy -> Where -> Take",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10).Distinct()
                    .OrderBy(f => f).Where(f => f.FamilyName.Count() < 20).Take(5), ErrorMessages.OrderByCorrelatedCollectionNotSupported));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> Distinct -> OrderBy -> Where -> Take -> Distinct",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10).Distinct()
                    .OrderBy(f => f).Where(f => f.FamilyName.Count() < 20).Take(5).Distinct(), ErrorMessages.OrderByInSubqueryNotSuppported));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> Distinct -> OrderBy -> Take",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10).Distinct()
                    .OrderBy(f => f).Take(5), ErrorMessages.OrderByCorrelatedCollectionNotSupported));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> Distinct -> OrderBy -> Take -> Distinct",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10).Distinct()
                    .OrderBy(f => f).Take(5).Distinct(), ErrorMessages.OrderByInSubqueryNotSuppported));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> Distinct -> Take",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10).Distinct().Take(5)));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> Distinct -> Take -> Distinct",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10).Distinct()
                    .Take(5).Distinct(), ErrorMessages.TopInSubqueryNotSupported));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> OrderBy",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10)
                    .OrderBy(f => f), ErrorMessages.OrderByCorrelatedCollectionNotSupported));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> OrderBy -> Distinct",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10)
                    .OrderBy(f => f).Distinct(), ErrorMessages.OrderByCorrelatedCollectionNotSupported));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> OrderBy -> Distinct -> OrderBy",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10)
                    .OrderBy(f => f).Distinct().OrderBy(f => f.GivenName.Length), ErrorMessages.OrderByCorrelatedCollectionNotSupported));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> OrderBy -> Distinct -> OrderBy -> Take",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10)
                    .OrderBy(f => f).Distinct().OrderBy(f => f.GivenName.Length).Take(5), ErrorMessages.OrderByCorrelatedCollectionNotSupported));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> OrderBy -> Distinct -> Take",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10)
                    .OrderBy(f => f).Take(5), ErrorMessages.OrderByCorrelatedCollectionNotSupported));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> OrderBy -> Take",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10)
                    .OrderBy(f => f).Take(5), ErrorMessages.OrderByCorrelatedCollectionNotSupported));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Where -> Where",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Where(f => f.FamilyName.Count() > 10)
                    .Where(f => f.FamilyName.Count() < 20)));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> OrderBy",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).OrderBy(f => f.FamilyName), ErrorMessages.OrderByCorrelatedCollectionNotSupported));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> OrderBy -> Take",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).OrderBy(f => f.FamilyName).Take(5), ErrorMessages.OrderByCorrelatedCollectionNotSupported));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Take",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Take(5)));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Select",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Select(f => f.FamilyName.Count())));

            inputs.Add(new LinqTestInput(
                "SelectMany(Distinct) -> Select -> Take",
                b => getQuery(b).SelectMany(f => f.Parents.Distinct()).Select(f => f.FamilyName.Count()).Take(5)));

            // SelectMany(Select -> Distinct)
            inputs.Add(new LinqTestInput(
                "SelectMany(Select -> Distinct) -> OrderBy -> Take",
                b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.GivenName).Distinct()).OrderBy(n => n).Take(5), ErrorMessages.OrderByCorrelatedCollectionNotSupported));

            inputs.Add(new LinqTestInput(
                "SelectMany(Select -> Distinct) -> Where -> OrderBy -> Take",
                b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.GivenName).Distinct()).Where(n => n.Count() > 10).OrderBy(n => n).Take(5), ErrorMessages.OrderByCorrelatedCollectionNotSupported));

            inputs.Add(new LinqTestInput(
                "SelectMany(Select) -> Distinct",
                b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.FamilyName)).Distinct()));

            inputs.Add(new LinqTestInput(
                "SelectMany(Select -> Distinct)",
                b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.FamilyName).Distinct())));

            inputs.Add(new LinqTestInput(
                "SelectMany(Select -> Distinct) -> Distinct",
                b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.GivenName).Distinct()).Distinct()));

            inputs.Add(new LinqTestInput(
                "SelectMany(Select -> Distinct) -> Where",
                b => getQuery(b).SelectMany(f => f.Parents.Select(p => p.GivenName).Distinct()).Where(n => n.Count() > 10)));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void ValidateDynamicLinq()
        {
            var inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("Select", b => getQuery(b).Select("FamilyId")));
            inputs.Add(new LinqTestInput("Where", b => getQuery(b).Where("FamilyId = \"some id\"")));
            inputs.Add(new LinqTestInput("Where longer", b => getQuery(b).Where("FamilyId = \"some id\" AND IsRegistered = True OR Int > 101")));
            // with parameters
            inputs.Add(new LinqTestInput("Where w/ parameters", b => getQuery(b).Where("FamilyId = @0 AND IsRegistered = @1 OR Int > @2", "some id", true, 101)));
            inputs.Add(new LinqTestInput("Where -> Select", b => getQuery(b).Where("FamilyId = \"some id\"").Select("Int")));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void ValidateLinqQueries()
        {
            DocumentCollection collection = client.CreateDocumentCollectionAsync(
                testDb.SelfLink, new DocumentCollection { Id = Guid.NewGuid().ToString("N") }).Result.Resource;

            Parent mother = new Parent { FamilyName = "Wakefield", GivenName = "Robin" };
            Parent father = new Parent { FamilyName = "Miller", GivenName = "Ben" };
            Pet pet = new Pet { GivenName = "Fluffy" };
            Child child = new Child
            {
                FamilyName = "Merriam",
                GivenName = "Jesse",
                Gender = "female",
                Grade = 1,
                Pets = new List<Pet>() { pet, new Pet() { GivenName = "koko" } },
                Things = new Dictionary<string, string>() { { "A", "B" }, { "C", "D" } }
            };

            Address address = new Address { State = "NY", County = "Manhattan", City = "NY" };
            Family family = new Family { FamilyId = "WakefieldFamily", Parents = new Parent[] { mother, father }, Children = new Child[] { child }, IsRegistered = false, Int = 3, NullableInt = 5 };

            List<Family> fList = new List<Family>();
            fList.Add(family);

            client.CreateDocumentAsync(collection.SelfLink, family).Wait();

            IOrderedQueryable<Family> query = client.CreateDocumentQuery<Family>(collection.DocumentsLink);

            IEnumerable<string> q1 = query.Select(f => f.Parents[0].FamilyName);
            Assert.AreEqual(q1.FirstOrDefault(), family.Parents[0].FamilyName);

            IEnumerable<int> q2 = query.Select(f => f.Children[0].Grade + 13);
            Assert.AreEqual(q2.FirstOrDefault(), family.Children[0].Grade + 13);

            IEnumerable<Family> q3 = query.Where(f => f.Children[0].Pets[0].GivenName == "Fluffy");
            Assert.AreEqual(q3.FirstOrDefault().FamilyId, family.FamilyId);

            IEnumerable<Family> q4 = query.Where(f => f.Children[0].Things["A"] == "B");
            Assert.AreEqual(q4.FirstOrDefault().FamilyId, family.FamilyId);

            for (int index = 0; index < 2; index++)
            {
                IEnumerable<Pet> q5 = query.Where(f => f.Children[0].Gender == "female").Select(f => f.Children[0].Pets[index]);
                Assert.AreEqual(q5.FirstOrDefault().GivenName, family.Children[0].Pets[index].GivenName);
            }

            IEnumerable<dynamic> q6 = query.SelectMany(f => f.Children.Select(c => new { Id = f.FamilyId }));
            Assert.AreEqual(q6.FirstOrDefault().Id, family.FamilyId);

            string nullString = null;
            IEnumerable<Family> q7 = query.Where(f => nullString == f.FamilyId);
            Assert.IsNull(q7.FirstOrDefault());

            object nullObject = null;
            q7 = query.Where(f => f.NullObject == nullObject);
            Assert.AreEqual(q7.FirstOrDefault().FamilyId, family.FamilyId);

            q7 = query.Where(f => f.FamilyId == nullString);
            Assert.IsNull(q7.FirstOrDefault());

            IEnumerable<Family> q8 = query.Where(f => null == f.FamilyId);
            Assert.IsNull(q8.FirstOrDefault());

            IEnumerable<Family> q9 = query.Where(f => f.IsRegistered == false);
            Assert.AreEqual(q9.FirstOrDefault().FamilyId, family.FamilyId);

            dynamic q10 = query.Where(f => f.FamilyId.Equals("WakefieldFamily")).AsEnumerable().FirstOrDefault();
            Assert.AreEqual(q10.FamilyId, family.FamilyId);

            GuidClass guidObject = new GuidClass() { Id = new Guid("098aa945-7ed8-4c50-b7b8-bd99eddb54bc") };
            client.CreateDocumentAsync(collection.SelfLink, guidObject).Wait();
            var guidData = new List<GuidClass>() { guidObject };

            var guid = client.CreateDocumentQuery<GuidClass>(collection.DocumentsLink);

            IQueryable<GuidClass> q11 = guid.Where(g => g.Id == guidObject.Id);
            Assert.AreEqual(((IEnumerable<GuidClass>)q11).FirstOrDefault().Id, guidObject.Id);

            IQueryable<GuidClass> q12 = guid.Where(g => g.Id.ToString() == guidObject.Id.ToString());
            Assert.AreEqual(((IEnumerable<GuidClass>)q12).FirstOrDefault().Id, guidObject.Id);

            ListArrayClass arrayObject = new ListArrayClass() { Id = "arrayObject", ArrayField = new int[] { 1, 2, 3 } };
            client.CreateDocumentAsync(collection.SelfLink, arrayObject).Wait();

            var listArrayQuery = client.CreateDocumentQuery<ListArrayClass>(collection.DocumentsLink);

            IEnumerable<dynamic> q13 = listArrayQuery.Where(a => a.ArrayField == arrayObject.ArrayField);
            Assert.AreEqual(q13.FirstOrDefault().Id, arrayObject.Id);

            int[] nullArray = null;
            q13 = listArrayQuery.Where(a => a.ArrayField == nullArray);
            Assert.IsNull(q13.FirstOrDefault());

            ListArrayClass listObject = new ListArrayClass() { Id = "listObject", ListField = new List<int> { 1, 2, 3 } };
            client.CreateDocumentAsync(collection.SelfLink, listObject).Wait();
            var listArrayObjectData = new List<ListArrayClass>() { arrayObject, listObject };

            IEnumerable<dynamic> q14 = listArrayQuery.Where(a => a.ListField == listObject.ListField);
            Assert.AreEqual(q14.FirstOrDefault().Id, listObject.Id);

            IEnumerable<dynamic> q15 = query.Where(f => f.NullableInt == null);
            Assert.AreEqual(q15.ToList().Count, 0);

            int? nullInt = null;
            q15 = query.Where(f => f.NullableInt == nullInt);
            Assert.AreEqual(q15.ToList().Count, 0);

            q15 = query.Where(f => f.NullableInt == 5);
            Assert.AreEqual(q15.FirstOrDefault().FamilyId, family.FamilyId);

            nullInt = 5;
            q15 = query.Where(f => f.NullableInt == nullInt);
            Assert.AreEqual(q15.FirstOrDefault().FamilyId, family.FamilyId);

            q15 = query.Where(f => f.NullableInt == nullInt.Value);
            Assert.AreEqual(q15.FirstOrDefault().FamilyId, family.FamilyId);

            nullInt = 3;
            q15 = query.Where(f => f.Int == nullInt);
            Assert.AreEqual(q15.FirstOrDefault().FamilyId, family.FamilyId);

            q15 = query.Where(f => f.Int == nullInt.Value);
            Assert.AreEqual(q15.FirstOrDefault().FamilyId, family.FamilyId);

            nullInt = null;
            q15 = query.Where(f => f.Int == nullInt);
            Assert.AreEqual(q15.ToList().Count, 0);

            var v = fList.Where(f => f.Int > nullInt).ToList();

            q15 = query.Where(f => f.Int < nullInt);

            string doc1Id = "document1:x:'!@TT){}\"";
            Document doubleQoutesDocument = new Document() { Id = doc1Id };
            client.CreateDocumentAsync(collection.DocumentsLink, doubleQoutesDocument).Wait();

            var docQuery = from book in client.CreateDocumentQuery<Document>(collection.DocumentsLink)
                           where book.Id == doc1Id
                           select book;

            Assert.AreEqual(docQuery.AsEnumerable().Single().Id, doc1Id);

            GreatFamily greatFamily = new GreatFamily() { Family = family };
            GreatGreatFamily greatGreatFamily = new GreatGreatFamily() { GreatFamilyId = Guid.NewGuid().ToString(), GreatFamily = greatFamily };
            client.CreateDocumentAsync(collection.DocumentsLink, greatGreatFamily).Wait();
            var greatGreatFamilyData = new List<GreatGreatFamily>() { greatGreatFamily };

            IOrderedQueryable<GreatGreatFamily> queryable = client.CreateDocumentQuery<GreatGreatFamily>(collection.DocumentsLink);

            IEnumerable<GreatGreatFamily> q16 = queryable.SelectMany(gf => gf.GreatFamily.Family.Children.Where(c => c.GivenName == "Jesse").Select(c => gf));

            Assert.AreEqual(q16.FirstOrDefault().GreatFamilyId, greatGreatFamily.GreatFamilyId);

            Sport sport = new Sport() { SportName = "Tennis", SportType = "Racquet" };
            client.CreateDocumentAsync(collection.DocumentsLink, sport).Wait();
            var sportData = new List<Sport>() { sport };

            var sportQuery = client.CreateDocumentQuery<Sport>(collection.DocumentsLink);

            IEnumerable<Sport> q17 = sportQuery.Where(s => s.SportName == "Tennis");

            Assert.AreEqual(sport.SportName, q17.FirstOrDefault().SportName);

            Sport2 sport2 = new Sport2() { id = "json" };
            client.CreateDocumentAsync(collection.DocumentsLink, sport2).Wait();
            var sport2Data = new List<Sport2>() { sport2 };

            var sport2Query = client.CreateDocumentQuery<Sport2>(collection.DocumentsLink);

            Func<bool, IQueryable<GuidClass>> getGuidQuery = useQuery => useQuery ? guid : guidData.AsQueryable();
            Func<bool, IQueryable<ListArrayClass>> getListArrayQuery = useQuery => useQuery ? listArrayQuery : listArrayObjectData.AsQueryable();
            Func<bool, IQueryable<GreatGreatFamily>> getGreatFamilyQuery = useQuery => useQuery ? queryable : greatGreatFamilyData.AsQueryable();
            Func<bool, IQueryable<Sport>> getSportQuery = useQuery => useQuery ? sportQuery : sportData.AsQueryable();
            Func<bool, IQueryable<Sport2>> getSport2Query = useQuery => useQuery ? sport2Query : sport2Data.AsQueryable();

            int? nullIntVal = null;
            int? nullableIntVal = 5;

            var inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("Select 1st parent family name", b => getQuery(b).Where(f => f.Parents.Count() > 0).Select(f => f.Parents[0].FamilyName)));
            inputs.Add(new LinqTestInput("Select 1st children grade expr", b => getQuery(b).Where(f => f.Children.Count() > 0).Select(f => f.Children[0].Grade + 13)));
            inputs.Add(new LinqTestInput("Filter 1st children's 1st pet name", b => getQuery(b).Where(f => f.Children.Count() > 0 && f.Children[0].Pets.Count() > 0 && f.Children[0].Pets[0].GivenName == "Fluffy")));
            inputs.Add(new LinqTestInput("Filter 1st children's thing A value", b => getQuery(b).Where(f => f.Children.Count() > 0 && f.Children[0].Things["A"] == "B")));
            inputs.Add(new LinqTestInput("Filter 1st children's gender -> Select his 1st pet", b => getQuery(b).Where(f => f.Children.Count() > 0 && f.Children[0].Pets.Count() > 0 && f.Children[0].Gender == "female").Select(f => f.Children[0].Pets[0])));
            inputs.Add(new LinqTestInput("Filter 1st children's gender -> Select his 2nd pet", b => getQuery(b).Where(f => f.Children.Count() > 0 && f.Children[0].Pets.Count() > 1 && f.Children[0].Gender == "female").Select(f => f.Children[0].Pets[1])));
            inputs.Add(new LinqTestInput("Select FamilyId of all children", b => getQuery(b).SelectMany(f => f.Children.Select(c => new { Id = f.FamilyId }))));
            inputs.Add(new LinqTestInput("Filter family with null Id", b => getQuery(b).Where(f => (string)null == f.FamilyId)));
            inputs.Add(new LinqTestInput("Filter family with null Id #2", b => getQuery(b).Where(f => f.FamilyId == (string)null)));
            inputs.Add(new LinqTestInput("Filter family with null object", b => getQuery(b).Where(f => f.NullObject == (object)null)));
            inputs.Add(new LinqTestInput("Filter family with null Id #3", b => getQuery(b).Where(f => null == f.FamilyId)));
            inputs.Add(new LinqTestInput("Filter registered family", b => getQuery(b).Where(f => f.IsRegistered == false)));
            inputs.Add(new LinqTestInput("Filter family by FamilyId", b => getQuery(b).Where(f => f.FamilyId.Equals("WakefieldFamily"))));
            inputs.Add(new LinqTestInput("Filter family nullable int", b => getQuery(b).Where(f => f.NullableInt == null)));
            inputs.Add(new LinqTestInput("Filter family nullable int #2", b => getQuery(b).Where(f => f.NullableInt == nullIntVal)));
            inputs.Add(new LinqTestInput("Filter family nullable int =", b => getQuery(b).Where(f => f.NullableInt == 5)));
            inputs.Add(new LinqTestInput("Filter nullableInt = nullInt", b => getQuery(b).Where(f => f.NullableInt == nullableIntVal)));
            inputs.Add(new LinqTestInput("Filter nullableInt = nullInt value", b => getQuery(b).Where(f => f.NullableInt == nullableIntVal.Value)));
            inputs.Add(new LinqTestInput("Filter int = nullInt", b => getQuery(b).Where(f => f.Int == nullableIntVal)));
            inputs.Add(new LinqTestInput("Filter int = nullInt value", b => getQuery(b).Where(f => f.Int == nullableIntVal.Value)));
            inputs.Add(new LinqTestInput("Filter int = nullInt", b => getQuery(b).Where(f => f.Int == nullIntVal)));
            inputs.Add(new LinqTestInput("Filter int < nullInt", b => getQuery(b).Where(f => f.Int < nullIntVal)));

            inputs.Add(new LinqTestInput("Guid filter by Id", b => getGuidQuery(b).Where(g => g.Id == guidObject.Id)));
            inputs.Add(new LinqTestInput("Guid filter by Id #2", b => getGuidQuery(b).Where(g => g.Id.ToString() == guidObject.Id.ToString())));
            inputs.Add(new LinqTestInput("Array compare", b => getListArrayQuery(b).Where(a => a.ArrayField == arrayObject.ArrayField)));
            inputs.Add(new LinqTestInput("Array compare null", b => getListArrayQuery(b).Where(a => a.ArrayField == nullArray)));
            inputs.Add(new LinqTestInput("List compare", b => getListArrayQuery(b).Where(a => a.ListField == listObject.ListField)));

            inputs.Add(new LinqTestInput("Nested great family query filter children name", b => getGreatFamilyQuery(b).SelectMany(gf => gf.GreatFamily.Family.Children.Where(c => c.GivenName == "Jesse").Select(c => gf))));
            inputs.Add(new LinqTestInput("Sport filter sport name", b => getSportQuery(b).Where(s => s.SportName == "Tennis")));
            inputs.Add(new LinqTestInput("Sport filter sport type", b => getSportQuery(b).Where(s => s.SportType == "Racquet")));
            inputs.Add(new LinqTestInput("Sport2 filter by id", b => getSport2Query(b).Where(s => s.id == "json")));
            this.ExecuteTestSuite(inputs);
        }

        internal static TValue CreateExecuteAndDeleteProcedure<TValue>(DocumentClient client,
            DocumentCollection collection,
            string transientProcedure,
            out StoredProcedureResponse<TValue> response)
        {
            // create
            StoredProcedure storedProcedure = new StoredProcedure
            {
                Id = "storedProcedure" + Guid.NewGuid().ToString(),
                Body = transientProcedure
            };
            StoredProcedure retrievedStoredProcedure = client.CreateStoredProcedureAsync(collection, storedProcedure).Result;

            // execute
            response = client.ExecuteStoredProcedureAsync<TValue>(retrievedStoredProcedure).Result;

            // delete
            client.Delete<StoredProcedure>(retrievedStoredProcedure.GetIdOrFullName());

            return response.Response;
        }

        [TestMethod]
        public void ValidateBasicQuery()
        {
            this.ValidateBasicQueryAsync().Wait();
        }

        private async Task ValidateBasicQueryAsync()
        {
            DocumentClient client = TestCommon.CreateClient(true);

            string databaseName = testDb.Id;

            List<Database> queryResults = new List<Database>();
            //Simple Equality
            IQueryable<Database> dbQuery = from db in client.CreateDatabaseQuery()
                                           where db.Id == databaseName
                                           select db;
            IDocumentQuery<Database> documentQuery = dbQuery.AsDocumentQuery();

            while (documentQuery.HasMoreResults)
            {
                FeedResponse<Database> pagedResponse = await documentQuery.ExecuteNextAsync<Database>();
                Assert.IsNotNull(pagedResponse.ResponseHeaders, "ResponseHeaders is null");
                Assert.IsNotNull(pagedResponse.ActivityId, "Query ActivityId is null");
                queryResults.AddRange(pagedResponse);
            }

            Assert.AreEqual(1, queryResults.Count);
            Assert.AreEqual(databaseName, queryResults[0].Id);

            //Logical Or 
            dbQuery = from db in client.CreateDatabaseQuery()
                      where db.Id == databaseName || db.ResourceId == testDb.ResourceId
                      select db;
            documentQuery = dbQuery.AsDocumentQuery();

            while (documentQuery.HasMoreResults)
            {
                queryResults.AddRange(await documentQuery.ExecuteNextAsync<Database>());
            }

            Assert.AreEqual(2, queryResults.Count);
            Assert.AreEqual(databaseName, queryResults[0].Id);

            //Select Property
            IQueryable<string> idQuery = from db in client.CreateDatabaseQuery()
                                         where db.Id == databaseName
                                         select db.ResourceId;
            IDocumentQuery<string> documentIdQuery = idQuery.AsDocumentQuery();

            List<string> idResults = new List<string>();
            while (documentIdQuery.HasMoreResults)
            {
                idResults.AddRange(await documentIdQuery.ExecuteNextAsync<string>());
            }

            Assert.AreEqual(1, idResults.Count);
            Assert.AreEqual(testDb.ResourceId, idResults[0]);
        }

        [TestMethod]
        public void ValidateTransformQuery()
        {
            DocumentCollection collection = new DocumentCollection
            {
                Id = Guid.NewGuid().ToString("N")
            };
            collection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;

            collection = client.Create<DocumentCollection>(testDb.ResourceId, collection);
            int documentsToCreate = 100;
            for (int i = 0; i < documentsToCreate; i++)
            {
                dynamic myDocument = new Document();
                myDocument.Id = "doc" + i;
                myDocument.Title = "MyBook"; //Simple Property.
                myDocument.Languages = new Language[] { new Language { Name = "English", Copyright = "London Publication" }, new Language { Name = "French", Copyright = "Paris Publication" } }; //Array Property
                myDocument.Author = new Author { Name = "Don", Location = "France" }; //Complex Property
                myDocument.Price = 9.99;
                myDocument = client.CreateDocumentAsync(collection.DocumentsLink, myDocument).Result;
            }

            //Read response as dynamic.
            IQueryable<dynamic> docQuery = client.CreateDocumentQuery(collection.DocumentsLink, @"select * from root r where r.Title=""MyBook""", null);

            IDocumentQuery<dynamic> DocumentQuery = docQuery.AsDocumentQuery();
            FeedResponse<dynamic> queryResponse = DocumentQuery.ExecuteNextAsync().Result;

            Assert.IsNotNull(queryResponse.ResponseHeaders, "ResponseHeaders is null");
            Assert.IsNotNull(queryResponse.ActivityId, "ActivityId is null");
            Assert.AreEqual(documentsToCreate, queryResponse.Count);

            foreach (dynamic myBook in queryResponse)
            {
                Assert.AreEqual(myBook.Title, "MyBook");
            }

            client.DeleteDocumentCollectionAsync(collection.SelfLink).Wait();
        }

        [TestMethod]
        public void ValidateDynamicDocumentQuery() //Ensure query on custom property of document.
        {
            DocumentClient client = TestCommon.CreateClient(true);

            Book myDocument = new Book();
            myDocument.Id = Guid.NewGuid().ToString();
            myDocument.Title = "My Book"; //Simple Property.
            myDocument.Languages = new Language[] { new Language { Name = "English", Copyright = "London Publication" }, new Language { Name = "French", Copyright = "Paris Publication" } }; //Array Property
            myDocument.Author = new Author { Name = "Don", Location = "France" }; //Complex Property
            myDocument.Price = 9.99;
            myDocument.Editions = new List<Edition>() { new Edition() { Name = "First", Year = 2001 }, new Edition() { Name = "Second", Year = 2005 } };

            //Create second document to make sure we have atleast one document which are filtered out of query.
            Book secondDocument = new Book
            {
                Id = Guid.NewGuid().ToString(),
                Title = "My Second Book",
                Languages = new Language[] { new Language { Name = "Spanish", Copyright = "Mexico Publication" } },
                Author = new Author { Name = "Carlos", Location = "Cancun" },
                Price = 25,
                Editions = new List<Edition>() { new Edition() { Name = "First", Year = 1970 } }
            };

            //Unfiltered execution.
            IOrderedQueryable<Book> bookDocQuery = LinqGeneralBaselineTests.client.CreateDocumentQuery<Book>(testCollection);
            Func<bool, IQueryable<Book>> getBookQuery = useQuery => useQuery ? bookDocQuery : new List<Book>().AsQueryable();

            var inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("Simple Equality on custom property",
                b => from book in getBookQuery(b)
                      where book.Title == "My Book"
                      select book));
            inputs.Add(new LinqTestInput("Nested Property access",
                b => from book in getBookQuery(b)
                      where book.Author.Name == "Don"
                      select book));
            inputs.Add(new LinqTestInput("Array references & Project Author out..",
                b => from book in getBookQuery(b)
                      where book.Languages[0].Name == "English"
                      select book.Author));
            inputs.Add(new LinqTestInput("SelectMany",
                b => getBookQuery(b).SelectMany(book => book.Languages).Where(lang => lang.Name == "French").Select(lang => lang.Copyright)));
            inputs.Add(new LinqTestInput("NumericRange query",
                b => from book in getBookQuery(b)
                      where book.Price < 10
                      select book.Author));
            inputs.Add(new LinqTestInput("Or query",
                b => from book in getBookQuery(b)
                      where book.Title == "My Book" || book.Author.Name == "Don"
                      select book));
            inputs.Add(new LinqTestInput("SelectMany query on a List type.",
                b => getBookQuery(b).SelectMany(book => book.Editions).Select(ed => ed.Name)));
            // Below samples are strictly speaking not Any equivalent. But they join and filter "all"
            // subchildren which match predicate. When SQL BE supports ANY, we can replace these with Any Flavor.
            inputs.Add(new LinqTestInput("SelectMany",
                b => getBookQuery(b).SelectMany(book =>
                           book.Languages
                           .Where(lng => lng.Name == "English")
                           .Select(lng => book.Author))));
            inputs.Add(new LinqTestInput("SelectMany",
                b => getBookQuery(b).SelectMany(book =>
                               book.Editions
                               .Where(edition => edition.Year == 2001)
                               .Select(lng => book.Author))));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void ValidateDynamicAttachmentQuery() //Ensure query on custom property of attachment.
        {
            IOrderedQueryable<SpecialAttachment2> attachmentQuery = client.CreateDocumentQuery<SpecialAttachment2>(testCollection);
            var myDocument = new Document();
            Func<bool, IQueryable<SpecialAttachment2>> getAttachmentQuery = useQuery => useQuery ? attachmentQuery : new List<SpecialAttachment2>().AsQueryable();

            var inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("Filter equality on custom property",
                b => from attachment in getAttachmentQuery(b)
                      where attachment.Title == "My Book Title2"
                      select attachment));
            inputs.Add(new LinqTestInput("Filter equality on custom property #2",
                b => from attachment in getAttachmentQuery(b)
                     where attachment.Title == "My Book Title"
                      select attachment));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestLinqTypeSystem()
        {
            Assert.AreEqual(null, TypeSystem.GetElementType(typeof(Book)));
            Assert.AreEqual(null, TypeSystem.GetElementType(typeof(Author)));

            Assert.AreEqual(typeof(Language), TypeSystem.GetElementType(typeof(Language[])));
            Assert.AreEqual(typeof(Language), TypeSystem.GetElementType(typeof(List<Language>)));
            Assert.AreEqual(typeof(Language), TypeSystem.GetElementType(typeof(IList<Language>)));
            Assert.AreEqual(typeof(Language), TypeSystem.GetElementType(typeof(IEnumerable<Language>)));
            Assert.AreEqual(typeof(Language), TypeSystem.GetElementType(typeof(ICollection<Language>)));

            Assert.AreEqual(typeof(DerivedFooItem), TypeSystem.GetElementType(typeof(DerivedFooItem[])));
            Assert.AreEqual(typeof(FooItem), TypeSystem.GetElementType(typeof(List<FooItem>)));
            Assert.AreEqual(typeof(string), TypeSystem.GetElementType(typeof(MyList<string>)));
            Assert.AreEqual(typeof(Tuple<string, string>), TypeSystem.GetElementType(typeof(MyTupleList<string>)));

            Assert.AreEqual(typeof(DerivedFooItem), TypeSystem.GetElementType(typeof(DerivedFooCollection)));
            Assert.AreEqual(typeof(string), TypeSystem.GetElementType(typeof(FooStringCollection)));

            Assert.AreEqual(typeof(FooItem), TypeSystem.GetElementType(typeof(FooTCollection<object>)));
            Assert.AreEqual(typeof(FooItem), TypeSystem.GetElementType(typeof(FooTCollection<IFooItem>)));
            Assert.AreEqual(typeof(FooItem), TypeSystem.GetElementType(typeof(FooTCollection<FooItem>)));
            Assert.AreEqual(typeof(DerivedFooItem), TypeSystem.GetElementType(typeof(FooTCollection<DerivedFooItem>)));
        }

        #region DataDocument type tests

        public class BaseDocument
        {
            [JsonProperty(PropertyName = Constants.Properties.Id)]
            public string Id { get; set; }
            public string TypeName { get; set; }
        }

        public class DataDocument : BaseDocument
        {
            public int Number { get; set; }
        }

        private class QueryHelper
        {
            private readonly DocumentClient client;
            private readonly DocumentCollection docCollection;

            public QueryHelper(DocumentClient client, DocumentCollection docCollection)
            {
                this.client = client;
                this.docCollection = docCollection;
            }

            public IQueryable<T> Query<T>() where T : BaseDocument
            {
                var query = this.client.CreateDocumentQuery<T>(this.docCollection.DocumentsLink)
                                       .Where(d => d.TypeName == "Hello");
                var queryString = query.ToString();
                return query;
            }
        }

        [TestMethod]
        public void ValidateLinqOnDataDocumentType()
        {
            DocumentCollection collection = client.CreateDocumentCollectionAsync(
                testDb.SelfLink, new DocumentCollection { Id = nameof(ValidateLinqOnDataDocumentType) }).Result.Resource;

            DataDocument doc = new DataDocument() { Id = Guid.NewGuid().ToString("N"), Number = 0, TypeName = "Hello" };
            client.CreateDocumentAsync(collection, doc).Wait();

            QueryHelper queryHelper = new QueryHelper(client, collection);
            IEnumerable<BaseDocument> result = queryHelper.Query<BaseDocument>();
            Assert.AreEqual(1, result.Count());

            BaseDocument baseDocument = result.FirstOrDefault<BaseDocument>();
            Assert.AreEqual(doc.Id, baseDocument.Id);

            BaseDocument iDocument = doc;
            IOrderedQueryable<DataDocument> q = client.CreateDocumentQuery<DataDocument>(collection.DocumentsLink);

            IEnumerable<DataDocument> iresult = from f in q
                                                where f.Id == iDocument.Id
                                                select f;
            DataDocument id = iresult.FirstOrDefault<DataDocument>();
            Assert.AreEqual(doc.Id, id.Id);
        }

        #endregion

        #region Book type tests
        public class Author
        {
            [JsonProperty(PropertyName = Constants.Properties.Id)]
            public string Name { get; set; }
            public string Location { get; set; }
        }

        public class Language
        {
            public string Name { get; set; }
            public string Copyright { get; set; }
        }

        public class Edition
        {
            public string Name { get; set; }
            public int Year { get; set; }
        }

        public class Book
        {
            //Verify that we can override the propertyName but still can query them using .NET Property names.
            [JsonProperty(PropertyName = "title")]
            public string Title { get; set; }
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }
            public Language[] Languages { get; set; }
            public Author Author { get; set; }
            public double Price { get; set; }
            [JsonProperty(PropertyName = Constants.Properties.Id)]
            public string Id { get; set; }
            public List<Edition> Editions { get; set; }
        }

        [TestMethod]
        public void ValidateServerSideQueryEvalWithPagination()
        {
            this.ValidateServerSideQueryEvalWithPaginationScenario().Wait();
        }

        private async Task ValidateServerSideQueryEvalWithPaginationScenario()
        {
            DocumentCollection collection = new DocumentCollection
            {
                Id = Guid.NewGuid().ToString()
            };
            collection.IndexingPolicy.IndexingMode = IndexingMode.Consistent;

            collection = client.Create<DocumentCollection>(
                testDb.ResourceId,
                collection);

            //Do script post to insert as many document as we could in a tight loop.
            string script = @"function() {
                var output = 0;
                var client = getContext().getCollection();
                function callback(err, docCreated) {
                    if(err) throw 'Error while creating document';
                    output++;
                    getContext().getResponse().setBody(output);
                    if(output < 50) 
                        client.createDocument(client.getSelfLink(), { id: 'testDoc' + output, title : 'My Book'}, {}, callback);                       
                };
                client.createDocument(client.getSelfLink(), { id: 'testDoc' + output, title : 'My Book'}, {}, callback); }";


            StoredProcedureResponse<int> scriptResponse = null;
            int totalNumberOfDocuments = GatewayTests.CreateExecuteAndDeleteProcedure(client, collection, script, out scriptResponse);

            int pageSize = 5;
            int totalHit = 0;
            IDocumentQuery<Book> documentQuery =
                (from book in client.CreateDocumentQuery<Book>(
                    collection.SelfLink, new FeedOptions { MaxItemCount = pageSize })
                 where book.Title == "My Book"
                 select book).AsDocumentQuery();

            while (documentQuery.HasMoreResults)
            {
                FeedResponse<dynamic> pagedResult = await documentQuery.ExecuteNextAsync();
                string isUnfiltered = pagedResult.ResponseHeaders[HttpConstants.HttpHeaders.IsFeedUnfiltered];
                Assert.IsTrue(string.IsNullOrEmpty(isUnfiltered), "Query is evaulated in client");
                Assert.IsTrue(pagedResult.Count <= pageSize, "Page size is not honored in client site eval");

                if (totalHit != 0 && documentQuery.HasMoreResults)
                {
                    //Except first page and last page we should have seen client continuation token.
                    Assert.IsFalse(pagedResult.ResponseHeaders[HttpConstants.HttpHeaders.Continuation].Contains(HttpConstants.Delimiters.ClientContinuationDelimiter),
                        "Client continuation is missing from the response continuation");
                }
                totalHit += pagedResult.Count;
            }
            Assert.AreEqual(totalHit, totalNumberOfDocuments, "Didnt get all the documents");

            //Do with default pagination.
            documentQuery =
                (from book in client.CreateDocumentQuery<Book>(
                    collection.SelfLink)
                 where book.Title == "My Book"
                 select book).AsDocumentQuery();

            totalHit = 0;

            while (documentQuery.HasMoreResults)
            {
                FeedResponse<dynamic> pagedResult = await documentQuery.ExecuteNextAsync();
                string isUnfiltered = pagedResult.ResponseHeaders[HttpConstants.HttpHeaders.IsFeedUnfiltered];
                Assert.IsTrue(string.IsNullOrEmpty(isUnfiltered), "Query is evaulated in client");
                Assert.IsTrue(pagedResult.Count == totalNumberOfDocuments, "Page size is not honored in client site eval");
                totalHit += pagedResult.Count;
            }
            Assert.AreEqual(totalHit, totalNumberOfDocuments, "Didnt get all the documents");
        }

        #endregion

        public class SpecialAttachment2 //Non attachemnt derived.
        {
            [JsonProperty(PropertyName = Constants.Properties.Id)]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "contentType")]
            public string ContentType { get; set; }

            [JsonProperty(PropertyName = Constants.Properties.MediaLink)]
            public string Media { get; set; }

            public string Author { get; set; }
            public string Title { get; set; }
        }

        #region TypeSystem test reference classes
        public interface IFooItem { }

        public class FooItem : IFooItem { }

        public class DerivedFooItem : FooItem { }

        public class MyList<T> : List<T> { }

        public class MyTupleList<T> : List<Tuple<T, T>> { }

        public class DerivedFooCollection : IList<IFooItem>, IEnumerable<DerivedFooItem>
        {
            public int IndexOf(IFooItem item)
            {
                throw new NotImplementedException();
            }

            public void Insert(int index, IFooItem item)
            {
                throw new NotImplementedException();
            }

            public void RemoveAt(int index)
            {
                throw new NotImplementedException();
            }

            public IFooItem this[int index]
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public void Add(IFooItem item)
            {
                throw new NotImplementedException();
            }

            public void Clear()
            {
                throw new NotImplementedException();
            }

            public bool Contains(IFooItem item)
            {
                throw new NotImplementedException();
            }

            public void CopyTo(IFooItem[] array, int arrayIndex)
            {
                throw new NotImplementedException();
            }

            public int Count
            {
                get { throw new NotImplementedException(); }
            }

            public bool IsReadOnly
            {
                get { throw new NotImplementedException(); }
            }

            public bool Remove(IFooItem item)
            {
                throw new NotImplementedException();
            }

            public IEnumerator<IFooItem> GetEnumerator()
            {
                throw new NotImplementedException();
            }

            IEnumerator<DerivedFooItem> IEnumerable<DerivedFooItem>.GetEnumerator()
            {
                throw new NotImplementedException();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }
        }

        public class FooStringCollection : IList<string>, IEnumerable<FooItem>
        {
            public int IndexOf(string item)
            {
                throw new NotImplementedException();
            }

            public void Insert(int index, string item)
            {
                throw new NotImplementedException();
            }

            public void RemoveAt(int index)
            {
                throw new NotImplementedException();
            }

            public string this[int index]
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public void Add(string item)
            {
                throw new NotImplementedException();
            }

            public void Clear()
            {
                throw new NotImplementedException();
            }

            public bool Contains(string item)
            {
                throw new NotImplementedException();
            }

            public void CopyTo(string[] array, int arrayIndex)
            {
                throw new NotImplementedException();
            }

            public int Count
            {
                get { throw new NotImplementedException(); }
            }

            public bool IsReadOnly
            {
                get { throw new NotImplementedException(); }
            }

            public bool Remove(string item)
            {
                throw new NotImplementedException();
            }

            public IEnumerator<string> GetEnumerator()
            {
                throw new NotImplementedException();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }

            IEnumerator<FooItem> IEnumerable<FooItem>.GetEnumerator()
            {
                throw new NotImplementedException();
            }
        }

        public class FooTCollection<T> : List<FooItem>, IEnumerable<T>
        {
            public new IEnumerator<T> GetEnumerator()
            {
                throw new NotImplementedException();
            }
        }
        #endregion

        public override LinqTestOutput ExecuteTest(LinqTestInput input)
        {
            return LinqTestsCommon.ExecuteTest(input);
        }
    }
}
