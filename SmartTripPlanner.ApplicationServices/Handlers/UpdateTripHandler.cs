using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Exceptions;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.ApplicationServices.Handlers;

public class UpdateTripHandler(
    ITripRepository tripRepository,
    IPlaceRepository placeRepository,
    IMapper mapper,
    ILogger<UpdateTripHandler> logger)
    : IRequestHandler<UpdateTrip, TripPlanResponse>
{
    public async Task<TripPlanResponse> Handle(UpdateTrip request, CancellationToken ct)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, ct);
        if (trip is null)
            throw new TripNotFoundException(request.TripId);

        var payload = request.Payload;

        // Block modifications if trip has already started
        if (trip.StartDate < DateOnly.FromDateTime(DateTime.UtcNow))
            throw new BusinessRuleException("Cannot modify a trip that has already started.");

        bool anyModification = false;

        // Apply partial updates
        if (payload.StartDate.HasValue || payload.EndDate.HasValue)
        {
            trip.UpdateDates(
                payload.StartDate ?? trip.StartDate,
                payload.EndDate ?? trip.EndDate);
            anyModification = true;
        }

        if (payload.BaseHotel is not null)
        {
            trip.UpdateBaseHotel(mapper.Map<Location>(payload.BaseHotel));
            anyModification = true;
        }

        if (payload.Travelers is not null)
        {
            trip.UpdateTravelers(mapper.Map<Travelers>(payload.Travelers));
            anyModification = true;
        }

        if (payload.Preferences is not null)
        {
            trip.UpdatePreferences(mapper.Map<TripPreferences>(payload.Preferences));
            anyModification = true;
        }

        if (payload.DefaultStartHour is not null)
        {
            trip.UpdateDefaultStartTime(TimeOnly.Parse(payload.DefaultStartHour));
            anyModification = true;
        }

        // Apply must-see modifications
        if (payload.MustSeesToAdd is not null && payload.MustSeesToAdd.Count > 0)
        {
            await AddMustSeesAsync(trip, payload.MustSeesToAdd, ct);
            anyModification = true;
        }

        if (payload.MustSeesToRemove is not null && payload.MustSeesToRemove.Count > 0)
        {
            RemoveMustSees(trip, payload.MustSeesToRemove);
            anyModification = true;
        }

        // If the trip was GENERATED and any modification was applied, invalidate itinerary
        if (anyModification && trip.Days.Any())
        {
            trip.ClearDaysAndReset();
        }

        await tripRepository.UpdateAsync(trip, ct);

        var response = MapResponse(trip);

        logger.LogInformation("Trip {TripId} updated", trip.TripId);

        return response;
    }

    private async Task AddMustSeesAsync(Trip trip, List<MustSeeInput> mustSees, CancellationToken ct)
    {
        var newPlaceIds = mustSees.Select(m => m.PlaceId).ToList();
        var existingPlaces = await placeRepository.GetManyByIdsAsync(newPlaceIds, ct);
        var existingIdSet = existingPlaces.Select(p => p.Id).ToHashSet();
        var missingIds = newPlaceIds.Where(id => !existingIdSet.Contains(id)).ToList();

        if (missingIds.Any())
            throw new BusinessRuleException(
                $"Some Must-See places were not found: {string.Join(", ", missingIds)}",
                missingIds.Cast<object>().ToList().AsReadOnly());

        foreach (var mustSeeInput in mustSees)
        {
            trip.AddMustSee(mapper.Map<MustSee>(mustSeeInput));
        }
    }

    private static void RemoveMustSees(Trip trip, List<long> placeIds)
    {
        foreach (var placeId in placeIds)
        {
            if (!trip.RemoveMustSee(placeId))
                throw new BusinessRuleException(
                    $"Cannot remove must-see with PlaceId {placeId} because it is not in the trip's MustSees list");
        }
    }

    private TripPlanResponse MapResponse(Trip trip)
    {
        return mapper.Map<TripPlanResponse>(trip, opts => opts.Items["City"] = trip.City);
    }
}
