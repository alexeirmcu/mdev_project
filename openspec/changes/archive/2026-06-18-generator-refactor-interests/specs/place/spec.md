# Delta for Place

## MODIFIED Requirements

### Requirement: FR4: IPlaceRepository

```csharp
namespace SmartTripPlanner.Domain.Repository;

public interface IPlaceRepository : IRepository<Place>
{
    Task<List<Place>> SearchAsync(string query, string cityCode, int maxResults = 20);
    Task<Place?> GetByProviderReferenceIdAsync(string providerReferenceId);
    Task<IEnumerable<Place>> GetManyByIdsAsync(IEnumerable<long> placeIds, CancellationToken ct);
    Task<List<Place>> GetManyByCityIdAsync(long cityId, CancellationToken ct = default);
    Task<List<Place>> GetCandidatesByCityAndInterestsAsync(long cityId, IReadOnlyList<string> interests, CancellationToken ct = default);
    Task<List<string>> GetDistinctAttributeValuesByCityCodeAsync(string cityCode, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<Place> places);
    Task UpsertRangeAsync(IEnumerable<Place> places);
}
```

(Previously: `IPlaceRepository` had no `GetCandidatesByCityAndInterestsAsync` or `GetDistinctAttributeValuesByCityCodeAsync` methods.)

#### Scenario: Cascade search uses port instead of direct Foursquare dependency

- GIVEN a `PlaceRepository` with `IPlaceExternalService` injected
- WHEN `SearchAsync` returns no local results
- THEN the repository calls `IPlaceExternalService.SearchPlacesAsync` (not `IFoursquareApiClient`)
- AND the external results are returned as `Place` entities

#### Scenario: Cascade returns local results without calling external service

- GIVEN a `PlaceRepository` with `IPlaceExternalService` injected
- WHEN `SearchAsync` finds local results matching the query
- THEN it returns the local results
- AND `IPlaceExternalService.SearchPlacesAsync` is NOT called

#### Scenario: Search matches attribute value

- GIVEN a Place with Name="Gran Palace" and Attribute("foursquare","category","Hotel") in city "MAD"
- WHEN SearchAsync("hotel", "MAD") is called
- THEN the Place is returned

#### Scenario: Interest-filtered candidate retrieval returns matching places

- GIVEN a city with places having attributes "museum", "history", "food"
- WHEN `GetCandidatesByCityAndInterestsAsync(cityId, ["museum", "food"])` is called
- THEN only places with at least one attribute matching any interest are returned
- AND matching is inclusive (place matches if ANY of its attributes matches ANY interest)

#### Scenario: Interest filtering falls back gracefully

- GIVEN a city with places having no attributes matching ["underwater"]
- WHEN `GetCandidatesByCityAndInterestsAsync(cityId, ["underwater"])` is called
- THEN an empty list is returned (not an exception)

#### Scenario: Distinct attribute values per city code

- GIVEN a city with places having attributes "museum", "museum", "history", "food"
- WHEN `GetDistinctAttributeValuesByCityCodeAsync("madrid-es")` is called
- THEN the result is ["museum", "history", "food"] (distinct, no duplicates)

### Requirement: FR5: PlaceRepository (Infrastructure)

EF Core implementation in `SmartTripPlanner.Infrastructure/Repositories/PlaceRepository.cs`. The `GetCandidatesByCityAndInterestsAsync` method MUST filter candidates in SQL using EF Core translation. The `GetDistinctAttributeValuesByCityCodeAsync` method MUST query distinct attribute values in SQL.

(Previously: PlaceRepository had no interest-filtered query or distinct-attribute-values query.)

#### Scenario: Interest filtering is performed in SQL, not in-memory

- GIVEN PlaceRepository with `GetCandidatesByCityAndInterestsAsync`
- WHEN the method is called with interests ["museum"]
- THEN the generated SQL includes a WHERE clause filtering on PlaceAttribute.Value
- AND no in-memory `.Where()` is applied after materialization

#### Scenario: Distinct attribute values query is performed in SQL

- GIVEN PlaceRepository with `GetDistinctAttributeValuesByCityCodeAsync`
- WHEN the method is called with cityCode "madrid-es"
- THEN the generated SQL includes SELECT DISTINCT on PlaceAttribute.Value
- AND no in-memory `.Distinct()` is applied after full table materialization