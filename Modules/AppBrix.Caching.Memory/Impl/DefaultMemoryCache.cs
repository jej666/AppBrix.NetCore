﻿// Copyright (c) MarinAtanasov. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the project root for license information.
//
using AppBrix.Caching.Memory.Configuration;
using AppBrix.Events;
using AppBrix.Events.Schedule;
using AppBrix.Lifecycle;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AppBrix.Caching.Memory.Impl
{
    internal sealed class DefaultMemoryCache : IMemoryCache, IApplicationLifecycle
    {
        #region IApplicationLifecycle implementation
        public void Initialize(IInitializeContext context)
        {
            this.app = context.App;
            this.app.GetEventHub().Subscribe<MemoryCacheCleanup>(this.RemoveExpiredEntries);
            this.scheduledArgs = this.app.GetTimerScheduledEventHub().Schedule(this.eventArgs, this.GetConfig().ExpirationCheck);
        }

        public void Uninitialize()
        {
            lock (this.cache)
            {
                this.app.GetEventHub().Unsubscribe<MemoryCacheCleanup>(this.RemoveExpiredEntries);
                this.app.GetTimerScheduledEventHub().Unschedule(this.scheduledArgs);
                this.scheduledArgs = null;
                this.cache.Keys.ToList().ForEach(this.Remove);
                this.app = null;
            }
        }
        #endregion

        #region IMemoryCache implementation
        public object Get(object key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            CacheItem cacheItem;
            lock (this.cache)
            {
                cacheItem = this.GetInternal(key);
                cacheItem?.UpdateLastAccessed(this.app.GetTime());
            }
            return cacheItem?.Item;
        }

        public void Set(object key, object item, Action dispose = null, TimeSpan absoluteExpiration = default(TimeSpan), TimeSpan slidingExpiration = default(TimeSpan))
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            if (absoluteExpiration < TimeSpan.Zero)
                throw new ArgumentException($"Negative absolute expiration: {absoluteExpiration}.");
            if (slidingExpiration < TimeSpan.Zero)
                throw new ArgumentException($"Negative sliding expiration: {slidingExpiration}.");

            var config = this.GetConfig();
            CacheItem oldItem;
            lock (this.cache)
            {
                oldItem = this.GetInternal(key);
                this.cache[key] = new CacheItem(item, dispose,
                    absoluteExpiration > TimeSpan.Zero ? absoluteExpiration : config.DefaultAbsoluteExpiration,
                    slidingExpiration > TimeSpan.Zero ? slidingExpiration : config.DefaultSlidingExpiration,
                    this.app.GetTime());
            }
            oldItem?.Dispose();
        }

        public void Remove(object key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            CacheItem item;

            lock (this.cache)
            {
                item = this.GetInternal(key);
                if (item != null)
                {
                    this.cache.Remove(key);
                }
            }

            item?.Dispose();
        }
        #endregion

        #region Private methods
        private CacheItem GetInternal(object key)
        {
            this.cache.TryGetValue(key, out var result);
            return result;
        }

        private void RemoveExpiredEntries(MemoryCacheCleanup unused)
        {
            List<KeyValuePair<object, CacheItem>> toRemove;
            lock (this.cache)
            {
                if (this.app == null)
                    return; // Unintialized

                var now = this.app.GetTime();

                toRemove = this.cache.Where(x => x.Value.HasExpired(now)).ToList();
                toRemove.ForEach(x => this.cache.Remove(x.Key));
                this.scheduledArgs = this.app.GetTimerScheduledEventHub().Schedule(this.eventArgs, this.GetConfig().ExpirationCheck);
            }
            toRemove.ForEach(x => this.RunSafe(x.Value.Dispose));
        }

        private void RunSafe(Action action)
        {
            try { action(); }
            catch (Exception) { }
        }

        private MemoryCachingConfig GetConfig()
        {
            return (MemoryCachingConfig)this.app.ConfigService.Get(typeof(MemoryCachingConfig));
        }
        #endregion

        #region Private fields and constants
        private readonly Dictionary<object, CacheItem> cache = new Dictionary<object, CacheItem>();
        private readonly MemoryCacheCleanup eventArgs = new MemoryCacheCleanup();
        private IScheduledEvent<MemoryCacheCleanup> scheduledArgs;
        private IApp app;
        #endregion

        #region Private classes
        private sealed class MemoryCacheCleanup : IEvent
        {
        }
        #endregion
    }
}
