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
            _fsMonitor = new FileSystemMonitor(_logger);
            _regMonitor = new RegistryMonitor(_logger);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            NativeMethods.SetConsoleCtrlHandler(Handler, true);

            if (Settings.Instance.Success)
            {
                _logger.LogInformation("Read settings successfully");
            }
            else
            {
                _logger.LogError("Failed to read settings.");
                Environment.Exit(1);
            }

            if (!Settings.Instance.DisableLocalDatabase)
            {
                Database.Start();
            }

            await StartFileMonitoringAsync().ConfigureAwait(false);
            await StartRegistryMonitoringAsync().ConfigureAwait(false);

            // This loop must continue until service is stopped.
            while (!stoppingToken.IsCancellationRequested)
            {
                if (Settings.Instance.HeartbeatInterval >= 0)
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                await Task.Delay(Settings.Instance.HeartbeatInterval * 1000, stoppingToken);
            }
        }

        private async Task StartFileMonitoringAsync()
        {
            var task = Task.Run(() =>
            {
                _fsMonitor.Start();
                return true;
            });
            var result = await task;
        }

        private async Task StartRegistryMonitoringAsync()
        {
            var task = Task.Run(() =>
            {
                if (Settings.Instance.EnableRegistryMonitoring)
                {
                    _regMonitor.Start();
                }
                return true;
            });
            var result = await task;
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