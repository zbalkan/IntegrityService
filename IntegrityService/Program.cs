using IntegrityService.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace IntegrityService
{
    public static class Program
    {
        public static void Main(string[] args) => CreateHostBuilder(args).Build().Run();

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(LogLevel.Information);
                    // Add Serilog for event logging
                    _ = logging.AddSerilog((Serilog.Core.Logger?)new LoggerConfiguration()
                        .WriteTo.EventLog("FIM", "FIM", manageEventSource: true,eventIdProvider: new EventIdProvider())
                        .CreateLogger());
                })
                .ConfigureServices(services =>
                {
                    _ = services.AddHostedService<Worker>();
                    _ = services.AddSingleton<BackgroundWorkerQueue>();
                })
                .UseWindowsService();
    }
}