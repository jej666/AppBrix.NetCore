﻿// Copyright (c) MarinAtanasov. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the project root for license information.
//
using AppBrix.Application;
using AppBrix.Lifecycle;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace AppBrix.Data.Migration.Impl
{
    internal sealed class DefaultMigrationDbContextLoader : IDbContextLoader, IApplicationLifecycle
    {
        #region Public and overriden methods
        public void Initialize(IInitializeContext context)
        {
            this.app = context.App;
            this.contextLoader = this.app.GetDbContextLoader();
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
            if (!typeof(DbContext).GetTypeInfo().IsAssignableFrom(type))
                throw new ArgumentException($"Provided type must inherit from {typeof(DbContext)}.");
            if (type.GetTypeInfo().IsAbstract)
                throw new ArgumentException($"Cannot create instance of abstract type {type}.");

            this.MigrateContextIfNeeded(type);

            return this.contextLoader.Get(type);
        }
        #endregion

        #region Private methods
        private void MigrateContextIfNeeded(Type type)
        {
            if ((typeof(DbContextBase).GetTypeInfo().IsAssignableFrom(type) || type == typeof(MigrationContext)) &&
                !this.initializedContexts.Contains(type) && this.dbSupportsMigrations)
            {
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
            using (var context = this.contextLoader.Get(migrationContextType))
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
            using (var context = this.contextLoader.Get<MigrationContext>())
            {
                snapshot = context.Snapshots.SingleOrDefault(x => x.Context == type.Name);
            }

            var assemblyVersion = type.GetTypeInfo().Assembly.GetName().Version;
            if (snapshot == null || Version.Parse(snapshot.Version) < assemblyVersion)
            {
                var oldSnapshotCode = snapshot?.Snapshot ?? string.Empty;
                var oldVersion = Version.Parse(snapshot?.Version ?? DefaultMigrationDbContextLoader.EmptyVersion);
                var oldMigrationsAssembly = this.GenerateMigrationAssemblyName(type, oldVersion);
                this.LoadAssembly(oldMigrationsAssembly, oldSnapshotCode);

                var newVersion = type.GetTypeInfo().Assembly.GetName().Version;
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
            var assemblies = new HashSet<string>();
            this.GetReferences(Assembly.GetEntryAssembly(), assemblies);
            return assemblies.Select(x => MetadataReference.CreateFromFile(x));
        }

        private void GetReferences(Assembly assembly, HashSet<string> locations, HashSet<string> names = null)
        {
            if (names == null)
                names = new HashSet<string>();

            if (!names.Contains(assembly.FullName))
            {
                names.Add(assembly.FullName);
                locations.Add(assembly.Location);
            }

            var newAssemblies = new List<AssemblyName>();
            foreach (var referencedAssemblyName in assembly.GetReferencedAssemblies())
            {
                if (!names.Contains(referencedAssemblyName.FullName))
                {
                    names.Add(referencedAssemblyName.FullName);
                    newAssemblies.Add(referencedAssemblyName);
                }
            }

            foreach (var referencedAssemblyName in newAssemblies)
            {
                var referencedAssembly = Assembly.Load(referencedAssemblyName);
                if (locations.Contains(referencedAssembly.Location))
                    continue;

                locations.Add(referencedAssembly.Location);
                GetReferences(referencedAssembly, locations, names);
            }
        }

        private ScaffoldedMigration CreateMigration(Type type, string oldMigrationsAssembly, string migrationName)
        {
            using (var context = (DbContextBase)this.contextLoader.Get(type))
            {
                context.Initialize(new DefaultInitializeDbContext(this.app, oldMigrationsAssembly));

                var codeHelper = new CSharpHelper();
                var scaffolder = ActivatorUtilities.CreateInstance<MigrationsScaffolder>(
                    ((IInfrastructure<IServiceProvider>)context).Instance,
                    new CSharpMigrationsGenerator(
                        codeHelper,
                        new CSharpMigrationOperationGenerator(codeHelper),
                        new CSharpSnapshotGenerator(codeHelper)));

                return scaffolder.ScaffoldMigration(migrationName, context.GetType().Namespace);
            }
        }

        private MigrationData ApplyMigration(Type type, Version version, ScaffoldedMigration scaffoldedMigration)
        {
            var migration = new MigrationData()
            {
                Context = type.Name,
                Version = version.ToString(),
                Migration = scaffoldedMigration.MigrationCode,
                Metadata = scaffoldedMigration.MetadataCode
            };

            var migrationAssemblyName = this.GenerateMigrationAssemblyName(type, version);
            this.LoadAssembly(migrationAssemblyName, scaffoldedMigration.SnapshotCode, migration);
            using (var context = (DbContextBase)this.contextLoader.Get(type))
            {
                context.Initialize(new DefaultInitializeDbContext(this.app, migrationAssemblyName));
                context.Database.Migrate();
            }

            return migration;
        }

        private void AddMigration(string contextName, string version, MigrationData migration, string snapshot, bool createNew)
        {
            using (var context = this.contextLoader.Get<MigrationContext>())
            {
                SnapshotData newSnapshot;
                if (createNew)
                {
                    newSnapshot = new SnapshotData() { Context = contextName };
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
                version = type.GetTypeInfo().Assembly.GetName().Version;

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
        private IDbContextLoader contextLoader;
        private bool dbSupportsMigrations;
        #endregion
    }
}