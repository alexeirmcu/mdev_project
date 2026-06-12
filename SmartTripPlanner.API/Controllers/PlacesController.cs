using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.ApiModels;

namespace SmartTripPlanner.API.Controllers;

[ApiController]
[Route("trips/places")]
public class PlacesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PlacesController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search(
        [FromBody] PlaceSearchRequest request)
    {
        try
        {
            var mediatRRequest = new SearchPlacesRequest(request);

            var response = await _mediator.Send(mediatRRequest);

            return Ok(response.Results.ToList());
        }
        catch (HttpRequestException)
        {
            return UnprocessableEntity(new List<ValidationResult>
            {
                new(ErrorCode.EXTERNAL_SERVICE_FAILURE,
                    "Unable to search places at this time. Please try again later.")
            });
        }
    }
}
