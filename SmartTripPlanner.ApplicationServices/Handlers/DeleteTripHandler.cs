using MediatR;
using Microsoft.Extensions.Logging;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.Exceptions;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.ApplicationServices.Handlers;

public class DeleteTripHandler(
    ITripRepository tripRepository,
    IUserContext userContext,
    ILogger<DeleteTripHandler> logger)
    : IRequestHandler<DeleteTrip, Unit>
{
    public async Task<Unit> Handle(DeleteTrip request, CancellationToken ct)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, ct);
        if (trip is null)
            throw new TripNotFoundException(request.TripId);

        if (trip.OwnerUserId != userContext.UserId)
            throw new TripForbiddenException(request.TripId, userContext.UserId);

        await tripRepository.DeleteAsync(request.TripId, ct);

        logger.LogInformation("Trip {TripId} deleted by user {UserId}", request.TripId, userContext.UserId);

        return Unit.Value;
    }
}
