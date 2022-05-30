using System.Collections.Generic;

namespace IntegrityService.Utils
{
    internal class AclBase
    {
        public string Owner { get; set; }

        public string? PrimaryGroupOfOwner { get; set; }

        public List<AceBase> Permissions { get; set; }
    }
}