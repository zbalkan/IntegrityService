using System;
using System.Diagnostics;
using System.Linq;
using IntegrityService.FIM;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Win32;

namespace IntegrityService.Utils
{
    public class ExtendedRegistryTraceData
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

            FullName = fullName;
            Hive = ParseHive(FullName);

            var stripped = fullName;
            if (!string.IsNullOrEmpty(ValueName))
            {
                // Remove value to get the full key path and clean up forward slash if needed
                stripped = fullName.Replace(ValueName, string.Empty);
                if (stripped.EndsWith('\\'))
                {
                    stripped = stripped.Substring(0, stripped.Length - 1);
                }
            }
            // Remove the Hive name
            stripped = stripped.Substring(stripped.IndexOf('\\') + 1);

            var baseKey = RegistryKey.OpenBaseKey(Hive, RegistryView.Default);

            Key = baseKey.OpenSubKey(stripped, false);

            if (Key != null)
            {
                if (KeyName?.Length == 0)
                {
                    KeyName = Key.Name;
                }

                ValueData = ExtractValueData();
            }

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

            var userInfo = Process.GetProcessById(ProcessID).Owner();
            Username = userInfo.Name;
            UserSID = userInfo.User?.Value ?? string.Empty;
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
    }
}