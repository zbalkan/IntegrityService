using IntegrityService.Data;
using IntegrityService.FIM;
using IntegrityService.IO;
using IntegrityService.Message;
using IntegrityService.Utils;
using Microsoft.Extensions.Configuration;
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
                    _ = logging.AddSerilog(new LoggerConfiguration()
                        .WriteTo.EventLog("FIM", "FIM", manageEventSource: true, eventIdProvider: new EventIdProvider())
                        .CreateLogger());
                })
                .ConfigureServices(services =>
                {
                    _ = services.Configure<LiteDbOptions>(options => options.DatabasePath = Settings.DatabasePath);
                    _ = services.AddSingleton<ILiteDbContext, LiteDbContext>();
                    _ = services.AddHostedService<WatcherWorker>();
                    _ = services.AddHostedService<PersistenceWorker>();
                    _ = services.AddSingleton<BackgroundWorkerQueue>();
                    _ = services.AddSingleton<IMessageStore<FileSystemChange, FileSystemChangeMessage>, FileSystemMessageStore>();
                    _ = services.AddSingleton<IMessageStore<RegistryChange, RegistryChangeMessage>, RegistryMessageStore>();

                    IConfiguration configuration = new ConfigurationBuilder()
                    .AddWindowsRegistry(Registry.RootName, Registry.Hive, false)
                    .Build();
                })
                .UseWindowsService();
    }
}