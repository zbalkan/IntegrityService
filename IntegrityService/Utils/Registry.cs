using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IntegrityService.Utils
{
    internal static class Registry
    {
#pragma warning disable CS8601 // Possible null reference assignment.
        private static readonly RegistryKey HklmSoftware = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software", true);
#pragma warning restore CS8601 // Possible null reference assignment.

        private const string FimKeyName = "FIM";

        public static RegistryKey Root
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8603 // Possible null reference return.
            get
            {
                RegistryKey result;

                if (HklmSoftware?.OpenSubKey(FimKeyName, true) == null)
                {
                    result = HklmSoftware.CreateSubKey(FimKeyName, true);

                }
                else
                {
                    result = HklmSoftware.OpenSubKey(FimKeyName, true);
                }

                return result;
            }
#pragma warning restore CS8603 // Possible null reference return.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        }

        public static void WriteMultiStringValue(string value, IEnumerable<string> valueData, bool overwrite = false)
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

            var valueData = Root.GetValue(value);
            return valueData != null ? ((string[])valueData).Where(path => !string.IsNullOrEmpty(path)).ToList() : new List<string>(0);
        }

        public static void WriteDwordValue(string value, int valueData, bool overwrite = false)
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
                return 0;
            }

            if (int.TryParse(valueData.ToString(), out var valueDataAsInt))
            {
                return valueDataAsInt;
            }

            return 0;
        }
    }
}
