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

        private List<string> ContinueFromLastScan(Stopwatch sw, List<string> filtered)
        {
            _logger.LogInformation("Filtering out the data in the database...");
            sw.Restart();
            var filteredOut = filtered.RemoveAll(x => Database.Context.FileSystemChanges.Exists(c => c.Entity.Equals(x)));
            sw.Stop();
            _logger.LogInformation("Filtering out completed: {elapsed}", sw.Elapsed);
            if (filteredOut > 0)
            {
                _logger.LogInformation("Number of files not in database: {filteredCount} (filtered out {filteredOut}, %{percentage})",
                    filtered.Count, filteredOut, ((double)filteredOut * 100 / filtered.Count).ToString("0.00"));
            }
            else
            {
                _logger.LogInformation("Nothing to filter out.");
            }

            return filtered;
        }

        private List<string> FilterAll(IEnumerable<string> paths)
        {
            var pattern = new Regex(GeneratePattern(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
            var matches = from path in paths.AsParallel().WithMergeOptions(ParallelMergeOptions.NotBuffered)
                          where pattern.IsMatch(path)
                          select path;

            return matches.ToList();
        }

        private List<string> FilterByConfig(Stopwatch sw, ConcurrentBag<string> files)
        {
            _logger.LogInformation("Starting filtering by configuration values...");
            sw.Restart();
            var filtered = FilterAll(files);
            sw.Stop();
            _logger.LogInformation("Path filtering completed: {elapsed}", sw.Elapsed);
            var diff = files.Count - filtered.Count;
            _logger.LogInformation("Number of files to be monitored: {filteredCount} (filtered out {diff}, %{percentage})",
                filtered.Count, diff, ((double)diff * 100 / files.Count).ToString("0.00"));
            return filtered;
        }

        private string GeneratePattern()
        {
            var sb = new StringBuilder(100);

            // Start with negative lookahead for exclusion
            sb.Append("(?:(?!(^(");
            if (Settings.Instance.ExcludedPaths.Count > 0)
            {
                sb.Append(Sanitize(new StringBuilder(20).AppendJoin('|', Settings.Instance.ExcludedPaths)));
                sb.Append(@")\\?.*)");
            }

            if (Settings.Instance.ExcludedExtensions.Count > 0)
            {
                sb.Append("|(^.*(");
                sb.Append(Sanitize(new StringBuilder(20).AppendJoin('|', Settings.Instance.ExcludedExtensions)));
                sb.Append(')');
            }

            sb.Append("$))");

            // Add included paths
            sb.Append("(?:^(");
            sb.Append(Sanitize(new StringBuilder(20).AppendJoin('|', Settings.Instance.MonitoredPaths)));
            sb.Append(@")\\?.*$))");

            return sb.ToString();
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

        private StringBuilder Sanitize(StringBuilder sb) => sb
            .Replace(@"\", @"\\")
            .Replace(@"\\\\", @"\\")
            .Replace(".", @"\.")
            .Replace(" ", "\\ ")
            .Replace("(", "\\(")
            .Replace(")", "\\)");

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