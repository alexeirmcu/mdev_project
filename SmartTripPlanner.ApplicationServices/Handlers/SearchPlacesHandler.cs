using AutoMapper;
using MediatR;
using SmartTripPlanner.ApplicationServices.Commands;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Domain.Repository;

namespace SmartTripPlanner.ApplicationServices.Handlers;

public class SearchPlacesHandler(IPlaceRepository repository, IPlaceExternalService externalService, IMapper mapper)
    : IRequestHandler<SearchPlacesRequest, SearchPlacesResponse>
{
    public async Task<SearchPlacesResponse> Handle(
        SearchPlacesRequest request, CancellationToken cancellationToken)
    {
        var sr = request.SearchRequest;
        var maxResults = sr.MaxResults ?? request.DefaultMaxResults;

        // Paso A: buscar en BD local
        var places = await repository.SearchAsync(sr.Query, sr.CityCode, maxResults);

        if (places.Count > 0)
        {
            var models = mapper.Map<List<PlaceModel>>(places);
            return new SearchPlacesResponse(models.AsReadOnly());
        }

        // Paso B: si no hay datos locales, llamar al servicio externo
        try
        {
            places = await externalService.SearchPlacesAsync(sr.Query, sr.CityCode, maxResults);
        }
        catch (HttpRequestException)
        {
            return new SearchPlacesResponse(new List<PlaceModel>().AsReadOnly());
        }

        if (places.Count == 0)
            return new SearchPlacesResponse(new List<PlaceModel>().AsReadOnly());

        // Paso C: guardar los resultados en BD local para futuras búsquedas
        await repository.AddRangeAsync(places);
        await repository.UnitOfWork.SaveChangesAsync(cancellationToken);

        var resultModels = mapper.Map<List<PlaceModel>>(places);
        return new SearchPlacesResponse(resultModels.AsReadOnly());
    }
}
