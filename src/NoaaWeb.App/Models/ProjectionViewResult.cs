using System.Collections.Generic;

namespace NoaaWeb.App.Models
{
    public class ProjectionViewResult
    {
        public IList<ProjectionItemViewModel> Past { get; set; }
        public ProjectionItemViewModel Current { get; set; }
        public IList<ProjectionItemViewModel> Future { get; set; }
    }
}
