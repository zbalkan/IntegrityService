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
            Registry.WriteMultiStringValue("MonitoredPaths", [string.Empty]);
            MonitoredPaths = Registry.ReadMultiStringValue("MonitoredPaths");

            Registry.WriteMultiStringValue("ExcludedPaths", [string.Empty]);
            ExcludedPaths = Registry.ReadMultiStringValue("ExcludedPaths");

            Registry.WriteMultiStringValue("ExcludedExtensions", [string.Empty]);
            ExcludedExtensions = Registry.ReadMultiStringValue("ExcludedExtensions");

            Registry.WriteDwordValue("EnableRegistryMonitoring", 0);
            EnableRegistryMonitoring = Registry.ReadDwordValue("EnableRegistryMonitoring") == 1;

            Registry.WriteMultiStringValue("MonitoredKeys", [string.Empty]);
            MonitoredKeys = Registry.ReadMultiStringValue("MonitoredKeys");

            Registry.WriteMultiStringValue("ExcludedKeys", [string.Empty]);
            ExcludedKeys = Registry.ReadMultiStringValue("ExcludedKeys");

            Registry.WriteDwordValue("HeartbeatInterval", DEFAULT_HEARTBEAT_INTERVAL);
            var heartbeat = Registry.ReadDwordValue("HeartbeatInterval");
            HeartbeatInterval = heartbeat >= 0 ? heartbeat : DEFAULT_HEARTBEAT_INTERVAL;

            Registry.WriteDwordValue("DisableLocalDatabase", 0);
            DisableLocalDatabase = Registry.ReadDwordValue("DisableLocalDatabase") == 1;
        }
    }
}
