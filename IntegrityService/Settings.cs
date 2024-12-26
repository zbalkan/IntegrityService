using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IntegrityService.IO;

namespace IntegrityService
{
    internal sealed class Settings
    {
        /// <summary>
        ///     Path to LiteDB database file
        /// </summary>
        /// <exception cref="PlatformNotSupportedException">
        /// </exception>
        public static string DatabasePath => $"{Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)}\\FIM\\fim.db";

        /// <summary>
        ///     Switch to enable/disable local database. When true, you cannot display previous hashes.
        ///     Default: false.
        /// </summary>
        public bool DisableLocalDatabase { get; private set; }

        /// <summary>
        ///     Switch to enable/disable Registry monitoring.
        ///     Default: false.
        /// </summary>
        public bool EnableRegistryMonitoring { get; private set; }

        /// <summary>
        ///     File extensions to exclude from monitoring.
        ///     Default: Empty list.
        /// </summary>
        public string[] ExcludedExtensions { get; private set; }

        /// <summary>
        ///     Registry keys to exclude from monitoring.
        ///     Default: Empty list.
        /// </summary>
        public string[] ExcludedKeys { get; private set; }

        /// <summary>
        ///     Filesystem directories to exclude from monitoring.
        ///     Default: Empty list.
        /// </summary>
        public string[] ExcludedPaths { get; private set; }

        /// <summary>
        ///     Interval in seconds to send an informational heartbeat log entry to allow monitoring
        ///     of the service itself. It can be disabled by setting it 0.
        ///     Default: 60
        /// </summary>
        public int HeartbeatInterval { get; private set; }

        /// <summary>
        ///     A flag that returns true if file discovery task is completed.
        /// </summary>
        public bool IsFileDiscoveryCompleted
        {
            get
            {
                return Registry.ReadDwordValue("FileDiscoveryCompleted") == 1;
            }
            set
            {
                if (value)
                {
                    Registry.WriteDwordValue("FileDiscoveryCompleted", 1);
                }
                else
                {
                    Registry.WriteDwordValue("FileDiscoveryCompleted", 0);
                }
            }
        }

        /// <summary>
        ///     Registry keys to monitor.
        ///     Default: Empty list.
        /// </summary>
        public string[] MonitoredKeys { get; private set; }

        /// <summary>
        ///     Filesystem directories to monitor.
        ///     Default: Empty list.
        /// </summary>
        public string[] MonitoredPaths { get; private set; }

        /// <summary>
        ///     A flag that returns true if application loads the Settings successfully.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        ///     The instance of the Settings singleton
        /// </summary>
#pragma warning disable Ex0101 // Member accessor may throw undocumented exception

        internal static Settings Instance => Lazy.Value;
#pragma warning restore Ex0101 // Member accessor may throw undocumented exception

        private const int DEFAULT_HEARTBEAT_INTERVAL = 60;

        private static readonly Lazy<Settings> Lazy = new(() => new Settings());

        private Regex? excludedExtensionsPattern;

        private Regex? excludedKeysPattern;

        private Regex? excludedPathsPattern;

        private Regex monitoredKeysPattern;

        private Regex monitoredPathsPattern;

        /// <summary>
        ///     Private ctor of the Settings singleton
        /// </summary>
        /// <exception cref="IOException">
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// </exception>
        /// <exception cref="System.Security.SecurityException">
        /// </exception>
        private Settings()
        {
            _ = Directory.CreateDirectory(Directory.GetParent(DatabasePath)!.ToString());
            try
            {
                ReadOrCreateRegistrySettings();
                Success = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                Success = false;
            }
        }

        /// <summary>
        ///     Filters out the initial list
        /// </summary>
        /// <param name="paths">
        ///     Initial list of file paths
        /// </param>
        /// <returns>
        ///     Filtered out fil paths
        /// </returns>
        /// <exception cref="RegexMatchTimeoutException">
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// </exception>
        /// <exception cref="AggregateException">
        /// </exception>
        /// <exception cref="OverflowException">
        /// </exception>
        public List<string> FilterPaths(IEnumerable<string> paths)
        {
            var matches = FilterMonitoredPaths(paths);

            matches = FilterOutExcludedPaths(matches);

            matches = FilterOutExcludedExtensions(matches);

            return matches.ToList();
        }

        public bool IsMonitoredKey(string keyName)
        {
            var monitored = monitoredKeysPattern.IsMatch(keyName);

            var excluded = false;
            if (excludedKeysPattern != null)
            {
                excluded = excludedKeysPattern.IsMatch(keyName);
            }
            return monitored && !excluded;
        }

        public bool IsMonitoredPath(string path)
        {
            var monitored = monitoredPathsPattern!.IsMatch(path);
            var excludedPath = false;
            if (excludedPathsPattern != null)
            {
                excludedPath = excludedPathsPattern.IsMatch(path);
            }

            var excludedExtension = false;
            if (excludedExtensionsPattern != null)
            {
                excludedExtension = excludedExtensionsPattern.IsMatch(path);
            }

            return monitored && !excludedPath && !excludedExtension;
        }

        private ParallelQuery<string> FilterMonitoredPaths(IEnumerable<string> paths) => from path in paths.AsParallel().WithMergeOptions(ParallelMergeOptions.NotBuffered)
                                                                                         where monitoredPathsPattern!.IsMatch(path)
                                                                                         select path;

        private ParallelQuery<string> FilterOutExcludedExtensions(ParallelQuery<string> matches)
        {
            if (excludedExtensionsPattern == null)
            {
                return matches;
            }
            return from path in matches.AsParallel().WithMergeOptions(ParallelMergeOptions.NotBuffered)
                   where !excludedExtensionsPattern.IsMatch(path)
                   select path;
        }

        private ParallelQuery<string> FilterOutExcludedPaths(ParallelQuery<string> matches)
        {
            if (excludedPathsPattern == null)
            {
                return matches;
            }
            return from path in matches.AsParallel().WithMergeOptions(ParallelMergeOptions.NotBuffered)
                   where !excludedPathsPattern.IsMatch(path)
                   select path;
        }

        /// <summary>
        ///     Generate the excluded extensions related RegEx pattern
        /// </summary>
        /// <returns>
        ///     RegEx pattern
        /// </returns>
        /// <exception cref="OverflowException">
        /// </exception>
        private Regex? GenerateExcludedExtensionsPattern()
        {
            if (ExcludedExtensions.Length > 0)
            {
                var sb = new StringBuilder(20);
                sb.Append("^.*(?:");
                sb.Append(Sanitize(new StringBuilder(20).AppendJoin("|", ExcludedExtensions)));
                sb.Append(")$");
                return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
            }
            return null;
        }

        /// <summary>
        ///     Generate the excluded keys related RegEx pattern
        /// </summary>
        /// <returns>
        ///     RegEx pattern
        /// </returns>
        /// <exception cref="OverflowException">
        /// </exception>
        private Regex? GenerateExcludedKeysPattern()
        {
            if (ExcludedKeys.Length > 0)
            {
                var sb = new StringBuilder(100);
                sb.Append("^(?:");
                sb.Append(Sanitize(new StringBuilder(20).AppendJoin("|", ExcludedKeys)));
                sb.Append(").*$");
                return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
            }
            return null;
        }

        /// <summary>
        ///     Generate the excluded paths related RegEx pattern
        /// </summary>
        /// <returns>
        ///     RegEx pattern
        /// </returns>
        /// <exception cref="OverflowException">
        /// </exception>
        private Regex? GenerateExcludedPathsPattern()
        {
            if (ExcludedPaths.Length > 0)
            {
                var sb = new StringBuilder(100);
                sb.Append("^(?:");
                sb.Append(Sanitize(new StringBuilder(20).AppendJoin("|", ExcludedPaths)));
                sb.Append(").*$");
                return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
            }
            return null;
        }

        /// <summary>
        ///     Generate the monitored keys related RegEx pattern
        /// </summary>
        /// <returns>
        ///     RegEx pattern
        /// </returns>
        /// <exception cref="OverflowException">
        /// </exception>
        private Regex GenerateMonitoredKeysPattern()
        {
            var sb = new StringBuilder(100);
            sb.Append("^(?:\"?(");
            sb.Append(Sanitize(new StringBuilder(20).AppendJoin("|", MonitoredKeys)));
            sb.Append(")).*$");
            return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        }

        /// <summary>
        ///     Generate the monitored paths related RegEx pattern
        /// </summary>
        /// <returns>
        ///     RegEx pattern
        /// </returns>
        /// <exception cref="OverflowException">
        /// </exception>
        private Regex GenerateMonitoredPathsPattern()
        {
            var sb = new StringBuilder(100);
            sb.Append("(?:^(");
            sb.Append(Sanitize(new StringBuilder(20).AppendJoin('|', MonitoredPaths)));
            sb.Append(@")\\?.*$)");
            return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        }

        /// <summary>
        ///     Reads the registry settings and loads into memory. If the registry keys do not
        ///     exist, creates the keys and values, writes the default value data.
        /// </summary>
        /// <remarks>
        ///     Ideally, when it is managed by Group Policy, we need to use a separate key to
        ///     prevent accidental overwrites.
        /// </remarks>
        /// <exception cref="OverflowException">
        /// </exception>
        private void ReadOrCreateRegistrySettings()
        {
            if (string.IsNullOrEmpty(Registry.ReadStringValue("DatabasePath")))
            {
                Registry.WriteStringValue("DatabasePath", DatabasePath);
            }

            var monitoredPaths = Registry.ReadMultiStringValue("MonitoredPaths");
            if (monitoredPaths.Length == 0)
            {
                monitoredPaths = [@"C:\Windows\System32", @"C:\Windows\SysWOW64", @"C:\Program Files", @"C:\Program Files (x86)"];
                Registry.WriteMultiStringValue("MonitoredPaths", monitoredPaths);
            }
            MonitoredPaths = monitoredPaths.Order().ToArray();
            monitoredPathsPattern = GenerateMonitoredPathsPattern();

            var excludedPaths = Registry.ReadMultiStringValue("ExcludedPaths");
            if (excludedPaths.Length == 0)
            {
                excludedPaths = [@"C:\Windows\System32\winevt",
                    @"C:\Windows\System32\sru",
                    @"C:\Windows\System32\config",
                    @"C:\Windows\System32\catroot2",
                    @"C:\Windows\System32\LogFiles",
                    @"C:\Windows\System32\wbem",
                    @"C:\Windows\System32\WDI\LogFiles",
                    @"C:\Windows\System32\Microsoft\Protect\Recovery",
                    @"C:\Windows\SysWOW64\winevt",
                    @"C:\Windows\SysWOW64\sru",
                    @"C:\Windows\SysWOW64\config",
                    @"C:\Windows\SysWOW64\catroot2",
                    @"C:\Windows\SysWOW64\LogFiles",
                    @"C:\Windows\SysWOW64\wbem",
                    @"C:\Windows\SysWOW64\WDI\LogFiles",
                    @"C:\Windows\SysWOW64\Microsoft\Protect\Recovery",
                    @"C:\Program Files\Windows Defender Advanced Threat Protection\Classification\Configuration",
                    @"C:\Program Files\Microsoft OneDrive\StandaloneUpdater\logs"];
                Registry.WriteMultiStringValue("ExcludedPaths", excludedPaths);
            }
            ExcludedPaths = excludedPaths.Order().ToArray();
            excludedPathsPattern = GenerateExcludedPathsPattern();

            var excludedExtensions = Registry.ReadMultiStringValue("ExcludedExtensions");
            if (excludedExtensions.Length == 0)
            {
                excludedExtensions = [".log", ".evtx", ".etl"];
                Registry.WriteMultiStringValue("ExcludedExtensions", excludedExtensions);
            }
            ExcludedExtensions = excludedExtensions.Order().ToArray();
            excludedExtensionsPattern = GenerateExcludedExtensionsPattern();

            var registryMonitoring = Registry.ReadDwordValue("EnableRegistryMonitoring");
            if (registryMonitoring == -1)
            {
                Registry.WriteDwordValue("EnableRegistryMonitoring", 0);
                registryMonitoring = 0;
            }
            EnableRegistryMonitoring = registryMonitoring == 1;

            var monitoredKeys = Registry.ReadMultiStringValue("MonitoredKeys");
            if (monitoredKeys.Length == 0)
            {
                monitoredKeys = [@"HKEY_LOCAL_MACHINE\SOFTWARE\FIM",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"];
                Registry.WriteMultiStringValue("MonitoredKeys", monitoredKeys);
            }
            MonitoredKeys = monitoredKeys.Order().ToArray();
            monitoredKeysPattern = GenerateMonitoredKeysPattern();

            var excludedKeys = Registry.ReadMultiStringValue("ExcludedKeys");
            if (excludedKeys.Length == 0)
            {
                excludedKeys = [string.Empty];
                Registry.WriteMultiStringValue("ExcludedKeys", excludedKeys);
            }
            ExcludedKeys = excludedKeys.Order().ToArray();
            excludedKeysPattern = ExcludedKeys.Length == 1 && ExcludedKeys[0]?.Length == 0 ? null : GenerateExcludedKeysPattern();

            var heartbeat = Registry.ReadDwordValue("HeartbeatInterval");
            if (heartbeat == -1)
            {
                Registry.WriteDwordValue("HeartbeatInterval", DEFAULT_HEARTBEAT_INTERVAL);
                heartbeat = DEFAULT_HEARTBEAT_INTERVAL;
            }

            HeartbeatInterval = heartbeat;

            var disableLocalDatabase = Registry.ReadDwordValue("DisableLocalDatabase");
            if (disableLocalDatabase == -1)
            {
                Registry.WriteDwordValue("DisableLocalDatabase", 0);
                disableLocalDatabase = 0;
            }
            DisableLocalDatabase = disableLocalDatabase == 1;

            var fileDiscoveryCompleted = Registry.ReadDwordValue("FileDiscoveryCompleted");
            if (fileDiscoveryCompleted == -1)
            {
                IsFileDiscoveryCompleted = false;
            }
        }

        private StringBuilder Sanitize(StringBuilder sb) => sb
            .Replace(@"\", @"\\")
            .Replace(@"\\\\", @"\\")
            .Replace(".", @"\.")
            .Replace(" ", "\\ ")
            .Replace("(", "\\(")
            .Replace(")", "\\)");
    }
}