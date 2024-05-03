using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Utf8Json;

namespace IntegrityService.Utils
{
    /// <summary>
    ///     Miscelaneous extension methods
    /// </summary>
    public static class ExtensionMethods
    {
        public static void AddRange<T>(this ConcurrentBag<T> @this, IEnumerable<T> toAdd)
        {
            foreach (var element in toAdd)
            {
                @this.Add(element);
            }
        }

        /// <summary>
        ///     Get formatted ACL of a Registry key
        /// </summary>
        /// <param name="acl">ACL object</param>
        /// <param name="key">Registry key</param>
        /// <returns>Formatted ACL</returns>
        /// <exception cref="System.Security.SecurityException"></exception>
        /// <exception cref="IdentityNotMappedException"></exception>
        /// <exception cref="SystemException"></exception>
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

        public static AccessControlList? OfFileSystem(this AccessControlList acl, FileInfo fileInfo)
        {
            FileSecurity fileSystemSecurity;
            try
            {
                fileSystemSecurity = fileInfo.GetAccessControl();
            }
            catch (FileNotFoundException)
            {
                return default;
            }

            acl.Owner = OwnerName(fileSystemSecurity);
            acl.PrimaryGroupOfOwner = PrimaryGroupOfOwnerName(fileSystemSecurity);
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

        /// <summary>
        ///     Get custom formatted ACL
        /// </summary>
        /// <param name="path">File path</param>
        /// <returns>Custom formatted ACL</returns>
        /// <exception cref="System.Security.SecurityException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="PathTooLongException"></exception>
        public static string GetACL(this string path) => ToJson(new AccessControlList().OfFileSystem(new FileInfo(path)));

        /// <summary>
        ///     Get custom formatted ACL
        /// </summary>
        /// <param name="key">Registry key</param>
        /// <returns>Custom formatted ACK</returns>
        /// <exception cref="System.Security.SecurityException"></exception>
        /// <exception cref="IdentityNotMappedException"></exception>
        /// <exception cref="SystemException"></exception>
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
            while (ex != null)
            {
                var sb = new StringBuilder(120).Append("Message: ")
                    .AppendLine(ex.Message)
                    .Append("Stacktrace: ")
                    .AppendLine(ex.StackTrace);

                logger.LogError("Exception: {ex}", sb.ToString());

                ex = ex.InnerException;
            }
        }

        private static string ToJson(AccessControlList? ac)
        {
            if (ac is null)
            {
                return string.Empty;
            }

            var json = Encoding.UTF8.GetString(JsonSerializer.Serialize(ac));

            return json ?? string.Empty;
        }

        /// <summary>
        ///     Translate primary group name from SID
        /// </summary>
        /// <param name="fileSecurity">FileSecurity object to parse</param>
        /// <returns>Transled group name, original SID or empty string.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        private static string PrimaryGroupOfOwnerName(FileSecurity fileSecurity)
        {
            ArgumentNullException.ThrowIfNull(fileSecurity);

            IdentityReference? primaryGroup = null;
            try
            {
                primaryGroup = fileSecurity.GetGroup(typeof(SecurityIdentifier));
                if (primaryGroup == null)
                {
                    return string.Empty;
                }

                if (primaryGroup.Translate(typeof(NTAccount)) is not NTAccount ntAccount)
                {
                    return primaryGroup.Value;
                }

                return ntAccount.Value;
            }
            catch (IdentityNotMappedException ex)
            {
                Debug.WriteLine(ex);
                return primaryGroup != null ? primaryGroup.ToString() : string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return string.Empty;
            }
        }

        /// <summary>
        ///     Translate file owner name from SID
        /// </summary>
        /// <param name="fileSecurity"></param>
        /// <returns>Translated owner name, original SID or empty string.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        private static string OwnerName(FileSecurity fileSecurity)
        {
            ArgumentNullException.ThrowIfNull(fileSecurity);
            IdentityReference? sid = null;
            try
            {
                sid = fileSecurity.GetOwner(typeof(SecurityIdentifier));
                if (sid == null)
                {
                    return string.Empty;
                }

                if (sid.Translate(typeof(NTAccount)) is not NTAccount ntAccount)
                {
                    return sid.Value;
                }

                return ntAccount.Value;
            }
            catch (IdentityNotMappedException ex)
            {
                Debug.WriteLine(ex);
                return sid != null ? sid.ToString() : string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return string.Empty;
            }
        }
    }
}
