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
    public class MultiPointTest : CommonSerializationTest
    {
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
                    ""bbox"":[20, 20, 30, 30]
                }";
            MultiPoint multiPoint = JsonSerializer.Deserialize<MultiPoint>(json, this.restContractOptions);

            Assert.AreEqual(new Position(20, 30), multiPoint.Points[0]);
            Assert.AreEqual(new Position(30, 40), multiPoint.Points[1]);

            Assert.AreEqual(new Position(20, 20), multiPoint.BoundingBox.Min);
            Assert.AreEqual(new Position(30, 30), multiPoint.BoundingBox.Max);

            Geometry geom = JsonSerializer.Deserialize<Geometry>(json, this.restContractOptions);
            Assert.AreEqual(GeometryType.MultiPoint, geom.Type);

            Assert.AreEqual(geom, multiPoint);

            string json1 = JsonSerializer.Serialize(multiPoint, this.restContractOptions);
            Geometry geom1 = JsonSerializer.Deserialize<Geometry>(json1, this.restContractOptions);
            Assert.AreEqual(geom1, geom);
        }

        /// <summary>
        /// Tests equality/hash code.
        /// </summary>
        [TestMethod]
        public void TestMultiPointEqualsHashCode()
        {
            MultiPoint multiPoint1 = new MultiPoint(
                new[] { new Position(20, 30), new Position(30, 40) },
                new BoundingBox(new Position(0, 0), new Position(40, 40)));

            MultiPoint multiPoint2 = new MultiPoint(
                new[] { new Position(20, 30), new Position(30, 40) },
                new BoundingBox(new Position(0, 0), new Position(40, 40)));

            MultiPoint multiPoint3 = new MultiPoint(
                new[] { new Position(20, 30), new Position(31, 40) },
                new BoundingBox(new Position(0, 0), new Position(40, 40)));

            MultiPoint multiPoint4 = new MultiPoint(
                new[] { new Position(20, 30), new Position(30, 40) },
                new BoundingBox(new Position(0, 0), new Position(40, 40)));

            MultiPoint multiPoint5 = new MultiPoint(
                new[] { new Position(20, 30), new Position(30, 40) },
                new BoundingBox(new Position(0, 0), new Position(40, 41)));

            MultiPoint multiPoint6 = new MultiPoint(
                new[] { new Position(20, 30), new Position(30, 40) },
                new BoundingBox(new Position(0, 0), new Position(40, 40)));

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
            MultiPoint multiPoint = new MultiPoint(
                new[] { new Position(20, 30), new Position(30, 40) },
                new BoundingBox(new Position(0, 0), new Position(40, 40)));

            Assert.AreEqual(new Position(20, 30), multiPoint.Points[0]);
            Assert.AreEqual(new Position(0, 0), multiPoint.BoundingBox.Min);
            Assert.AreEqual(new Position(40, 40), multiPoint.BoundingBox.Max);
        }
    }
}
