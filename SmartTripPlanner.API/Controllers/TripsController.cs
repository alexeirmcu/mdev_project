using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Ports;

namespace SmartTripPlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/trips")]
public class TripsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContext _userContext;

    public TripsController(IMediator mediator, IUserContext userContext)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
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
        var command = new GenerateTrip(request, _userContext.UserId);
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
    /// Generates or regenerates the itinerary for a trip.
    /// </summary>
    [HttpPost("{tripId:guid}/generate")]
    [ProducesResponseType(typeof(TripPlanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GenerateTripItinerary(Guid tripId, CancellationToken ct)
    {
        var command = new GenerateTripItinerary(tripId);
        var response = await _mediator.Send(command, ct);
        return Ok(response);
    }

    /// <summary>
    /// Gets a trip by its TripId.
    /// </summary>
    [HttpGet("{tripId:guid}")]
    [ProducesResponseType(typeof(TripPlanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTrip(
        Guid tripId,
        CancellationToken ct)
    {
        var query = new GetTrip(tripId);
        var response = await _mediator.Send(query, ct);
        return Ok(response);
    }

    /// <summary>
    /// Lists all trips for the authenticated user, with optional filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<TripSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ListTrips(
        [FromQuery] string? cityCode,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        CancellationToken ct)
    {
        var query = new ListTrips(cityCode, startDate, endDate);
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Deletes a trip by its TripId. Only the owner can delete.
    /// </summary>
    [HttpDelete("{tripId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteTrip(
        Guid tripId,
        CancellationToken ct)
    {
        var command = new DeleteTrip(tripId);
        await _mediator.Send(command, ct);
        return NoContent();
    }

    /// <summary>
    /// Refreshes weather data for all days in a trip and marks stale days where weather changed.
    /// </summary>
    [HttpPost("{tripId:guid}/weather-refresh")]
    [ProducesResponseType(typeof(WeatherRefreshResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RefreshWeather(
        Guid tripId,
        CancellationToken ct)
    {
        var command = new RefreshWeather(tripId, _userContext.UserId);
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Regenerates activities for a specific day using the replanning engine.
    /// </summary>
    [HttpPost("{tripId:guid}/regenerate-day")]
    [ProducesResponseType(typeof(TripPlanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RegenerateDay(
        Guid tripId,
        [FromBody] RegenerateDayRequest request,
        CancellationToken ct)
    {
        var command = new RegenerateDay(tripId, request.DayIndex, _userContext.UserId);
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Smart replan from the current point forward with scope and weather awareness.
    /// </summary>
    [HttpPost("{tripId:guid}/replan")]
    [ProducesResponseType(typeof(TripPlanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> TripSmartReplan(
        Guid tripId,
        [FromBody] TripSmartReplanRequest request,
        CancellationToken ct)
    {
        var command = new TripSmartReplan(tripId, request, _userContext.UserId);
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Toggles the completion status of an activity within a specific day.
    /// </summary>
    [HttpPatch("{tripId:guid}/days/{dayIndex:int}/completion")]
    [ProducesResponseType(typeof(ActivityCompletionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ToggleActivityCompletion(
        Guid tripId,
        int dayIndex,
        [FromBody] ActivityCompletionRequest request,
        CancellationToken ct)
    {
        var command = new ToggleActivityCompletion(tripId, dayIndex, request, _userContext.UserId);
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }
}
