using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Filesystem.Ntfs;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IntegrityService.FIM;
using NUlid;

namespace IntegrityService.Utils
{
    internal static class FileSystem
    {
        // Move this to FilesystemDiscovery class, leave static methods only.
        internal static void StartSearch()
        {
            Console.WriteLine("Starting file search...");
            var sw = new Stopwatch();
            sw.Start();
            var files = InvokeNtfsSearch();
            if (files == null)
            {
                return;
            }
            sw.Stop();
            Console.WriteLine($"Filesystem search completed: {sw.Elapsed}");
            Console.WriteLine($"Number of all files in the device: {files.Count}");

            Console.WriteLine("Starting filtering by configuration values...");
            sw.Restart();
            var filtered = FilterAll(files);
            sw.Stop();
            Console.WriteLine($"Path filtering completed: {sw.Elapsed}");
            var diff = files.Count - filtered.Count;
            Console.WriteLine($"Number of files to be monitored: {filtered.Count} (filtered out {diff}, %{diff * 100 / files.Count:0.###})");

            Console.WriteLine("Filtering out the data in the database...");
            sw.Restart();
            var filteredOut = filtered.RemoveAll(x => Database.Context.FileSystemChanges.Exists(c => c.Entity.Equals(x)));
            sw.Stop();
            Console.WriteLine($"Filtering out completed: {sw.Elapsed}");
            if (filteredOut > 0)
            {
                Console.WriteLine($"Number of files not in database: {filtered.Count} (filtered out {filteredOut}, %{filteredOut * 100 / filtered.Count:0.###})");
            }
            else
                Console.WriteLine("Nothing to filter out.");

            Console.WriteLine("Starting inventory discovery (path and hash)...");
            sw.Restart();
            Parallel.ForEach(filtered, new ParallelOptions() { MaxDegreeOfParallelism = 10 }, path => GenerateChange(path));
            sw.Stop();
            Console.WriteLine($"Database update completed: {sw.Elapsed}");
        }

        private static ConcurrentBag<string>? InvokeNtfsSearch()
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

        private static List<string> FilterAll(IEnumerable<string> paths)
        {
            var pattern = new Regex(GeneratePattern(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
            var matches = from path in paths.AsParallel().WithMergeOptions(ParallelMergeOptions.NotBuffered)
                          where pattern.IsMatch(path)
                          select path;

            return matches.ToList();
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

        private static void GenerateChange(string path)
        {
            var change = new FileSystemChange
            {
                Id = Ulid.NewUlid().ToString(),
                ChangeCategory = ChangeCategory.Discovery,
                ConfigChangeType = ConfigChangeType.FileSystem,
                Entity = path,
                DateTime = DateTime.Now,
                FullPath = path,
                SourceComputer = Environment.MachineName,
                CurrentHash = CalculateFileDigest(path),
                PreviousHash = string.Empty,
                ACLs = path.GetACL()
            };

            Database.Context.FileSystemChanges.Insert(change);
        }


        private static string GeneratePattern()
        {
            var sb = new StringBuilder(100);

            // Start with negative lookahead for exclusion
            sb.Append("(?:(?!(^(");
            if (Settings.Instance.ExcludedPaths.Count > 0)
            {
                sb.Append(new StringBuilder(20).AppendJoin('|', Settings.Instance.ExcludedPaths).Sanitize());
                sb.Append(@")\\?.*)");
            }

            if (Settings.Instance.ExcludedExtensions.Count > 0)
            {
                sb.Append(@"|(^.*(");
                sb.Append(new StringBuilder(20).AppendJoin('|', Settings.Instance.ExcludedExtensions).Sanitize());
                sb.Append(')');
            }

            sb.Append("$))");

            // Add included paths
            sb.Append("(?:^(");
            sb.Append(new StringBuilder(20).AppendJoin('|', Settings.Instance.MonitoredPaths).Sanitize());
            sb.Append(@")\\?.*$))");

            return sb.ToString();
        }

        private static StringBuilder Sanitize(this StringBuilder sb) =>
            sb
                .Replace(@"\", @"\\").Replace(@"\\\\", @"\\")
                .Replace(".", @"\.")
                .Replace(" ", "\\ ")
                .Replace("(", "\\(")
                .Replace(")", "\\)");
    }
}
