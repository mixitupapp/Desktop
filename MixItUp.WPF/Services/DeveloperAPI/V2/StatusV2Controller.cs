using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace MixItUp.WPF.Services.DeveloperAPI.V2
{
    [Route("api/v2/status")]
    [ApiController]
    public class StatusV2Controller : ControllerBase
    {
        [Route("version")]
        [HttpGet]
        public IActionResult GetVersion()
        {
            return Ok(Assembly.GetEntryAssembly().GetName().Version.ToString());
        }
    }
}