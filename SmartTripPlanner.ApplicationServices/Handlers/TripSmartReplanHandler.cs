using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Exceptions;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.ApplicationServices.Handlers;

public class TripSmartReplanHandler(
    ITripRepository tripRepository,
    IPlaceRepository placeRepository,
    IWeatherProvider weatherProvider,
    IItineraryReplanningEngine replanningEngine,
    IMapper mapper,
    ILogger<TripSmartReplanHandler> logger,
    IUserContext userContext)
    : IRequestHandler<TripSmartReplan, TripPlanResponse>
{
    public async Task<TripPlanResponse> Handle(TripSmartReplan request, CancellationToken ct)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, ct);
        if (trip is null)
            throw new TripNotFoundException(request.TripId);

        if (trip.OwnerUserId != userContext.UserId)
            throw new TripForbiddenException(request.TripId, userContext.UserId);

        var currentDateTime = request.Request.CurrentDateTime;
        var currentDate = DateOnly.FromDateTime(currentDateTime);
        if (currentDate > trip.EndDate)
            throw new BusinessRuleException("Current time is after the trip end");

        var currentDayIndex = ResolveCurrentDayIndex(currentDate, trip);
        var currentBlock = ResolveCurrentBlock(currentDateTime.Hour);
        var scope = ParseScope(request.Request.Scope);

        var candidates = await placeRepository.GetManyByCityIdAsync(trip.CityId, null, ct);

        var weather = await weatherProvider.GetWeatherAsync(
            trip.CityId, trip.StartDate, trip.EndDate, ct);

        var context = new ReplanContext(
            currentDayIndex,
            currentBlock,
            scope,
            request.Request.CurrentBlockWeather == WeatherCondition.Bad,
            new DateTimeOffset(currentDateTime, TimeSpan.Zero));

        await replanningEngine.ReplanAsync(trip, context, candidates, weather, ct);

        await tripRepository.UpdateAsync(trip, ct);

        logger.LogInformation(
            "Trip {TripId} smart replan: day {DayIndex}, block {Block}, scope {Scope}",
            request.TripId, currentDayIndex, currentBlock, scope);

        return mapper.Map<TripPlanResponse>(trip, opts => opts.Items["City"] = trip.City);
    }

    private static int ResolveCurrentDayIndex(DateOnly currentDate, Trip trip)
    {
        if (currentDate < trip.StartDate)
            return 0;

        var offset = currentDate.DayNumber - trip.StartDate.DayNumber;
        return Math.Min(offset, trip.Days.Count - 1);
    }

    private static BlockType ResolveCurrentBlock(int hour)
    {
        if (hour < 12)
            return BlockType.Morning;
        if (hour < 18)
            return BlockType.Afternoon;
        return BlockType.Evening;
    }

    private static ReplanScope ParseScope(string scope) => scope switch
    {
        "CurrentBlock" => ReplanScope.CurrentBlock,
        "CurrentDay" => ReplanScope.CurrentDay,
        "RemainingTrip" => ReplanScope.RemainingTrip,
        _ => ReplanScope.CurrentDay
    };
}
