using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace IntegrityService.Utils
{
    internal sealed class RegistryAcl
    {
        public string Owner { get; set; }

        public string? PrimaryGroupOfOwner { get; set; }

        public List<RegistryAce> Permissions { get; set; }

        public RegistryAcl(RegistryKey key)
        {
            var ac = key.GetAccessControl(AccessControlSections.All);
            Owner = ac.GetOwner(typeof(NTAccount))?.Value ?? string.Empty;
            PrimaryGroupOfOwner = ac.GetGroup(typeof(NTAccount))?.Value ?? string.Empty;

            Permissions = ac
                .GetAccessRules(true, true, typeof(NTAccount))
                .Cast<RegistryAccessRule>()
                .Select(rule => new RegistryAce(rule))
                .ToList();
        }
    }
}
