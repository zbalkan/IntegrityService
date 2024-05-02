using System;
using IntegrityService.FIM;
using LiteDB;

namespace IntegrityService.Utils
{
    internal class Context : IDisposable
    {
        public ILiteCollection<FileSystemChange> FileSystemChanges { get; }

        public ILiteCollection<RegistryChange> RegistryChanges { get; }

        private readonly LiteDatabase _database;
        private bool disposedValue;

        /// <summary>
        ///     The default size is 800MB
        /// </summary>
        private const long InitialDatabaseSize = 800 * MB;

        private const long MB = 1024 * 1024;

        /// <summary>
        ///     Hardcoded database file name is fim.db. Initial database size is set to 800MB for performance reasons.
        /// </summary>
        public Context()
        {
            _database = new LiteDatabase(new ConnectionString()
            {
                Filename = Settings.Instance.DatabasePath,
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

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _database.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
