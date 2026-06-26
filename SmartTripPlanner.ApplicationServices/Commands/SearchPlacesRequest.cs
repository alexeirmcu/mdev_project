using MediatR;
using SmartTripPlanner.Domain.ApiModels;

namespace SmartTripPlanner.ApplicationServices.Commands;

public record SearchPlacesRequest(PlaceSearchRequest SearchRequest, int DefaultMaxResults = 10)
    : IRequest<SearchPlacesResponse>
{
    public bool FetchFromExternalIfInsufficient =>
        SearchRequest.FetchFromExternalIfInsufficient ?? true;
}
