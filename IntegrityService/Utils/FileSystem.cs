using IntegrityService.FIM;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading.Tasks;

namespace IntegrityService.Utils
{
    internal static class FileSystem
    {
        private static readonly SHA256 Sha256 = SHA256.Create();

        internal static void StartSearch(List<string> pathsToSearch, List<string> excludedPaths, List<string> excludedExtensions, bool useDigest = true)
        {
            if (pathsToSearch is null)
            {
                throw new ArgumentNullException(nameof(pathsToSearch));
            }

            if (excludedPaths is null)
            {
                throw new ArgumentNullException(nameof(excludedPaths));
            }

            if (excludedExtensions is null)
            {
                throw new ArgumentNullException(nameof(excludedExtensions));
            }

            var defaultEnumOptions = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = false
            };

            Parallel.ForEach(pathsToSearch, path => SearchFiles(path, defaultEnumOptions, excludedPaths, excludedExtensions, useDigest));
        }

        internal static bool IsExcluded(string path, List<string> excludedPaths, List<string> excludedExtensions)
        {
            var isFile = IsFile(path);
            if (isFile == null) return true;

            bool result;
            if (isFile.Value)
            {
#pragma warning disable U2U1212 // Capture intermediate results in lambda expressions
                result = (excludedPaths ??
                          throw new InvalidOperationException()) // If file, sanitize file path and check. Then, check extensions.
                         .Any(excludedPath =>
                             Path.GetFileName(path).Contains(Path.GetFileName(excludedPath)!,
                                 StringComparison.OrdinalIgnoreCase)) ||
                         (excludedExtensions ?? throw new InvalidOperationException())
                         .Any(extension =>
                             extension.Contains(Path.GetExtension(path), StringComparison.OrdinalIgnoreCase));
#pragma warning restore U2U1212 // Capture intermediate results in lambda expressions
            }
            else
            {
                result = (excludedPaths ??
                          throw new InvalidOperationException()) // If directory, sanitize directory path and check
                    .Any(excludedPath =>
                        Path.GetDirectoryName(path)!.Contains(Path.GetDirectoryName(excludedPath)!,
                            StringComparison.OrdinalIgnoreCase));
            }

            return result;
        }

        internal static bool? IsFile(string fullPath)
        {
            try
            {
                return (File.GetAttributes(fullPath) & FileAttributes.Directory) != FileAttributes.Directory;
            }
            catch (Exception ex)
            {
                // TODO: Log properly
                Debug.WriteLine(ex.Message);
                return null;
            }
        }

        /// <summary> 
        ///     A safe way to get all the files in a directory and sub directory without crashing on UnauthorizedException or PathTooLongException 
        /// </summary> 
        /// <param name="directoryPath">Starting directory</param>
        /// <param name="options">Enumeration options</param>
        /// <param name="excludedPaths"></param>
        /// <param name="excludedExtensions"></param>
        /// <returns>List of files</returns> 
        private static void SearchFiles(string directoryPath, EnumerationOptions options, List<string> excludedPaths, List<string> excludedExtensions, bool useDigest)
        {
            if (IsExcluded(directoryPath, excludedPaths, excludedExtensions)) return;

            try
            {
                foreach (var directory in Directory.EnumerateDirectories(directoryPath))
                {
                    WriteDiscoveryToDatabase(directory, useDigest);
                    SearchFiles(directory, options, excludedPaths, excludedExtensions, useDigest);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (IOException ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(directoryPath, "*.*", options).Where(f => !IsExcluded(f, excludedPaths, excludedExtensions)))
                {
                    WriteDiscoveryToDatabase(file, useDigest);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (IOException ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public static string OwnerName(FileSecurity fileSecurity)
        {
            if (fileSecurity is null)
            {
                throw new ArgumentNullException(nameof(fileSecurity));
            }
            IdentityReference? sid = null;
            try
            {
                sid = fileSecurity.GetOwner(typeof(SecurityIdentifier));
                if (sid == null) return string.Empty;

                var ntAccount = sid.Translate(typeof(NTAccount)) as NTAccount;
                if (ntAccount == null) return string.Empty;

                return ntAccount.Value;
            }
            catch (IdentityNotMappedException)
            {
                return sid != null ? sid.ToString() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string PrimaryGroupOfOwnerName(FileSecurity fileSecurity)
        {
            if (fileSecurity is null)
            {
                throw new ArgumentNullException(nameof(fileSecurity));
            }

            IdentityReference? primaryGroup = null;
            try
            {
                primaryGroup = fileSecurity.GetGroup(typeof(SecurityIdentifier));
                if (primaryGroup == null) return string.Empty;

                var ntAccount = primaryGroup.Translate(typeof(NTAccount)) as NTAccount;
                if (ntAccount == null) return string.Empty;

                return ntAccount.Value;
            }
            catch (IdentityNotMappedException)
            {
                return primaryGroup != null ? primaryGroup.ToString() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void WriteDiscoveryToDatabase(string path, bool useDigest)
        {
            var change = new FileSystemChange
            {
                Id = Guid.NewGuid(),
                ChangeCategory = ChangeCategory.Discovery,
                ConfigChangeType = ConfigChangeType.FileSystem,
                Entity = path,
                DateTime = DateTime.Now,
                FullPath = path,
                SourceComputer = Environment.MachineName,
                CurrentHash = useDigest ? CalculateFileDigest(path) : string.Empty,
                PreviousHash = string.Empty,
                ACLs = path.GetACL()
            };

            Database.Context.FileSystemChanges.Insert(change);
        }

        public static string CalculateFileDigest(string path)
        {
            var digest = string.Empty;
            var isFile = IsFile(path);
            if (isFile != null && isFile.Value)
            {
                try
                {
                    using var fileStream = File.OpenRead(path);
                    digest = Convert.ToHexString(Sha256.ComputeHash(fileStream));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }
            return digest;
        }
    }
}
