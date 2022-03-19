using FileProviders.WebDav;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NoaaWeb.Data;
using NoaaWeb.Data.UpcomingPass;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace NoaaWeb.Service
{
    internal class UpcomingPassScraper
    {
        private readonly ILogger<UpcomingPassScraper> _logger;
        private readonly SiteConfiguration _siteConfig;
        private readonly IUpcomingPassRepository _upcomingPassRepository;
        private readonly WebDavFileProvider _fileProvider;
        private readonly IDictionary<string, DateTimeOffset> _lastModifiedBySite = new Dictionary<string, DateTimeOffset>();
        private readonly object _scrapeLock = new object();

        public UpcomingPassScraper(ILogger<UpcomingPassScraper> logger, IOptions<SiteConfiguration> siteConfig, IUpcomingPassRepository upcomingPassRepository, WebDavFileProvider fileProvider)
        {
            _logger = logger;
            _siteConfig = siteConfig.Value;
            _upcomingPassRepository = upcomingPassRepository;
            _fileProvider = fileProvider;
        }

        public void Scrape(CancellationToken cancellationToken)
        {
            lock (_scrapeLock)
            {
                _logger.LogInformation("starting upcoming pass scrape");

                var upcomingPasses = _upcomingPassRepository.Get().ToList();

                foreach (var site in _siteConfig.Sites ?? new List<string> { "" })
                {
                    var sitePasses = Scrape(site);
                    if (sitePasses != null)
                    {
                        upcomingPasses.RemoveAll(x => x.Site == site);
                        upcomingPasses.AddRange(sitePasses);
                    }
                }

                _upcomingPassRepository.Clear();
                _upcomingPassRepository.Insert(upcomingPasses);

                _logger.LogInformation("scrape done!");
            }
        }

        private IList<UpcomingSatellitePass> Scrape(string site)
        {
            var upcomingPassFileInfo = _fileProvider.GetFileInfo(site == "" ? "/upcoming_passes.txt" : $"/{site}/upcoming_passes.txt");
            if (!upcomingPassFileInfo.Exists)
            {
                _logger.LogWarning("Upcoming Passes file does not exist!");
                return new List<UpcomingSatellitePass>();
            }
            var currentLastModified = upcomingPassFileInfo.LastModified;
            if (currentLastModified != DateTimeOffset.MinValue && _lastModifiedBySite.TryGetValue(site, out var savedLastModified) && currentLastModified == savedLastModified)
            {
                _logger.LogInformation("Upcoming passes file has not been modified");
                return null;
            }
            _lastModifiedBySite[site] = currentLastModified;

            var toReturn = new List<UpcomingSatellitePass>();
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

                    toReturn.Add(new UpcomingSatellitePass
                    {
                        Site = site,
                        StartTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(int.Parse(splitLine[0], CultureInfo.InvariantCulture)),
                        EndTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(int.Parse(splitLine[1], CultureInfo.InvariantCulture)),
                        SatelliteName = splitLine[4].Replace(" ", ""),
                        MaxElevation = int.Parse(splitLine[2], CultureInfo.InvariantCulture)
                    });
                }
            }
            return toReturn;
        }
    }
}
