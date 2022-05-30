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
            Owner = FileSystem.OwnerName(fileSystemSecurity);
            PrimaryGroupOfOwner = FileSystem.PrimaryGroupOfOwnerName(fileSystemSecurity);

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
