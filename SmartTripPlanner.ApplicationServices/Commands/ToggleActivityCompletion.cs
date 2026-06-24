using MediatR;
using SmartTripPlanner.Domain.ApiModels;

namespace SmartTripPlanner.ApplicationServices.Commands;

public record ToggleActivityCompletion(Guid TripId, int DayIndex, long PlaceId, ActivityCompletionRequest Request, string UserId) : IRequest<ActivityCompletionResponse>;
