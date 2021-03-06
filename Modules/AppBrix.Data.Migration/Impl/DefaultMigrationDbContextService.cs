﻿// Copyright (c) MarinAtanasov. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the project root for license information.
//
using AppBrix.Data.Migration.Data;
using AppBrix.Lifecycle;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace AppBrix.Data.Migration.Impl
{
    internal sealed class DefaultMigrationDbContextService : IDbContextService, IApplicationLifecycle
    {
        #region Public and overriden methods
        public void Initialize(IInitializeContext context)
        {
            this.app = context.App;
            this.contextService = this.app.GetDbContextService();
            this.dbSupportsMigrations = true;
        }

        public void Uninitialize()
        {
            this.app = null;
            this.dbSupportsMigrations = false;
            this.initializedContexts.Clear();
        }

        public DbContext Get(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            this.MigrateContextIfNeeded(type);

            return this.contextService.Get(type);
        }
        #endregion

        #region Private methods
        private void MigrateContextIfNeeded(Type type)
        {
            if (!this.initializedContexts.Contains(type) && this.dbSupportsMigrations)
            {
                if (!typeof(DbContextBase).IsAssignableFrom(type))
                {
                    this.initializedContexts.Add(type);
                    return;
                }

                lock (this.initializedContexts)
                {
                    if (!this.initializedContexts.Contains(type) && this.dbSupportsMigrations)
                    {
                        this.MigrateMigrationContext();
                        if (!this.initializedContexts.Contains(type) && this.dbSupportsMigrations)
                        {
                            this.initializedContexts.Add(type);
                            this.MigrateContext(type);
                        }
                    }
                }
            }
        }

        private void MigrateMigrationContext()
        {
            var migrationContextType = typeof(MigrationContext);
            if (this.initializedContexts.Contains(migrationContextType))
                return;

            this.initializedContexts.Add(migrationContextType);
            using (var context = this.contextService.Get(migrationContextType))
            {
                try
                {
                    context.Database.Migrate();
                }
                catch (InvalidOperationException)
                {
                    // Context does not support migrations.
                    this.dbSupportsMigrations = false;
                    context.Database.EnsureCreated();
                }
            }
        }

        private void MigrateContext(Type type)
        {
            SnapshotData snapshot;
            using (var context = this.contextService.Get<MigrationContext>())
            {
                snapshot = context.Snapshots
                    .AsNoTracking()
                    .SingleOrDefault(x => x.Context == type.Name);
            }

            var assemblyVersion = type.Assembly.GetName().Version;
            if (snapshot == null || Version.Parse(snapshot.Version) < assemblyVersion)
            {
                var oldSnapshotCode = snapshot?.Snapshot ?? string.Empty;
                var oldVersion = Version.Parse(snapshot?.Version ?? DefaultMigrationDbContextService.EmptyVersion);
                var oldMigrationsAssembly = this.GenerateMigrationAssemblyName(type, oldVersion);
                this.LoadAssembly(oldMigrationsAssembly, oldSnapshotCode);

                var newVersion = type.Assembly.GetName().Version;
                var newMigrationName = this.GenerateMigrationName(type, newVersion);
                var scaffoldedMigration = this.CreateMigration(type, oldMigrationsAssembly, newMigrationName);

                MigrationData migration = null;
                if (scaffoldedMigration.SnapshotCode != oldSnapshotCode)
                {
                    migration = this.ApplyMigration(type, newVersion, scaffoldedMigration);
                }
                
                this.AddMigration(type.Name, newVersion.ToString(), migration, scaffoldedMigration.SnapshotCode, snapshot == null);
            }
        }

        private void LoadAssembly(string assemblyName, string snapshot, params MigrationData[] migrations)
        {
            var compilation = CSharpCompilation.Create(assemblyName,
                syntaxTrees: this.GetSyntaxTrees(snapshot, migrations),
                references: this.GetReferences(),
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var ms = new MemoryStream())
            {
                compilation.Emit(ms);
                ms.Seek(0, SeekOrigin.Begin);
                AssemblyLoadContext.Default.LoadFromStream(ms);
            }
        }
        
        private IEnumerable<SyntaxTree> GetSyntaxTrees(string snapshot, params MigrationData[] migrations)
        {
            var trees = new List<SyntaxTree>();

            if (snapshot != null)
            {
                trees.Add(SyntaxFactory.ParseSyntaxTree(snapshot));
            }

            if (migrations != null)
            {
                foreach (var migration in migrations)
                {
                    trees.Add(SyntaxFactory.ParseSyntaxTree(migration.Migration));
                    trees.Add(SyntaxFactory.ParseSyntaxTree(migration.Metadata));
                }
            }

            return trees;
        }

        private IEnumerable<MetadataReference> GetReferences()
        {
            return this.GetAllAssemblies().Select(x => MetadataReference.CreateFromFile(x.Location));
        }

        private IEnumerable<Assembly> GetAllAssemblies()
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            yield return entryAssembly;
            foreach (var referencedAssembly in entryAssembly.GetAllReferencedAssemblies())
            {
                yield return referencedAssembly;
            }
        }

        private ScaffoldedMigration CreateMigration(Type type, string oldMigrationsAssembly, string migrationName)
        {
            using (var context = (DbContextBase)this.contextService.Get(type))
            {
                context.Initialize(new DefaultInitializeDbContext(this.app, oldMigrationsAssembly));
                var scaffolder = this.CreateMigrationsScaffolder(context);
                return scaffolder.ScaffoldMigration(migrationName, context.GetType().Namespace);
            }
        }

        private MigrationsScaffolder CreateMigrationsScaffolder(DbContext context)
        {
            var logger = this.app.GetLogHub();

            var reporter = new OperationReporter(new OperationReportHandler(
                m => logger.Error(m),
                m => logger.Warning(m),
                m => logger.Info(m),
                m => logger.Trace(m)));

            var designTimeServices = new ServiceCollection()
                .AddSingleton(context.GetService<IHistoryRepository>())
                .AddSingleton(context.GetService<IMigrationsIdGenerator>())
                .AddSingleton(context.GetService<IMigrationsModelDiffer>())
                .AddSingleton(context.GetService<IMigrationsAssembly>())
                .AddSingleton(context.Model)
                .AddSingleton(context.GetService<ICurrentDbContext>())
                .AddSingleton(context.GetService<IDatabaseProvider>())
                .AddSingleton<MigrationsCodeGeneratorDependencies>()
                .AddSingleton<ICSharpHelper, CSharpHelper>()
                .AddSingleton<CSharpMigrationOperationGeneratorDependencies>()
                .AddSingleton<ICSharpMigrationOperationGenerator, CSharpMigrationOperationGenerator>()
                .AddSingleton<CSharpSnapshotGeneratorDependencies>()
                .AddSingleton<ICSharpSnapshotGenerator, CSharpSnapshotGenerator>()
                .AddSingleton<CSharpMigrationsGeneratorDependencies>()
                .AddSingleton<IMigrationsCodeGenerator, CSharpMigrationsGenerator>()
                .AddSingleton<ISnapshotModelProcessor, SnapshotModelProcessor>()
                .AddSingleton<IOperationReporter>(reporter)
                .AddSingleton<MigrationsScaffolderDependencies>()
                .AddSingleton<MigrationsScaffolder>()
                .BuildServiceProvider();

            return designTimeServices.GetRequiredService<MigrationsScaffolder>();
        }

        private MigrationData ApplyMigration(Type type, Version version, ScaffoldedMigration scaffoldedMigration)
        {
            var migration = new MigrationData
            {
                Context = type.Name,
                Version = version.ToString(),
                Migration = scaffoldedMigration.MigrationCode,
                Metadata = scaffoldedMigration.MetadataCode
            };

            var migrationAssemblyName = this.GenerateMigrationAssemblyName(type, version);
            this.LoadAssembly(migrationAssemblyName, scaffoldedMigration.SnapshotCode, migration);
            using (var context = (DbContextBase)this.contextService.Get(type))
            {
                context.Initialize(new DefaultInitializeDbContext(this.app, migrationAssemblyName));
                context.Database.Migrate();
            }

            return migration;
        }

        private void AddMigration(string contextName, string version, MigrationData migration, string snapshot, bool createNew)
        {
            using (var context = this.contextService.Get<MigrationContext>())
            {
                SnapshotData newSnapshot;
                if (createNew)
                {
                    newSnapshot = new SnapshotData { Context = contextName };
                    context.Snapshots.Add(newSnapshot);
                }
                else
                {
                    newSnapshot = context.Snapshots.Single(x => x.Context == contextName);
                }

                newSnapshot.Version = version;
                if (migration != null)
                {
                    newSnapshot.Snapshot = snapshot;
                    context.Migrations.Add(migration);
                }

                context.SaveChanges();
            }
        }

        private string GenerateMigrationAssemblyName(Type type, Version version = null)
        {
            if (version == null)
                version = type.Assembly.GetName().Version;

            return string.Format("Generated.Migrations.{0}.{1}.dll",
                this.GenerateMigrationName(type, version), Guid.NewGuid());
        }

        private string GenerateMigrationName(Type type, Version version)
        {
            return string.Format("{0}_{1}", type.Name, version.ToString().Replace('.', '_'));
        }
        #endregion

        #region Private fields and constants
        private const string EmptyVersion = "0.0.0.0";
        private readonly HashSet<Type> initializedContexts = new HashSet<Type>();
        private IApp app;
        private IDbContextService contextService;
        private bool dbSupportsMigrations;
        #endregion
    }
}
