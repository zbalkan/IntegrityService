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

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;

            _fsMonitor = new FileSystemMonitor(_logger, true);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (Settings.Instance.MonitoredPaths != default)
            {
                _logger.LogInformation("Read settings successfully");
            }
            else
            {
                _logger.LogError("Failed to read settings.");
            }

            _fsMonitor.Start();

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(30000, stoppingToken);
            }

            _fsMonitor.Stop();
        }
    }
}