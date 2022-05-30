using System.Linq;
using System.Security.AccessControl;

namespace IntegrityService.Utils
{
    internal sealed class FileSystemAce : AceBase
    {
        public FileSystemAce(FileSystemAccessRule rule)
        {
            UserOrGroup = rule.IdentityReference.Value;
            Permissions = rule.FileSystemRights.ListFlags().ToList();
            IsInherited = rule.IsInherited;
        }
    }
}