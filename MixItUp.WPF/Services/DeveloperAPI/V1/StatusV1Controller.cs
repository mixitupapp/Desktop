using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace MixItUp.WPF.Services.DeveloperAPI.V1
{
    [Route("api/status")]
    [ApiController]
    public class StatusV1Controller : ControllerBase
    {
        [Route("version")]
        [HttpGet]
        public IActionResult GetVersion()
        {
            return Ok(Assembly.GetEntryAssembly().GetName().Version.ToString());
        }
    }
}
