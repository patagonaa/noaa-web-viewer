using FileProviders.WebDav;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NoaaWeb.Data;
using NoaaWeb.Data.SatellitePass;
using NoaaWeb.Data.UpcomingPass;

namespace NoaaWeb.App
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddResponseCompression();

            services.AddControllers();
            services.AddSingleton<ISatellitePassRepository, SatellitePassFileRepository>();
            services.AddSingleton<IUpcomingPassRepository, UpcomingPassFileRepository>();
            services.Configure<WebDavConfiguration>(Configuration.GetSection("WebDav"));
            services.Configure<FileDbConfiguration>(Configuration);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IOptions<WebDavConfiguration> webDavConfig)
        {
            app.UseResponseCompression();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseDefaultFiles();

            app.UseStaticFiles(new StaticFileOptions
            {
                RequestPath = "/data",
                FileProvider = new WebDavFileProvider(webDavConfig)
            });

            app.UseStaticFiles();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
