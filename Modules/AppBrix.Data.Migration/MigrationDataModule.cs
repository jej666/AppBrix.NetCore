// Copyright (c) MarinAtanasov. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the project root for license information.
//
using AppBrix.Data.Migration.Impl;
using AppBrix.Lifecycle;
using AppBrix.Modules;
using System;
using System.Linq;

namespace AppBrix.Data.Migration
{
    /// <summary>
    /// Module used for enabling database CodeFirst migration functionality.
    /// </summary>
    public sealed class MigrationDataModule : ModuleBase
    {
        #region Public and overriden methods
        /// <summary>
        /// Initializes the module.
        /// Automatically called by <see cref="ModuleBase"/>.<see cref="ModuleBase.Initialize"/>
        /// </summary>
        /// <param name="context">The initialization context.</param>
        protected override void InitializeModule(IInitializeContext context)
        {
            this.App.Container.Register(this);
            this.contextService.Initialize(context);
            this.App.Container.Register(this.contextService);
        }

        /// <summary>
        /// Uninitializes the module.
        /// Automatically called by <see cref="ModuleBase"/>.<see cref="ModuleBase.Uninitialize"/>
        /// </summary>
        protected override void UninitializeModule()
        {
            this.contextService.Uninitialize();
        }
        #endregion

        #region Private fields and constants
        private readonly DefaultMigrationDbContextService contextService = new DefaultMigrationDbContextService();
        #endregion
    }
}
