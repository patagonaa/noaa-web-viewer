using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace NoaaWeb.Service
{
    class InfluxMetricsHost : IHostedService
    {
        private readonly Timer _timer;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly InfluxMetricsSender _sender;

        public InfluxMetricsHost(InfluxMetricsSender sender)
        {
            _timer = new Timer
            {
                Interval = 1000 * 60 * 15, // 15 minutes
                AutoReset = true
            };
            _timer.Elapsed += (sender, e) => _sender.Send(_cts.Token);
            _sender = sender;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer.Start();
            _sender.Send(cancellationToken);
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
