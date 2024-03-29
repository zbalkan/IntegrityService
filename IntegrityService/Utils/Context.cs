﻿using System;
using IntegrityService.FIM;
using LiteDB;

namespace IntegrityService.Utils
{
    internal sealed class Context : IDisposable
    {
        public ILiteCollection<FileSystemChange> FileSystemChanges { get; }

        public ILiteCollection<RegistryChange> RegistryChanges { get; }

        private readonly LiteDatabase _database;

        /// <summary>
        ///     The default size is 800MB
        /// </summary>
        private const long InitialDatabaseSize = 800 * MB;

        private const long MB = 1024 * 1024;

        /// <summary>
        ///     The default file name is fim.db
        /// </summary>
        private const string DatabaseFileName = "fim.db";
        /// <summary>
        ///     Hardcoded database file name is fim.db. Initial database size is set to 800MB for performance reasons.
        /// </summary>
        public Context()
        {
            _database = new LiteDatabase(new ConnectionString()
            {
                Filename = DatabaseFileName,
                Connection = ConnectionType.Direct,
                InitialSize = InitialDatabaseSize
            });

            FileSystemChanges = _database.GetCollection<FileSystemChange>("fileSystemChanges");
            FileSystemChanges.EnsureIndex(x => x.Id);
            FileSystemChanges.EnsureIndex(x => x.Entity);

            RegistryChanges = _database.GetCollection<RegistryChange>("registryChanges");
            RegistryChanges.EnsureIndex(x => x.Id);
            RegistryChanges.EnsureIndex(x => x.Entity);
        }

        public void Dispose() => _database.Dispose();
    }
}
