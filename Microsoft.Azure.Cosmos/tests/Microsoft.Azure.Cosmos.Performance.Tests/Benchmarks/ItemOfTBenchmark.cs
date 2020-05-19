﻿// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Performance.Tests.Benchmarks
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos;

    public class ItemOfTBenchmark : IItemBenchmark
    {
        public ItemBenchmarkHelper BenchmarkHelper { get; set; }

        /// <summary>
        /// Benchmark for CreateItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task CreateItem()
        {
            ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.TestContainer.CreateItemAsync<ToDoActivity>(
                this.BenchmarkHelper.TestItem,
                ItemBenchmarkHelper.ExistingPartitionId);

            if ((int)response.StatusCode > 300 || response.Resource == null)
            {
                throw new Exception();
            }
        }

        /// <summary>
        /// Benchmark for UpsertItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task UpsertItem()
        {
            ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.TestContainer.UpsertItemAsync<ToDoActivity>(
                this.BenchmarkHelper.TestItem,
                ItemBenchmarkHelper.ExistingPartitionId);

            if ((int)response.StatusCode > 300 || response.Resource == null)
            {
                throw new Exception();
            }
        }

        /// <summary>
        /// Benchmark for ReadItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task ReadItemNotExists()
        {
            try
            {
                ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.TestContainer.ReadItemAsync<ToDoActivity>(
                    ItemBenchmarkHelper.NonExistingItemId,
                    ItemBenchmarkHelper.ExistingPartitionId);
                throw new Exception();
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
            }
        }

        /// <summary>
        /// Benchmark for ReadItemStreamAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task ReadItemExists()
        {
            ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.TestContainer.ReadItemAsync<ToDoActivity>(
                ItemBenchmarkHelper.ExistingItemId,
                ItemBenchmarkHelper.ExistingPartitionId);

            if ((int)response.StatusCode > 300 || response.Resource == null)
            {
                throw new Exception();
            }
        }

        /// <summary>
        /// Benchmark for ReplaceItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task UpdateItem()
        {
            ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.TestContainer.ReplaceItemAsync<ToDoActivity>(
                this.BenchmarkHelper.TestItem,
                ItemBenchmarkHelper.ExistingItemId,
                ItemBenchmarkHelper.ExistingPartitionId);

            if ((int)response.StatusCode > 300 || response.Resource == null)
            {
                throw new Exception();
            }
        }

        /// <summary>
        /// Benchmark for DeleteItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task DeleteItemExists()
        {
            ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.TestContainer.DeleteItemAsync<ToDoActivity>(
                ItemBenchmarkHelper.ExistingItemId,
                ItemBenchmarkHelper.ExistingPartitionId);

            if ((int)response.StatusCode > 300 || response.Resource == null)
            {
                throw new Exception();
            }
        }

        /// <summary>
        /// Benchmark for DeleteItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task DeleteItemNotExists()
        {
            try
            {
                ItemResponse<ToDoActivity> response = await this.BenchmarkHelper.TestContainer.DeleteItemAsync<ToDoActivity>(
                    ItemBenchmarkHelper.NonExistingItemId,
                    ItemBenchmarkHelper.ExistingPartitionId);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
            }
        }

        /// <summary>
        /// Benchmark for DeleteItemAsync.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Benchmark]
        public async Task ReadFeed()
        {
            FeedIterator<ToDoActivity> resultIterator = this.BenchmarkHelper.TestContainer.GetItemQueryIterator<ToDoActivity>();
            while (resultIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> response = await resultIterator.ReadNextAsync();
                if (response.StatusCode != HttpStatusCode.OK || response.Resource.Count() == 0)
                {
                    throw new Exception();
                }
            }
        }
    }
}
