using System.Collections.Generic;

namespace NoaaWeb.App.Models
{
    public class ProjectionViewResult
    {
        required public IList<ProjectionItemViewModel> Past { get; set; }
        required public ProjectionItemViewModel Current { get; set; }
        required public IList<ProjectionItemViewModel> Future { get; set; }
    }
}
