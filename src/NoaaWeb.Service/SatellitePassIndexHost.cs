using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace NoaaWeb.Service
{
    class SatellitePassIndexHost : IHostedService
    {
        private readonly Timer _timer;
        private readonly SatellitePassScraper _scraper;

        public SatellitePassIndexHost(SatellitePassScraper scraper)
        {
            _timer = new Timer
            {
                Interval = 60 * 1000,
                AutoReset = true
            };
            _timer.Elapsed += (sender, e) => _scraper.Scrape();
            _scraper = scraper;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer.Start();
            _scraper.Scrape();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer.Stop();
            return Task.CompletedTask;
        }
    }
}
