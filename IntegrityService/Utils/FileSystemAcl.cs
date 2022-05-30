using System.Security.AccessControl;
using System.Security.Principal;
using System.Linq;
using System.IO;

namespace IntegrityService.Utils
{
    internal sealed class FileSystemAcl : AclBase
    {
        public FileSystemAcl(FileInfo fileInfo)
        {
            var fileSystemSecurity = fileInfo.GetAccessControl();
            Owner = fileSystemSecurity.GetOwner(typeof(NTAccount))?.Value ?? string.Empty;
            PrimaryGroupOfOwner = fileSystemSecurity.GetGroup(typeof(NTAccount))?.Value ?? string.Empty;

#pragma warning disable U2U1007 // Do not call redundant functions
            Permissions = fileSystemSecurity
                .GetAccessRules(true, true, typeof(NTAccount))
                .Cast<FileSystemAccessRule>()
                .Select(rule => new FileSystemAce(rule))
                .Cast<AceBase>()
                .ToList();
#pragma warning restore U2U1007 // Do not call redundant functions
        }
    }
}
