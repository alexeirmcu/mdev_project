using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.ApplicationServices.Configurations;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Exceptions;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.ApplicationServices.Handlers;

public class GenerateTripHandler(
    ITripRepository tripRepository,
    ICityRepository cityRepository,
    IPlaceRepository placeRepository,
    ITripCodeGenerator tripCodeGenerator,
    IItineraryGenerator itineraryGenerator,
    IWeatherProvider weatherProvider,
    IMapper mapper,
    ILogger<GenerateTripHandler> logger)
    : IRequestHandler<GenerateTrip, TripPlanResponse>
{
    private const int MaxTripDurationDays = 14;

    public async Task<TripPlanResponse> Handle(GenerateTrip request, CancellationToken ct)
    {
        var payload = request.Payload;

        // 1. Validate city exists and is allowed
        var city = await cityRepository.GetByCodeAsync(payload.CityCode, ct);
        if (city is null)
            throw new CityNotFoundException(payload.CityCode);

        if (!city.IsAllowed)
            throw new BusinessRuleException($"City '{payload.CityCode}' is not available for planning");

        // 2. Validate PlaceIds exist
        var placeIds = payload.MustSees.Select(m => m.PlaceId).ToList();
        var existingPlaces = await placeRepository.GetManyByIdsAsync(placeIds, ct);
        var existingIdSet = existingPlaces.Select(p => p.Id).ToHashSet();
        var missingIds = placeIds.Where(id => !existingIdSet.Contains(id)).ToList();

        if (missingIds.Any())
            throw new BusinessRuleException(
                $"Some Must-See places were not found: {string.Join(", ", missingIds)}",
                missingIds.Cast<object>().ToList().AsReadOnly());

        // 3. Validate PinnedDay range
        var tripDuration = payload.EndDate.DayNumber - payload.StartDate.DayNumber + 1;

        if (tripDuration > MaxTripDurationDays)
            throw new BusinessRuleException(
                $"Trip duration ({tripDuration} days) exceeds maximum allowed ({MaxTripDurationDays} days)");

        foreach (var mustSee in payload.MustSees)
        {
            if (mustSee.PinnedBlock.HasValue && !mustSee.PinnedDayIndex.HasValue)
                throw new BusinessRuleException(
                    "PinnedBlock cannot be set without PinnedDayIndex");

            if (mustSee.PinnedDayIndex.HasValue)
            {
                if (mustSee.PinnedDayIndex < 0 || mustSee.PinnedDayIndex >= tripDuration)
                    throw new BusinessRuleException(
                        $"PinnedDayIndex {mustSee.PinnedDayIndex} is out of range [0, {tripDuration - 1}]");
            }
        }

        // 4. Generate TripCode
        var tripCode = await tripCodeGenerator.GenerateAsync(city.CityCode, payload.StartDate.Year, ct);

        // 5. Materialize Trip aggregate
        var trip = new Trip
        {
            TripId = Guid.NewGuid(),
            TripCode = tripCode,
            CityId = city.Id,
            StartDate = payload.StartDate,
            EndDate = payload.EndDate,
            BaseHotel = new Location(
                payload.BaseHotel.Name,
                payload.BaseHotel.Latitude,
                payload.BaseHotel.Longitude),
            Travelers = new Travelers(
                payload.Travelers?.Adults ?? 2,
                payload.Travelers?.Children ?? 0,
                payload.Travelers?.Infants ?? 0),
            Preferences = new TripPreferences(
                payload.Preferences?.CarAvailable ?? false,
                payload.Preferences?.MaxWalkingMinutes ?? 30,
                payload.Preferences?.WeatherAwareEnabled ?? true),
            DefaultStartTime = TimeOnly.Parse(payload.DefaultStartHour),
            CreatedAt = DateTimeOffset.UtcNow
        };

        foreach (var mustSeeInput in payload.MustSees)
        {
            trip.AddMustSee(new MustSee(
                mustSeeInput.PlaceId,
                mustSeeInput.Priority,
                mustSeeInput.PinnedDayIndex,
                mustSeeInput.PinnedBlock));
        }

        // 6. Persist trip
        await tripRepository.AddAsync(trip, ct);

        // 6.5 — Generate itinerary
        var candidatePlaces = await placeRepository.GetManyByCityIdAsync(city.Id, ct);
        var weatherData = await weatherProvider.GetWeatherAsync(city.Id, trip.StartDate, trip.EndDate, ct);
        await itineraryGenerator.GenerateAsync(trip, candidatePlaces, weatherData, ct);
        trip.UpdateStatus(TripStatus.GENERATED);
        await tripRepository.UpdateAsync(trip, ct);

        // 7. Map to response
        var response = new TripPlanResponse(
            trip.TripId,
            trip.TripCode,
            trip.CityId,
            city.CityCode,
            city.CityName,
            trip.StartDate,
            trip.EndDate,
            mapper.Map<LocationModel>(trip.BaseHotel),
            new TravelersInput(trip.Travelers.Adults, trip.Travelers.Children, trip.Travelers.Infants),
            new TripPreferencesInput(trip.Preferences.CarAvailable, trip.Preferences.MaxWalkingMinutes, trip.Preferences.WeatherAwareEnabled),
            trip.OriginalMustSees.Select(m => new MustSeeResponse(
                m.PlaceId,
                m.Priority.ToString(),
                m.PinnedDayIndex,
                m.PinnedBlock?.ToString()
            )).ToList(),
            trip.Status.ToString(),
            trip.DefaultStartTime.ToString("HH:mm")
        )
        {
            Days = trip.Days.Select(MapDayPlan).ToList()
        };

        logger.LogInformation("Trip {TripId} created with code {TripCode} ({DayCount} days)",
            trip.TripId, trip.TripCode, trip.Days.Count);

        return response;
    }

    private static DayPlanResponse MapDayPlan(DayPlan dayPlan)
    {
        return new DayPlanResponse
        {
            DayIndex = dayPlan.DayIndex,
            Date = dayPlan.Date,
            WeatherSummary = dayPlan.WeatherSummary.ToString(),
            Blocks = new List<BlockResponse>
            {
                MapBlock(dayPlan.Morning),
                MapBlock(dayPlan.Afternoon),
                MapBlock(dayPlan.Evening)
            }
        };
    }

    private static BlockResponse MapBlock(BlockTimeline block)
    {
        return new BlockResponse
        {
            BlockType = block.BlockType.ToString(),
            TotalDurationMinutes = block.BlockTotalDurationMinutes,
            Activities = block.Activities.Select((a, i) => MapActivity(a, i)).ToList()
        };
    }

    private static ActivityResponse MapActivity(ActivityNode activity, int sequenceOrder)
    {
        return new ActivityResponse
        {
            PlaceId = activity.PlaceId,
            PlaceName = activity.Name,
            DurationMinutes = activity.DurationMinutes,
            SequenceOrder = sequenceOrder,
            IsIndoor = activity.IsIndoor,
            Priority = activity.Priority.ToString(),
            TransportMode = activity.TransitToNext?.TransportMode.ToString() ?? string.Empty,
            TransitDurationMinutes = activity.TransitToNext?.DurationMinutes ?? 0,
            BufferMinutes = activity.TransitToNext?.BufferMinutes ?? 0,
            FrictionAlert = activity.TransitToNext?.FrictionAlert ?? false
        };
    }
}
