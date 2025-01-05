﻿// {{ FIM }} Copyright (C) {{ 2022 }} {{ Zafer Balkan }}
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation, either version 3
// of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
using Utf8Json;

namespace IntegrityService.IO.Security
{
    /// <summary>
    ///     ACE and ACL related extension methods
    /// </summary>
    public static class ExtensionMethods
    {
        /// <summary>
        ///     Get custom formatted ACL
        /// </summary>
        /// <param name="path">
        ///     File path
        /// </param>
        /// <returns>
        ///     Custom formatted ACL
        /// </returns>
        /// <exception cref="System.Security.SecurityException">
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// </exception>
        /// <exception cref="PathTooLongException">
        /// </exception>
        public static string GetACL(this string path) => ToJson(new AccessControlList().OfFileSystem(new FileInfo(path)));

        /// <summary>
        ///     Get custom formatted ACL
        /// </summary>
        /// <param name="key">
        ///     Registry key
        /// </param>
        /// <returns>
        ///     Custom formatted ACL
        /// </returns>
        /// <exception cref="System.Security.SecurityException">
        /// </exception>
        /// <exception cref="IdentityNotMappedException">
        /// </exception>
        /// <exception cref="SystemException">
        /// </exception>
        public static string GetACL(this RegistryKey key) => ToJson(new AccessControlList().OfRegistryKey(key));

        private static IEnumerable<string> ListFlags<T>(this T value) where T : struct, Enum
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

        private static AccessControlList? OfFileSystem(this AccessControlList acl, FileInfo fileInfo)
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

        /// <summary>
        ///     Get formatted ACL of a Registry key
        /// </summary>
        /// <param name="acl">
        ///     ACL object
        /// </param>
        /// <param name="key">
        ///     Registry key
        /// </param>
        /// <returns>
        ///     Formatted ACL
        /// </returns>
        /// <exception cref="System.Security.SecurityException">
        /// </exception>
        /// <exception cref="IdentityNotMappedException">
        /// </exception>
        /// <exception cref="SystemException">
        /// </exception>
        private static AccessControlList OfRegistryKey(this AccessControlList acl, RegistryKey key)
        {
            try
            {
                var registryPermissions = key.GetAccessControl(AccessControlSections.All);
                acl.Owner = registryPermissions.GetOwner(typeof(NTAccount))?.Value ?? string.Empty;
                acl.PrimaryGroupOfOwner = registryPermissions.GetGroup(typeof(NTAccount))?.Value ?? string.Empty;
                acl.Permissions = registryPermissions
                    .GetAccessRules(true, true, typeof(NTAccount))
                    .Cast<RegistryAccessRule>()
                    .Select(rule => rule.ToAce())
                    .ToList();
            }
            catch (Exception)
            {
                // return same acl
            }

            return acl;
        }

        /// <summary>
        ///     Translate file owner name from SID
        /// </summary>
        /// <param name="fileSecurity">
        /// </param>
        /// <returns>
        ///     Translated owner name, original SID or empty string.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// </exception>
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

        /// <summary>
        ///     Translate primary group name from SID
        /// </summary>
        /// <param name="fileSecurity">
        ///     FileSecurity object to parse
        /// </param>
        /// <returns>
        ///     Transled group name, original SID or empty string.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// </exception>
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
                Debug.WriteLine("ex");
                return primaryGroup != null ? primaryGroup.ToString() : string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return string.Empty;
            }
        }

        private static AccessControlEntry ToAce(this RegistryAccessRule rule) => new()
        {
            UserOrGroup = rule.IdentityReference.Value,
            Permissions = rule.RegistryRights.ListFlags().ToList(),
            IsInherited = rule.IsInherited
        };

        private static AccessControlEntry ToAce(this FileSystemAccessRule rule) => new()
        {
            UserOrGroup = rule.IdentityReference.Value,
            Permissions = rule.FileSystemRights.ListFlags().ToList(),
            IsInherited = rule.IsInherited
        };

        private static string ToJson(AccessControlList? ac)
        {
            if (ac is null)
            {
                return string.Empty;
            }

            var json = Encoding.UTF8.GetString(JsonSerializer.Serialize(ac));

            return json ?? string.Empty;
        }
    }
}