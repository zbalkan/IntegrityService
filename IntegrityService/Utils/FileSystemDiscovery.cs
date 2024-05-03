using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IntegrityService.FIM;
using Microsoft.Extensions.Logging;

// {{ FIM }}
// Copyright (C) {{ 2022 }}  {{ Zafer Balkan }}
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY

namespace IntegrityService.Utils
{
    internal class FileSystemDiscovery
    {
        private readonly ILogger _logger;
        public FileSystemDiscovery(ILogger logger)
        {
            _logger = logger;
        }
        internal void Start()
        {
            var files = RunNtfsDiscovery(out var sw);

            var filtered = FilterByConfig(sw, files);

            filtered = ContinueFromLastScan(sw, filtered);

            UpdateDiscoveryDatabase(sw, filtered);
        }

        private static List<string> FilterByPattern(IEnumerable<string> paths)
        {
            var includePattern = new Regex(GetIncludePattern(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

            var matches = (from path in paths.AsParallel().WithMergeOptions(ParallelMergeOptions.NotBuffered)
                           where includePattern.IsMatch(path)
                           select path).ToList();

            var excludePathPatternString = GetExcludePathPattern();
            if (!string.IsNullOrEmpty(excludePathPatternString))
            {
                var excludePathPattern = new Regex(excludePathPatternString, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
                matches = (from path in matches.AsParallel().WithMergeOptions(ParallelMergeOptions.NotBuffered)
                           where excludePathPattern.IsMatch(path)
                           select path).ToList();
            }

            var excludeExtPatternString = GetExcludeExtPattern();
            if (!string.IsNullOrEmpty(excludeExtPatternString))
            {
                var excludeExtPattern = new Regex(excludeExtPatternString, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
                matches = (from path in matches.AsParallel().WithMergeOptions(ParallelMergeOptions.NotBuffered)
                           where excludeExtPattern.IsMatch(path)
                           select path).ToList();
            }

            return matches;
        }

        private static string GetExcludeExtPattern()
        {
            if (Settings.Instance.ExcludedExtensions.Length > 0)
            {
                var sb = new StringBuilder(20);
                sb.Append("^(?!.*(");
                sb.Append(Sanitize(new StringBuilder(20).AppendJoin('|', Settings.Instance.ExcludedExtensions)));
                sb.Append(")$).*$");
                return sb.ToString();
            }
            return string.Empty;
        }

        private static string GetExcludePathPattern()
        {
            if (Settings.Instance.ExcludedPaths.Length > 0)
            {
                var sb = new StringBuilder(100);
                sb.Append("^(?!(");
                sb.Append(Sanitize(new StringBuilder(20).AppendJoin('|', Settings.Instance.ExcludedPaths)));
                sb.Append(")).*$");
                return sb.ToString();
            }
            return string.Empty;
        }

        // Use multiple patterns and iterate
        private static string GetIncludePattern()
        {
            var sb = new StringBuilder(100);
            sb.Append("(?:^(");
            sb.Append(Sanitize(new StringBuilder(20).AppendJoin('|', Settings.Instance.MonitoredPaths)));
            sb.Append(@")\\?.*$)");

            return sb.ToString();
        }

        private static StringBuilder Sanitize(StringBuilder sb) => sb
            .Replace(@"\", @"\\")
            .Replace(@"\\\\", @"\\")
            .Replace(".", @"\.")
            .Replace(" ", "\\ ")
            .Replace("(", "\\(")
            .Replace(")", "\\)");

        private List<string> ContinueFromLastScan(Stopwatch sw, List<string> filtered)
        {
            _logger.LogInformation("Filtering out the data in the database...");
            sw.Restart();
            var initialCount = filtered.Count;
            var filteredOut = filtered.RemoveAll(x => Database.Context.FileSystemChanges.Exists(c => c.Entity.Equals(x)));
            sw.Stop();
            _logger.LogInformation("Filtering out completed: {elapsed}", sw.Elapsed);
            if (filteredOut > 0)
            {
                _logger.LogInformation("Number of files not in database: {filteredCount} (filtered out {filteredOut}, %{percentage})",
                    filtered.Count, filteredOut, (double)filteredOut * 100 / initialCount);
            }
            else
            {
                _logger.LogInformation("Nothing to filter out. Discovery database is empty.");
            }

            return filtered;
        }
        private List<string> FilterByConfig(Stopwatch sw, ConcurrentBag<string> files)
        {
            _logger.LogInformation("Starting filtering by configuration values...");
            sw.Restart();
            var filtered = FilterByPattern(files);
            sw.Stop();
            _logger.LogInformation("Path filtering completed: {elapsed}", sw.Elapsed);
            _logger.LogInformation("Number of files to be monitored: {filteredCount} (filtered out {diff}, %{percentage})",
                filtered.Count, files.Count - filtered.Count, (double)(files.Count - filtered.Count) * 100 / files.Count);
            return filtered;
        }
        private ConcurrentBag<string> RunNtfsDiscovery(out Stopwatch sw)
        {
            _logger.LogInformation("Starting file search...");
            sw = new Stopwatch();
            sw.Start();
            var files = FileSystem.InvokeNtfsSearch();
            sw.Stop();
            _logger.LogInformation("Filesystem search completed: {elapsed}", sw.Elapsed);
            _logger.LogInformation("Number of all files in the device: {filesCount}", files.Count);

            return files;
        }
        private void UpdateDiscoveryDatabase(Stopwatch sw, List<string> filtered)
        {
            _logger.LogInformation("Starting inventory discovery (path and hash)...");
            sw.Restart();
            Parallel.ForEach(filtered, new ParallelOptions() { MaxDegreeOfParallelism = 10 }, path => FileSystem.GenerateChange(path, ChangeCategory.Discovery, out var _));
            sw.Stop();
            _logger.LogInformation("Database update completed: {elapsed}", sw.Elapsed);
        }
    }
}