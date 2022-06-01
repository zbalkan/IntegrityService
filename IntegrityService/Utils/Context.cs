using System;
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
        ///     Hardcoded database file name is fim.db. Initial database size is set to 50MB for performance reasons.
        /// </summary>
        public Context()
        {
            _database = new LiteDatabase(new ConnectionString()
            {
                Filename = @"fim.db",
                Connection = ConnectionType.Shared,
                InitialSize = 8192 * 100 * 1000
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
