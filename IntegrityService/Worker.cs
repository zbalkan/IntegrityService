using IntegrityService.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IntegrityService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly FileSystemMonitor _fsMonitor;
        private readonly RegistryMonitor _regMonitor;
        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
            _fsMonitor = new FileSystemMonitor(_logger, true);
            _regMonitor = new RegistryMonitor(_logger);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            NativeMethods.SetConsoleCtrlHandler(Handler, true);

            if (Settings.Instance.MonitoredPaths.Count > 0)
            {
                _logger.LogInformation("Read settings successfully");
            }
            else
            {
                _logger.LogError("Failed to read settings.");
            }

            Database.Start();
            _fsMonitor.Start();
            if (Settings.Instance.EnableRegistryMonitoring)
            {
                _regMonitor.Start();
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(Settings.Instance.HeartbeatInterval * 1000, stoppingToken);
            }
        }

        private bool Handler(CtrlType signal)
        {
            switch (signal)
            {
                case CtrlType.CtrlBreakEvent:
                case CtrlType.CtrlCEvent:
                case CtrlType.CtrlLogoffEvent:
                case CtrlType.CtrlShutdownEvent:
                case CtrlType.CtrlCloseEvent:
                    _logger.LogInformation("Worker stopped at: {time}", DateTimeOffset.Now);
                    Cleanup();
                    Environment.Exit(0);
                    return false;

                default:
                    return false;
            }
        }

        private void Cleanup()
        {
            // Cleanup members here
            _fsMonitor.Stop();
            _fsMonitor.Dispose();

            if (Settings.Instance.EnableRegistryMonitoring)
            {
                _regMonitor.Stop();
                _regMonitor.Dispose();
            }

            Database.Stop();
        }
    }
}