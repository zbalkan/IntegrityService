using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using IntegrityService.Data;
using IntegrityService.IO.Security;
using NUlid;
using static IntegrityService.IO.FileSystem;

namespace IntegrityService.FIM
{
    public class FileSystemChange : Change
    {
        public string CurrentHash { get; set; }

        public string FullPath { get; set; }

        public string PreviousHash { get; set; }

        public ObjectType ObjectType { get; set; }

        public static string RetrievePreviousHash(string path, ILiteDbContext ctx)
        {
            var previousChange = ctx.FileSystemChanges.Query()
                                       .Where(x => x.FullPath.Equals(path, StringComparison.Ordinal))
                                       .ToList()
                                       .OrderByDescending(c => c.DateTime)
                                       .FirstOrDefault();
            return previousChange?.CurrentHash ?? string.Empty;
        }

        /// <summary> Generates new file system change record from parameters </summary>
        /// <param name="path">The path to filekey</param>
        /// <param name="category"><see cref="ChangeCategory"></param>
        /// <param name="fileSystemChange">The change object</param>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="System.Security.SecurityException"></exception>
        /// <exception cref="System.Reflection.TargetInvocationException"></exception>
        /// <exception cref="PathTooLongException"></exception> <exception cref="UnauthorizedAccessException"></exception>
        public static FileSystemChange? FromPath(string path, ChangeCategory category)
        {
            var objectType = GetObjectType(path);
            if (objectType == ObjectType.Unknown && category != ChangeCategory.Deleted)
            {
                return null;
            }

            var hash = string.Empty;
            if (IsHashableFile(path, objectType))
            {
                hash = CalculateFileHash(path);
            }

            return new FileSystemChange
            {
                Id = Ulid.NewUlid().ToString(),
                ChangeCategory = category,
                ConfigChangeType = ConfigChangeType.FileSystem,
                Entity = path,
                DateTime = DateTime.Now,
                ObjectType = objectType,
                FullPath = path,
                SourceComputer = Environment.MachineName,
                CurrentHash = hash,
                PreviousHash = string.Empty,
                ACLs = path.GetACL()
            };
        }

        private static bool IsHashableFile(string path, ObjectType objectType)
        {
            if (objectType != ObjectType.File) return false;
            try
            {
                if(new FileInfo(path).Length < Settings.Instance.HashLimitMB)
                {
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
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