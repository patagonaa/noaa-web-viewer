using FileProviders.WebDav;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NoaaWeb.Data;
using System;
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
                .RunConsoleAsync();
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
