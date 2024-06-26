using System;
using System.Threading;
using System.Threading.Tasks;
using IntegrityService.Data;
using IntegrityService.Jobs;
using IntegrityService.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IntegrityService
{
    public class Worker : BackgroundService
    {
        private readonly BackgroundWorkerQueue _backgroundWorkerQueue;
        private readonly FileSystemMonitorJob _fsMonitor;
        private readonly ILogger<Worker> _logger;
        private RegistryMonitorJob _regMonitor;

        public Worker(ILogger<Worker> logger, BackgroundWorkerQueue backgroundWorkerQueue)
        {
            _logger = logger;
            _backgroundWorkerQueue = backgroundWorkerQueue;
            _fsMonitor = new FileSystemMonitorJob(_logger);
            Database.Start();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _ = NativeMethods.SetConsoleCtrlHandler(Handler, true);

            ReloadConfig();

            if (!Settings.Instance.DisableLocalDatabase && !Settings.Instance.IsFileDiscoveryCompleted)
            {
                _backgroundWorkerQueue.QueueBackgroundWorkItem(_ => StartFilesystemDiscoveryAsync(stoppingToken).Unwrap());
                ReloadConfig();
            }
            _fsMonitor.Start();

            if (Settings.Instance.EnableRegistryMonitoring)
            {
                _regMonitor = new RegistryMonitorJob(_logger);
                _regMonitor.Start();
            }

            // This loop must continue until service is stopped.
            while (!stoppingToken.IsCancellationRequested)
            {
                if (Settings.Instance.HeartbeatInterval >= 0)
                {
                    _logger.LogInformation("HEARTBEAT: Worker running at: {time}", DateTimeOffset.Now);
                }

                var workItem = await _backgroundWorkerQueue.DequeueAsync(stoppingToken);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                if (workItem?.Target != null)
                {
                    workItem(stoppingToken);
                }
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                await Task.Delay(Settings.Instance.HeartbeatInterval * 1000, stoppingToken);
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

        private void ReloadConfig()
        {
            if (Settings.Instance.Success)
            {
                _logger.LogInformation("Read settings successfully");
            }
            else
            {
                _logger.LogError("Failed to read settings.");
                Environment.Exit(1);
            }
        }

        private async Task<Task> StartFilesystemDiscoveryAsync(CancellationToken token)
        {
            var tcs = new TaskCompletionSource();

            try
            {
                await Task.Run(() =>
                {
                    _logger.LogInformation(
                        "File discovery not completed. Initiating file system discovery. It will take time.");
                    var fsDiscovery = new FileSystemDiscoveryJob(_logger);
                    fsDiscovery.Start();
                    Settings.Instance.IsFileDiscoveryCompleted = true;
                    _logger.LogInformation("File system discovery completed.");
                },
                       token);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
            return tcs.Task;
        }
    }
}