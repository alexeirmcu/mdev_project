using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Exceptions;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.ApplicationServices.Handlers;

public class RegenerateDayHandler(
    ITripRepository tripRepository,
    IPlaceRepository placeRepository,
    IWeatherProvider weatherProvider,
    IItineraryReplanningEngine replanningEngine,
    IMapper mapper,
    ILogger<RegenerateDayHandler> logger,
    IUserContext userContext)
    : IRequestHandler<RegenerateDay, TripPlanResponse>
{
    public async Task<TripPlanResponse> Handle(RegenerateDay request, CancellationToken ct)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, ct);
        if (trip is null)
            throw new TripNotFoundException(request.TripId);

        if (trip.OwnerUserId != userContext.UserId)
            throw new TripForbiddenException(request.TripId, userContext.UserId);

        if (trip.Days.Count == 0)
            throw new BusinessRuleException("Itinerary not generated");

        if (request.DayIndex < 0 || request.DayIndex >= trip.Days.Count)
            throw new DayNotFoundException(request.TripId, request.DayIndex);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (trip.Days[request.DayIndex].Date < today)
            throw new BusinessRuleException(
                $"Cannot regenerate day {request.DayIndex}: the day ({trip.Days[request.DayIndex].Date}) is in the past.");

        var candidates = await placeRepository.GetManyByCityIdAsync(trip.CityId, null, ct);

        var weather = await weatherProvider.GetWeatherAsync(
            trip.CityId, trip.StartDate, trip.EndDate, ct);

        await replanningEngine.RegenerateDayAsync(trip, request.DayIndex, candidates, weather, ct);

        await tripRepository.UpdateAsync(trip, ct);

        logger.LogInformation("Trip {TripId} day {DayIndex} regenerated", request.TripId, request.DayIndex);

        return mapper.Map<TripPlanResponse>(trip, opts => opts.Items["City"] = trip.City);
    }
}
