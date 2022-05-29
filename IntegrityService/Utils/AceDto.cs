using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;

namespace IntegrityService.Utils
{
    public class AceDto
    {
        public string UserOrGroup { get; set; }

        public List<string> Permissions { get; set; }

        public bool IsInherited { get; set; }

        public AceDto(FileSystemAccessRule rule)
        {
            UserOrGroup = rule.IdentityReference.Value;
            Permissions = rule.FileSystemRights.ListFlags().ToList();
            IsInherited = rule.IsInherited;
        }
    }
}