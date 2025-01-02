﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using IntegrityService.IO.Security;
using IntegrityService.Utils;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Win32;
using NUlid;

namespace IntegrityService.FIM
{
    public partial class RegistryChange : Change
    {
        public string Hive { get; set; }

        public string KeyName { get; set; }

        public string? ValueData { get; set; }

        public string? ValueName { get; set; }

        public string? Username { get; set; }

        public string? UserSID { get; set; }

        public string EventName { get; set; }

        private readonly RegistryKey? _key;

        public int ProcessID { get; set; }

        public string ProcessName { get; set; }

        public RegistryChange(RegistryTraceData data, string fullName)
        {
            Id = Ulid.NewUlid().ToString();
            EventName = data.OpcodeName;
            ProcessID = data.ProcessID;
            ProcessName = data.ProcessName;
            ValueName = data.ValueName ?? string.Empty;
            ConfigChangeType = ConfigChangeType.Registry;
            SourceComputer = Environment.MachineName;
            DateTime = data.TimeStamp;
            KeyName = data.KeyName;

            switch ((RegistryEventCategory)(int)data.Opcode)
            {
                case RegistryEventCategory.Create:
                    ChangeCategory = ChangeCategory.Created;
                    break;

                case RegistryEventCategory.SetValue:
                case RegistryEventCategory.SetInformation:
                    ChangeCategory = ChangeCategory.Changed;
                    break;

                case RegistryEventCategory.Delete:
                case RegistryEventCategory.DeleteValue:
                    ChangeCategory = ChangeCategory.Deleted;
                    break;
            }

            Entity = fullName;

            var hive = ParseHive(fullName);

            Hive = Enum.GetName(hive) ?? string.Empty;

            using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default))
            {
                _key = baseKey.OpenSubKey(StripFullName(fullName, ValueName), false);
                if (_key != null)
                {
                    if (KeyName?.Length == 0)
                    {
                        KeyName = _key.Name;
                    }
                    if (ChangeCategory != ChangeCategory.Deleted)
                    {
                        ValueData = ExtractValueData();
                    }
                }
            }

            var process = Process.GetProcessById(ProcessID);
            if (process != null)
            {
                ProcessName = process.ProcessName;
                try
                {
                    var userInfo = process.Owner();
                    Username = userInfo.Name;
                    UserSID = userInfo.User?.Value ?? string.Empty;
                }
                catch
                {
                    // ignore
                }
            }
            else
            {
                ProcessName = data.ProcessName;
            }

            ACLs = _key?.GetACL() ?? string.Empty;
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

        [GeneratedRegex(@"^[^\\]+\\(.+?)(\\)?$")]
        private static partial Regex StrippedKeyNameRegex();

        private string? ExtractValueData()
        {
            if (string.IsNullOrEmpty(ValueName))
            {
                return null;
            }

            var o = _key!.GetValue(ValueName);
            string? result = null;
            if (o != null && !string.IsNullOrEmpty(o.ToString()))
            {
                switch (_key.GetValueKind(ValueName))
                {
                    case RegistryValueKind.DWord:
                        result = Convert.ToString((int)o);
                        break;

                    case RegistryValueKind.QWord:
                        result = Convert.ToString((long)o);
                        break;

                    case RegistryValueKind.String:
                    case RegistryValueKind.ExpandString:
                        result = o!.ToString();
                        break;

                    case RegistryValueKind.Binary:
                        result = string.Join(" ", ((byte[])o).Select(b => $"{b:x2}"));
                        break;

                    case RegistryValueKind.MultiString:
                        result = string.Join(" ", (string[])o);
                        break;
                }
            }
            return result;
        }

        private string StripFullName(string fullName, string valueName)
        {
            if (string.IsNullOrEmpty(fullName))
                return string.Empty;

            // Remove the ValueName if provided
            if (!string.IsNullOrEmpty(valueName))
            {
                var valuePattern = $@"\\{Regex.Escape(valueName)}$";
                fullName = Regex.Replace(fullName, valuePattern, string.Empty); // Remove ValueName
            }

            // Apply regex to strip the hive name and clean the full key path
            return StrippedKeyNameRegex().Replace(fullName, "$1");
        }

        public override string ToString() => $"Timestamp: {DateTime:O}\nEvent Name: {EventName}\nChange Category: {ChangeCategory}\nEntity: {Entity}\nKey Name: {KeyName}\nValue Name: {ValueName}\nValue Data: {ValueData}\nProcess: {ProcessName} (PID: {ProcessID})\nUser Info: {Username} (SID: {UserSID})";
    }
}