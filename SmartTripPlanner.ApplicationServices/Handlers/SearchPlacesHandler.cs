using AutoMapper;
using MediatR;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.ApiModels;
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

        #region LocalSearch
        var places = await repository.SearchAsync(sr.Query, sr.CityCode, maxResults);

        if (places.Count > 0)
        {
            var models = mapper.Map<List<PlaceModel>>(places);
            return new SearchPlacesResponse(models.AsReadOnly());
        }
        #endregion

        #region ExternalSearch
        var city = await cityRepo.GetByCodeAsync(sr.CityCode, cancellationToken);
        if (city is null)
            return new SearchPlacesResponse(new List<PlaceModel>().AsReadOnly());

        try
        {
            places = await externalService.SearchPlacesAsync(sr.Query, sr.CityCode, city.Id, maxResults);
        }
        catch (HttpRequestException)
        {
            return new SearchPlacesResponse(new List<PlaceModel>().AsReadOnly());
        }

        if (places.Count == 0)
            return new SearchPlacesResponse(new List<PlaceModel>().AsReadOnly());
        #endregion

        #region SaveToLocalDatabase
        // Paso C: guardar los resultados en BD local para futuras búsquedas (upsert)
        await repository.UpsertRangeAsync(places);
        await repository.UnitOfWork.SaveChangesAsync(cancellationToken);
        #endregion

        var resultModels = mapper.Map<List<PlaceModel>>(places);
        return new SearchPlacesResponse(resultModels.AsReadOnly());
    }
}
