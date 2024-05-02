using IntegrityService.Utils;
using System;
using System.Collections.Generic;
using System.IO;

namespace IntegrityService
{
    internal sealed class Settings
    {
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
        public List<string> ExcludedExtensions { get; private set; }

        /// <summary>
        ///     Registry keys to exclude from monitoring.
        ///     Default: Empty list.
        /// </summary>
        public List<string> ExcludedKeys { get; private set; }

        /// <summary>
        ///     Filesystem directories to exclude from monitoring.
        ///     Default: Empty list.
        /// </summary>
        public List<string> ExcludedPaths { get; private set; }

        /// <summary>
        ///     Interval in seconds to send an informational heartbeat log entry to allow monitoring of the service itself. It can be disabled by setting it 0.
        ///     Default: 60
        /// </summary>
        public int HeartbeatInterval { get; private set; }

        /// <summary>
        ///     Registry keys to monitor.
        ///     Default: Empty list.
        /// </summary>
        public List<string> MonitoredKeys { get; private set; }

        /// <summary>
        ///     Filesystem directories to monitor.
        ///     Default: Empty list.
        /// </summary>
        public List<string> MonitoredPaths { get; private set; }
        /// <summary>
        ///     A flag that returns true if application loads the Settings successfully.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        ///     The default file name is fim.db
        /// </summary>
        public readonly string DatabasePath = $"{Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)}\\FIM\\fim.db";


        internal static Settings Instance => Lazy.Value;

        private const int DEFAULT_HEARTBEAT_INTERVAL = 60;
        private static readonly Lazy<Settings> Lazy = new(() => new Settings());

        private Settings()
        {
            Directory.CreateDirectory(Directory.GetParent(DatabasePath)!.ToString());
            try
            {
                ReadOrCreateSubKeys();
                Success = true;
            }
            catch
            {
                Success = false;
            }
        }

        private void ReadOrCreateSubKeys()
        {
            var monitoredPaths = Registry.ReadMultiStringValue("MonitoredPaths");
            if (monitoredPaths.Count == 0)
            {
                monitoredPaths = [@"C:\Windows\System32", @"C:\Windows\SysWOW64", @"C:\Program Files", @"C:\Program Files (x86)"];
                Registry.WriteMultiStringValue("MonitoredPaths", monitoredPaths);
            }
            MonitoredPaths = monitoredPaths;

            var excludedPaths = Registry.ReadMultiStringValue("ExcludedPaths");
            if (excludedPaths.Count == 0)
            {
                excludedPaths = [@"C:\Windows\System32\winevt", @"C:\Windows\System32\sru", @"C:\Windows\System32\config",
                @"C:\Windows\System32\catroot2", @"C:\Windows\System32\LogFiles", @"C:\Windows\System32\wbem",
                @"C:\Windows\System32\WDI\LogFiles", @"C:\Windows\System32\Microsoft\Protect\Recovery",
                @"C:\Windows\SysWOW64\winevt", @"C:\Windows\SysWOW64\sru", @"C:\Windows\SysWOW64\config",
                @"C:\Windows\SysWOW64\catroot2", @"C:\Windows\SysWOW64\LogFiles", @"C:\Windows\SysWOW64\wbem",
                @"C:\Windows\SysWOW64\WDI\LogFiles", @"C:\Windows\SysWOW64\Microsoft\Protect\Recovery",
                @"C:\Program Files\Windows Defender Advanced Threat Protection\Classification\Configuration",
                @"C:\Program Files\Microsoft OneDrive\StandaloneUpdater\logs"];
                Registry.WriteMultiStringValue("ExcludedPaths", excludedPaths);
            }
            ExcludedPaths = excludedPaths;

            var excludedExtensions = Registry.ReadMultiStringValue("ExcludedExtensions");
            if (excludedExtensions.Count == 0)
            {
                excludedExtensions = [".log", ".evtx", ".etl"];
                Registry.WriteMultiStringValue("ExcludedExtensions", excludedExtensions);
            }
            ExcludedExtensions = excludedExtensions;

            var registryMonitoring = Registry.ReadDwordValue("EnableRegistryMonitoring");
            if (registryMonitoring == -1)
            {
                Registry.WriteDwordValue("EnableRegistryMonitoring", 0);
                registryMonitoring = 0;
            }
            EnableRegistryMonitoring = registryMonitoring == 1;

            var monitoredKeys = Registry.ReadMultiStringValue("MonitoredKeys");
            if (monitoredKeys.Count == 0)
            {
                monitoredKeys = [@"Computer\HKEY_LOCAL_MACHINE\SOFTWARE\FIM",
                @"Computer\HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                @"Computer\HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
                @"Computer\HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"];
                Registry.WriteMultiStringValue("MonitoredKeys", monitoredKeys);
            }
            MonitoredKeys = monitoredKeys;

            var excludedKeys = Registry.ReadMultiStringValue("ExcludedKeys");
            if (excludedKeys.Count == 0)
            {
                excludedKeys = [string.Empty];
                Registry.WriteMultiStringValue("ExcludedKeys", excludedKeys);
            }
            ExcludedKeys = excludedKeys;


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
        }
    }
}
