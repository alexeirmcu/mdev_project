using MediatR;
using SmartTripPlanner.Domain.ApiModels;

namespace SmartTripPlanner.ApplicationServices.Commands;

public record GenerateTrip(TripGenerationRequest Payload, string OwnerUserId) : IRequest<TripPlanResponse>;
