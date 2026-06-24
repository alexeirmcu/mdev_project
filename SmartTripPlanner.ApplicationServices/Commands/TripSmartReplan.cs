using MediatR;
using SmartTripPlanner.Domain.ApiModels;

namespace SmartTripPlanner.ApplicationServices.Commands;

public record TripSmartReplan(Guid TripId, TripSmartReplanRequest Request, string UserId) : IRequest<TripPlanResponse>;
