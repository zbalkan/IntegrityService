using System.Collections.Generic;

namespace IntegrityService.IO.Security
{
    public class AccessControlList
    {
        public string Owner { get; set; }

        public string? PrimaryGroupOfOwner { get; set; }

        public List<AccessControlEntry> Permissions { get; set; }
    }
}