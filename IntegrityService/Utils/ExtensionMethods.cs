using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace IntegrityService.Utils
{
    /// <summary>
    ///     Miscelaneous extension methods
    /// </summary>
    public static class ExtensionMethods
    {
        public static void AddRange<T>(this ConcurrentBag<T> @this, IEnumerable<T> toAdd)
        {
            foreach (var element in toAdd)
            {
                @this.Add(element);
            }
        }

        public static void Log(this Exception? ex, ILogger logger)
        {
            while (ex != null)
            {
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
