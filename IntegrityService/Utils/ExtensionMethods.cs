using System.Collections.Concurrent;
using System.Collections.Generic;

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
    }
}
