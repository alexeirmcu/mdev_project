using MediatR;

namespace SmartTripPlanner.ApplicationServices.Commands;

public record DeleteTrip(Guid TripId) : IRequest<Unit>;
