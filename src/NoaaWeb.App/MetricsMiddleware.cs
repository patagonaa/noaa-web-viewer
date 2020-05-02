using Microsoft.AspNetCore.Http;
using Prometheus;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;

namespace NoaaWeb.App
{
    public class MetricsMiddleware : IMiddleware
    {
        private Histogram _responseTimeHistogram;
        private Counter _responseBytesTotalCounter;

        public MetricsMiddleware()
        {
            _responseTimeHistogram = Metrics.CreateHistogram(
                "noaa_request_duration_seconds",
                "how long a request took",
                new HistogramConfiguration
                {
                    LabelNames = new string[] { "code", "method", "type" }
                });
            _responseBytesTotalCounter = Metrics.CreateCounter(
                "noaa_response_bytes_total",
                "number of bytes sent to the client",
                new CounterConfiguration
                {
                    LabelNames = new string[] { "code", "method", "type" }
                });
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var sw = Stopwatch.StartNew();
            await next(context);
            sw.Stop();

            var code = context.Response.StatusCode;
            var method = context.Request.Method;
            var bytes = context.Response.ContentLength ?? 0;

            string type;

            if (context.Request.Path.StartsWithSegments("/data"))
            {
                type = "data";
            }
            else if (context.Request.Path.StartsWithSegments("/api"))
            {
                type = "api";
            }
            else
            {
                type = "other";
            }

            _responseTimeHistogram.WithLabels(code.ToString(CultureInfo.InvariantCulture), method, type).Observe(sw.Elapsed.TotalSeconds);
            _responseBytesTotalCounter.WithLabels(code.ToString(CultureInfo.InvariantCulture), method, type).Inc(bytes);
        }
    }
}
