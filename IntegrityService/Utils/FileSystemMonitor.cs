using IntegrityService.FIM;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        private readonly ILogger _logger;
        private readonly bool _useDigest;
        private readonly List<FileSystemWatcher> _watchers;
        private bool disposedValue;

        public FileSystemMonitor(ILogger logger, bool useDigest)
        {
            _logger = logger;
            _useDigest = useDigest;
            _duplicateCheckBuffer = new FixedSizeDictionary<string, DateTime>();
            _watchers = new List<FileSystemWatcher>();
        }

        public void Start()
        {
            if (Registry.ReadDwordValue("FileDiscoveryCompleted") == 0)
            {
                _logger.LogInformation("Could not find the database file. Initiating file system discovery. It will take up to 10 minutes.");
                Registry.WriteDwordValue("FileDiscoveryCompleted", 0, true);
                FileSystem.StartSearch(Settings.Instance.MonitoredPaths, Settings.Instance.ExcludedPaths,
                    Settings.Instance.ExcludedExtensions);

                Registry.WriteDwordValue("FileDiscoveryCompleted", 1, true);
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

        private void ProcessEvent(string path, ChangeCategory category)
        {
            if (FileSystem.IsExcluded(path, Settings.Instance.ExcludedPaths, Settings.Instance.ExcludedExtensions) || IsDuplicate(path))
            {
                return;
            }

            var previousChange = Database.Context.FileSystemChanges
                .Query()
                .Where(x => x.FullPath.Equals(path))
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
                Entity = path,
                DateTime = DateTime.Now,
                FullPath = path,
                SourceComputer = Environment.MachineName,
                CurrentHash = _useDigest ? FileSystem.CalculateFileDigest(path) : string.Empty,
                PreviousHash = previousHash,
                ACLs = path.GetACL()
            };

            Database.Context.FileSystemChanges.Insert(change);
            _logger.LogInformation("Category: {category}\nChange Type: {changeType}\nPath: {path}\nCurrent Hash: {currentHash}\nPreviousHash: {previousHash}", Enum.GetName(change.ChangeCategory), Enum.GetName(ConfigChangeType.FileSystem), change.FullPath, change.CurrentHash, change.PreviousHash);
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

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // Dispose managed resources
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
