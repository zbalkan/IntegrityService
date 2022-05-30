using Microsoft.Win32;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;

namespace IntegrityService.Utils
{
    internal sealed class RegistryAcl : AclBase
    {
        public RegistryAcl(RegistryKey key)
        {
            var registryPermissions = key.GetAccessControl(AccessControlSections.All);
            Owner = registryPermissions.GetOwner(typeof(NTAccount))?.Value ?? string.Empty;
            PrimaryGroupOfOwner = registryPermissions.GetGroup(typeof(NTAccount))?.Value ?? string.Empty;

#pragma warning disable U2U1007 // Do not call redundant functions
            Permissions = registryPermissions
                .GetAccessRules(true, true, typeof(NTAccount))
                .Cast<RegistryAccessRule>()
                .Select(rule => new RegistryAce(rule))
                .Cast<AceBase>()
                .ToList();
#pragma warning restore U2U1007 // Do not call redundant functions
        }
    }
}
