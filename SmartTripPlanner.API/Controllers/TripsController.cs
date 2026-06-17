using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.ApiModels;

namespace SmartTripPlanner.API.Controllers;

[ApiController]
[Route("api/trips")]
public class TripsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TripsController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Creates a new trip with the specified parameters and must-see places.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TripPlanResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateTrip(
        [FromBody] TripGenerationRequest request,
        CancellationToken ct)
    {
        var command = new GenerateTrip(request);
        var response = await _mediator.Send(command, ct);

        return CreatedAtAction(nameof(GetTrip), new { tripId = response.TripId }, response);
    }

    /// <summary>
    /// Updates an existing trip. Restrictions apply based on trip status.
    /// </summary>
    [HttpPatch("{tripId:guid}")]
    [ProducesResponseType(typeof(TripPlanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateTrip(
        Guid tripId,
        [FromBody] TripUpdateRequest request,
        CancellationToken ct)
    {
        var command = new UpdateTrip(tripId, request);
        var response = await _mediator.Send(command, ct);

        return Ok(response);
    }

    /// <summary>
    /// Gets a trip by its TripId. Placeholder for future use.
    /// </summary>
    [HttpGet("{tripId:guid}")]
    [ProducesResponseType(typeof(TripPlanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTrip(
        Guid tripId,
        CancellationToken ct)
    {
        // This is a placeholder endpoint referenced by CreatedAtAction.
        // Full implementation will be added in a future flow.
        return NotFound(new { message = $"Trip with id '{tripId}' not found." });
    }
}
