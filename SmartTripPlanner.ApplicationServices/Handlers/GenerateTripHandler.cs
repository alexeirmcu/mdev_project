using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.ApplicationServices.Configurations;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Exceptions;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.ApplicationServices.Handlers;

public class GenerateTripHandler(
    ITripRepository tripRepository,
    ICityRepository cityRepository,
    IPlaceRepository placeRepository,
    ITripCodeGenerator tripCodeGenerator,
    IMapper mapper,
    ILogger<GenerateTripHandler> logger,
    IUserContext userContext)
    : IRequestHandler<GenerateTrip, TripPlanResponse>
{
    private const int MaxTripDurationDays = 14;

    public async Task<TripPlanResponse> Handle(GenerateTrip request, CancellationToken ct)
    {
        if (request.OwnerUserId != userContext.UserId)
            throw new BusinessRuleException("OwnerUserId mismatch: the command owner does not match the authenticated user.");

        var payload = request.Payload;

        var city = await ValidateCityAsync(payload.CityCode, ct);

        Dictionary<long, string> placeNames = new();
        if (payload.MustSees is not null && payload.MustSees.Count > 0)
        {
            placeNames = await ResolvePlaceNamesAsync(payload.MustSees.ToList(), ct);
        }

        var tripDuration = payload.EndDate.DayNumber - payload.StartDate.DayNumber + 1;
        if (tripDuration > MaxTripDurationDays)
            throw new BusinessRuleException(
                $"Trip duration ({tripDuration} days) exceeds maximum allowed ({MaxTripDurationDays} days)");

        if (payload.MustSees is not null)
        {
            ValidatePinnedDays(payload.MustSees, tripDuration);
        }

        var tripCode = await tripCodeGenerator.GenerateAsync(city.CityCode, payload.StartDate.Year, ct);

        var trip = CreateTripAggregate(city, payload, tripCode, request.OwnerUserId, placeNames);

        await tripRepository.AddAsync(trip, ct);

        var response = MapResponse(trip, city);

        logger.LogInformation("Trip {TripId} created with code {TripCode} (status: CREATED, itinerary not generated)",
            trip.TripId, trip.TripCode);

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

    private async Task<Dictionary<long, string>> ResolvePlaceNamesAsync(List<MustSeeInput> mustSees, CancellationToken ct)
    {
        var placeIds = mustSees.Select(m => m.PlaceId).ToList();
        var places = await placeRepository.GetManyByIdsAsync(placeIds, ct);
        var placeNames = places.ToDictionary(p => p.Id, p => p.Name);

        var missingIds = placeIds.Where(id => !placeNames.ContainsKey(id)).ToList();
        if (missingIds.Any())
            throw new BusinessRuleException(
                $"Some Must-See places were not found: {string.Join(", ", missingIds)}",
                missingIds.Cast<object>().ToList().AsReadOnly());

        return placeNames;
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

    private Trip CreateTripAggregate(City city, TripGenerationRequest payload, string tripCode, string ownerUserId, Dictionary<long, string> placeNames)
    {
        var trip = new Trip
        {
            TripId = Guid.NewGuid(),
            TripCode = tripCode,
            CityId = city.Id,
            StartDate = payload.StartDate,
            EndDate = payload.EndDate,
            BaseHotel = payload.BaseHotel is not null ? mapper.Map<Location>(payload.BaseHotel) : null,
            Travelers = payload.Travelers is not null ? mapper.Map<Travelers>(payload.Travelers) : new Travelers(2, 0, 0),
            Preferences = payload.Preferences is not null ? mapper.Map<TripPreferences>(payload.Preferences) : new TripPreferences(),
            DefaultStartTime = TimeOnly.Parse(payload.DefaultStartHour),
            OwnerUserId = ownerUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        if (payload.MustSees is not null)
        {
            foreach (var mustSeeInput in payload.MustSees)
            {
                var mustSee = new MustSee(
                    mustSeeInput.PlaceId,
                    placeNames[mustSeeInput.PlaceId],
                    mustSeeInput.Priority,
                    mustSeeInput.PinnedDayIndex,
                    mustSeeInput.PinnedBlock,
                    mustSeeInput.ForceIncludeDespiteWeather);
                trip.AddMustSee(mustSee);
            }
        }

        return trip;
    }

    private TripPlanResponse MapResponse(Trip trip, City city)
    {
        return mapper.Map<TripPlanResponse>(trip, opts => opts.Items["City"] = city);
    }
}
