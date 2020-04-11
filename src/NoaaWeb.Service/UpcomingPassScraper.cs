using FileProviders.WebDav;
using Microsoft.Extensions.Logging;
using NoaaWeb.Data;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace NoaaWeb.Service
{
    internal class UpcomingPassScraper
    {
        private readonly ILogger<UpcomingPassScraper> _logger;
        private readonly IUpcomingPassRepository _upcomingPassRepository;
        private readonly WebDavFileProvider _fileProvider;
        private readonly object _scrapeLock = new object();

        private DateTimeOffset _lastModifiedAt = DateTimeOffset.MinValue;

        public UpcomingPassScraper(ILogger<UpcomingPassScraper> logger, IUpcomingPassRepository upcomingPassRepository, WebDavFileProvider fileProvider)
        {
            _logger = logger;
            _upcomingPassRepository = upcomingPassRepository;
            _fileProvider = fileProvider;
        }

        public void Scrape(CancellationToken cancellationToken)
        {
            lock (_scrapeLock)
            {
                _logger.LogInformation("starting upcoming pass scrape");
                var upcomingPassFileInfo = _fileProvider.GetFileInfo("/upcoming_passes.txt");
                if (!upcomingPassFileInfo.Exists)
                {
                    _logger.LogWarning("Upcoming Passes file does not exist!");
                }
                else if (upcomingPassFileInfo.LastModified == _lastModifiedAt)
                {
                    _logger.LogInformation("Upcoming passes did not change, skipping scrape.");
                }
                else
                {
                    _lastModifiedAt = upcomingPassFileInfo.LastModified;

                    _upcomingPassRepository.Clear();

                    using (var sr = new StreamReader(upcomingPassFileInfo.CreateReadStream(), Encoding.UTF8))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line))
                                continue;

                            var splitLine = line.Split(',');
                            if (splitLine.Length != 7)
                            {
                                _logger.LogWarning("Invalid Line: {LineContent}", splitLine);
                                continue;
                            }

                            _upcomingPassRepository.Insert(new UpcomingSatellitePass
                            {
                                StartTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(int.Parse(splitLine[0], CultureInfo.InvariantCulture)),
                                SatelliteName = splitLine[4].Replace(" ", ""),
                                MaxElevation = int.Parse(splitLine[2], CultureInfo.InvariantCulture)
                            });
                        }
                    }
                }

                _logger.LogInformation("scrape done!");
            }
        }
    }
}
