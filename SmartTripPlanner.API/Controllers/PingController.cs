using Microsoft.AspNetCore.Mvc;

namespace SmartTripPlanner.API.Controllers;

[ApiController]
[Route("[controller]")]
public class PingController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok("pong");
    }
}
