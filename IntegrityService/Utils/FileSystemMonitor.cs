using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using IntegrityService.FIM;

namespace IntegrityService.Utils
{
    internal sealed class FileSystemMonitor
    {
        private readonly ILogger _logger;
        private readonly Context _context;
        private readonly bool _useDigest;
        private List<FileSystemWatcher> _watchers;

        /// <summary>
        ///     Windows file system creates multiple events for creation and change events. These are by design but creates pollution.
        ///     It is impossible to remove all of them but it can be minimized. For this, a buffer is used to check duplicate records.
        /// </summary>
        /// <see href="https://devblogs.microsoft.com/oldnewthing/20140507-00/?p=1053"/>
        private readonly FixedSizeDictionary<string, DateTime> _duplicateCheckBuffer;

        public FileSystemMonitor(
            ILogger logger,
            Context context,
            bool useDigest)
        {
            _logger = logger;
            _context = context;
            _useDigest = useDigest;

            _duplicateCheckBuffer = new FixedSizeDictionary<string, DateTime>();
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

        private void OnChanged(object sender, FileSystemEventArgs e) => ProcessEvent(e.FullPath, ChangeCategory.Changed);

        private void OnCreated(object sender, FileSystemEventArgs e) => ProcessEvent(e.FullPath, ChangeCategory.Created);
        
        private void OnDeleted(object sender, FileSystemEventArgs e) => ProcessEvent(e.FullPath, ChangeCategory.Deleted);

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

        private void ProcessEvent(string filePath, ChangeCategory category)
        {
            if (IsExcluded(filePath) || IsDuplicate(filePath))
            {
                return;
            }

            var digest = CalculateFileDigest(filePath);

            WriteToDatabase(filePath, category, digest);
            WriteLog(filePath, category, digest);
        }

        private void WriteLog(string filePath, ChangeCategory category, string digest) => _logger.LogInformation("{category}: {path}\nDigest: {digest}", Enum.GetName(category), filePath, digest);

        private void WriteToDatabase(string filePath, ChangeCategory category, string digest) =>
            _context.FileSystemChanges.Insert(new FileSystemChange
            {
                Id = Guid.NewGuid(),
                ChangeCategory = category,
                ConfigChangeType = ConfigChangeType.FileSystem,
                Entity = filePath,
                DateTime = DateTime.Now,
                FullPath = filePath,
                SourceComputer = Environment.MachineName,
                CurrentHash = digest
            });

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

        private static bool IsExcluded(string path) =>
            Settings.Instance.ExcludedPaths
                .Any(excludedPath => path.StartsWith(excludedPath, StringComparison.OrdinalIgnoreCase))
            ||
            Settings.Instance.ExcludedExtensions
                .Any(excludedPath => path.EndsWith(excludedPath, StringComparison.OrdinalIgnoreCase));

        private static bool IsFile(string fullPath) => File.Exists(fullPath); // If it is a directory or a removed file, you cannot get a digest. So return false.

        private string CalculateFileDigest(string path)
        {
            var digest = string.Empty;

            if (_useDigest && IsFile(path))
            {
                digest = Sha256CheckSum(path);
            }

            return digest;
        }

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
