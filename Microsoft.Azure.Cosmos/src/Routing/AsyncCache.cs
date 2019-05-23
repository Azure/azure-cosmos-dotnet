﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Common
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Cache which supports asynchronous value initialization.
    /// It ensures that for given key only single inintialization funtion is running at any point in time.
    /// </summary>
    /// <typeparam name="TKey">Type of keys.</typeparam>
    /// <typeparam name="TValue">Type of values.</typeparam>
    internal sealed class AsyncCache<TKey, TValue>
    {
        private readonly ConcurrentDictionary<TKey, AsyncLazy<TValue>> values;

        private readonly IEqualityComparer<TValue> valueEqualityComparer;

        public AsyncCache(IEqualityComparer<TValue> valueEqualityComparer, IEqualityComparer<TKey> keyEqualityComparer = null)
        {
            this.values = new ConcurrentDictionary<TKey, AsyncLazy<TValue>>(keyEqualityComparer ?? EqualityComparer<TKey>.Default);
            this.valueEqualityComparer = valueEqualityComparer;
        }

        public AsyncCache()
            : this(EqualityComparer<TValue>.Default)
        {
        }

        public ICollection<TKey> Keys
        {
            get
            {
                return this.values.Keys;
            }
        }

        public void Set(TKey key, TValue value)
        {
            AsyncLazy<TValue> lazyValue = new AsyncLazy<TValue>(() => value, CancellationToken.None);

            // Access it to mark as created+completed, so that further calls to getasync do not overwrite.
            TValue x = lazyValue.Value.Result;

            this.values.AddOrUpdate(key, lazyValue, (k, existingValue) =>
            {
                // Observe all exceptions thrown for existingValue.
                if (existingValue.IsValueCreated)
                {
                    Task unused = existingValue.Value.ContinueWith(c => c.Exception, TaskContinuationOptions.OnlyOnFaulted);
                }

                return lazyValue;
            });
        }

        /// <summary>
        /// <para>
        /// Gets value corresponding to <paramref name="key"/>.
        /// </para>
        /// <para>
        /// If another initialization function is already running, new initialization function will not be started.
        /// The result will be result of currently running initialization function.
        /// </para>
        /// <para>
        /// If previous initialization function is successfully completed - value returned by it will be returned unless
        /// it is equal to <paramref name="obsoleteValue"/>, in which case new initialization function will be started.
        /// </para>
        /// <para>
        /// If previous initialization function failed - new one will be launched.
        /// </para>
        /// </summary>
        /// <param name="key">Key for which to get a value.</param>
        /// <param name="obsoleteValue">Value which is obsolete and needs to be refreshed.</param>
        /// <param name="singleValueInitFunc">Initialization function.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="forceRefresh">Skip cached value and generate new value.</param>
        /// <returns>Cached value or value returned by initialization function.</returns>
        public async Task<TValue> GetAsync(
           TKey key,
           TValue obsoleteValue,
           Func<Task<TValue>> singleValueInitFunc,
           CancellationToken cancellationToken,
           bool forceRefresh = false)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AsyncLazy<TValue> initialLazyValue;

            // Check if we have a generator for that value and it's running/ran.
            // If not, we prefer to the use the generator passed-in, to let old closures go.
            if (this.values.TryGetValue(key, out initialLazyValue) && initialLazyValue.IsValueCreated)
            {
                // If we're currently computing a value, then return it...
                if (!initialLazyValue.Value.IsCompleted)
                {
                    try
                    {
                        return await initialLazyValue.Value;
                    }

                    // It does not matter to us if this instance of the task throws - the lambda that failed was provided by a different caller.
                    // The exception that we see here will be handled/logged by whatever caller provided the failing lambda, if any. Our part is catching and observing it.
                    // As such, we discard this exception and will retry with our own lambda below, for which we will let exception bubble up.
                    catch
                    {
                    }
                }

                // Don't check Task if there's an exception or it's been canceled. Accessing Task.Exception marks it as observed, which we want.
                else if (initialLazyValue.Value.Exception == null && !initialLazyValue.Value.IsCanceled)
                {
                    TValue cachedValue = await initialLazyValue.Value;

                    // If not forcing refresh or obsolete value, use cached value.
                    if (!forceRefresh && !this.valueEqualityComparer.Equals(cachedValue, obsoleteValue))
                    {
                        return cachedValue;
                    }
                }
            }

            AsyncLazy<TValue> newLazyValue = new AsyncLazy<TValue>(singleValueInitFunc, cancellationToken);

            // Update the new task in the cache - compare-and-swap style.
            AsyncLazy<TValue> actualValue = this.values.AddOrUpdate(
                key,
                newLazyValue,
                (existingKey, existingValue) => object.ReferenceEquals(existingValue, initialLazyValue) ? newLazyValue : existingValue);

            // Task starts running here.
            Task<TValue> generator = actualValue.Value;

            // Even if the current thread goes away, all exceptions will be observed.
            Task unused = generator.ContinueWith(c => c.Exception, TaskContinuationOptions.OnlyOnFaulted);

            return await generator;
        }

        public void Remove(TKey key)
        {
            AsyncLazy<TValue> initialLazyValue;

            if (this.values.TryRemove(key, out initialLazyValue) && initialLazyValue.IsValueCreated)
            {
                // Observe all exceptions thrown.
                Task unused = initialLazyValue.Value.ContinueWith(c => c.Exception, TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        public bool TryRemoveIfCompleted(TKey key)
        {
            AsyncLazy<TValue> initialLazyValue;

            if (this.values.TryGetValue(key, out initialLazyValue) && initialLazyValue.IsValueCreated && initialLazyValue.Value.IsCompleted)
            {
                // Accessing Exception marks as observed.
                Exception e = initialLazyValue.Value.Exception;

                // This is a nice trick to do "atomic remove if value not changed".
                // ConcurrentDictionary inherits from ICollection<KVP<..>>, which allows removal of specific key value pair, instead of removal just by key.
                ICollection<KeyValuePair<TKey, AsyncLazy<TValue>>> valuesAsCollection = this.values as ICollection<KeyValuePair<TKey, AsyncLazy<TValue>>>;
                Debug.Assert(valuesAsCollection != null, "Values collection expected to implement ICollection<KVP<TKey, AsyncLazy<TValue>>.");
                return valuesAsCollection?.Remove(new KeyValuePair<TKey, AsyncLazy<TValue>>(key, initialLazyValue)) ?? false;
            }

            return false;
        }

        /// <summary>
        /// Remove value from cache and return it if present.
        /// </summary>
        /// <param name="key"></param>
        /// <returns>Value if present, default value if not present.</returns>
        public async Task<TValue> RemoveAsync(TKey key)
        {
            AsyncLazy<TValue> initialLazyValue;
            if (this.values.TryRemove(key, out initialLazyValue))
            {
                try
                {
                    return await initialLazyValue.Value;
                }
                catch
                {
                }
            }

            return default(TValue);
        }

        public void Clear()
        {
            // Ensure all tasks are observed.
            foreach (AsyncLazy<TValue> value in this.values.Values)
            {
                if (value.IsValueCreated)
                {
                    Task unused = value.Value.ContinueWith(c => c.Exception, TaskContinuationOptions.OnlyOnFaulted);
                }
            }

            this.values.Clear();
        }

        /// <summary>
        /// Runs a background task that will started refreshing the cached value for a given key.
        /// This observes the same logic as GetAsync - a running value will still take precedence over a call to this.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="singleValueInitFunc">Generator function.</param>
        public void BackgroundRefreshNonBlocking(TKey key, Func<Task<TValue>> singleValueInitFunc)
        {
            // Trigger background refresh of cached value.
            // Fire and forget.
            Task unused = Task.Factory.StartNewOnCurrentTaskSchedulerAsync(async () =>
            {
                try
                {
                    AsyncLazy<TValue> initialLazyValue;

                    // If we don't have a value, or we have one that has completed running (i.e. if a value is currently being generated, we do nothing).
                    if (!this.values.TryGetValue(key, out initialLazyValue) || (initialLazyValue.IsValueCreated && initialLazyValue.Value.IsCompleted))
                    {
                        // Use GetAsync to trigger the generation of a value.
                        await this.GetAsync(
                            key,
                            default(TValue), // obsolete value unused since forceRefresh: true
                            singleValueInitFunc,
                            CancellationToken.None,
                            forceRefresh: true);
                    }
                }
                catch
                {
                    // Observe all exceptions.
                }
            }).Unwrap();
        }
    }
}
