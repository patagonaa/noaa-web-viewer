using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NoaaWeb.App.Models;
using NoaaWeb.Data;

namespace NoaaWeb.App.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SatellitePassController : ControllerBase
    {
        private const int _pageSize = 20;

        private readonly ILogger<SatellitePassController> _logger;
        private readonly ISatellitePassRepository _passRepository;
        private readonly IUpcomingPassRepository _upcomingPassRepository;

        public SatellitePassController(ILogger<SatellitePassController> logger, ISatellitePassRepository passRepository, IUpcomingPassRepository upcomingPassRepository)
        {
            _logger = logger;
            _passRepository = passRepository;
            _upcomingPassRepository = upcomingPassRepository;
        }

        [HttpGet]
        public SatellitePassResult Get(string sortField, string sortDir, int page = 0)
        {
            var passes = _passRepository.Get().ToList(); // todo: when/if this is a real database some day, we really shouldn't do ToList here...

            var latestPassTime = passes.Max(x => x.StartTime);
            var upcomingPasses = _upcomingPassRepository.Get()
                .Where(x => x.StartTime > latestPassTime)
                .OrderBy(x => x.StartTime)
                .Take(5);

            var data = passes.Select(x => new SatellitePassViewModel
            {
                ImageDir = x.ImageDir,
                FileKey = x.FileKey,
                StartTime = x.StartTime,
                SatelliteName = x.SatelliteName,
                ChannelA = x.ChannelA,
                ChannelB = x.ChannelB,
                MaxElevation = x.MaxElevation,
                Gain = double.IsNaN(x.Gain) ? -1000 : x.Gain,
                EnhancementTypes = x.EnhancementTypes,
                ThumbnailUri = x.ThumbnailUri,
                ThumbnailEnhancementType = x.ThumbnailEnhancementType,
                IsUpcomingPass = false
            }).Concat(upcomingPasses.Select(x => new SatellitePassViewModel
            {
                StartTime = x.StartTime,
                SatelliteName = x.SatelliteName,
                MaxElevation = x.MaxElevation,
                IsUpcomingPass = true
            }));

            if (sortField == null)
            {
                data = data.OrderByDescending(x => x.StartTime);
            }

            if (sortField == nameof(SatellitePass.StartTime) && sortDir == "asc")
            {
                data = data.OrderBy(x => x.StartTime);
            }
            else if (sortField == nameof(SatellitePass.StartTime) && sortDir == "desc")
            {
                data = data.OrderByDescending(x => x.StartTime);
            }

            if (sortField == nameof(SatellitePass.Gain) && sortDir == "asc")
            {
                data = data.OrderBy(x => x.Gain);
            }
            else if (sortField == nameof(SatellitePass.Gain) && sortDir == "desc")
            {
                data = data.OrderByDescending(x => x.Gain);
            }

            if (sortField == nameof(SatellitePass.MaxElevation) && sortDir == "asc")
            {
                data = data.OrderBy(x => x.MaxElevation);
            }
            else if (sortField == nameof(SatellitePass.MaxElevation) && sortDir == "desc")
            {
                data = data.OrderByDescending(x => x.MaxElevation);
            }

            return new SatellitePassResult
            {
                Page = page,
                PageCount = data.Count() / _pageSize + 1,
                Results = data.Skip(page * _pageSize).Take(_pageSize).ToList()
            };
        }
    }
}
