﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Principal;
using System.Security;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace IntegrityService.Utils
{
    /// <summary>
    ///     Miscelaneous extension methods
    /// </summary>
    public static class ExtensionMethods
    {
        private const int DesiredAccess = (int)(SecurityImpersonationLevel.TokenQuery |
                                                                              SecurityImpersonationLevel.TokenImpersonate |
                                                                              SecurityImpersonationLevel.TokenDuplicate);

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

        public static WindowsIdentity Owner(this Process process)
        {
            ArgumentNullException.ThrowIfNull(process);

            var hToken = IntPtr.Zero;

            var token = NativeMethods.OpenProcessToken(process.Handle, DesiredAccess, ref hToken) == 0
                ? throw new SecurityException($"Failed to access the token of the owner of {process.ProcessName}")
                : hToken;
            return new WindowsIdentity(token);
        }

        [Flags]
        internal enum SecurityImpersonationLevel
        {
            None = 0,
            TokenDuplicate = 1 << 1,
            TokenQuery = 1 << 3,
            TokenImpersonate = 1 << 2
        }
    }
}