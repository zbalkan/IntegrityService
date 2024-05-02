using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IntegrityService.Utils
{
    internal static class Registry
    {
        private const string FimKeyName = "FIM";

#pragma warning disable CS8602 // Dereference of a possibly null reference.
        public static RegistryKey Root => Microsoft.Win32.Registry
            .LocalMachine
            .OpenSubKey("Software", true)
            .OpenSubKey(FimKeyName, true) ?? Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software", true).CreateSubKey(FimKeyName, true);
#pragma warning restore CS8602 // Dereference of a possibly null reference.

        public static void WriteMultiStringValue(string value, IEnumerable<string> valueData, bool overwrite = true)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException($"'{nameof(value)}' cannot be null or empty.", nameof(value));
            }

            if (valueData is null)
            {
                throw new ArgumentNullException(nameof(valueData));
            }

            try
            {
                _ = Root.GetValueKind(value);
                if (overwrite)
                {
                    Root.SetValue(value, valueData);
                }
            }
            catch (Exception)
            {
                Root.SetValue(value, valueData);
            }
        }

        public static List<string> ReadMultiStringValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException($"'{nameof(value)}' cannot be null or empty.", nameof(value));
            }

            var valueData = Root.GetValue(value, new List<string>());

            //if (valueData == null) return [];

            var multiStringValue = new List<string>();

            if (multiStringValue.Count == 0) return [];

            multiStringValue.AddRange(valueData as List<string>);

            if (multiStringValue.Count == 0) return [];
            return
                multiStringValue.Where(path => !string.IsNullOrEmpty(path)).ToList();
        }

        public static void WriteDwordValue(string value, int valueData, bool overwrite = true)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException($"'{nameof(value)}' cannot be null or empty.", nameof(value));
            }

            try
            {
                _ = Root.GetValueKind(value);
                if (overwrite)
                {
                    Root.SetValue(value, valueData, RegistryValueKind.DWord);
                }
            }
            catch
            {
                Root.SetValue(value, valueData, RegistryValueKind.DWord);
            }
        }

        public static int ReadDwordValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException($"'{nameof(value)}' cannot be null or empty.", nameof(value));
            }

            var valueData = Root.GetValue(value);

            if (valueData == null)
            {
                return -1;
            }

            if (int.TryParse(valueData.ToString(), out var valueDataAsInt))
            {
                return valueDataAsInt;
            }

            return 0;
        }
    }
}
