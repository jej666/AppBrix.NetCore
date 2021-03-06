﻿// Copyright (c) MarinAtanasov. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the project root for license information.
//
using AppBrix.Caching.Memory;
using System;
using System.Linq;

namespace AppBrix
{
    /// <summary>
    /// Extension methods for easier manipulation of AppBrix memory caches.
    /// </summary>
    public static class MemoryCacheExtensions
    {
        /// <summary>
        /// Gets the currently registered local in-memory cache.
        /// This can be used for objects which are long running and should
        /// be disposed after absolute or sliding expiration time.
        /// </summary>
        /// <param name="app">The currently running application.</param>
        /// <returns>The local in-memory cache.</returns>
        public static IMemoryCache GetMemoryCache(this IApp app)
        {
            return (IMemoryCache)app.Get(typeof(IMemoryCache));
        }

        /// <summary>
        /// Gets a cached object by its key.
        /// </summary>
        /// <typeparam name="T">The type of the item to return.</typeparam>
        /// <param name="cache">The local in-memory cache.</param>
        /// <param name="key">The key which is used to store the object in the cache.</param>
        /// <returns>The cached object. Returns null if no object is found.</returns>
        public static T Get<T>(this IMemoryCache cache, object key)
        {
            return (T)(cache.Get(key) ?? default(T));
        }
    }
}
