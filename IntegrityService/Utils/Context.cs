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

        public Context(string connectionString)
        {
            _database = new LiteDatabase(connectionString);
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
