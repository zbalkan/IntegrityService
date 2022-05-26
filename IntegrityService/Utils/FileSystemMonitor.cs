using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace IntegrityService.Utils
{
    internal sealed class FileSystemMonitor
    {
        private readonly ILogger _logger;
        private readonly bool _useDigest;
        private List<FileSystemWatcher> _watchers;

        /// <summary>
        ///     Windows file system creates multiple events for creation and change events. These are by design but creates pollution.
        ///     It is impossible to remove all of them but it can be minimized. For this, a buffer is used to check duplicate records.
        /// </summary>
        /// <see href="https://devblogs.microsoft.com/oldnewthing/20140507-00/?p=1053"/>
        private readonly FixedSizeDictionary<string, DateTime> _duplicateCheckBuffer;

        public FileSystemMonitor(ILogger logger, bool useDigest)
        {
            _logger = logger;
            _useDigest = useDigest;

            _duplicateCheckBuffer = new FixedSizeDictionary<string, DateTime>(50);
        }

        public void Start() => InvokeWatchers();

        public void Stop()
        {
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
            _watchers = new List<FileSystemWatcher>();

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
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e) => WriteLog(e, "Changed");

        private void OnCreated(object sender, FileSystemEventArgs e) => WriteLog(e, "Created");
        
        private void OnDeleted(object sender, FileSystemEventArgs e) => WriteLog(e, "Deleted");

        private void OnError(object sender, ErrorEventArgs e) => PrintException(e.GetException());

        private void PrintException(Exception? ex)
        {
            while (true)
            {
                if (ex == null)
                {
                    break;
                }

                var sb = new StringBuilder(120).Append("Message: ")
                    .AppendLine(ex.Message)
                    .Append("Stacktrace: ")
                    .AppendLine(ex.StackTrace);

                _logger.LogError("Exception: {ex}", sb.ToString());

                ex = ex.InnerException;
            }
        }

        private void WriteLog(FileSystemEventArgs e, string category)
        {
            if (IsExcluded(e.FullPath) || IsDuplicate(e.FullPath))
            {
                return;
            }

            if (_useDigest && IsFile(e.FullPath))
            {
                var digest = Sha256CheckSum(e.FullPath);
                _logger.LogInformation("{category}: {path}\nDigest: {digest}", category, e.FullPath, digest);
            }
            else
            {
                _logger.LogInformation("{category}: {path}", category, e.FullPath);
            }
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

        private static bool IsFile(string fullPath) => (File.GetAttributes(fullPath) & FileAttributes.Directory) != FileAttributes.Directory;

        private static bool IsExcluded(string path) =>
            !IsFile(path)
                ? (Settings.Instance.ExcludedPaths ??
                   throw new InvalidOperationException()) // If file, sanitize file path and check. Then, check extensions.
                  .Any(excludedPath =>
                      Path.GetFileName(path).Contains(Path.GetFileName(excludedPath)!,
                          StringComparison.OrdinalIgnoreCase)) ||
                  (Settings.Instance.ExcludedExtensions ?? throw new InvalidOperationException())
                  .Any(extension =>
                      extension.Contains(Path.GetExtension(path), StringComparison.OrdinalIgnoreCase))
                : (Settings.Instance.ExcludedPaths ??
                   throw new InvalidOperationException()) // If directory, sanitize directory path and check
                .Any(excludedPath =>
                    Path.GetDirectoryName(path)!.Contains(Path.GetDirectoryName(excludedPath)!,
                        StringComparison.OrdinalIgnoreCase));

        private string Sha256CheckSum(string filePath)
        {
            var digest = string.Empty;

            try
            {
                using var sha256 = SHA256.Create();
                using var fileStream = File.OpenRead(filePath);
                digest = Convert.ToHexString(sha256.ComputeHash(fileStream));
            }
            catch (Exception ex)
            {
                PrintException(ex);
            }
            return digest;
        }
    }
}
