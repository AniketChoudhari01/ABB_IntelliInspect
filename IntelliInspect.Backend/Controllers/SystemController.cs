using Microsoft.AspNetCore.Mvc;

namespace IntelliInspect.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SystemController : ControllerBase
    {
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok("API is alive");
        }
    }
}

