using MediatR;

namespace SmartTripPlanner.ApplicationServices.Commands;

public record SearchPlacesRequest(string? Query, string CityId, int MaxResults = 20)
    : IRequest<SearchPlacesResponse>;
