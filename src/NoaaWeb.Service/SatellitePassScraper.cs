using FileProviders.WebDav;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NoaaWeb.Data;
using NoaaWeb.Data.SatellitePass;
using Prometheus;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace NoaaWeb.Service
{
    internal class SatellitePassScraper
    {
        private readonly ILogger<SatellitePassScraper> _logger;
        private readonly SiteConfiguration _siteConfig;
        private readonly ISatellitePassRepository _satellitePassRepository;
        private readonly NoaaWebDavFileProvider _fileProvider;
        private readonly Counter _scrapeCounter;
        private readonly Counter _scrapeDurationCounter;
        private readonly Counter _passCounter;
        private readonly Counter _passDurationCounter;
        private readonly IList<string> _invalidMetaPasses = new List<string>();
        private readonly object _scrapeLock = new object();

        public SatellitePassScraper(ILogger<SatellitePassScraper> logger, IOptions<SiteConfiguration> siteConfig, ISatellitePassRepository satellitePassRepository, NoaaWebDavFileProvider fileProvider)
        {
            _logger = logger;
            _siteConfig = siteConfig.Value;
            _satellitePassRepository = satellitePassRepository;
            _fileProvider = fileProvider;

            _scrapeCounter = Metrics.CreateCounter(
                "noaa_pass_scrape_total",
                "total number of times satellite passes were scraped",
                new CounterConfiguration
                {
                    LabelNames = new string[] { "result" }
                });
            _scrapeDurationCounter = Metrics.CreateCounter(
                "noaa_pass_scrape_seconds_total",
                "total time satellite passes were scraped",
                new CounterConfiguration
                {
                    LabelNames = new string[] { }
                });
            _passCounter = Metrics.CreateCounter(
                "noaa_pass_scrape_passes_total",
                "total number of scraped satellite passes",
                new CounterConfiguration
                {
                    LabelNames = new string[] { "sat" }
                });
            _passDurationCounter = Metrics.CreateCounter(
                "noaa_pass_scrape_passes_seconds_total",
                "total duration of scraped satellite passes",
                new CounterConfiguration
                {
                    LabelNames = new string[] { "sat" }
                });
        }

        public void Scrape(CancellationToken cancellationToken)
        {
            lock (_scrapeLock)
            {
                foreach (var site in _siteConfig.Sites ?? new List<string> { "" })
                {
                    Scrape(cancellationToken, site);
                }
            }
        }

        private void Scrape(CancellationToken cancellationToken, string site)
        {
            try
            {
                _logger.LogInformation("starting pass scrape for site {Site}", site);
                var sw = Stopwatch.StartNew();

                var existingPasses = _satellitePassRepository.Get().Select(x => x.FileKey).ToHashSet();

                var baseUrl = site == "" ? "" : ("/" + site);

                var yearsDir = _fileProvider.GetDirectoryContents($"{baseUrl}/meta");

                foreach (var year in yearsDir.Where(x => x.IsDirectory).Select(x => x.Name).OrderBy(x => x))
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var monthsDir = _fileProvider.GetDirectoryContents($"{baseUrl}/meta/{year}");
                    foreach (var month in monthsDir.Where(x => x.IsDirectory).Select(x => x.Name).OrderBy(x => x))
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        var monthDir = _fileProvider.GetDirectoryContents($"{baseUrl}/meta/{year}/{month}");
                        var monthImagesDir = _fileProvider.GetDirectoryContents($"{baseUrl}/images/{year}/{month}");

                        _logger.LogInformation("scraping {ScrapeMonth}", $"{year}-{month}");

                        foreach (var metaFileInfo in monthDir.OrderBy(x => x.Name))
                        {
                            if (cancellationToken.IsCancellationRequested)
                                break;

                            var fileKey = Path.GetFileNameWithoutExtension(metaFileInfo.Name);

                            if (existingPasses.Contains(fileKey) || _invalidMetaPasses.Contains(GetUniquePassKey(site, fileKey)))
                            {
                                continue;
                            }

                            _logger.LogInformation("scraping {FileKey}", fileKey);

                            var startTimeStr = fileKey.Substring(0, 15);
                            var startTime = DateTime.ParseExact(startTimeStr, "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

                            var imageDir = $"{baseUrl}/images/{year}/{month}";

                            var rawImage = _fileProvider.GetFileInfo($"{imageDir}/{fileKey}-RAW.png");

                            if (!rawImage.Exists)
                            {
                                _logger.LogInformation("no raw image for {FileKey}", fileKey);
                                continue;
                            }

                            var satName = fileKey.Substring(16);

                            string metaData;

                            using (var sr = new StreamReader(metaFileInfo.CreateReadStream()))
                            {
                                metaData = sr.ReadToEnd();
                            }

                            var endTimeMatch = Regex.Match(metaData, @"^END_TIME=(.*)$", RegexOptions.Multiline);
                            DateTime? endTime = null;
                            if (endTimeMatch.Success)
                            {
                                endTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(double.Parse(endTimeMatch.Groups[1].Value));
                            }

                            Match channelAMatch = Regex.Match(metaData, @"^CHAN_A=Channel A: (.*) \(.*\)$", RegexOptions.Multiline);
                            Match channelBMatch = Regex.Match(metaData, @"^CHAN_B=Channel B: (.*) \(.*\)$", RegexOptions.Multiline);
                            Match gainMatch = Regex.Match(metaData, @"^GAIN=Gain: (.*)$", RegexOptions.Multiline);
                            Match maxElevMatch = Regex.Match(metaData, @"^MAXELEV=(.*)$", RegexOptions.Multiline);

                            if (!channelAMatch.Success ||
                                !channelBMatch.Success ||
                                !gainMatch.Success || !double.TryParse(gainMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var gainRaw) || double.IsNaN(gainRaw) ||
                                !maxElevMatch.Success || !int.TryParse(maxElevMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxElev))
                            {
                                _logger.LogInformation("metadata invalid for {FileKey}", fileKey);
                                _invalidMetaPasses.Add(GetUniquePassKey(site, fileKey));
                                continue;
                            }

                            var channelA = channelAMatch.Groups[1].Value;
                            var channelB = channelBMatch.Groups[1].Value;
                            var gain = -gainRaw;

                            var enhancementTypes = EnhancementTypes.None;

                            if (new[] { channelA, channelB }.Any(x => x == "4") && new[] { channelA, channelB }.Any(x => x == "1" || x == "2"))
                            {
                                enhancementTypes |= EnhancementTypes.Msa;
                            }
                            else
                            {
                                if (monthImagesDir.Any(x => x.Name == $"{fileKey}-MSA.png"))
                                    _fileProvider.DeleteFile($"{imageDir}/{fileKey}-MSA.png");
                                if (monthImagesDir.Any(x => x.Name == $"{fileKey}-MSA-merc.png"))
                                    _fileProvider.DeleteFile($"{imageDir}/{fileKey}-MSA-merc.png");
                                if (monthImagesDir.Any(x => x.Name == $"{fileKey}-MSA-stereo.png"))
                                    _fileProvider.DeleteFile($"{imageDir}/{fileKey}-MSA-stereo.png");
                            }

                            if (new[] { channelA, channelB }.Any(x => x == "4"))
                            {
                                enhancementTypes |= EnhancementTypes.Mcir;
                                enhancementTypes |= EnhancementTypes.Therm;
                                enhancementTypes |= EnhancementTypes.Za;
                                enhancementTypes |= EnhancementTypes.No;
                            }
                            else
                            {
                                if (monthImagesDir.Any(x => x.Name == $"{fileKey}-MCIR.png"))
                                    _fileProvider.DeleteFile($"{imageDir}/{fileKey}-MCIR.png");
                                if (monthImagesDir.Any(x => x.Name == $"{fileKey}-THERM.png"))
                                    _fileProvider.DeleteFile($"{imageDir}/{fileKey}-THERM.png");
                                if (monthImagesDir.Any(x => x.Name == $"{fileKey}-ZA.png"))
                                    _fileProvider.DeleteFile($"{imageDir}/{fileKey}-ZA.png");
                                if (monthImagesDir.Any(x => x.Name == $"{fileKey}-NO.png"))
                                    _fileProvider.DeleteFile($"{imageDir}/{fileKey}-NO.png");
                                if (monthImagesDir.Any(x => x.Name == $"{fileKey}-THERM-merc.png"))
                                    _fileProvider.DeleteFile($"{imageDir}/{fileKey}-THERM-merc.png");
                                if (monthImagesDir.Any(x => x.Name == $"{fileKey}-THERM-stereo.png"))
                                    _fileProvider.DeleteFile($"{imageDir}/{fileKey}-THERM-stereo.png");
                            }

                            var projectionTypes = ProjectionTypes.None;

                            if (enhancementTypes.HasFlag(EnhancementTypes.Msa) && monthImagesDir.Any(x => x.Name == $"{fileKey}-MSA-merc.png"))
                            {
                                projectionTypes |= ProjectionTypes.MsaMercator;
                            }

                            if (enhancementTypes.HasFlag(EnhancementTypes.Msa) && monthImagesDir.Any(x => x.Name == $"{fileKey}-MSA-stereo.png"))
                            {
                                projectionTypes |= ProjectionTypes.MsaStereographic;
                            }

                            if (enhancementTypes.HasFlag(EnhancementTypes.Therm) && monthImagesDir.Any(x => x.Name == $"{fileKey}-THERM-merc.png"))
                            {
                                projectionTypes |= ProjectionTypes.ThermMercator;
                            }

                            if (enhancementTypes.HasFlag(EnhancementTypes.Therm) && monthImagesDir.Any(x => x.Name == $"{fileKey}-THERM-stereo.png"))
                            {
                                projectionTypes |= ProjectionTypes.ThermStereographic;
                            }

                            var toInsert = new SatellitePass
                            {
                                Site = site,
                                ImageDir = imageDir,
                                FileKey = fileKey,
                                StartTime = startTime,
                                EndTime = endTime,
                                SatelliteName = satName,
                                ChannelA = channelA,
                                ChannelB = channelB,
                                Gain = gain,
                                MaxElevation = maxElev,
                                EnhancementTypes = enhancementTypes,
                                ProjectionTypes = projectionTypes
                            };

                            IFileInfo thumbnailSource = null;
                            string thumbnailEnhancementType = null;
                            if (enhancementTypes.HasFlag(EnhancementTypes.Msa))
                            {
                                var msaImage = _fileProvider.GetFileInfo($"{imageDir}/{fileKey}-MSA.png");

                                if (msaImage.Exists)
                                {
                                    thumbnailSource = msaImage;
                                    thumbnailEnhancementType = "MSA";
                                }
                            }
                            if (thumbnailSource == null)
                            {
                                thumbnailSource = rawImage;
                                thumbnailEnhancementType = "RAW";
                            }

                            using (var imageStream = thumbnailSource.CreateReadStream())
                            {
                                toInsert.ThumbnailUri = GetThumbnail(imageStream);
                                toInsert.ThumbnailEnhancementType = thumbnailEnhancementType;
                            }

                            _satellitePassRepository.Insert(toInsert);
                            _passCounter.WithLabels(satName).Inc();
                            if (endTime.HasValue)
                                _passDurationCounter.WithLabels(satName).Inc((endTime.Value - startTime).TotalSeconds);
                            _logger.LogInformation("{FileKey} successfully scraped", fileKey);
                        }
                    }
                }
                sw.Stop();

                _scrapeCounter.WithLabels("success").Inc();
                _scrapeDurationCounter.Inc(sw.Elapsed.TotalSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while scraping!");
                _scrapeCounter.WithLabels("error").Inc();
            }

            _logger.LogInformation("scrape done!");
        }

        private static string GetUniquePassKey(string site, string fileKey)
        {
            return $"{site}/{fileKey}";
        }

        private string GetThumbnail(Stream file)
        {
            var thumbHeight = 200;

            using (var image = Image.FromStream(file))
            {
                var thumbWidth = (int)((double)image.Width / image.Height * thumbHeight);

                using (var thumb = image.GetThumbnailImage(thumbWidth, thumbHeight, () => false, IntPtr.Zero))
                {
                    using (var ms = new MemoryStream())
                    {
                        var encoderParameters = new EncoderParameters(1);
                        encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 70L);

                        thumb.Save(ms, GetEncoder(ImageFormat.Jpeg), encoderParameters);

                        return $"data:image/jpeg;base64,{Convert.ToBase64String(ms.ToArray())}";
                    }
                }
            }
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            var codecs = ImageCodecInfo.GetImageDecoders();
            foreach (var codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }
    }
}
