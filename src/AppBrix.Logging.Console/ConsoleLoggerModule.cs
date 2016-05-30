// Copyright (c) MarinAtanasov. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the project root for license information.
//
using AppBrix.Lifecycle;
using AppBrix.Logging.Configuration;
using AppBrix.Logging.Loggers;
using AppBrix.Modules;
using System;
using System.Linq;

namespace AppBrix.Logging.Console
{
    /// <summary>
    /// Class that registers a console logger.
    /// </summary>
    public sealed class ConsoleLoggerModule : ModuleBase
    {
        #region Public and overriden methods
        protected override void InitializeModule(IInitializeContext context)
        {
            this.App.GetContainer().Register(this);
            var async = this.App.GetConfig<LoggingConfig>().Async;
            this.logger = Logger.Create(new ConsoleLogWriter(), async);
            this.logger.Initialize(context);
            this.App.GetContainer().Register(this.logger, this.logger.GetType());
        }

        protected override void UninitializeModule()
        {
            this.logger.Uninitialize();
            this.logger = null;
        }
        #endregion

        #region Private fields and constants
        private Logger logger;
        #endregion
    }
}