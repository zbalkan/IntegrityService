using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace IntegrityService.Utils
{
    public static class ExtensionMethods
    {
        public static void AddRange<T>(this ConcurrentBag<T> @this, IEnumerable<T> toAdd)
        {
            foreach (var element in toAdd)
            {
                @this.Add(element);
            }
        }

        public static string GetACL(this string path)
        {
            var ac = new FileSystemAcl(new FileInfo(path));
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                ReadCommentHandling = JsonCommentHandling.Skip,
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(ac, options);

            return json;
        }

        public static string GetACL(this RegistryKey key)
        {
            var ac = new RegistryAcl(key);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                ReadCommentHandling = JsonCommentHandling.Skip,
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(ac, options);

            return json;
        }

        public static IEnumerable<string> ListFlags<T>(this T value) where T : struct, Enum
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

        public static void Log(this Exception? ex, ILogger logger)
        {
            while (true)
            {
                if (ex == null)
                {
                    break;
                }

                var sb = new StringBuilder(120).Append("Message: ")
                    .AppendLine(ex.Message)
                    .Append("Stacktrace: ")
                    .AppendLine(ex.StackTrace);

                logger.LogError("Exception: {ex}", sb.ToString());

                ex = ex.InnerException;
            }
        }
    }
}
