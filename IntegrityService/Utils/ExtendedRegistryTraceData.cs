using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using IntegrityService.FIM;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Win32;

namespace IntegrityService.Utils
{
    public partial class ExtendedRegistryTraceData
    {
        public ChangeCategory ChangeCategory { get; set; }

        public double ElapsedTimeMSec { get; set; }

        public int EventIndex { get; set; }

        public string EventName { get; set; }

        public string FullName { get; set; }

        public RegistryHive Hive { get; }

        public int Index { get; set; }

        public RegistryKey? Key { get; set; }

        public string KeyName { get; }

        public int ProcessID { get; set; }

        public string ProcessName { get; set; }

        public int Status { get; set; }

        public int ThreadID { get; set; }

        public DateTime Timestamp { get; set; }

        public string? Username { get; set; }

        public string? UserSID { get; set; }

        public string? ValueData { get; }

        public string? ValueName { get; }

        public ExtendedRegistryTraceData(RegistryTraceData data, string fullName)
        {
            ElapsedTimeMSec = data.ElapsedTimeMSec;
            EventIndex = (int)data.EventIndex;
            EventName = data.OpcodeName;
            Index = data.Index;
            ProcessID = data.ProcessID;
            ProcessName = data.ProcessName;
            Status = data.Status;
            ThreadID = data.ThreadID;
            Timestamp = data.TimeStamp;
            KeyName = data.KeyName;
            ValueName = data.ValueName;

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

            FullName = fullName;
            Hive = ParseHive(FullName);

            using (var baseKey = RegistryKey.OpenBaseKey(Hive, RegistryView.Default))
            {
                Key = baseKey.OpenSubKey(StripFullName(fullName, ValueName), false);
                if (Key != null)
                {
                    if (KeyName?.Length == 0)
                    {
                        KeyName = Key.Name;
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
        }

        public override bool Equals(object? obj)
        {
            if (obj == null)
                return false;
            if (obj.GetType() != GetType())
                return false;
            if (ReferenceEquals(this, obj)) return true;

            var o = obj as ExtendedRegistryTraceData;
            return FullName == o!.FullName && Timestamp == o.Timestamp && ChangeCategory == o.ChangeCategory;
        }

        public override int GetHashCode() => HashCode.Combine(FullName, Timestamp, ChangeCategory, ProcessID, ThreadID);

        public override string ToString() => $"Timestamp: {Timestamp:O}\nEvent Name: {EventName}\nChange Category: {ChangeCategory}\nFull Name: {FullName}\nKey Name: {KeyName}\nValue Name: {ValueName}\nValue Data: {ValueData}\nProcess: {ProcessName} (PID: {ProcessID})\nUser Info: {Username} (SID: {UserSID})";

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

        [GeneratedRegex(@"^(?:[^\\]+\\)?(.*?)(?:\\[^\\]*)?$")]
        private static partial Regex StrippedKeyNameRegex();

        private string? ExtractValueData()
        {
            if (string.IsNullOrEmpty(ValueName))
            {
                return null;
            }

            var o = Key!.GetValue(ValueName);
            string? result = null;
            if (o != null && !string.IsNullOrEmpty(o.ToString()))
            {
                switch (Key.GetValueKind(ValueName))
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
    }
}