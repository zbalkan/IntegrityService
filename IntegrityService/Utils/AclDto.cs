using System.Collections.Generic;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Linq;

namespace IntegrityService.Utils
{
    public class AclDto
    {
        public string Owner { get; set; }

        public string? PrimaryGroupOfOwner { get; set; }

        public List<AceDto> Permissions { get; set; }

        public AclDto(FileSystemSecurity fileSystemSecurity)
        {
            Owner = fileSystemSecurity.GetOwner(typeof(NTAccount))?.Value ?? string.Empty;
            PrimaryGroupOfOwner = fileSystemSecurity.GetGroup(typeof(NTAccount))?.Value ?? string.Empty;

            Permissions = fileSystemSecurity
                .GetAccessRules(true, true, typeof(NTAccount))
                .Cast<FileSystemAccessRule>()
                .Select(rule => new AceDto(rule))
                .ToList();
        }
    }
}
