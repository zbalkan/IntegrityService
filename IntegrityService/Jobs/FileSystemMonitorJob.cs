using System;
using System.Collections.Generic;
using System.IO;
using FastCache;
using IntegrityService.Data;
using IntegrityService.FIM;
using IntegrityService.IO;
using IntegrityService.Utils;
using Microsoft.Extensions.Logging;

namespace IntegrityService.Jobs
{
    internal partial class FileSystemMonitorJob : IMonitor
    {
        private readonly ILiteDbContext _ctx;

        /// <summary>
        ///     Windows file system creates multiple events for creation and change events. These
        ///     are by design but creates pollution. It is impossible to remove all of them but it
        ///     can be minimized. For this, a buffer is used to check duplicate records.
        /// </summary>
        /// <see href="https://devblogs.microsoft.com/oldnewthing/20140507-00/?p=1053" />

        private readonly ILogger _logger;

        private readonly IBuffer<FileSystemChange> _messageStore;

        private readonly List<FileSystemWatcher> _watchers;

        private bool _disposedValue;

        public FileSystemMonitorJob(ILogger logger, IBuffer<FileSystemChange> fsStore, ILiteDbContext ctx)
        {
            _logger = logger;
            _watchers = [];
            _messageStore = fsStore;
            _ctx = ctx;
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
            return Cached<DateTime>.TryGet(fullPath, out var cached) && cached == lastWriteTime;
        }

        private void OnChanged(object sender, FileSystemEventArgs e) => ProcessEvent(e.FullPath, ChangeCategory.Changed);

        private void OnCreated(object sender, FileSystemEventArgs e) => ProcessEvent(e.FullPath, ChangeCategory.Created);

        private void OnDeleted(object sender, FileSystemEventArgs e) => ProcessEvent(e.FullPath, ChangeCategory.Deleted);

        private void OnError(object sender, ErrorEventArgs e) => e.GetException().Log(_logger);

        private void ProcessEvent(string path, ChangeCategory category)
        {
            if (Settings.Instance.IsMonitoredPath(path) && !IsDuplicate(path))
            {
                var change = FileSystemChange.FromPath(path, category);

                if (change != null)
                {
                    if (Settings.Instance.EnableLocalDatabase && change.ObjectType == FileSystem.ObjectType.File)
                    {
                        change.PreviousHash = FileSystemChange.RetrievePreviousHash(path, _ctx);
                    }

                    _messageStore.Add(change);
                    Cached<DateTime>.Save(path, change.DateTime, TimeSpan.FromSeconds(5));
                }
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