using System;

namespace NoaaWeb.Data.UpcomingPass
{
    public class UpcomingSatellitePass
    {
        required public string Site { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        required public string SatelliteName { get; set; }
        public int MaxElevation { get; set; }
    }
}
