using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Enums;
using SmartTripPlanner.Domain.Exceptions;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.ApplicationServices.Handlers;

public class UpdateTripHandler(
    ITripRepository tripRepository,
    ICityRepository cityRepository,
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

        // Enforce status-based restrictions
        if (trip.Status == TripStatus.GENERATED)
        {
            if (payload.StartDate.HasValue || payload.EndDate.HasValue ||
                payload.BaseHotel is not null || payload.DefaultStartHour is not null)
                throw new BusinessRuleException(
                    "Cannot modify StartDate, EndDate, BaseHotel, or DefaultStartHour when trip status is GENERATED");
        }

        // Apply updates
        // Note: For MVP, we only support basic updates. Full DayPlan-aware MustSee
        // removal is deferred to Flow 2.
        if (payload.MustSeesToAdd is not null && payload.MustSeesToAdd.Count > 0)
        {
            var newPlaceIds = payload.MustSeesToAdd.Select(m => m.PlaceId).ToList();
            var existingPlaces = await placeRepository.GetManyByIdsAsync(newPlaceIds, ct);
            var existingIdSet = existingPlaces.Select(p => p.Id).ToHashSet();
            var missingIds = newPlaceIds.Where(id => !existingIdSet.Contains(id)).ToList();

            if (missingIds.Any())
                throw new BusinessRuleException(
                    $"Some Must-See places were not found: {string.Join(", ", missingIds)}",
                    missingIds.Cast<object>().ToList().AsReadOnly());

            foreach (var mustSeeInput in payload.MustSeesToAdd)
            {
                trip.AddMustSee(new MustSee(
                    mustSeeInput.PlaceId,
                    mustSeeInput.Priority,
                    mustSeeInput.PinnedDayIndex,
                    mustSeeInput.PinnedBlock));
            }
        }

        if (payload.MustSeesToRemove is not null && payload.MustSeesToRemove.Count > 0)
        {
            foreach (var placeId in payload.MustSeesToRemove)
            {
                if (!trip.RemoveMustSee(placeId))
                    throw new BusinessRuleException(
                        $"Cannot remove must-see with PlaceId {placeId} because it is not in the trip's MustSees list");
            }
        }

        await tripRepository.UpdateAsync(trip, ct);

        // Reload city for response
        var city = await cityRepository.GetByIdAsync(trip.CityId, ct);

        var response = new TripPlanResponse(
            trip.TripId,
            trip.TripCode,
            trip.CityId,
            city?.CityCode ?? string.Empty,
            city?.CityName ?? string.Empty,
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
        );

        logger.LogInformation("Trip {TripId} updated", trip.TripId);

        return response;
    }
}
