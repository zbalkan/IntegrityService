using IntegrityService.Data;
using IntegrityService.FIM;
using IntegrityService.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace IntegrityService.Jobs
{
    internal class FileSystemMonitorJob : IMonitor
    {
        /// <summary>
        ///     Windows file system creates multiple events for creation and change events. These are by design but creates pollution.
        ///     It is impossible to remove all of them but it can be minimized. For this, a buffer is used to check duplicate records.
        /// </summary>
        /// <see href="https://devblogs.microsoft.com/oldnewthing/20140507-00/?p=1053"/>
        private readonly FixedSizeDictionary<string, DateTime> _duplicateCheckBuffer;
        private readonly ILogger _logger;
        private readonly List<FileSystemWatcher> _watchers;
        private bool _disposedValue;

        public FileSystemMonitorJob(ILogger logger)
        {
            _logger = logger;
            _duplicateCheckBuffer = new FixedSizeDictionary<string, DateTime>();
            _watchers = [];
        }
        // This should run async
        public void Start() => InvokeWatchers();

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
                _logger.LogInformation("Initiated file system watcher for directory {directory}", path);
            }
        }

        private bool IsDuplicate(string fullPath)
        {
            var lastWriteTime = File.GetLastWriteTime(fullPath);
            if (_duplicateCheckBuffer.ContainsKey(fullPath) &&
                _duplicateCheckBuffer[fullPath] == lastWriteTime)
            {
                return true;
            }

            _duplicateCheckBuffer.AddOrUpdate(fullPath, lastWriteTime);
            return false;
        }

        private void OnChanged(object sender, FileSystemEventArgs e) => ProcessEvent(e.FullPath, ChangeCategory.Changed);

        private void OnCreated(object sender, FileSystemEventArgs e) => ProcessEvent(e.FullPath, ChangeCategory.Created);

        private void OnDeleted(object sender, FileSystemEventArgs e) => ProcessEvent(e.FullPath, ChangeCategory.Deleted);

        private void OnError(object sender, ErrorEventArgs e) => e.GetException().Log(_logger);

        private void ProcessEvent(string path, ChangeCategory category)
        {
            if (!Settings.Instance.IsMonitoredPath(path) || IsDuplicate(path))
            {
                return;
            }

            if (Settings.Instance.DisableLocalDatabase)
            {
                _logger.LogInformation("Category: {category}\nChange Type: {changeType}\nPath: {path}\nCurrent Hash: {currentHash}\nPreviousHash: {previousHash}", Enum.GetName(category), Enum.GetName(ConfigChangeType.FileSystem), path, FileSystem.CalculateFileHash(path), string.Empty);
            }
            else
            {
                FileSystem.GenerateChange(path, category, out var change);
                _logger.LogInformation("Category: {category}\nChange Type: {changeType}\nPath: {path}\nCurrent Hash: {currentHash}\nPreviousHash: {previousHash}", Enum.GetName(change.ChangeCategory), Enum.GetName(ConfigChangeType.FileSystem), path, change.CurrentHash, change.PreviousHash);
            }
        }
        #region Dispose
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // Dispose managed resources
                }

                _disposedValue = true;
            }
        }
        #endregion Dispose
    }
}
