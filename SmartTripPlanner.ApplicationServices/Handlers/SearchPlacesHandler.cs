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

        
        var filter = new PlaceSearchFilter(
            sr.Category,
            sr.IsIndoor,
            sr.IsFamilyFriendly,
            sr.MaxDurationMinutes);

        // Step 1: Local search
        var localPlaces = await repository.SearchAsync(sr.Query, sr.CityCode, maxResults, filter);

        // Step 2: If local results are sufficient OR fallback disabled, return local
        if (localPlaces.Count >= maxResults || !request.FetchFromExternalIfInsufficient)
            return MapResponse(localPlaces);

        // Step 3: Category resolution for external fallback
        List<string>? fsqCategoryIds = null;
        if (!string.IsNullOrEmpty(sr.Category))
        {
            var providerId = await repository.GetProviderIdForCategoryAsync(sr.Category, cancellationToken);
            if (!string.IsNullOrEmpty(providerId))
                fsqCategoryIds = [providerId];
            // Cold start: no ProviderId found → skip external call, return local
        }

        // Step 4: Skip external if cold start (category set but no provider IDs)
        if (!string.IsNullOrEmpty(sr.Category) && fsqCategoryIds is null or { Count: 0 })
            return MapResponse(localPlaces);

        // Step 5: External search
        var city = await cityRepo.GetByCodeAsync(sr.CityCode, cancellationToken);
        if (city is null)
            return MapResponse(localPlaces);

        var externalPlaces = await FetchExternalAsync(
            sr.Query, sr.CityCode, city.Id, maxResults, filter, fsqCategoryIds, cancellationToken);

        if (externalPlaces.Count == 0)
            return MapResponse(localPlaces);

        // Step 6: Dedup merge — combine local + external by ProviderReferenceId
        var merged = MergePlaces(localPlaces, externalPlaces);

        // Step 7: Persist merged results
        await repository.UpsertRangeAsync(merged);
        await repository.UnitOfWork.SaveChangesAsync(cancellationToken);

        // Step 8: Return merged results mapped
        return MapResponse(merged);
    }

    private static List<Place> MergePlaces(List<Place> localPlaces, List<Place> externalPlaces)
    {
        var localByRefId = localPlaces
            .Where(p => !string.IsNullOrEmpty(p.ProviderReferenceId))
            .ToDictionary(p => p.ProviderReferenceId, p => p);

        var merged = new List<Place>(localPlaces);

        foreach (var external in externalPlaces)
        {
            if (string.IsNullOrEmpty(external.ProviderReferenceId))
            {
                merged.Add(external);
                continue;
            }

            if (localByRefId.TryGetValue(external.ProviderReferenceId, out var local))
            {
                // Apply external basic fields to local, preserving enrichment
                ApplyExternalFields(local, external);
                // local is already in merged list — in-place update
            }
            else
            {
                merged.Add(external);
            }
        }

        return merged;
    }

    private static void ApplyExternalFields(Place local, Place external)
    {
        // External wins for basic fields
        // Enrichment fields (FamilyFriendlyScore, Popularity, IsEnriched) are PRESERVED
        // by not calling MarkEnriched or any enrichment setter
        try
        {
            // Use the existing UpdateFromExternalProvider which handles basic field updates
            // while preserving enrichment (it doesn't touch FamilyFriendlyScore, Popularity, IsEnriched)
            // However, it uses the external's TypicalDurationMinutes, IsIndoor, IsFamilyFriendly
            // We need to preserve those from local if enrichment is set
            var preserveDuration = local.IsEnriched ? local.TypicalDurationMinutes : external.TypicalDurationMinutes;
            var preserveIsIndoor = local.IsEnriched ? local.IsIndoor : external.IsIndoor;
            var preserveIsFamilyFriendly = local.IsEnriched ? local.IsFamilyFriendly : external.IsFamilyFriendly;

            // We can't easily call UpdateFromExternalProvider with modified params,
            // so we directly set the fields
            // Name, Location always from external
            // But we need a way to update these... let's use reflection or a helper

            // Actually, let's just let UpdateFromExternalProvider do its thing
            // The enrichment fields (FamilyFriendlyScore, Popularity, IsEnriched) are not touched by it
            // but TypicalDurationMinutes, IsIndoor, IsFamilyFriendly ARE overwritten by design
            local.UpdateFromExternalProvider(
                external.Name,
                external.Location,
                preserveDuration,
                preserveIsIndoor,
                preserveIsFamilyFriendly,
                external.Attributes);
        }
        catch (InvalidOperationException)
        {
            // If auto-update is disabled, skip silently
        }
    }

    private async Task<List<Place>> FetchExternalAsync(
        string? query, string cityCode, long cityId, int maxResults,
        PlaceSearchFilter filter, List<string>? fsqCategoryIds, CancellationToken ct)
    {
        try
        {
            var places = await externalService.SearchPlacesAsync(
                query ?? string.Empty, cityCode, cityId, maxResults, filter, fsqCategoryIds);
            return places;
        }
        catch (HttpRequestException)
        {
            return new List<Place>();
        }
    }

    private SearchPlacesResponse MapResponse(IReadOnlyList<Place> places)
    {
        if (places.Count == 0)
            return new SearchPlacesResponse(new List<PlaceModel>().AsReadOnly());

        var models = mapper.Map<List<PlaceModel>>(places);
        return new SearchPlacesResponse(models.AsReadOnly());
    }
}
