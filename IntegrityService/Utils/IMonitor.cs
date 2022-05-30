namespace IntegrityService.Utils
{
    internal interface IMonitor
    {
        void Dispose();
        void Start();
        void Stop();
    }
}