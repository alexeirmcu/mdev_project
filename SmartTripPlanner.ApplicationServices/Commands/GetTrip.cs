using MediatR;
using SmartTripPlanner.Domain.ApiModels;

namespace SmartTripPlanner.ApplicationServices.Commands;

public record GetTrip(Guid TripId) : IRequest<TripPlanResponse>;
