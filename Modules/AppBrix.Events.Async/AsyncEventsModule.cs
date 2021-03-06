// Copyright (c) MarinAtanasov. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the project root for license information.
//
using AppBrix.Events.Async.Impl;
using AppBrix.Lifecycle;
using AppBrix.Modules;
using System;
using System.Linq;

namespace AppBrix.Events.Async
{
    /// <summary>
    /// Module used for registering an asynchronous event hub.
    /// </summary>
    public sealed class AsyncEventsModule : ModuleBase
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
            this.eventHub.Initialize(context);
            this.App.Container.Register(this.eventHub);
        }

        /// <summary>
        /// Uninitializes the module.
        /// Automatically called by <see cref="ModuleBase"/>.<see cref="ModuleBase.Uninitialize"/>
        /// </summary>
        protected override void UninitializeModule()
        {
            this.eventHub.Uninitialize();
        }
        #endregion

        #region Private fields and constants
        private readonly DefaultAsyncEventHub eventHub = new DefaultAsyncEventHub();
        #endregion
    }
}
