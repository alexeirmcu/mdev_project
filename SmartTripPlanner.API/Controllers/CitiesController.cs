using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartTripPlanner.ApplicationServices.Commands;

namespace SmartTripPlanner.API.Controllers;

[ApiController]
[Route("api/cities")]
public class CitiesController(IMediator mediator) : ControllerBase
{
    [HttpGet("{cityCode}/interests")]
    public async Task<IActionResult> GetInterests(string cityCode, CancellationToken ct)
    {
        var query = new GetCityInterests(cityCode);
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }
}
