// Copyright (c) MarinAtanasov. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the project root for license information.
//
using AppBrix.Factory;
using System;
using System.Linq;

namespace AppBrix
{
    /// <summary>
    /// Extension methods for easier manipulation of AppBrix factories.
    /// </summary>
    public static class FactoryExtensions
    {
        /// <summary>
        /// Gets the currently loaded factory.
        /// </summary>
        /// <param name="app">The current application.</param>
        /// <returns>The factory.</returns>
        public static IFactory GetFactory(this IApp app)
        {
            return (IFactory)app.Get(typeof(IFactory));
        }

        /// <summary>
        /// Registers a factory method for the specified type.
        /// </summary>
        /// <typeparam name="T">The type to be returned by the factory.</typeparam>
        /// <param name="factory">The factory.</param>
        /// <param name="factoryMethod">The factory method.</param>
        public static void Register<T>(this IFactory factory, Func<T> factoryMethod)
        {
            factory.Register((Func<object>)(object)factoryMethod, typeof(T));
        }

        /// <summary>
        /// Returns an object of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the object to be returned.</typeparam>
        /// <param name="factory">The factory.</param>
        /// <returns>An instance of an object of type T.</returns>
        public static T Get<T>(this IFactory factory)
        {
            return (T)factory.Get(typeof(T));
        }
    }
}
