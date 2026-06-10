using AutoMapper;
using MediatR;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.ApplicationServices.Handlers;

public class SearchPlacesHandler(IPlaceRepository repository, IMapper mapper)
    : IRequestHandler<SearchPlacesRequest, SearchPlacesResponse>
{
    public async Task<SearchPlacesResponse> Handle(
        SearchPlacesRequest request, CancellationToken cancellationToken)
    {
        var places = await repository.SearchAsync(
            request.Query, request.CityId, request.MaxResults);
        var models = mapper.Map<List<PlaceModel>>(places);
        return new SearchPlacesResponse(models.AsReadOnly());
    }
}
