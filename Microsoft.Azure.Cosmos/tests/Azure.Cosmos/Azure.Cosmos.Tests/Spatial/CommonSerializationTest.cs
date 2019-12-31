﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Test.Spatial
{
    using System.Text.Json;
    using Azure.Cosmos.Spatial;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Serialization tests applying to all geometry types.
    /// </summary>
    [TestClass]
    public class CommonSerializationTest
    {
        /// <summary>
        /// Tests that incorrect JSON throws exception.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(JsonException))]
        public void TestInvalidJson()
        {
            string json = @"{""type"":""Poi}";
            var point = JsonSerializer.Deserialize<Point>(json);
        }

        /// <summary>
        /// Tests that coordinates cannot be null.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(JsonException))]
        public void TestNullCoordinates()
        {
            string json = @"{""type"":""Point"",""coordinates"":null}";
            var point = JsonSerializer.Deserialize<Point>(json);
        }

        /// <summary>
        /// Tests that type cannot be null.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(JsonException))]
        public void TestNullType()
        {
            string json = @"{""type"":null, ""coordinates"":[20, 30]}";
            var point = JsonSerializer.Deserialize<Point>(json);
        }

        /// <summary>
        /// Tests that type cannot be null and uses <see cref="GeometryJsonConverter"/>.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(JsonException))]
        public void TestNullTypeGeometry()
        {
            string json = @"{""type"":null, ""coordinates"":[20, 30]}";
            var point = JsonSerializer.Deserialize<Geometry>(json);
        }

        /// <summary>
        /// Tests bounding box with not even number of coordinates.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(JsonException))]
        public void TestBoundingBoxWithNonEvenNumberOfCoordinates()
        {
            string json = @"{""type"":""Point"", ""coordinates"":[20, 30], ""bbox"":[0, 0, 0, 5, 5]}";
            var point = JsonSerializer.Deserialize<Point>(json);
        }

        /// <summary>
        /// Tests bounding box with insufficient number of coordinates.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(JsonException))]
        public void TestBoundingBoxWithNotEnoughCoordinates()
        {
            string json = @"{""type"":""Point"", ""coordinates"":[20, 30], ""bbox"":[0, 0]}";
            var point = JsonSerializer.Deserialize<Point>(json);
        }

        /// <summary>
        /// Tests bounding box which is null.
        /// </summary>
        [TestMethod]
        public void TestNullBoundingBox()
        {
            string json = @"{""type"":""Point"", ""coordinates"":[20, 30], ""bbox"":null}";
            var point = JsonSerializer.Deserialize<Point>(json);
            Assert.IsNull(point.BoundingBox);
        }

        /// <summary>
        /// Tests empty coordinates.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(JsonException))]
        public void TestEmptyCoordinates()
        {
            string json = @"{
                    ""type"":""Point"",
                    ""coordinates"":[],
                    }";
            JsonSerializer.Deserialize<Point>(json);
        }
    }
}
