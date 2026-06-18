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

        EnforceStatusRestrictions(trip, payload);

        if (payload.MustSeesToAdd is not null && payload.MustSeesToAdd.Count > 0)
            await AddMustSeesAsync(trip, payload.MustSeesToAdd, ct);

        if (payload.MustSeesToRemove is not null && payload.MustSeesToRemove.Count > 0)
            RemoveMustSees(trip, payload.MustSeesToRemove);

        await tripRepository.UpdateAsync(trip, ct);

        var city = await cityRepository.GetByIdAsync(trip.CityId, ct);

        var response = MapResponse(trip, city);

        logger.LogInformation("Trip {TripId} updated", trip.TripId);

        return response;
    }

    private static void EnforceStatusRestrictions(Trip trip, TripUpdateRequest payload)
    {
        if (trip.Status == TripStatus.GENERATED)
        {
            if (payload.StartDate.HasValue || payload.EndDate.HasValue ||
                payload.BaseHotel is not null || payload.DefaultStartHour is not null)
                throw new BusinessRuleException(
                    "Cannot modify StartDate, EndDate, BaseHotel, or DefaultStartHour when trip status is GENERATED");
        }
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

    private TripPlanResponse MapResponse(Trip trip, City? city)
    {
        return mapper.Map<TripPlanResponse>(trip, opts => opts.Items["City"] = city);
    }
}
