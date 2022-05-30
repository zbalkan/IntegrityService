using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace IntegrityService.Utils
{
    internal sealed class RegistryAce
    {
        public string UserOrGroup { get; set; }

        public List<string> Permissions { get; set; }

        public bool IsInherited { get; set; }

        public RegistryAce(RegistryAccessRule rule)
        {
            UserOrGroup = rule.IdentityReference.Value;
            Permissions = rule.RegistryRights.ListFlags().ToList();
            IsInherited = rule.IsInherited;
        }
    }
}
