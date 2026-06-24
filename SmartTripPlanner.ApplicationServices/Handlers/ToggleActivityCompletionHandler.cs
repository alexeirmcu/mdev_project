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

public class ToggleActivityCompletionHandler(
    ITripRepository tripRepository,
    ILogger<ToggleActivityCompletionHandler> logger,
    IUserContext userContext)
    : IRequestHandler<ToggleActivityCompletion, ActivityCompletionResponse>
{
    public async Task<ActivityCompletionResponse> Handle(ToggleActivityCompletion request, CancellationToken ct)
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

        var day = trip.Days[request.DayIndex];

        if (day.Date > DateOnly.FromDateTime(DateTime.UtcNow) && request.Request.IsCompleted)
            throw new BusinessRuleException("Cannot complete an activity in a future day");

        var activity = FindActivityAcrossBlocks(day, request.Request.PlaceId);
        if (activity is null)
            throw new ActivityNotFoundException(request.Request.PlaceId);

        activity.SetCompleted(request.Request.IsCompleted);

        var completedCount = trip.Days
            .SelectMany(d => new[]
            {
                d.Morning.Activities,
                d.Afternoon.Activities,
                d.Evening.Activities
            }.SelectMany(a => a))
            .Count(a => a.IsCompleted);

        await tripRepository.UpdateAsync(trip, ct);

        logger.LogInformation(
            "Activity {PlaceId} in trip {TripId} completion set to {IsCompleted}",
            request.Request.PlaceId, request.TripId, request.Request.IsCompleted);

        return new ActivityCompletionResponse(
            request.Request.PlaceId,
            request.Request.IsCompleted,
            completedCount);
    }

    private static ActivityNode? FindActivityAcrossBlocks(DayPlan day, long placeId)
    {
        return day.Morning.Activities.FirstOrDefault(a => a.PlaceId == placeId)
            ?? day.Afternoon.Activities.FirstOrDefault(a => a.PlaceId == placeId)
            ?? day.Evening.Activities.FirstOrDefault(a => a.PlaceId == placeId);
    }
}
