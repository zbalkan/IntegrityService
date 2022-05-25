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


        private readonly RegistryKey HklmSoftware = Registry.LocalMachine.OpenSubKey("Software");

        private const string fimKeyName = "FIM";

        private RegistryKey? fimKey;
        private Settings()
        {
            ReadOrCreateFimKey();
            ReadOrCreateSubKeys();

        }

        private void ReadOrCreateSubKeys()
        {
            if (fimKey.GetValueKind("MonitoredPaths") == default)
            {
                fimKey.SetValue("MonitoredPaths", string.Empty);
            }
            MonitoredPaths = (fimKey.GetValue("MonitoredPaths") as string[]).Where(path => !string.IsNullOrEmpty(path)).ToList();

            if (fimKey.GetValueKind("ExcludedPaths") == default)
            {
                fimKey.SetValue("ExcludedPaths", string.Empty);
            }
            ExcludedPaths = (fimKey.GetValue("ExcludedPaths") as string[]).Where(path => !string.IsNullOrEmpty(path)).ToList();

            if (fimKey.GetValueKind("ExcludedExtensions") == default)
            {
                fimKey.SetValue("ExcludedExtensions", string.Empty);
            }
            ExcludedExtensions = (fimKey.GetValue("ExcludedExtensions") as string[]).Where(path => !string.IsNullOrEmpty(path)).ToList();
        }

        private void ReadOrCreateFimKey()
        {
            if (HklmSoftware?.OpenSubKey(fimKeyName) == null)
            {
                fimKey = HklmSoftware.CreateSubKey(fimKeyName);
            }
            else
            {
                fimKey = HklmSoftware.OpenSubKey(fimKeyName);
            }
        }
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8601 // Possible null reference assignment.

}
