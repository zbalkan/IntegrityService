using System;
using System.Linq;
using IntegrityService.Data;
using IntegrityService.FIM;
using IntegrityService.IO.Security;
using IntegrityService.Utils;
using Microsoft.Win32;
using NUlid;

namespace IntegrityService.IO
{
    /// <summary>
    ///     Static class to access Registry.
    /// </summary>
    internal static class Registry
    {
        /// <summary>
        ///     Root object for Registry operations
        /// </summary>
        /// <exception cref="System.Security.SecurityException" accessor="get"></exception>
        /// <exception cref="UnauthorizedAccessException" accessor="get"></exception>
        /// <exception cref="System.IO.IOException" accessor="get"></exception>
        public static RegistryKey Root => Microsoft.Win32.Registry
            .LocalMachine
            .OpenSubKey("Software", true)!
            .OpenSubKey(FimKeyName, true)! ?? Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software", true)!.CreateSubKey(FimKeyName, true);

        private const string FimKeyName = "FIM";

        /// <summary>
        ///     Generates new Registry change record from parameters and saves into database
        /// </summary>
        /// <param name="key">The registry key</param>
        /// <param name="valueName">The registry value name</param>
        /// <param name="valueData">The registry value data</param>
        /// <param name="changeCategory"><see cref="ChangeCategory"></param>
        public static void GenerateChange(RegistryKey key, string valueName, string valueData, ChangeCategory changeCategory)
        {
            var change = new RegistryChange
            {
                Id = Ulid.NewUlid().ToString(),
                ChangeCategory = changeCategory,
                ConfigChangeType = ConfigChangeType.Registry,
                Entity = key.Name,
                DateTime = DateTime.Now,
                Key = key.Name,
                Hive = Enum.GetName(ParseHive(key.Name)) ?? string.Empty,
                SourceComputer = Environment.MachineName,
                ValueName = valueName,
                ValueData = valueData,
                ACLs = key.GetACL()
            };
            Database.Context.RegistryChanges.Insert(change);
        }

        /// <summary>
        ///     Translates the Registry value data in Dword to Int32
        /// </summary>
        /// <param name="value">Name of the Registry value</param>
        /// <returns><see cref="int"></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="System.Security.SecurityException"></exception>
        /// <exception cref="System.IO.IOException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        public static int ReadDwordValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(value));
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

        /// <summary>
        ///     Translates the Registry value data from Multistring to <see cref="string[]">
        /// </summary>
        /// <param name="value">Name of the Registry value</param>
        /// <returns><see cref="string[]"></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="System.Security.SecurityException"></exception>
        /// <exception cref="System.IO.IOException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        public static string[] ReadMultiStringValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(value));
            }

            var valueData = Root.GetValue(value, null);

            return valueData == null || valueData is not string[] multiStringValue
                ? []
                : multiStringValue.Where(path => !string.IsNullOrEmpty(path)).ToArray();
        }

        /// <summary>
        ///     Save the <see cref="int"> value as Dword in Registry
        /// </summary>
        /// <param name="value">name of the Registry value</param>
        /// <param name="valueData">The <see cref="int"> value to be saved</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="System.Security.SecurityException"></exception>
        /// <exception cref="System.IO.IOException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        public static void WriteDwordValue(string value, int valueData)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(value));
            }

            Root.SetValue(value, valueData, RegistryValueKind.DWord);
        }

        /// <summary>
        ///     Save the <see cref="string[]"> value as MultiString in Registry
        /// </summary>
        /// <param name="value">name of the Registry value</param>
        /// <param name="valueData">The <see cref="string[]"> value to be saved</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="System.Security.SecurityException"></exception>
        /// <exception cref="System.IO.IOException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        public static void WriteMultiStringValue(string value, string[] valueData)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(value));
            }

            ArgumentNullException.ThrowIfNull(valueData);

            Root.SetValue(value, valueData);
        }
        private static RegistryHive ParseHive(string keyName)
        {
            if (keyName.Contains("HKEY_LOCAL_MACHINE"))
            {
                return RegistryHive.LocalMachine;
            }

            if (keyName.Contains("HKEY_CURRENT_USER"))
            {
                return RegistryHive.CurrentUser;
            }

            if (keyName.Contains("HKEY_CURRENT_CONFIG"))
            {
                return RegistryHive.CurrentConfig;
            }

            if (keyName.Contains("HKEY_CLASSES_ROOT"))
            {
                return RegistryHive.ClassesRoot;
            }
            else
            {
                return RegistryHive.Users;
            }
        }
    }
}
