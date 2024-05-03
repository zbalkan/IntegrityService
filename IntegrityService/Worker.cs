using System;
using System.Threading;
using System.Threading.Tasks;
using IntegrityService.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IntegrityService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly FileSystemMonitor _fsMonitor;
        private readonly BackgroundWorkerQueue _backgroundWorkerQueue;
        private RegistryMonitor _regMonitor;

        public Worker(ILogger<Worker> logger, BackgroundWorkerQueue backgroundWorkerQueue)
        {
            _logger = logger;
            _fsMonitor = new FileSystemMonitor(_logger);
            _backgroundWorkerQueue = backgroundWorkerQueue;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _ = NativeMethods.SetConsoleCtrlHandler(Handler, true);

            if (Settings.Instance.Success)
            {
                _logger.LogInformation("Read settings successfully");
            }
            else
            {
                _logger.LogError("Failed to read settings.");
                Environment.Exit(1);
            }

            if (!Settings.Instance.DisableLocalDatabase && Registry.ReadDwordValue("FileDiscoveryCompleted") == 0)
            {
                _backgroundWorkerQueue.QueueBackgroundWorkItem(_ => StartFilesystemDiscoveryAsync(stoppingToken).Unwrap());
            }
            _fsMonitor.Start();

            if (Settings.Instance.EnableRegistryMonitoring)
            {
                _regMonitor = new RegistryMonitor(_logger);
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

        private async Task<Task> StartFilesystemDiscoveryAsync(CancellationToken token)
        {
            var tcs = new TaskCompletionSource();

            try
            {
                await Task.Run(() =>
                       {
                           _logger.LogInformation(
                               "File discovery not completed. Initiating file system discovery. It will take time.");
                           Database.Start();
                           Registry.WriteDwordValue("FileDiscoveryCompleted", 0, true);
                           var fsDiscovery = new FileSystemDiscovery(_logger);
                           fsDiscovery.Start();
                           Registry.WriteDwordValue("FileDiscoveryCompleted", 1, true);
                           _logger.LogInformation("File system discovery completed.");
                           Environment.Exit(0); // Kill the service here. The OS will restart the service.
                       },
                       token);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
            return tcs.Task;
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