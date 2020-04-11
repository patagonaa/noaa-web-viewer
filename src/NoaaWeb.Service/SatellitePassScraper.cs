using FileProviders.WebDav;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using NoaaWeb.Data;
using System;
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
        private readonly object _scrapeLock = new object();

        public SatellitePassScraper(ILogger<SatellitePassScraper> logger, ISatellitePassRepository satellitePassRepository, WebDavFileProvider fileProvider)
        {
            _logger = logger;
            _satellitePassRepository = satellitePassRepository;
            _fileProvider = fileProvider;
        }

        public void Scrape(CancellationToken cancellationToken)
        {
            lock (_scrapeLock)
            {
                _logger.LogInformation("starting pass scrape");
                var existingPasses = _satellitePassRepository.Get().Select(x => x.FileKey).ToHashSet();

                var metaFiles = _fileProvider.GetDirectoryContents("/meta");
                foreach (var metaFileInfo in metaFiles)
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

                    var rawImage = _fileProvider.GetFileInfo($"images/{fileKey}-RAW.png");

                    if (!rawImage.Exists)
                    {
                        _logger.LogInformation("no raw image for {FileKey}", fileKey);
                        continue;
                    }

                    var startTimeStr = fileKey.Substring(0, 15);
                    var startTime = DateTime.ParseExact(startTimeStr, "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

                    var satName = fileKey.Substring(16);

                    string metaData;

                    using (var sr = new StreamReader(metaFileInfo.CreateReadStream()))
                    {
                        metaData = sr.ReadToEnd();
                    }

                    var channelA = Regex.Match(metaData, @"^CHAN_A=Channel A: (.*) \(.*\)$", RegexOptions.Multiline).Groups[1].Value;
                    var channelB = Regex.Match(metaData, @"^CHAN_B=Channel B: (.*) \(.*\)$", RegexOptions.Multiline).Groups[1].Value;
                    var gain = -double.Parse(Regex.Match(metaData, @"^GAIN=Gain: (.*)$", RegexOptions.Multiline).Groups[1].Value, CultureInfo.InvariantCulture);
                    var maxElev = int.Parse(Regex.Match(metaData, @"^MAXELEV=(.*)$", RegexOptions.Multiline).Groups[1].Value, CultureInfo.InvariantCulture);

                    var enhancementTypes = (EnhancementTypes)0;

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

                    var toInsert = new SatellitePass
                    {
                        FileKey = fileKey,
                        StartTime = startTime,
                        SatelliteName = satName,
                        ChannelA = channelA,
                        ChannelB = channelB,
                        Gain = gain,
                        MaxElevation = maxElev,
                        EnhancementTypes = enhancementTypes
                    };

                    IFileInfo thumbnailSource = null;
                    string thumbnailEnhancementType = null;
                    if (enhancementTypes.HasFlag(EnhancementTypes.Msa))
                    {
                        var msaImage = _fileProvider.GetFileInfo($"images/{fileKey}-MSA.png");

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

                    _logger.LogInformation("{FileKey} successfully scraped", fileKey);
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
