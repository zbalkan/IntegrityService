using System.Collections.Generic;

namespace IntegrityService.Utils
{
    public class AccessControlEntry
    {
        public string UserOrGroup { get; set; }

        public List<string> Permissions { get; set; }

        public bool IsInherited { get; set; }
    }
}