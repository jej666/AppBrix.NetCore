// Copyright (c) MarinAtanasov. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the project root for license information.
//
using System;
using System.Collections.Generic;
using System.Linq;

namespace AppBrix.Container
{
    /// <summary>
    /// Registers objects and enables resolving of registered objects.
    /// </summary>
    public interface IContainer
    {
        /// <summary>
        /// Registers an object as the passed-in type, its parent types and interfaces.
        /// This method can be used when the type is not known during compile time.
        /// </summary>
        /// <param name="obj">The object to be registered. Required.</param>
        /// <param name="type">The type to be used as base upon registration. Required. Cannot be <see cref="object"/>.</param>
        void Register(object obj, Type type);

        /// <summary>
        /// Returns the last registered object of a given type.
        /// </summary>
        /// <param name="type">The type of the registered object.</param>
        /// <returns>The last registered object.</returns>
        object Get(Type type);

        /// <summary>
        /// Resolves all registered objects in the order in which they were registered.
        /// </summary>
        /// <returns>All registered objects.</returns>
        IEnumerable<object> GetAll();
    }
}
