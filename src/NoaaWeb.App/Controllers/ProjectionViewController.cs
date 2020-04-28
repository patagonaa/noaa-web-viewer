using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NoaaWeb.App.Models;
using NoaaWeb.Data.SatellitePass;
using System.Linq;

namespace NoaaWeb.App.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectionViewController : ControllerBase
    {
        private readonly ILogger<ProjectionViewController> _logger;
        private readonly ISatellitePassRepository _passRepository;

        public ProjectionViewController(ILogger<ProjectionViewController> logger, ISatellitePassRepository passRepository)
        {
            _logger = logger;
            _passRepository = passRepository;
        }

        public ActionResult<ProjectionViewResult> Get(string fileKey, ProjectionTypes? projectionType)
        {
            var passes = _passRepository.Get();

            var currentPass = passes.SingleOrDefault(x => x.FileKey == fileKey);

            if(currentPass == null)
            {
                return NotFound();
            }

            if(projectionType == null)
            {
                // this is the same fallback logic as in projection.ts init() so we don't have to make a second request
                // to get the past/future passes with the currently selected projection
                projectionType = currentPass.ProjectionTypes.HasFlag(ProjectionTypes.MsaStereographic) ?
                    ProjectionTypes.MsaStereographic :
                    ProjectionTypes.ThermStereographic;
            }

            var pastPasses = passes.OrderByDescending(x => x.StartTime).Where(x => x.StartTime < currentPass.StartTime && x.ProjectionTypes.HasFlag(projectionType)).Take(5).ToList();
            var futurePasses = passes.OrderBy(x => x.StartTime).Where(x => x.StartTime > currentPass.StartTime && x.ProjectionTypes.HasFlag(projectionType)).Take(5).ToList();

            return new ProjectionViewResult
            {
                Past = pastPasses.Select(Map).ToList(),
                Current = Map(currentPass),
                Future = futurePasses.Select(Map).ToList(),
            };
        }

        private ProjectionItemViewModel Map(SatellitePass pass)
        {
            return new ProjectionItemViewModel
            {
                FileKey = pass.FileKey,
                ImageDir = pass.ImageDir,
                ProjectionTypes = pass.ProjectionTypes,
                StartTime = pass.StartTime
            };
        }
    }
}