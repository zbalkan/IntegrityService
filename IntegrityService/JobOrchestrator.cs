using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IntegrityService.Data;
using IntegrityService.FIM;
using IntegrityService.Jobs;
using IntegrityService.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IntegrityService
{
    public partial class JobOrchestrator : BackgroundService
    {
        private readonly FileSystemDiscoveryJob _fsDiscovery;

        private readonly FileSystemMonitorJob _fsMonitor;

        private readonly ILogger<JobOrchestrator> _logger;

        private readonly RegistryMonitorJob _regMonitor;

        public JobOrchestrator(ILogger<JobOrchestrator> logger,
                      IBuffer<FileSystemChange> fsStore,
                      IBuffer<RegistryChange> regStore,
                      ILiteDbContext ctx)
        {
            _logger = logger;
            _fsMonitor = new FileSystemMonitorJob(_logger, fsStore, ctx);
            _regMonitor = new RegistryMonitorJob(_logger, regStore);
            _fsDiscovery = new FileSystemDiscoveryJob(_logger, fsStore, ctx);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.Register(() =>
            {
                _logger.LogInformation("Cancellation requested. Cleaning up resources...");
                Cleanup();
            });
            return _ = Task.Run(async () => await ExecutableTask(stoppingToken), stoppingToken);
        }

        // Workaround for synchronous actions
        // Reference: https://blog.stephencleary.com/2020/05/backgroundservice-gotcha-startup.html
        protected Task ExecutableTask(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Service started.");

            _ = NativeMethods.SetConsoleCtrlHandler(Handler, true);

            var tasks = new List<Task>();

            if (Settings.Instance.EnableLocalDatabase && !Settings.Instance.IsFileDiscoveryCompleted)
            {
                tasks.Add(StartFileSystemDiscoveryAsync(stoppingToken));
            }

            if (Settings.Instance.EnableRegistryMonitoring)
            {
                tasks.Add(StartRegistryMonitoringAsync(stoppingToken));
            }

            tasks.Add(StartFileSystemMonitoringAsync(stoppingToken));
            tasks.Add(RunHeartbeatAsync(stoppingToken));

            return Task.WhenAll(tasks);
        }

        private Task StartFileSystemMonitoringAsync(CancellationToken stoppingToken) => Task.Run(_fsMonitor.Start, stoppingToken)
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception, "Error during file system monitoring.");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);

        private Task StartRegistryMonitoringAsync(CancellationToken stoppingToken) => Task.Run(_regMonitor.Start, stoppingToken)
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception, "Error during registry monitoring.");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);

        private Task StartFileSystemDiscoveryAsync(CancellationToken stoppingToken) => Task.Run(_fsDiscovery.Start, stoppingToken)
        .ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                _logger.LogError(t.Exception, "Error during file system discovery.");
            }
        }, TaskContinuationOptions.OnlyOnFaulted);

        private async Task RunHeartbeatAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Heartbeat task started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                if (Settings.Instance.HeartbeatInterval >= 0)
                {
                    _logger.LogInformation("HEARTBEAT: Worker running at: {time}", DateTimeOffset.Now);
                }

                try
                {
                    await Task.Delay(Settings.Instance.HeartbeatInterval * 1000, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Ignore TaskCanceledException when stopping
                    break;
                }
            }

            _logger.LogInformation("Heartbeat task stopped.");
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
    }
}