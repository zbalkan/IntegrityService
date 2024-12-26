﻿using System;
using System.Runtime.InteropServices;
using System.Security;

namespace IntegrityService.Utils
{
    internal static class NativeMethods
    {
        // https://docs.microsoft.com/en-us/windows/console/handlerroutine?WT.mc_id=DT-MVP-5003978
        internal delegate bool SetConsoleCtrlEventHandler(CtrlType sig);

        private enum KEY_INFORMATION_CLASS
        {
            KeyBasicInformation,            // A KEY_BASIC_INFORMATION structure is supplied.

            KeyNodeInformation,             // A KEY_NODE_INFORMATION structure is supplied.

            KeyFullInformation,             // A KEY_FULL_INFORMATION structure is supplied.

            KeyNameInformation,             // A KEY_NAME_INFORMATION structure is supplied.

            KeyCachedInformation,           // A KEY_CACHED_INFORMATION structure is supplied.

            KeyFlagsInformation,            // Reserved for system use.

            KeyVirtualizationInformation,   // A KEY_VIRTUALIZATION_INFORMATION structure is supplied.

            KeyHandleTagsInformation,       // Reserved for system use.

            MaxKeyInfoClass                 // The maximum value in this enumeration type.
        }

        [DllImport("advapi32", SetLastError = true), SuppressUnmanagedCodeSecurity]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern int OpenProcessToken(IntPtr processHandle, // handle to process
            int desiredAccess, // desired access to process
            ref IntPtr tokenHandle // handle to open access token
        );

        // https://docs.microsoft.com/en-us/windows/console/setconsolectrlhandler?WT.mc_id=DT-MVP-5003978
        [DllImport("Kernel32")]
        internal static extern bool SetConsoleCtrlHandler(SetConsoleCtrlEventHandler handler, bool add);

        [StructLayout(LayoutKind.Sequential)]
        public struct KEY_NAME_INFORMATION
        {
            public uint NameLength;     // The size, in bytes, of the key name string in the Name array.

            public char[] Name;           // An array of wide characters that contains the name of the key.

            // This character string is not null-terminated. Only the first element in this array is
            // included in the KEY_NAME_INFORMATION structure definition. The storage for the
            // remaining elements in the array immediately follows this element.
        }
    }
}