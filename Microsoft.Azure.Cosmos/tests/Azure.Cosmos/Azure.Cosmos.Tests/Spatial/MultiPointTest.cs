﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Test.Spatial
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Azure.Cosmos.Spatial;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests <see cref="MultiPoint"/> class and serialization.
    /// </summary>
    [TestClass]
    public class MultiPointTest
    {
        private JsonSerializerOptions restContractOptions;
        public MultiPointTest()
        {
            this.restContractOptions = new JsonSerializerOptions();
            CosmosTextJsonSerializer.InitializeDataContractConverters(this.restContractOptions);
        }

        /// <summary>
        /// Tests serialization/deserialization
        /// </summary>
        [TestMethod]
        public void TestMultiPointSerialization()
        {
            string json =
                @"{
                    ""type"":""MultiPoint"",
                    ""coordinates"":[[20,30], [30, 40]],
                    ""bbox"":[20, 20, 30, 30],
                    ""extra"":1,
                    ""crs"":{""type"":""name"", ""properties"":{""name"":""hello""}}}";
            var multiPoint = JsonSerializer.Deserialize<MultiPoint>(json, this.restContractOptions);

            Assert.AreEqual(new Position(20, 30), multiPoint.Points[0]);
            Assert.AreEqual(new Position(30, 40), multiPoint.Points[1]);

            Assert.AreEqual(new Position(20, 20), multiPoint.BoundingBox.Min);
            Assert.AreEqual(new Position(30, 30), multiPoint.BoundingBox.Max);
            Assert.AreEqual("hello", ((NamedCrs)multiPoint.Crs).Name);
            Assert.AreEqual(1, multiPoint.AdditionalProperties.Count);
            Assert.AreEqual(1L, multiPoint.AdditionalProperties["extra"]);

            var geom = JsonSerializer.Deserialize<Geometry>(json, this.restContractOptions);
            Assert.AreEqual(GeometryType.MultiPoint, geom.Type);

            Assert.AreEqual(geom, multiPoint);

            string json1 = JsonSerializer.Serialize(multiPoint, this.restContractOptions);
            var geom1 = JsonSerializer.Deserialize<Geometry>(json1, this.restContractOptions);
            Assert.AreEqual(geom1, geom);
        }

        /// <summary>
        /// Tests equality/hash code.
        /// </summary>
        [TestMethod]
        public void TestMultiPointEqualsHashCode()
        {
            var multiPoint1 = new MultiPoint(
                new[] { new Position(20, 30), new Position(30, 40) },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            var multiPoint2 = new MultiPoint(
                new[] { new Position(20, 30), new Position(30, 40) },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            var multiPoint3 = new MultiPoint(
                new[] { new Position(20, 30), new Position(31, 40) },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            var multiPoint4 = new MultiPoint(
                new[] { new Position(20, 30), new Position(30, 40) },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "b", "c" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            var multiPoint5 = new MultiPoint(
                new[] { new Position(20, 30), new Position(30, 40) },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 41)),
                    Crs = Crs.Named("SomeCrs")
                });

            var multiPoint6 = new MultiPoint(
                new[] { new Position(20, 30), new Position(30, 40) },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs1")
                });

            Assert.AreEqual(multiPoint1, multiPoint2);
            Assert.AreEqual(multiPoint1.GetHashCode(), multiPoint2.GetHashCode());

            Assert.AreNotEqual(multiPoint1, multiPoint3);
            Assert.AreNotEqual(multiPoint1.GetHashCode(), multiPoint3.GetHashCode());

            Assert.AreNotEqual(multiPoint1, multiPoint4);
            Assert.AreNotEqual(multiPoint1.GetHashCode(), multiPoint4.GetHashCode());

            Assert.AreNotEqual(multiPoint1, multiPoint5);
            Assert.AreNotEqual(multiPoint1.GetHashCode(), multiPoint5.GetHashCode());

            Assert.AreNotEqual(multiPoint1, multiPoint6);
            Assert.AreNotEqual(multiPoint1.GetHashCode(), multiPoint6.GetHashCode());
        }

        /// <summary>
        /// Tests constructor exceptions.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestMultiPointConstructorNullException()
        {
            new MultiPoint(null);
        }

        /// <summary>
        /// Tests construction.
        /// </summary>
        [TestMethod]
        public void TestMultiPointConstructors()
        {
            var multiPoint = new MultiPoint(
                new[] { new Position(20, 30), new Position(30, 40) },
                new GeometryParams
                {
                    AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                    BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                    Crs = Crs.Named("SomeCrs")
                });

            Assert.AreEqual(new Position(20, 30), multiPoint.Points[0]);
            Assert.AreEqual(new Position(0, 0), multiPoint.BoundingBox.Min);
            Assert.AreEqual(new Position(40, 40), multiPoint.BoundingBox.Max);
            Assert.AreEqual("b", multiPoint.AdditionalProperties["a"]);
            Assert.AreEqual("SomeCrs", ((NamedCrs)multiPoint.Crs).Name);
        }
    }
}
