using System;

namespace NoaaWeb.Data.UpcomingPass
{
    public class UpcomingSatellitePass
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string SatelliteName { get; set; }
        public int MaxElevation { get; set; }
    }
}
