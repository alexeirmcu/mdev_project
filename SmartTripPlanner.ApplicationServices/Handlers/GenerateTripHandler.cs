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

        var city = await ValidateCityAsync(payload.CityCode, ct);

        await ValidatePlacesAsync(payload.MustSees.ToList(), ct);

        var tripDuration = payload.EndDate.DayNumber - payload.StartDate.DayNumber + 1;
        if (tripDuration > MaxTripDurationDays)
            throw new BusinessRuleException(
                $"Trip duration ({tripDuration} days) exceeds maximum allowed ({MaxTripDurationDays} days)");

        ValidatePinnedDays(payload.MustSees, tripDuration);

        var tripCode = await tripCodeGenerator.GenerateAsync(city.CityCode, payload.StartDate.Year, ct);

        var trip = CreateTripAggregate(city, payload, tripCode);

        await tripRepository.AddAsync(trip, ct);

        if (payload.GenerateItinerary)
        {
            await GenerateItineraryAsync(trip, city, ct);
        }

        var response = MapResponse(trip, city);

        logger.LogInformation("Trip {TripId} created with code {TripCode} ({DayCount} days)",
            trip.TripId, trip.TripCode, trip.Days.Count);

        return response;
    }

    private async Task<City> ValidateCityAsync(string cityCode, CancellationToken ct)
    {
        var city = await cityRepository.GetByCodeAsync(cityCode, ct);
        if (city is null)
            throw new CityNotFoundException(cityCode);

        if (!city.IsAllowed)
            throw new BusinessRuleException($"City '{cityCode}' is not available for planning");

        return city;
    }

    private async Task ValidatePlacesAsync(List<MustSeeInput> mustSees, CancellationToken ct)
    {
        var placeIds = mustSees.Select(m => m.PlaceId).ToList();
        var existingPlaces = await placeRepository.GetManyByIdsAsync(placeIds, ct);
        var existingIdSet = existingPlaces.Select(p => p.Id).ToHashSet();
        var missingIds = placeIds.Where(id => !existingIdSet.Contains(id)).ToList();

        if (missingIds.Any())
            throw new BusinessRuleException(
                $"Some Must-See places were not found: {string.Join(", ", missingIds)}",
                missingIds.Cast<object>().ToList().AsReadOnly());
    }

    private static void ValidatePinnedDays(IReadOnlyList<MustSeeInput> mustSees, int tripDuration)
    {
        foreach (var mustSee in mustSees)
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
    }

    private Trip CreateTripAggregate(City city, TripGenerationRequest payload, string tripCode)
    {
        var trip = new Trip
        {
            TripId = Guid.NewGuid(),
            TripCode = tripCode,
            CityId = city.Id,
            StartDate = payload.StartDate,
            EndDate = payload.EndDate,
            BaseHotel = mapper.Map<Location>(payload.BaseHotel),
            Travelers = mapper.Map<Travelers>(payload.Travelers),
            Preferences = mapper.Map<TripPreferences>(payload.Preferences),
            DefaultStartTime = TimeOnly.Parse(payload.DefaultStartHour),
            CreatedAt = DateTimeOffset.UtcNow
        };

        foreach (var mustSeeInput in payload.MustSees)
        {
            trip.AddMustSee(mapper.Map<MustSee>(mustSeeInput));
        }

        return trip;
    }

    private async Task GenerateItineraryAsync(Trip trip, City city, CancellationToken ct)
    {
        var candidatePlaces = await placeRepository.GetManyByCityIdAsync(city.Id, ct);
        var weatherData = await weatherProvider.GetWeatherAsync(city.Id, trip.StartDate, trip.EndDate, ct);
        await itineraryGenerator.GenerateAsync(trip, candidatePlaces, weatherData, ct);
        trip.UpdateStatus(TripStatus.GENERATED);
        await tripRepository.UpdateAsync(trip, ct);
    }

    private TripPlanResponse MapResponse(Trip trip, City city)
    {
        return mapper.Map<TripPlanResponse>(trip, opts => opts.Items["City"] = city);
    }
}
