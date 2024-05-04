using System.Runtime.InteropServices;

namespace IntegrityService.Utils
{
    internal static class NativeMethods
    {
        // https://docs.microsoft.com/en-us/windows/console/setconsolectrlhandler?WT.mc_id=DT-MVP-5003978
        [DllImport("Kernel32")]
        internal static extern bool SetConsoleCtrlHandler(SetConsoleCtrlEventHandler handler, bool add);

        // https://docs.microsoft.com/en-us/windows/console/handlerroutine?WT.mc_id=DT-MVP-5003978
        internal delegate bool SetConsoleCtrlEventHandler(CtrlType sig);
    }
}
