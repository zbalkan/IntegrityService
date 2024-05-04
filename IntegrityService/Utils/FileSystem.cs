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
        /// <summary>
        ///     Calculate <see cref="SHA256"/> digest of a file
        /// </summary>
        /// <param name="path">Full pathof the file</param>
        /// <returns><see cref="SHA256"/> digest converted into <see cref="string"/></returns>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="System.Security.SecurityException"></exception>
        /// <exception cref="System.Reflection.TargetInvocationException"></exception>
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

        /// <summary>
        ///     Check if the given path is in the excluded paths
        /// </summary>
        /// <param name="path">Ful path of the file to be checked</param>
        /// <returns>True if path is in excluded paths</returns>
        public static bool IsExcluded(string path)
        {
            foreach (var excluded in Settings.Instance.ExcludedPaths)
            {
                if (path.StartsWith(excluded, StringComparison.InvariantCultureIgnoreCase)) { return true; }
            }
            return false;
        }

        /// <summary>
        ///     Generates new file system change record from parameters and saves into database
        /// </summary>
        /// <param name="path">The path to filekey</param>
        /// <param name="category"><see cref="ChangeCategory"></param>
        /// <param name="fileSystemChange">The change object</param>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="System.Security.SecurityException"></exception>
        /// <exception cref="System.Reflection.TargetInvocationException"></exception>
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

        /// <summary>
        ///     Reads file list fron NTFS indexes
        /// </summary>
        /// <returns>List of all files</returns>
        /// <exception cref="IOException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        /// <exception cref="AggregateException"></exception>
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
