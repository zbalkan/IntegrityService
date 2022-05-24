using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;

namespace IntegrityService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddEventLog();
                })
                .ConfigureServices(services =>
                    {
                        services.AddHostedService<Worker>()
                        .Configure<EventLogSettings>(config =>
                        {
                            config.SourceName = "File Integrity Monitoring Service";
                        });
                    });
    }
}