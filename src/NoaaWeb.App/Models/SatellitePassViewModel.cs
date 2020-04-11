using NoaaWeb.Data;
using System;

namespace NoaaWeb.App.Models
{
    public class SatellitePassViewModel
    {
        public string FileKey { get; set; }
        public DateTime StartTime { get; set; }
        public string SatelliteName { get; set; }
        public string ChannelA { get; set; }
        public string ChannelB { get; set; }
        public int MaxElevation { get; set; }
        public double? Gain { get; set; }
        public EnhancementTypes? EnhancementTypes { get; set; }
        public string ThumbnailUri { get; set; }
        public string ThumbnailEnhancementType { get; set; }
        public bool IsUpcomingPass { get; set; }
    }
}
