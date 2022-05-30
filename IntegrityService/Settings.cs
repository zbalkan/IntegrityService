using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace IntegrityService
{
#pragma warning disable CS8601 // Possible null reference assignment.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8604 // Possible null reference argument.

    internal sealed class Settings
    {
        public List<string> MonitoredPaths { get; private set; }

        public List<string> ExcludedPaths { get; private set; }

        public List<string> ExcludedExtensions { get; private set; }

        public List<string> MonitoredKeys { get; private set; }

        public List<string> ExcludedKeys { get; private set; }

        /// <summary>
        ///     Hardcoded database file name is fim.db. Initial database size is set to 50MB for performance reasons.
        /// </summary>
        // ReSharper disable once MemberCanBeMadeStatic.Global
#pragma warning disable CA1822 // Mark members as static
        public string ConnectionString => "Filename=fim.db;InitialSize=50MB;";
#pragma warning restore CA1822 // Mark members as static

        internal static Settings Instance => Lazy.Value;

        private static readonly Lazy<Settings> Lazy = new(() => new Settings());

        private readonly RegistryKey _hklmSoftware = Registry.LocalMachine.OpenSubKey("Software", true);

        private const string FimKeyName = "FIM";

        private RegistryKey? _fimKey;

        private Settings()
        {
            ReadOrCreateFimKey();
            ReadOrCreateSubKeys();
        }

        private void ReadOrCreateSubKeys()
        {
            try
            {
                _ = _fimKey.GetValueKind("MonitoredPaths");
            }
            catch

            {
                _fimKey.SetValue("MonitoredPaths", new string[] { string.Empty }, RegistryValueKind.MultiString);
            }
            MonitoredPaths = (_fimKey.GetValue("MonitoredPaths") as string[]).Where(path => !string.IsNullOrEmpty(path)).ToList();

            try
            {
                _ = _fimKey.GetValueKind("ExcludedPaths");
            }
            catch

            {
                _fimKey.SetValue("ExcludedPaths", new string[] { string.Empty }, RegistryValueKind.MultiString);
            }
            ExcludedPaths = (_fimKey.GetValue("ExcludedPaths") as string[]).Where(path => !string.IsNullOrEmpty(path)).ToList();

            try
            {
                _ = _fimKey.GetValueKind("ExcludedExtensions");
            }
            catch

            {
                _fimKey.SetValue("ExcludedExtensions", new string[] { string.Empty }, RegistryValueKind.MultiString);
            }
            ExcludedExtensions = (_fimKey.GetValue("ExcludedExtensions") as string[]).Where(path => !string.IsNullOrEmpty(path)).ToList();

            try
            {
                _ = _fimKey.GetValueKind("MonitoredKeys");
            }
            catch

            {
                _fimKey.SetValue("MonitoredKeys", new string[] { string.Empty }, RegistryValueKind.MultiString);
            }
            ExcludedExtensions = (_fimKey.GetValue("MonitoredKeys") as string[]).Where(path => !string.IsNullOrEmpty(path)).ToList();

            try
            {
                _ = _fimKey.GetValueKind("ExcludedKeys");
            }
            catch

            {
                _fimKey.SetValue("ExcludedKeys", new string[] { string.Empty }, RegistryValueKind.MultiString);
            }
            ExcludedExtensions = (_fimKey.GetValue("ExcludedKeys") as string[]).Where(path => !string.IsNullOrEmpty(path)).ToList();
        }

        private void ReadOrCreateFimKey()
        {
            if (_hklmSoftware?.OpenSubKey(FimKeyName, true) == null)
            {
                _fimKey = _hklmSoftware.CreateSubKey(FimKeyName, true);
            }
            else
            {
                _fimKey = _hklmSoftware.OpenSubKey(FimKeyName, true);
            }
        }
    }
#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8601 // Possible null reference assignment.

}
