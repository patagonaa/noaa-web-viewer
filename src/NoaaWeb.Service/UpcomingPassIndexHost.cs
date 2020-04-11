using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace NoaaWeb.Service
{
    class UpcomingPassIndexHost : IHostedService
    {
        private readonly Timer _timer;
        private readonly UpcomingPassScraper _scraper;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public UpcomingPassIndexHost(UpcomingPassScraper scraper)
        {
            _timer = new Timer
            {
                Interval = 60 * 1000,
                AutoReset = true
            };
            _timer.Elapsed += (sender, e) => _scraper.Scrape(_cts.Token);
            _scraper = scraper;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer.Start();
            _scraper.Scrape(cancellationToken);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer.Stop();
            _cts.Cancel();
            return Task.CompletedTask;
        }
    }
}
