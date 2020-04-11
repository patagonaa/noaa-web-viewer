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

        public SatellitePassController(ILogger<SatellitePassController> logger, ISatellitePassRepository passRepository)
        {
            _logger = logger;
            _passRepository = passRepository;
        }

        [HttpGet]
        public SatellitePassResult Get(string sortField, string sortDir, int page = 0)
        {
            var data = _passRepository.Get();

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
