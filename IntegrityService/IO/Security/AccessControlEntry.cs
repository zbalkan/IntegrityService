using System.Collections.Generic;

namespace IntegrityService.IO.Security
{
    public class AccessControlEntry
    {
        public bool IsInherited { get; set; }

        public List<string> Permissions { get; set; }

        public string UserOrGroup { get; set; }
    }
}