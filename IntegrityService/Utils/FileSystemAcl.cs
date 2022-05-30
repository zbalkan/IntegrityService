using System.Collections.Generic;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Linq;
using System.IO;

namespace IntegrityService.Utils
{
    public class FileSystemAcl
    {
        public string Owner { get; set; }

        public string? PrimaryGroupOfOwner { get; set; }

        public List<FileSystemAce> Permissions { get; set; }

        public FileSystemAcl(FileInfo fileInfo)
        {
            var fileSystemSecurity = fileInfo.GetAccessControl();
            Owner = fileSystemSecurity.GetOwner(typeof(NTAccount))?.Value ?? string.Empty;
            PrimaryGroupOfOwner = fileSystemSecurity.GetGroup(typeof(NTAccount))?.Value ?? string.Empty;

            Permissions = fileSystemSecurity
                .GetAccessRules(true, true, typeof(NTAccount))
                .Cast<FileSystemAccessRule>()
                .Select(rule => new FileSystemAce(rule))
                .ToList();
        }
    }
}
