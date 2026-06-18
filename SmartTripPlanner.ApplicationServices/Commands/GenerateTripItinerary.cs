using MediatR;
using SmartTripPlanner.Domain.ApiModels;

namespace SmartTripPlanner.ApplicationServices.Commands;

public record GenerateTripItinerary(Guid TripId) : IRequest<TripPlanResponse>;
