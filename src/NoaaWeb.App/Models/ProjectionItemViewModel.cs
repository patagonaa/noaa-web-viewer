using NoaaWeb.Data.SatellitePass;
using System;

namespace NoaaWeb.App.Models
{
    public class ProjectionItemViewModel
    {
        public DateTime StartTime { get; set; }
        public string FileKey { get; set; }
        public string ImageDir { get; set; }
        public ProjectionTypes ProjectionTypes { get; set; }
    }
}
