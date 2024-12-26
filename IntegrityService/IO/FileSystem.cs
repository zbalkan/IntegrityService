using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Filesystem.Ntfs;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using IntegrityService.Data;
using IntegrityService.FIM;
using IntegrityService.IO.Security;
using NUlid;

namespace IntegrityService.Utils
{
    internal static partial class FileSystem
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

        /// <summary> Generates new file system change record from parameters and saves into
        /// database </summary> <param name="path">The path to filekey</param> <param
        /// name="category"><see cref="ChangeCategory"></param> <param name="fileSystemChange">The
        /// change object</param> <exception cref="NotSupportedException"></exception> <exception
        /// cref="System.Security.SecurityException"></exception> <exception
        /// cref="System.Reflection.TargetInvocationException"></exception> <exception
        /// cref="PathTooLongException"></exception> <exception cref="UnauthorizedAccessException"></exception>
        public static FileSystemChange? GenerateChange(string path, ChangeCategory category, ILiteDbContext ctx)
        {
            var hash = string.Empty;

            var objectType = GetObjectType(path);
            if (objectType == ObjectType.Unknown && category != ChangeCategory.Deleted)
            {
                return null;
            }

            var previousHash = string.Empty;
            if (objectType == ObjectType.File)
            {
                hash = CalculateFileHash(path);

                var previousChange = ctx.FileSystemChanges.Query()
                                           .Where(x => x.FullPath.Equals(path, StringComparison.Ordinal))
                                           .ToList()
                                           .OrderByDescending(c => c.DateTime)
                                           .FirstOrDefault();
                previousHash = previousChange?.CurrentHash ?? string.Empty;
            }

            return new FileSystemChange
            {
                Id = Ulid.NewUlid().ToString(),
                ChangeCategory = category,
                ConfigChangeType = ConfigChangeType.FileSystem,
                Entity = path,
                DateTime = DateTime.Now,
                FullPath = path,
                SourceComputer = Environment.MachineName,
                CurrentHash = hash,
                PreviousHash = previousHash,
                ACLs = path.GetACL()
            };
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

        private static ObjectType GetObjectType(string path)
        {
            var objectType = ObjectType.Unknown;
            try
            {
                if (Path.Exists(path))
                {
                    var attr = File.GetAttributes(path);
                    if (attr.HasFlag(FileAttributes.Directory))
                    {
                        objectType = ObjectType.Directory;
                    }

                    // We know it is a file, but is it a regular file?
                    var file = new FileInfo(path);
                    if (file.LinkTarget != null)
                    {
                        objectType = ObjectType.SymbolicLink;
                    }

                    objectType = ObjectType.File;
                }
            }
            catch (Exception)
            {
                // When a file is deleted, this returns null
                objectType = ObjectType.Unknown;
            }

            return objectType;
        }
    }
}