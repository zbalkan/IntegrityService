using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace IntegrityService.Utils
{
    internal sealed class FileSystemMonitor
    {
        private readonly ILogger _logger;
        private readonly bool _useDigest;

        private List<FileSystemWatcher> watchers;

        public FileSystemMonitor(ILogger logger, bool useDigest)
        {
            _logger = logger;
            _useDigest = useDigest;
        }

        public void Start() => InvokeWatchers();

        public void Stop()
        {
            foreach (var watcher in watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= OnChanged;
                watcher.Renamed -= OnChanged;
                watcher.Created -= OnCreated;
                watcher.Deleted -= OnDeleted;
                watcher.Error -= OnError;
                watcher.Dispose();
            }
            watchers.Clear();
        }

        private void InvokeWatchers()
        {
            watchers = new List<FileSystemWatcher>();

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
                watchers.Add(watcher);
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if ((e.ChangeType != WatcherChangeTypes.Changed && e.ChangeType != WatcherChangeTypes.Renamed) || IsExcluded(e.FullPath))
            {
                return;
            }

            if (_useDigest && IsFile(e.FullPath))
            {
                var digest = SHA256CheckSum(e.FullPath);
                _logger.LogInformation("Changed: {path}\nDigest: {digest}", e.FullPath, digest);
            }
            else
            {
                _logger.LogInformation("Changed: {path}", e.FullPath);
            }
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            if (IsExcluded(e.FullPath))
            {
                return;
            }

            if (_useDigest && IsFile(e.FullPath))
            {
                var digest = SHA256CheckSum(e.FullPath);
                _logger.LogInformation("Created: {path}\nDigest: {digest}", e.FullPath, digest);
            }
            else
            {
                _logger.LogInformation("Created: {path}", e.FullPath);
            }
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            if (IsExcluded(e.FullPath))
            {
                return;
            }

            if (_useDigest && IsFile(e.FullPath))
            {
                var digest = SHA256CheckSum(e.FullPath);
                _logger.LogInformation("Delete: {path}\nDigest: {digest}", e.FullPath, digest);
            }
            else
            {
                _logger.LogInformation("Delete: {path}", e.FullPath);
            }
        }

        private void OnError(object sender, ErrorEventArgs e) => PrintException(e.GetException());

        private void PrintException(Exception? ex)
        {
            if (ex != null)
            {
                var sb = new StringBuilder(120)
                    .Append("Message: ").AppendLine(ex.Message)
                    .Append("Stacktrace: ").AppendLine(ex.StackTrace);

                _logger.LogError("Exception: {ex}", sb.ToString());

                PrintException(ex.InnerException);
            }
        }

        private static bool IsExcluded(string path)
        {
            for (var i = 0; i < Settings.Instance.ExcludedPaths.Count; i++)
            {
                if (path.StartsWith(Settings.Instance.ExcludedPaths[i], StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(Settings.Instance.ExcludedPaths[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFile(string fullPath) => File.Exists(fullPath); // If it is a directory or a removed file, you cannot get a digest. So return false.

        private string SHA256CheckSum(string filePath)
        {
            var digest = string.Empty;

            try
            {
                using var SHA256 = System.Security.Cryptography.SHA256.Create();
                using var fileStream = File.OpenRead(filePath);
                digest = Convert.ToHexString(SHA256.ComputeHash(fileStream));

            }
            catch (Exception ex)
            {
                PrintException(ex);
            }
            return digest;
        }
    }
}
