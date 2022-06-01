using IntegrityService.Utils;
using System;
using System.Collections.Generic;

namespace IntegrityService
{
    internal sealed class Settings
    {
        /// <summary>
        ///     Filesystem directories to monitor.
        ///     Default: Empty list.
        /// </summary>
        public List<string> MonitoredPaths { get; private set; }

        /// <summary>
        ///     Filesystem directories to exclude from monitoring.
        ///     Default: Empty list.
        /// </summary>
        public List<string> ExcludedPaths { get; private set; }

        /// <summary>
        ///     File extensions to exclude from monitoring.
        ///     Default: Empty list.
        /// </summary>
        public List<string> ExcludedExtensions { get; private set; }

        /// <summary>
        ///     Switch to enable/disable Registry monitoring.
        ///     Default: false.
        /// </summary>
        public bool EnableRegistryMonitoring { get; private set; }

        /// <summary>
        ///     Registry keys to monitor.
        ///     Default: Empty list.
        /// </summary>
        public List<string> MonitoredKeys { get; private set; }

        /// <summary>
        ///     Registry keys to exclude from monitoring.
        ///     Default: Empty list.
        /// </summary>
        public List<string> ExcludedKeys { get; private set; }

        /// <summary>
        ///     Interval in seconds to send an informational heartbeat log entry to allow monitoring of the service itself. It can be disabled by setting it 0.
        ///     Default: 60
        /// </summary>
        public int HeartbeatInterval { get; private set; }

        internal static Settings Instance => Lazy.Value;

        private static readonly Lazy<Settings> Lazy = new(() => new Settings());

        private Settings()
        {
            ReadOrCreateSubKeys();
        }

        private void ReadOrCreateSubKeys()
        {
            Registry.WriteMultiStringValue("MonitoredPaths", new[] { string.Empty });
            MonitoredPaths = Registry.ReadMultiStringValue("MonitoredPaths");

            Registry.WriteMultiStringValue("ExcludedPaths", new[] { string.Empty });
            ExcludedPaths = Registry.ReadMultiStringValue("ExcludedPaths");

            Registry.WriteMultiStringValue("ExcludedExtensions", new[] { string.Empty });
            ExcludedExtensions = Registry.ReadMultiStringValue("ExcludedExtensions");

            Registry.WriteDwordValue("EnableRegistryMonitoring", 0);
            EnableRegistryMonitoring = Registry.ReadDwordValue("EnableRegistryMonitoring") == 1;

            Registry.WriteMultiStringValue("MonitoredKeys", new[] { string.Empty });
            MonitoredKeys = Registry.ReadMultiStringValue("MonitoredKeys");

            Registry.WriteMultiStringValue("ExcludedKeys", new[] { string.Empty });
            ExcludedKeys = Registry.ReadMultiStringValue("ExcludedKeys");

            Registry.WriteDwordValue("HeartbeatInterval", 60);
            var heartbeat = Registry.ReadDwordValue("HeartbeatInterval");
            HeartbeatInterval = heartbeat >= 0 ? heartbeat : 60;
        }
    }
}
