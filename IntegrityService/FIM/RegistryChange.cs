using IntegrityService.IO.Security;
using IntegrityService.Utils;
using NUlid;
using System;

namespace IntegrityService.FIM
{
    public class RegistryChange : Change
    {
        public string Hive { get; set; }

        public string Key { get; set; }

        public string ValueData { get; set; }

        public string ValueName { get; set; }

        public string? Username { get; set; }

        public string? UserSID { get; set; }

        /// <summary>
        /// Generates new Registry change record from parameters
        /// </summary>
        /// <param name="eev"></param>
        /// <returns></returns>
        public static RegistryChange FromTrace(ExtendedRegistryTraceData eev) => new RegistryChange
        {
            Id = Ulid.NewUlid().ToString(),
            ChangeCategory = eev.ChangeCategory,
            ConfigChangeType = ConfigChangeType.Registry,
            Entity = eev.FullName,
            DateTime = DateTime.Now,
            Key = eev.FullName,
            Hive = Enum.GetName(eev.Hive) ?? string.Empty,
            SourceComputer = Environment.MachineName,
            ValueName = eev.ValueName ?? string.Empty,
            ValueData = eev.ValueData ?? string.Empty,
            ACLs = eev.Key?.GetACL() ?? string.Empty,
            Username = eev.Username,
            UserSID = eev.UserSID
        };
    }
}