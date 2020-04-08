using FileProviders.WebDav;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoaaWeb.Data;
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
            services.Configure<FileDbConfiguration>(ctx.Configuration);
            services.Configure<WebDavConfiguration>(ctx.Configuration.GetSection("WebDav"));

            services.AddTransient<ISatellitePassRepository, SatellitePassFileRepository>();
            services.AddTransient<SatellitePassScraper>();
            services.AddTransient<WebDavFileProvider>();
            services.AddHostedService<SatellitePassIndexHost>();
        }
    }
}
