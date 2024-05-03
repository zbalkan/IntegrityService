using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Filesystem.Ntfs;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading.Tasks;
using IntegrityService.FIM;
using NUlid;

namespace IntegrityService.Utils
{
    internal static class FileSystem
    {
        public static string CalculateFileDigest(string path)
        {
            var digest = string.Empty;

            try
            {
                var fileStream = new FileStream(path, FileMode.OpenOrCreate,
            FileAccess.Read);
                using var bufferedStream = new BufferedStream(fileStream, 1024 * 32);
                var sha = SHA256.Create();
                var checksum = sha.ComputeHash(bufferedStream);
                digest = BitConverter.ToString(checksum).Replace("-", string.Empty);
            }
            catch (UnauthorizedAccessException ex)
            {
                // Access denied
                Debug.WriteLine(ex);
            }
            catch (IOException ex)
            {
                // File is locked by another process
                Debug.WriteLine(ex);
            }
            return digest;
        }

        public static bool IsExcluded(string path)
        {
            foreach (var excluded in Settings.Instance.ExcludedPaths)
            {
                if (path.StartsWith(excluded, StringComparison.InvariantCultureIgnoreCase)) { return true; }
            }
            return false;
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
                if (sid == null)
                {
                    return string.Empty;
                }

                var ntAccount = sid.Translate(typeof(NTAccount)) as NTAccount;
                if (ntAccount == null)
                {
                    return string.Empty;
                }

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
                if (primaryGroup == null)
                {
                    return string.Empty;
                }

                var ntAccount = primaryGroup.Translate(typeof(NTAccount)) as NTAccount;
                if (ntAccount == null)
                {
                    return string.Empty;
                }

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

        internal static void GenerateChange(string path, ChangeCategory category, out FileSystemChange fileSystemChange)
        {
            fileSystemChange = new FileSystemChange
            {
                Id = Ulid.NewUlid().ToString(),
                ChangeCategory = category,
                ConfigChangeType = ConfigChangeType.FileSystem,
                Entity = path,
                DateTime = DateTime.Now,
                FullPath = path,
                SourceComputer = Environment.MachineName,
                CurrentHash = CalculateFileDigest(path),
                PreviousHash = string.Empty,
                ACLs = path.GetACL()
            };

            Database.Context.FileSystemChanges.Insert(fileSystemChange);
        }

        internal static ConcurrentBag<string> InvokeNtfsSearch()
        {
            var ntfsDrives = DriveInfo.GetDrives()
                .Where(d => d.DriveFormat == "NTFS").ToList();

            var allPaths = new ConcurrentBag<string>();

            Parallel.ForEach(ntfsDrives, driveToAnalyze =>
            {
                var ntfsReader =
                    new NtfsReader(driveToAnalyze, RetrieveMode.All);
                var files =
                    ntfsReader.GetNodesParallel(driveToAnalyze.Name)
                        .Where(n => (n.Attributes &
                                     (Attributes.Temporary |
                                      Attributes.System |
                                      Attributes.Device |
                                      Attributes.Directory |
                                      Attributes.Offline |
                                      Attributes.ReparsePoint |
                                      Attributes.SparseFile)) == 0)
                        .Select(n => n.FullName);

                allPaths.AddRange(files);
            });

            return allPaths;
        }
    }
}
