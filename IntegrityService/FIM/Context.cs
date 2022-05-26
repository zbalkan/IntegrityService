using System;
using System.IO;
using LiteDB;

namespace IntegrityService.FIM
{
    internal class Context : IDisposable
    {
        public ILiteCollection<FileSystemChange> FileSystemChanges { get; private set; }

        private readonly LiteDatabase _database;

        public Context(string connectionString)
        {
            _database = new LiteDatabase(connectionString);
            FileSystemChanges = _database.GetCollection<FileSystemChange>("fileSystemChanges");
            FileSystemChanges.EnsureIndex(x => x.Id);
            FileSystemChanges.EnsureIndex(x => x.Entity);
        }

        public static bool DatabaseExists(string connectionString) => File.Exists(connectionString);

        public void Dispose() => _database.Dispose();
    }
}
