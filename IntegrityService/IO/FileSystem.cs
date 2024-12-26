using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Filesystem.Ntfs;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace IntegrityService.Utils
{
    public static partial class FileSystem
    {
        /// <summary>
        ///     Calculate <see cref="SHA256" /> digest of a file
        /// </summary>
        /// <param name="path">
        ///     Full pathof the file
        /// </param>
        /// <returns>
        ///     <see cref="SHA256" /> digest converted into <see cref="string" />
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// </exception>
        /// <exception cref="System.Security.SecurityException">
        /// </exception>
        /// <exception cref="System.Reflection.TargetInvocationException">
        /// </exception>
        public static string CalculateFileHash(string path)
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
        ///     Reads file list fron NTFS indexes
        /// </summary>
        /// <returns>
        ///     List of all files
        /// </returns>
        /// <exception cref="IOException">
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">
        /// </exception>
        /// <exception cref="AggregateException">
        /// </exception>
        public static ConcurrentBag<string> InvokeNtfsSearch()
        {
            var ntfsDrives = DriveInfo.GetDrives()
                .Where(d => d.DriveFormat == "NTFS");

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