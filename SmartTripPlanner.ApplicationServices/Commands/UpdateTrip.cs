using MediatR;
using SmartTripPlanner.Domain.ApiModels;

namespace SmartTripPlanner.ApplicationServices.Commands;

public record UpdateTrip(Guid TripId, TripUpdateRequest Payload) : IRequest<TripPlanResponse>;
