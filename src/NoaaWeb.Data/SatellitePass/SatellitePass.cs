using System;
using System.Text.Json.Serialization;

namespace NoaaWeb.Data.SatellitePass
{
    public class SatellitePass
    {
        required public string Site { get; set; }
        required public string FileKey { get; set; }
        required public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        required public string SatelliteName { get; set; }
        public string? ChannelA { get; set; }
        public string? ChannelB { get; set; }
        public int? MaxElevation { get; set; }
        public double? Gain { get; set; }
        required public ImageTypes ImageTypes { get; set; }
        required public ProjectionTypes ProjectionTypes { get; set; }
        [JsonIgnore]
        public string? ThumbnailUri { get; set; }
        public string? ThumbnailImageType { get; set; }
        required public string ImageDir { get; set; }
    }
}
