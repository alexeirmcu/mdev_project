using MediatR;
using SmartTripPlanner.Domain.ApiModels;

namespace SmartTripPlanner.ApplicationServices.Commands;

public record ToggleActivityCompletion(Guid TripId, int DayIndex, ActivityCompletionRequest Request, string UserId) : IRequest<ActivityCompletionResponse>;
