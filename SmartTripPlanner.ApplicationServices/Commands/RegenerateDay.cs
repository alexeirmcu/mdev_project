using MediatR;
using SmartTripPlanner.Domain.ApiModels;

namespace SmartTripPlanner.ApplicationServices.Commands;

public record RegenerateDay(Guid TripId, int DayIndex, string UserId) : IRequest<TripPlanResponse>;
