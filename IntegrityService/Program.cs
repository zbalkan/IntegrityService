using IntegrityService;

namespace Company.WebApplication1
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    services.AddHostedService<Worker>();
                })
                .UseWindowsService()
                .ConfigureLogging(logging
                    => logging.AddEventLog(conf =>
                        {
                            conf.SourceName = "File Integrity Monitoring Service";
                            conf.LogName = "FIM";
                        }
                        ))
                .Build();

            host.Run();
        }
    }
}