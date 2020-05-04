using FileProviders.WebDav;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using NoaaWeb.Data.SatellitePass;
using Prometheus;
using System;
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
        private readonly ISatellitePassRepository _satellitePassRepository;
        private readonly WebDavFileProvider _fileProvider;
        private readonly Counter _scrapeCounter;
        private readonly Counter _scrapeDurationCounter;
        private readonly Counter _passCounter;
        private readonly Counter _passDurationCounter;
        private readonly object _scrapeLock = new object();

        public SatellitePassScraper(ILogger<SatellitePassScraper> logger, ISatellitePassRepository satellitePassRepository, WebDavFileProvider fileProvider)
        {
            _logger = logger;
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
                    LabelNames = new string[] {}
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
                try
                {
                    _logger.LogInformation("starting pass scrape");
                    var sw = Stopwatch.StartNew();

                    var existingPasses = _satellitePassRepository.Get().Select(x => x.FileKey).ToHashSet();

                    var yearsDir = _fileProvider.GetDirectoryContents("/meta");

                    foreach (var year in yearsDir.Where(x => x.IsDirectory).Select(x => x.Name).OrderBy(x => x))
                    {
                        var monthsDir = _fileProvider.GetDirectoryContents($"/meta/{year}");
                        foreach (var month in monthsDir.Where(x => x.IsDirectory).Select(x => x.Name).OrderBy(x => x))
                        {
                            var monthDir = _fileProvider.GetDirectoryContents($"/meta/{year}/{month}");
                            var monthImagesDir = _fileProvider.GetDirectoryContents($"/images/{year}/{month}");

                            foreach (var metaFileInfo in monthDir.OrderBy(x => x.Name))
                            {
                                if (cancellationToken.IsCancellationRequested)
                                    break;

                                var fileKey = Path.GetFileNameWithoutExtension(metaFileInfo.Name);

                                _logger.LogInformation("scraping {FileKey}", fileKey);

                                if (existingPasses.Contains(fileKey))
                                {
                                    _logger.LogInformation("{FileKey} already in database", fileKey);
                                    continue;
                                }

                                var startTimeStr = fileKey.Substring(0, 15);
                                var startTime = DateTime.ParseExact(startTimeStr, "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

                                var imageDir = $"images/{year}/{month}";

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

                                var channelA = Regex.Match(metaData, @"^CHAN_A=Channel A: (.*) \(.*\)$", RegexOptions.Multiline).Groups[1].Value;
                                var channelB = Regex.Match(metaData, @"^CHAN_B=Channel B: (.*) \(.*\)$", RegexOptions.Multiline).Groups[1].Value;
                                var gain = -double.Parse(Regex.Match(metaData, @"^GAIN=Gain: (.*)$", RegexOptions.Multiline).Groups[1].Value, CultureInfo.InvariantCulture);
                                var maxElev = int.Parse(Regex.Match(metaData, @"^MAXELEV=(.*)$", RegexOptions.Multiline).Groups[1].Value, CultureInfo.InvariantCulture);

                                var enhancementTypes = EnhancementTypes.None;

                                if (new[] { channelA, channelB }.Any(x => x == "4") && new[] { channelA, channelB }.Any(x => x == "1" || x == "2"))
                                {
                                    enhancementTypes |= EnhancementTypes.Msa;
                                }

                                if (new[] { channelA, channelB }.Any(x => x == "4"))
                                {
                                    enhancementTypes |= EnhancementTypes.Mcir;
                                    enhancementTypes |= EnhancementTypes.Therm;
                                    enhancementTypes |= EnhancementTypes.Za;
                                    enhancementTypes |= EnhancementTypes.No;
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
