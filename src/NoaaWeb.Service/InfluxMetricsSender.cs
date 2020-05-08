using InfluxDB.LineProtocol.Client;
using InfluxDB.LineProtocol.Payload;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NoaaWeb.Data.SatellitePass;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NoaaWeb.Service
{
    class InfluxMetricsSender
    {
        private readonly object _sendLock = new object();
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

        public void Send(CancellationToken cancellationToken)
        {
            if (_client == null)
                return;

            lock (_sendLock)
            {
                _logger.LogInformation("Starting InfluxDB metrics send.");

                try
                {
                    var passes = _passRepository.Get()
                        .Where(x => x.StartTime > _lastPass)
                        .OrderBy(x => x.StartTime)
                        .Take(500)
                        .ToList();

                    if (passes.Count > 0)
                    {
                        var points = new LineProtocolPayload();

                        foreach (var pass in passes)
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
                                {"sat", pass.SatelliteName },
                                {"channelA", pass.ChannelA },
                                {"channelB", pass.ChannelB },
                            };

                            var passPoint = new LineProtocolPoint("pass", fields, tags, pass.StartTime);
                            points.Add(passPoint);
                        }
                        _logger.LogInformation("Adding {PassCount} new passes to InfluxDB", passes.Count);

                        var writeResult = _client.WriteAsync(points, cancellationToken).Result;

                        if (!writeResult.Success)
                            throw new Exception(writeResult.ErrorMessage);
                    }

                    _lastPass = passes.Max(x => x.StartTime);

                    _logger.LogInformation("InfluxDB write success!");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while sending InfluxDB metrics");
                }
            }
        }
    }
}
