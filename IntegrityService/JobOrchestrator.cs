using System;
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
        private readonly ILiteDbContext _ctx;

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
            _ctx = ctx;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken) => _ = Task.Run(async () => await ExecutableTask(stoppingToken));

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

        // Workaround for synchronous actions
        // Reference: https://blog.stephencleary.com/2020/05/backgroundservice-gotcha-startup.html
        private async Task ExecutableTask(CancellationToken stoppingToken)
        {
            _ = NativeMethods.SetConsoleCtrlHandler(Handler, true);

            if (Settings.Instance.EnableLocalDatabase && !Settings.Instance.IsFileDiscoveryCompleted)
            {
                StartFilesystemDiscoveryAsync(stoppingToken);
            }
            _fsMonitor.Start();

            if (Settings.Instance.EnableRegistryMonitoring)
            {
                _regMonitor.Start();
            }

            // This loop must continue until service is stopped.
            while (!stoppingToken.IsCancellationRequested)
            {
                if (Settings.Instance.HeartbeatInterval >= 0)
                {
                    _logger.LogInformation("HEARTBEAT: Worker running at: {time}", DateTimeOffset.Now);
                }
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

        private Task StartFilesystemDiscoveryAsync(CancellationToken stoppingToken) => Task.Run(() =>
                       {
                           _logger.LogInformation(
                               "File discovery not completed. Initiating file system discovery. It will take time.");
                           _fsDiscovery.Start();
                           Settings.Instance.IsFileDiscoveryCompleted = true;
                           _logger.LogInformation("File system discovery completed.");
                       },
                       stoppingToken).ContinueWith(t =>
                       {
                           if (t.IsFaulted)
                           {
                               _logger.LogError(t.Exception, "Error during file system discovery.");
                           }
                       }, TaskContinuationOptions.OnlyOnFaulted);
    }
}