using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace IntegrityService.Utils
{
    internal static class FileSystem
    {
        internal static ConcurrentBag<string> StartSearch(IEnumerable<string> pathsToSearch, IEnumerable<string> excludedPaths, IEnumerable<string> excludedExtensions)
        {
            var defaultEnumOptions = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = false
            };

            var files = new ConcurrentBag<string>();
            Parallel.ForEach(pathsToSearch, path => files.AddRange(SearchFiles(path, defaultEnumOptions, excludedPaths, excludedExtensions)));
            return files;
        }

        internal static bool IsExcluded(string path, IEnumerable<string> excludedPaths, IEnumerable<string> excludedExtensions)
        {
            bool result;
            if (IsFile(path)!.Value)
            {
                result = (excludedPaths ??
                          throw new InvalidOperationException()) // If file, sanitize file path and check. Then, check extensions.
                         .Any(excludedPath =>
                             Path.GetFileName(path).Contains(Path.GetFileName(excludedPath)!,
                                 StringComparison.OrdinalIgnoreCase)) ||
                         (excludedExtensions ?? throw new InvalidOperationException())
                         .Any(extension =>
                             extension.Contains(Path.GetExtension(path), StringComparison.OrdinalIgnoreCase));
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
            catch
            {
                return null;
            }
        }

        /// <summary> 
        ///     A safe way to get all the files in a directory and sub directory without crashing on UnauthorizedException or PathTooLongException 
        /// </summary> 
        /// <param name="path">Starting directory</param>
        /// <param name="options">Enumeration options</param>
        /// <param name="excludedPaths"></param>
        /// <param name="excludedExtensions"></param>
        /// <returns>List of files</returns> 
        private static IEnumerable<string> SearchFiles(string path, EnumerationOptions options, IEnumerable<string> excludedPaths, IEnumerable<string> excludedExtensions)
        {
            var files = new List<string>();
            var excPaths = excludedPaths.ToList();
            var excExtensions = excludedExtensions.ToList();

            if (IsExcluded(path, excPaths, excExtensions)) return files;

            try
            {
                foreach (var directory in Directory.EnumerateDirectories(path))
                {
                    files.AddRange(SearchFiles(directory, options, excPaths, excExtensions));
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
                files.AddRange(Directory.EnumerateFiles(path, "*.*", options).Where(f => !IsExcluded(f, excPaths, excExtensions)));
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

            return files;
        }

    }
}
