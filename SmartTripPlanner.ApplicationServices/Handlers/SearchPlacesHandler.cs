using AutoMapper;
using MediatR;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.AggregatesModel;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.ApplicationServices.Handlers;

public class SearchPlacesHandler(
    IPlaceRepository repository,
    IPlaceExternalService externalService,
    ICityRepository cityRepo,
    IMapper mapper)
    : IRequestHandler<SearchPlacesRequest, SearchPlacesResponse>
{
    public async Task<SearchPlacesResponse> Handle(
        SearchPlacesRequest request, CancellationToken cancellationToken)
    {
        var sr = request.SearchRequest;
        var maxResults = sr.MaxResults ?? request.DefaultMaxResults;

        var places = await SearchLocalAsync(sr.Query, sr.CityCode, maxResults);
        if (places.Count > 0)
            return MapResponse(places);

        var city = await cityRepo.GetByCodeAsync(sr.CityCode, cancellationToken);
        if (city is null)
            return MapResponse(new List<Place>().AsReadOnly());

        var externalPlaces = await SearchExternalAsync(sr.Query, sr.CityCode, city.Id, maxResults, cancellationToken);
        if (externalPlaces is null || externalPlaces.Count == 0)
            return MapResponse(new List<Place>().AsReadOnly());

        await PersistResultsAsync(externalPlaces.ToList(), cancellationToken);
        return MapResponse(externalPlaces);
    }

    private async Task<IReadOnlyList<Place>> SearchLocalAsync(string query, string cityCode, int maxResults)
    {
        return await repository.SearchAsync(query, cityCode, maxResults);
    }

    private async Task<IReadOnlyList<Place>?> SearchExternalAsync(
        string query, string cityCode, long cityId, int maxResults, CancellationToken ct)
    {
        try
        {
            var places = await externalService.SearchPlacesAsync(query, cityCode, cityId, maxResults);
            return places.Count == 0 ? null : places;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task PersistResultsAsync(List<Place> places, CancellationToken ct)
    {
        await repository.UpsertRangeAsync(places);
        await repository.UnitOfWork.SaveChangesAsync(ct);
    }

    private SearchPlacesResponse MapResponse(IReadOnlyList<Place> places)
    {
        if (places.Count == 0)
            return new SearchPlacesResponse(new List<PlaceModel>().AsReadOnly());

        var models = mapper.Map<List<PlaceModel>>(places);
        return new SearchPlacesResponse(models.AsReadOnly());
    }
}
