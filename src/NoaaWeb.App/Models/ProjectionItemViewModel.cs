using NoaaWeb.Data.SatellitePass;
using System;

namespace NoaaWeb.App.Models
{
    public class ProjectionItemViewModel
    {
        required public DateTime StartTime { get; set; }
        required public string FileKey { get; set; }
        required public string ImageDir { get; set; }
        required public ProjectionTypes ProjectionTypes { get; set; }
    }
}
