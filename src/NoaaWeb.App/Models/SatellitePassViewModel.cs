﻿using NoaaWeb.Data.SatellitePass;
using System;

namespace NoaaWeb.App.Models
{
    public class SatellitePassViewModel
    {
        public string ImageDir { get; set; }
        public string FileKey { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string SatelliteName { get; set; }
        public string ChannelA { get; set; }
        public string ChannelB { get; set; }
        public int MaxElevation { get; set; }
        public double? Gain { get; set; }
        public EnhancementTypes? EnhancementTypes { get; set; }
        public ProjectionTypes? ProjectionTypes { get; set; }
        public string ThumbnailUri { get; set; }
        public string ThumbnailEnhancementType { get; set; }
        public bool IsUpcomingPass { get; set; }
    }
}
