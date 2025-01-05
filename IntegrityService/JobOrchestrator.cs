using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IntegrityService.Data;
using IntegrityService.FIM;
using IntegrityService.Jobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IntegrityService
{
    public partial class JobOrchestrator : BackgroundService
    {
        //private readonly BufferConsumer _consumer;

        //private readonly FileSystemDiscoveryJob _fsDiscovery;

        //private readonly FileSystemMonitorJob _fsMonitor;

        private readonly ILiteDbContext _ctx;

        private readonly IBuffer<FileSystemChange> _fsChangeBuffer;

        private readonly ILogger<JobOrchestrator> _logger;

        //private readonly RegistryMonitorJob _regMonitor;
        private readonly IBuffer<RegistryChange> _regChangeBuffer;
        private Timer _heartbeatTimer;

        public JobOrchestrator(ILogger<JobOrchestrator> logger,
                      IBuffer<FileSystemChange> fsChangeBuffer,
                      IBuffer<RegistryChange> regChangeBuffer,
                      ILiteDbContext ctx)
        {
            _logger = logger;
            _fsChangeBuffer = fsChangeBuffer;
            _regChangeBuffer = regChangeBuffer;
            _ctx = ctx;

            // Move these to wrapper functions that creates and starts the jobs in the same thread
            //_fsMonitor = new FileSystemMonitorJob(_logger, fsChangeBuffer, ctx);
            //_regMonitor = new RegistryMonitorJob(_logger, regChangeBuffer);
            //_fsDiscovery = new FileSystemDiscoveryJob(_logger, fsChangeBuffer, ctx);

            //_consumer = new BufferConsumer(logger, fsChangeBuffer, regChangeBuffer, ctx);
        }

        // Workaround for synchronous actions
        // Reference: https://blog.stephencleary.com/2020/05/backgroundservice-gotcha-startup.html
        internal Task StartJobs(CancellationToken stoppingToken)
        {
            var tasks = new List<Task>
            { StartBufferConsumerAsync(stoppingToken)
            };

            if (Settings.Instance.EnableLocalDatabase && !Settings.Instance.IsFileDiscoveryCompleted)
            {
                tasks.Add(StartFilesystemDiscoveryAsync(stoppingToken));
            }

            if (Settings.Instance.EnableRegistryMonitoring)
            {
                tasks.Add(StartRegistryMonitoringAsync(stoppingToken));
            }


            // These jobs run synchronously
            StartHeartbeat(stoppingToken);

            using (var fsMonitor = new FileSystemMonitorJob(_logger, _fsChangeBuffer, _ctx))
            {
                fsMonitor.Start();
            }

            // start async jobs
            return Task.WhenAll(tasks).ContinueWith(_ => _logger.LogInformation("Worker stopped at: {time}", DateTimeOffset.Now), TaskContinuationOptions.ExecuteSynchronously);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken) => _ = Task.Run(async () => await StartJobs(stoppingToken), stoppingToken);

        private Task StartBufferConsumerAsync(CancellationToken stoppingToken)
        {
            var consumer = new BufferConsumer(_logger, _fsChangeBuffer, _regChangeBuffer, _ctx);
            return consumer.StartAsync(stoppingToken);
        }

        private Task StartFilesystemDiscoveryAsync(CancellationToken stoppingToken) => Task.Run(async () =>
                       {
                           _logger.LogInformation(
                               "File discovery not completed. Initiating file system discovery. It will take time.");
                           var fsDiscovery = new FileSystemDiscoveryJob(_logger, _fsChangeBuffer, _ctx);
                           fsDiscovery.Start();
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


        private Task StartRegistryMonitoringAsync(CancellationToken stoppingToken) =>
            Task.Run(() =>
            {
                var regMonitor = new RegistryMonitorJob(_logger, _regChangeBuffer);
                regMonitor.Start();
            }, stoppingToken)
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception, "Error during registry monitoring.");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);

        private void StartHeartbeat(CancellationToken stoppingToken)
        {
            _heartbeatTimer = new Timer(
                state =>
                {
                    if (Settings.Instance.HeartbeatInterval >= 0)
                    {
                        _logger.LogInformation("HEARTBEAT: Worker running at: {time}", DateTimeOffset.Now);
                    }
                },
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(Settings.Instance.HeartbeatInterval));

            stoppingToken.Register(() =>
            {
                _heartbeatTimer?.Change(Timeout.Infinite, 0);
                _heartbeatTimer?.Dispose();
                _logger.LogInformation("Heartbeat task was canceled.");
            });
        }
    }
}