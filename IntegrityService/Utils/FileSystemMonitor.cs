using IntegrityService.FIM;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace IntegrityService.Utils
{
    internal sealed class FileSystemMonitor : IMonitor
    {
        /// <summary>
        ///     Windows file system creates multiple events for creation and change events. These are by design but creates pollution.
        ///     It is impossible to remove all of them but it can be minimized. For this, a buffer is used to check duplicate records.
        /// </summary>
        /// <see href="https://devblogs.microsoft.com/oldnewthing/20140507-00/?p=1053"/>
        private readonly FixedSizeDictionary<string, DateTime> _duplicateCheckBuffer;
        private readonly SHA256 _sha256;
        private readonly ILogger _logger;
        private readonly bool _useDigest;
        private readonly List<FileSystemWatcher> _watchers;
        private bool disposedValue;

        public FileSystemMonitor(ILogger logger, bool useDigest)
        {
            _logger = logger;
            _useDigest = useDigest;
            _sha256 = SHA256.Create();
            _duplicateCheckBuffer = new FixedSizeDictionary<string, DateTime>();
            _watchers = new List<FileSystemWatcher>();
        }

        public void Start()
        {
            if (Database.Context.FileSystemChanges.Count() == 0)
            {
                _logger.LogInformation("Could not find the database file. Initiating file system discovery. It will take up to 10 minutes.");
                var files = FileSystem.StartSearch(Settings.Instance.MonitoredPaths, Settings.Instance.ExcludedPaths,
                    Settings.Instance.ExcludedExtensions);
                if (files.IsEmpty)
                {
                    _logger.LogError("Could not access files.");
                }
                SeedDatabase(files);
                _logger.LogInformation("File system discovery completed.");
            }

            InvokeWatchers();
        }

        public void Stop()
        {
            if (_watchers.Count == 0) return;

            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= OnChanged;
                watcher.Renamed -= OnChanged;
                watcher.Created -= OnCreated;
                watcher.Deleted -= OnDeleted;
                watcher.Error -= OnError;
                watcher.Dispose();
            }
            _watchers.Clear();
        }

        private void InvokeWatchers()
        {
            foreach (var path in Settings.Instance.MonitoredPaths)
            {
                var watcher = new FileSystemWatcher(path)
                {
                    NotifyFilter = NotifyFilters.Attributes
                                | NotifyFilters.CreationTime
                                | NotifyFilters.DirectoryName
                                | NotifyFilters.FileName
                                // | NotifyFilters.LastAccess // This creates so much bloat.
                                | NotifyFilters.LastWrite
                                | NotifyFilters.Security
                                | NotifyFilters.Size,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true,
                    Filter = string.Empty
                };

                watcher.Changed += OnChanged;
                watcher.Renamed += OnChanged;
                watcher.Created += OnCreated;
                watcher.Deleted += OnDeleted;

                watcher.Error += OnError;

                _watchers.Add(watcher);
                _logger.LogInformation("Initiated file system watcher for director {directory}", path);
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e) => ProcessEvent(e.FullPath, ChangeCategory.Changed);

        private void OnCreated(object sender, FileSystemEventArgs e) => ProcessEvent(e.FullPath, ChangeCategory.Created);

        private void OnDeleted(object sender, FileSystemEventArgs e) => ProcessEvent(e.FullPath, ChangeCategory.Deleted);

        private void OnError(object sender, ErrorEventArgs e) => e.GetException().Log(_logger);

        private void ProcessEvent(string filePath, ChangeCategory category)
        {
            if (FileSystem.IsExcluded(filePath, Settings.Instance.ExcludedPaths, Settings.Instance.ExcludedExtensions) || IsDuplicate(filePath))
            {
                return;
            }

            var previousChange = Database.Context.FileSystemChanges
                .Query()
                .Where(x => x.FullPath.Equals(filePath))
                .OrderByDescending(c => c.DateTime)
                .ToList();

            var previousHash = string.Empty;
            if (previousChange.Count > 0)
            {
                previousHash = previousChange[0]?.CurrentHash ?? string.Empty;
            }

            var change = new FileSystemChange
            {
                Id = Guid.NewGuid(),
                ChangeCategory = category,
                ConfigChangeType = ConfigChangeType.FileSystem,
                Entity = filePath,
                DateTime = DateTime.Now,
                FullPath = filePath,
                SourceComputer = Environment.MachineName,
                CurrentHash = CalculateFileDigest(filePath),
                PreviousHash = previousHash,
                ACLs = filePath.GetACL()
            };

            Database.Context.FileSystemChanges.Insert(change);
            _logger.LogInformation("Category: {category}\nPath: {path}\nCurrent Hash: {currentHash}\nPreviousHash: {previousHash}", Enum.GetName(change.ChangeCategory), change.FullPath, change.CurrentHash, change.PreviousHash);
        }

        private bool IsDuplicate(string fullPath)
        {
            if (_duplicateCheckBuffer.ContainsKey(fullPath) &&
                _duplicateCheckBuffer[fullPath] == File.GetLastWriteTime(fullPath))
            {
                return true;
            }

            _duplicateCheckBuffer.AddOrUpdate(fullPath, File.GetLastWriteTime(fullPath));
            return false;
        }

        private string CalculateFileDigest(string path)
        {
            var digest = string.Empty;

            if (!_useDigest || !(FileSystem.IsFile(path) != null && FileSystem.IsFile(path)!.Value))
            {
                return digest;
            }

            try
            {
                using var fileStream = File.OpenRead(path);
                digest = Convert.ToHexString(_sha256.ComputeHash(fileStream));
            }
            catch (Exception ex)
            {
                ex.Log(_logger);
            }

            return digest;
        }

        private void SeedDatabase(ConcurrentBag<string> files)
        {
            if (files == null)
            {
                throw new ArgumentNullException(nameof(files));
            }

            if (files.IsEmpty)
            {
                throw new ArgumentException("Value cannot be an empty collection.", nameof(files));
            }

            var changes = new List<FileSystemChange>(files.Count);
            changes.AddRange(files.Select(file => new FileSystemChange
            {
                Id = Guid.NewGuid(),
                ChangeCategory = ChangeCategory.Discovery,
                ConfigChangeType = ConfigChangeType.FileSystem,
                Entity = file,
                DateTime = DateTime.Now,
                FullPath = file,
                SourceComputer = Environment.MachineName,
                CurrentHash = CalculateFileDigest(file),
                PreviousHash = string.Empty,
                ACLs = file.GetACL()
            }));

            Database.Context.FileSystemChanges.InsertBulk(changes, changes.Count);
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _sha256.Dispose();
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
