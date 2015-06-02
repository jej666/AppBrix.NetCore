// Copyright (c) MarinAtanasov. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the project root for license information.
//
using System;
using System.Linq;

namespace AppBrix.Modules
{
    /// <summary>
    /// Contains values for load priorities of the default modules.
    /// </summary>
    public enum ModuleLoadPriority : int
    {
        Default = 0,
        Logger = 1 << 23,
        LogHub = 1 << 24,
        Time = 1 << 25,
        Events = 1 << 26,
        Cache = 1 << 27,
        Config = 1 << 28,
        Factory = 1 << 29,
        Resolver = 1 << 30,
    }
}
