using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace IntegrityService
{
#pragma warning disable CS8601 // Possible null reference assignment.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    internal sealed class Settings
    {
        public List<string> MonitoredPaths { get; private set; }

        public List<string> ExcludedPaths { get; private set; }

        public List<string> ExcludedExtensions { get; private set; }

        internal static Settings Instance => Lazy.Value;

        private static readonly Lazy<Settings> Lazy = new(() => new Settings());

        private readonly RegistryKey HklmSoftware = Registry.LocalMachine.OpenSubKey("Software", true);

        private const string fimKeyName = "FIM";

        private RegistryKey? fimKey;
        private Settings()
        {
            ReadOrCreateFimKey();
            ReadOrCreateSubKeys();

        }

        private void ReadOrCreateSubKeys()
        {
            try
            {
                _ = fimKey.GetValueKind("MonitoredPaths");
            }
            catch

            {
                fimKey.SetValue("MonitoredPaths", new string[] { string.Empty }, RegistryValueKind.MultiString);
            }
            MonitoredPaths = (fimKey.GetValue("MonitoredPaths") as string[]).Where(path => !string.IsNullOrEmpty(path)).ToList();

            try
            {
                _ = fimKey.GetValueKind("ExcludedPaths");
            }
            catch

            {
                fimKey.SetValue("ExcludedPaths", new string[] { string.Empty }, RegistryValueKind.MultiString);
            }
            ExcludedPaths = (fimKey.GetValue("ExcludedPaths") as string[]).Where(path => !string.IsNullOrEmpty(path)).ToList();

            try
            {
                _ = fimKey.GetValueKind("ExcludedExtensions");
            }
            catch

            {
                fimKey.SetValue("ExcludedExtensions", new string[] { string.Empty }, RegistryValueKind.MultiString);
            }
            ExcludedExtensions = (fimKey.GetValue("ExcludedExtensions") as string[]).Where(path => !string.IsNullOrEmpty(path)).ToList();
        }

        private void ReadOrCreateFimKey()
        {
            if (HklmSoftware?.OpenSubKey(fimKeyName, true) == null)
            {
                fimKey = HklmSoftware.CreateSubKey(fimKeyName, true);
            }
            else
            {
                fimKey = HklmSoftware.OpenSubKey(fimKeyName, true);
            }
        }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8601 // Possible null reference assignment.

}
