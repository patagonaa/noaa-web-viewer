using InfluxDB.LineProtocol.Client;
using InfluxDB.LineProtocol.Payload;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NoaaWeb.Data.SatellitePass;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NoaaWeb.Service
{
    class InfluxMetricsSender
    {
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly ILogger<InfluxMetricsSender> _logger;
        private readonly ISatellitePassRepository _passRepository;
        private readonly LineProtocolClient _client;
        private DateTime _lastPass = DateTime.MinValue;

        public InfluxMetricsSender(ILogger<InfluxMetricsSender> logger, ISatellitePassRepository passRepository, IOptions<InfluxMetricsConfiguration> options)
        {
            _logger = logger;
            _passRepository = passRepository;
            var config = options.Value;

            if (string.IsNullOrEmpty(config.Url))
            {
                _logger.LogInformation("InfluxDB Url not set, skipping pass metrics");
            }
            else
            {
                _client = new LineProtocolClient(new Uri(config.Url), config.Database, config.Username, config.Password);
            }
        }

        public async Task Send(CancellationToken cancellationToken)
        {
            if (_client == null)
                return;

            try
            {
                await _sendLock.WaitAsync(cancellationToken);

                _logger.LogInformation("Starting InfluxDB metrics send.");

                try
                {
                    var allPasses = _passRepository.Get()
                        .Where(x => x.StartTime > _lastPass)
                        .OrderBy(x => x.StartTime)
                        .ToList();

                    foreach (var chunk in allPasses.Chunk(500))
                    {
                        var points = new LineProtocolPayload();

                        foreach (var pass in chunk)
                        {
                            points.Add(MapPass(pass));
                        }

                        var writeResult = await _client.WriteAsync(points, cancellationToken);

                        if (!writeResult.Success)
                            throw new Exception(writeResult.ErrorMessage);

                        _logger.LogInformation("Written {PassCount} passes to InfluxDB", chunk.Length);

                        _lastPass = chunk.Max(x => x.StartTime);
                    }

                    _logger.LogInformation("InfluxDB write success!");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while sending InfluxDB metrics");
                }
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private static LineProtocolPoint MapPass(SatellitePass pass)
        {
            var fields = new Dictionary<string, object>
            {
                {"enhancementTypes", (int)pass.EnhancementTypes},
                {"projectionTypes", (int)pass.ProjectionTypes},
                {"gain", pass.Gain },
                {"maxElevation", pass.MaxElevation }
            };
            if (pass.EndTime.HasValue)
            {
                fields.Add("durationSeconds", (pass.EndTime.Value - pass.StartTime).TotalSeconds);
            }

            var tags = new Dictionary<string, string>
            {
                {"site", pass.Site },
                {"sat", pass.SatelliteName },
                {"channelA", pass.ChannelA },
                {"channelB", pass.ChannelB },
            };

            return new LineProtocolPoint("pass", fields, tags, pass.StartTime);
        }
    }
}
