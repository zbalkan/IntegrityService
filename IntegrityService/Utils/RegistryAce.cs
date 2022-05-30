using System.Linq;
using System.Security.AccessControl;

namespace IntegrityService.Utils
{
    internal sealed class RegistryAce : AceBase
    {
        public RegistryAce(RegistryAccessRule rule)
        {
            UserOrGroup = rule.IdentityReference.Value;
            Permissions = rule.RegistryRights.ListFlags().ToList();
            IsInherited = rule.IsInherited;
        }
    }
}
