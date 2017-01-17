// Copyright (c) MarinAtanasov. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the project root for license information.
//
using AppBrix.Data.Impl;
using AppBrix.Lifecycle;
using AppBrix.Modules;
using System;
using System.Linq;

namespace AppBrix.Data
{
    /// <summary>
    /// Module used for enabling database functionality.
    /// </summary>
    public sealed class DataModule : ModuleBase
    {
        #region Public and overriden methods
        protected override void InitializeModule(IInitializeContext context)
        {
            this.App.GetContainer().Register(this);
            this.contextLoader.Value.Initialize(context);
            this.App.GetContainer().Register(this.contextLoader.Value);
        }

        protected override void UninitializeModule()
        {
            this.contextLoader.Value.Uninitialize();
        }
        #endregion

        #region Private fields and constants
        private readonly Lazy<DefaultDbContextLoader> contextLoader = new Lazy<DefaultDbContextLoader>();
        #endregion
    }
}
