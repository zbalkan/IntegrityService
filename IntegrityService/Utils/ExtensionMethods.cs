using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace IntegrityService.Utils
{
    public static class ExtensionMethods
    {
        public static void AddRange<T>(this ConcurrentBag<T> @this, IEnumerable<T> toAdd)
        {
            foreach (var element in toAdd)
            {
                @this.Add(element);
            }
        }

        public static AccessControlList OfRegistryKey(this AccessControlList acl, RegistryKey key)
        {
            var registryPermissions = key.GetAccessControl(AccessControlSections.All);
            acl.Owner = registryPermissions.GetOwner(typeof(NTAccount))?.Value ?? string.Empty;
            acl.PrimaryGroupOfOwner = registryPermissions.GetGroup(typeof(NTAccount))?.Value ?? string.Empty;
            acl.Permissions = registryPermissions
                .GetAccessRules(true, true, typeof(NTAccount))
                .Cast<RegistryAccessRule>()
                .Select(rule => rule.ToAce())
                .ToList();

            return acl;
        }

        public static AccessControlList OfFileSystem(this AccessControlList acl, FileInfo fileInfo)
        {
            var fileSystemSecurity = fileInfo.GetAccessControl();
            acl.Owner = FileSystem.OwnerName(fileSystemSecurity);
            acl.PrimaryGroupOfOwner = FileSystem.PrimaryGroupOfOwnerName(fileSystemSecurity);
            acl.Permissions = fileSystemSecurity
                .GetAccessRules(true, true, typeof(NTAccount))
                .Cast<FileSystemAccessRule>()
                .Select(rule => rule.ToAce())
                .ToList();

            return acl;
        }

        public static AccessControlEntry ToAce(this RegistryAccessRule rule) => new()
        {
            UserOrGroup = rule.IdentityReference.Value,
            Permissions = rule.RegistryRights.ListFlags().ToList(),
            IsInherited = rule.IsInherited
        };

        public static AccessControlEntry ToAce(this FileSystemAccessRule rule) => new()
        {
            UserOrGroup = rule.IdentityReference.Value,
            Permissions = rule.FileSystemRights.ListFlags().ToList(),
            IsInherited = rule.IsInherited
        };

        public static string GetACL(this string path) => ToJson(new AccessControlList().OfFileSystem(new FileInfo(path)));

        public static string GetACL(this RegistryKey key) => ToJson(new AccessControlList().OfRegistryKey(key));

        public static IEnumerable<string> ListFlags<T>(this T value) where T : struct, Enum
        {
            // Check that this is really a "flags" enum:
            if (!Attribute.IsDefined(typeof(T), typeof(FlagsAttribute)))
            {
                yield return Enum.GetName(value) ?? string.Empty;
                yield break;
            }

            var names = Enum.GetNames(typeof(T));
            foreach (var flag in names)
            {
                yield return flag;
            }
        }

        public static void Log(this Exception? ex, ILogger logger)
        {
            while (true)
            {
                if (ex == null)
                {
                    break;
                }

                var sb = new StringBuilder(120).Append("Message: ")
                    .AppendLine(ex.Message)
                    .Append("Stacktrace: ")
                    .AppendLine(ex.StackTrace);

                logger.LogError("Exception: {ex}", sb.ToString());

                ex = ex.InnerException;
            }
        }

        private static string ToJson(AccessControlList ac)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                ReadCommentHandling = JsonCommentHandling.Skip,
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(ac, options);

            return json ?? string.Empty;
        }

    }
}
