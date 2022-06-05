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
        private static readonly SHA256 Sha256 = SHA256.Create();

        private static Regex Pattern => _pattern ??= new Regex(GeneratePattern(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static Regex? _pattern;

        internal static void StartSearch()
        {
            var sw = new Stopwatch();
            sw.Start();
            var files = InvokeNtfsSearch();
            if (files == null)
            {
                return;
            }
            sw.Stop();
            Console.WriteLine($"File search completed: {sw.Elapsed}");

            sw.Restart();
            var filtered = FilterAll(files);
            sw.Stop();
            Console.WriteLine($"File filtering completed: {sw.Elapsed}");

            sw.Restart();
            var changes = PrepareData(filtered);
            sw.Stop();
            Console.WriteLine($"File data preparation completed: {sw.Elapsed}");

            sw.Restart();
            Database.Context.FileSystemChanges.InsertBulk(changes, changes.Count());
            sw.Stop();
            Console.WriteLine($"File insert bulk completed: {sw.Elapsed}");
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
                        // TODO: Generate regex from rules and use just that one.
                        .Select(n => n.FullName);

                allPaths.AddRange(files);
            });

            return allPaths;
        }

        private static IEnumerable<string> FilterAll(IEnumerable<string> paths)
        {
            var matches = from path in paths.AsParallel().WithMergeOptions(ParallelMergeOptions.NotBuffered)
                          where Pattern.IsMatch(path)
                          select path;

            return matches.ToList();
        }

        public static bool IsExcluded(string path) => !Pattern.IsMatch(path);

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

        private static IEnumerable<FileSystemChange> PrepareData(IEnumerable<string> paths)
        {
            var count = 0;
            foreach (var path in paths)
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
                yield return change;
                Debug.WriteLine($"Count: {count++}, Total: {paths.Count()}");
            }
        }

        public static string CalculateFileDigest(string path)
        {
            var digest = string.Empty;

            try
            {
                using var fileStream = File.OpenRead(path);
                digest = Convert.ToHexString(Sha256.ComputeHash(fileStream));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            return digest;
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
