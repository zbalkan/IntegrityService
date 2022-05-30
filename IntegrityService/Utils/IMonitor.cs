using System;

namespace IntegrityService.Utils
{
    internal interface IMonitor: IDisposable
    {
        void Start();
        void Stop();
    }
}