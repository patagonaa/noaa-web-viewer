using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NoaaWeb.Data;

namespace NoaaWeb.App.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SatellitePassController : ControllerBase
    {
        private readonly ILogger<SatellitePassController> _logger;
        private readonly ISatellitePassRepository _passRepository;

        public SatellitePassController(ILogger<SatellitePassController> logger, ISatellitePassRepository passRepository)
        {
            _logger = logger;
            _passRepository = passRepository;
        }

        [HttpGet]
        public IEnumerable<SatellitePass> Get()
        {
            return _passRepository.Get();
        }
    }
}
