using FileProviders.WebDav;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoaaWeb.Common;
using NoaaWeb.Data;
using NoaaWeb.Data.SatellitePass;
using NoaaWeb.Data.UpcomingPass;
using Serilog;
using System.Threading.Tasks;

namespace NoaaWeb.Service
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(ConfigureAppConfiguration)
                .ConfigureServices(ConfigureServices)
                .ConfigureLogging(ConfigureLogging)
                .RunConsoleAsync();
        }

        private static void ConfigureLogging(HostBuilderContext hostContext, ILoggingBuilder loggingBuilder)
        {
            loggingBuilder.ClearProviders();

            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();
            loggingBuilder.AddSerilog(Log.Logger);
        }

        private static void ConfigureAppConfiguration(HostBuilderContext ctx, IConfigurationBuilder configBuilder)
        {
            configBuilder
                .AddEnvironmentVariables()
                .AddJsonFile("./config/appSettings.json", optional: true)
                .Build();
        }

        private static void ConfigureServices(HostBuilderContext ctx, IServiceCollection services)
        {
            services.Configure<SiteConfiguration>(ctx.Configuration);
            services.Configure<FileDbConfiguration>(ctx.Configuration);
            services.Configure<WebDavConfiguration>(ctx.Configuration.GetSection("WebDav"));
            services.Configure<InfluxMetricsConfiguration>(ctx.Configuration.GetSection("InfluxDB"));

            services.AddTransient<WebDavFileProvider>();

            services.AddHostedService<MetricsServer>();

            services.AddTransient<IUpcomingPassRepository, UpcomingPassFileRepository>();
            services.AddSingleton<UpcomingPassScraper>();
            services.AddHostedService<UpcomingPassIndexHost>();

            services.AddTransient<ISatellitePassRepository, SatellitePassFileRepository>();
            services.AddSingleton<SatellitePassScraper>();
            services.AddHostedService<SatellitePassIndexHost>();

            services.AddSingleton<InfluxMetricsSender>();
            services.AddHostedService<InfluxMetricsHost>();
        }
    }
}
