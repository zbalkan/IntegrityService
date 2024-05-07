using System;

namespace IntegrityService.Jobs
{
    internal interface IMonitor : IDisposable
    {
        void Start();
        void Stop();
    }
}