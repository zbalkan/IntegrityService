using System;
using LiteDB;

namespace IntegrityService.FIM
{
    internal class Context : IDisposable
    {
        public ILiteCollection<FileSystemChange> FileSystemChanges { get; private set; }

        private readonly LiteDatabase _database;

        public Context(string path)
        {
            _database = new LiteDatabase(path);
            FileSystemChanges = _database.GetCollection<FileSystemChange>("fileSystemChanges");
            FileSystemChanges.EnsureIndex(x => x.Id);
            FileSystemChanges.EnsureIndex(x => x.Entity);
        }

        public void Dispose() => _database.Dispose();
    }
}
