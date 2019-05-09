﻿namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class CosmosSpatialTests
    {
        private CosmosContainerCore Container = null;
        private DocumentClient documentClient;
        private CosmosDefaultJsonSerializer jsonSerializer = null;
        private readonly string spatialName = "SpatialName";
        protected CancellationTokenSource cancellationTokenSource = null;
        protected CancellationToken cancellationToken;
        protected CosmosClient cosmosClient = null;
        protected CosmosDatabase database = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.cancellationToken = this.cancellationTokenSource.Token;

            this.cosmosClient = TestCommon.CreateCosmosClient();
            this.database = await this.cosmosClient.Databases.CreateDatabaseAsync(Guid.NewGuid().ToString(),
                cancellationToken: this.cancellationToken);

            this.documentClient = TestCommon.CreateClient(true, defaultConsistencyLevel: Documents.ConsistencyLevel.Session);

            string PartitionKey = "/partitionKey";
            ContainerResponse response = await this.database.Containers.CreateContainerAsync(
                new CosmosContainerSettings(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = (CosmosContainerCore)response;
            this.jsonSerializer = new CosmosDefaultJsonSerializer();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (this.documentClient != null)
            {
                this.documentClient.Dispose();
            }

            if (this.cosmosClient == null)
            {
                return;
            }

            if (this.database != null)
            {
                await this.database.DeleteAsync(
                    requestOptions: null,
                    cancellationToken: this.cancellationToken);
            }

            this.cancellationTokenSource?.Cancel();

            this.cosmosClient.Dispose();
        }

        [TestMethod]
        public async Task CreateDropMultiPolygonTest()
        {
            SpatialItem spatialItem = new SpatialItem
            {
                Name = spatialName,
                partitionKey = Guid.NewGuid().ToString(),
                id = Guid.NewGuid().ToString(),
                multiPolygon = this.GetMultiPoygon(),
            };

            CosmosItemResponse<SpatialItem> createResponse = await this.Container.Items.CreateItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, item: spatialItem);
            Assert.IsNotNull(createResponse);
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

            CosmosItemResponse<SpatialItem> readResponse = await this.Container.Items.ReadItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, id: spatialItem.id);
            Assert.IsNotNull(readResponse);
            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            Assert.IsNotNull(readResponse.Resource.multiPolygon);

            IOrderedQueryable<SpatialItem> multipolygonQuery =
              this.documentClient.CreateDocumentQuery<SpatialItem>(this.Container.LinkUri.OriginalString, new FeedOptions() { EnableScanInQuery = true, EnableCrossPartitionQuery = true });
            SpatialItem[] withinQuery = multipolygonQuery
              .Where(f => f.multiPolygon.Within(this.GetMultiPoygon()) && f.multiPolygon.IsValid())
              .ToArray();
            Assert.IsTrue(withinQuery.Length == 1);
            foreach (SpatialItem item in withinQuery)
            {
                Assert.IsTrue(item.multiPolygon.Equals(this.GetMultiPoygon()));
            }

            CosmosItemResponse<SpatialItem> deleteResponse = await this.Container.Items.DeleteItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, id: spatialItem.id);
            Assert.IsNotNull(deleteResponse);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        [TestMethod]
        public async Task CreateDropPolygonTest()
        {
            SpatialItem spatialItem = new SpatialItem
            {
                Name = spatialName,
                partitionKey = Guid.NewGuid().ToString(),
                id = Guid.NewGuid().ToString(),
                polygon = this.GetPolygon(),
            };

            CosmosItemResponse<SpatialItem> createResponse = await this.Container.Items.CreateItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, item: spatialItem);
            Assert.IsNotNull(createResponse);
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

            CosmosItemResponse<SpatialItem> readResponse = await this.Container.Items.ReadItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, id: spatialItem.id);
            Assert.IsNotNull(readResponse);
            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            Assert.IsNotNull(readResponse.Resource.polygon);

            CosmosItemResponse<SpatialItem> deleteResponse = await this.Container.Items.DeleteItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, id: spatialItem.id);
            Assert.IsNotNull(deleteResponse);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        [TestMethod]
        public async Task CreateDropLineStringTest()
        {
            SpatialItem spatialItem = new SpatialItem
            {
                Name = spatialName,
                partitionKey = Guid.NewGuid().ToString(),
                id = Guid.NewGuid().ToString(),
                lineString = this.GetLineString(),
            };

            CosmosItemResponse<SpatialItem> createResponse = await this.Container.Items.CreateItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, item: spatialItem);
            Assert.IsNotNull(createResponse);
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

            CosmosItemResponse<SpatialItem> readResponse = await this.Container.Items.ReadItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, id: spatialItem.id);
            Assert.IsNotNull(readResponse);
            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            Assert.IsNotNull(readResponse.Resource.lineString);

            CosmosItemResponse<SpatialItem> deleteResponse = await this.Container.Items.DeleteItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, id: spatialItem.id);
            Assert.IsNotNull(deleteResponse);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        [TestMethod]
        public async Task CreateDropPointTest()
        {
            SpatialItem spatialItem = new SpatialItem
            {
                Name = spatialName,
                partitionKey = Guid.NewGuid().ToString(),
                id = Guid.NewGuid().ToString(),
                point = this.GetPoint(),
            };
            CosmosItemResponse<SpatialItem> createResponse = await this.Container.Items.CreateItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, item: spatialItem);
            Assert.IsNotNull(createResponse);
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

            CosmosItemResponse<SpatialItem> readResponse = await this.Container.Items.ReadItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, id: spatialItem.id);
            Assert.IsNotNull(readResponse);
            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            Assert.IsNotNull(readResponse.Resource.point);

            CosmosItemResponse<SpatialItem> deleteResponse = await this.Container.Items.DeleteItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, id: spatialItem.id);
            Assert.IsNotNull(deleteResponse);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        internal class SpatialItem
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            public string id { get; set; }


            [JsonProperty("point")]
            public Point point { get; set; }

            [JsonProperty("polygon")]
            public Polygon polygon { get; set; }

            [JsonProperty("lineString")]
            public LineString lineString { get; set; }

            [JsonProperty("partitionKey")]
            public string partitionKey { get; set; }

            [JsonProperty("MultiPointLocation")]
            public MultiPolygon multiPolygon { get; set; }

        }

        private MultiPolygon GetMultiPoygon()
        {
            MultiPolygon multiPolygon =
           new MultiPolygon(
           new[]
               {
                    new PolygonCoordinates(
                            new[]
                                {
                                    new LinearRing(
                                        new[]
                                            {
                                                new Position(20, 20), new Position(20, 21), new Position(21, 21),
                                                new Position(21, 20), new Position(20, 20)
                                            })
                                })
               });

            return multiPolygon;
        }

        private Polygon GetPolygon()
        {
            Polygon polygon = new Polygon(
                          new[]
        {
                        new LinearRing(
                            new[]
                                {
                                    new Position(20, 20),
                                    new Position(20, 21),
                                    new Position(21, 21),
                                    new Position(21, 20),
                                    new Position(22, 20)
                                })
        },
                          new GeometryParams
                          {
                              AdditionalProperties = new Dictionary<string, object> { { "b", "c" } },
                              BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                              Crs = Crs.Named("SomeCrs")
                          });
            return polygon;
        }

        private LineString GetLineString()
        {
            LineString lineString = new LineString(
                             new[] {
                                 new Position(20, 30), new Position(30, 40) },
                                 new GeometryParams
                                 {
                                     AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                                     BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                                     Crs = Crs.Named("SomeCrs")
                                 });
            return lineString;
        }

        private Point GetPoint()
        {
            Point point = new Point(
                        new Position(20, 30),
                        new GeometryParams
                        {
                            AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                            BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                            Crs = Crs.Named("SomeCrs")
                        });
            return point;
        }
    }
}
