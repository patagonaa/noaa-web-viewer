using System.Collections.Generic;

namespace NoaaWeb.App.Models
{
    public class SatellitePassResult
    {
        public int Page { get; set; }
        public int PageCount { get; set; }
        required public IList<SatellitePassViewModel> Results { get; set; }
    }
}
