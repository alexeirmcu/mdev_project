using MediatR;
using SmartTripPlanner.Domain.ApiModels;

namespace SmartTripPlanner.ApplicationServices.Commands;

public record ListTrips(string? CityCode, DateOnly? StartDate, DateOnly? EndDate)
    : IRequest<List<TripSummaryResponse>>;
