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
        private readonly BackgroundWorkerQueue _backgroundWorkerQueue;
        public Worker(ILogger<Worker> logger, BackgroundWorkerQueue backgroundWorkerQueue)
        {
            _logger = logger;
            _fsMonitor = new FileSystemMonitor(_logger);
            _regMonitor = new RegistryMonitor(_logger);
            _backgroundWorkerQueue = backgroundWorkerQueue;
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

            _backgroundWorkerQueue.QueueBackgroundWorkItem(token => StartFilesystemDiscoveryAsync());
            await StartFileMonitoringAsync().ConfigureAwait(false);
            await StartRegistryMonitoringAsync().ConfigureAwait(false);

            // This loop must continue until service is stopped.
            while (!stoppingToken.IsCancellationRequested)
            {
                if (Settings.Instance.HeartbeatInterval >= 0)
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                var workItem = await _backgroundWorkerQueue.DequeueAsync(stoppingToken);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                workItem(stoppingToken);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                await Task.Delay(Settings.Instance.HeartbeatInterval * 1000, stoppingToken);
            }
        }

        private async Task StartFilesystemDiscoveryAsync()
        {
            _ = await Task.Run(async () =>
            {
                if (!Settings.Instance.DisableLocalDatabase && Registry.ReadDwordValue("FileDiscoveryCompleted") == 0)
                {
                    _logger.LogInformation("Could not find the database file. Initiating file system discovery. It will take time.");
                    Database.Start();
                    Registry.WriteDwordValue("FileDiscoveryCompleted", 0, true);
                    FileSystem.StartSearch(Settings.Instance.MonitoredPaths, Settings.Instance.ExcludedPaths,
                        Settings.Instance.ExcludedExtensions);

                    Registry.WriteDwordValue("FileDiscoveryCompleted", 1, true);
                    _logger.LogInformation("File system discovery completed.");
                }
                return true; // This is kept here just to suppress warnings
            }).ConfigureAwait(false);
        }

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        private async Task StartFileMonitoringAsync() => Task.Run(() => _fsMonitor.Start()).ConfigureAwait(false);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

        private async Task StartRegistryMonitoringAsync()
        {
            _ = await Task.Run(() =>
            {
                if (Settings.Instance.EnableRegistryMonitoring)
                {
                    _regMonitor.Start();
                }
                return true; // This is kept here just to suppress warnings
            }).ConfigureAwait(false);
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