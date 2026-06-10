# Delta for place — External Service Abstraction

## ADDED Requirements

### ADDED Requirement: IPlaceExternalService (Port)

The Domain layer MUST define `IPlaceExternalService` as a port for searching places in external providers. The interface SHALL return domain entities (`Place`), not DTOs.

```csharp
namespace SmartTripPlanner.Domain.Repository;

public interface IPlaceExternalService
{
    Task<List<Place>> SearchPlacesAsync(string query, string cityId, int maxResults = 20);
}
```

(Previously: `PlaceRepository` depended directly on `IFoursquareApiClient`, an Infrastructure-level interface.)

#### Scenario: Port abstracts external provider lookup

- GIVEN a query, cityId, and maxResults
- WHEN `SearchPlacesAsync` is called
- THEN it returns a `List<Place>` with mapped results from the external provider

### ADDED Requirement: FoursquarePlaceService (Adapter)

The Infrastructure layer MUST implement `IPlaceExternalService` via a `FoursquarePlaceService` class that:
- Wraps `IFoursquareApiClient` internally
- Maps `FoursquarePlace` → `Place` domain entity using `FoursquareCategoryHeuristics`
- Is registered via DI as `IPlaceExternalService`

(Previously: Foursquare-to-Place mapping lived in `PlaceRepository` directly.)

#### Scenario: Adapter returns mapped domain entities

- GIVEN a `FoursquarePlaceService` with a working `IFoursquareApiClient`
- WHEN `SearchPlacesAsync` is called with "Museum" in "madrid-es"
- THEN it returns `Place` entities with `PlaceId`, `Name`, `Location`, `TypicalDurationMinutes`, and `IsIndoor` correctly mapped

#### Scenario: Adapter returns empty list on API failure

- GIVEN a `FoursquarePlaceService` whose `IFoursquareApiClient` throws `HttpRequestException`
- WHEN `SearchPlacesAsync` is called
- THEN it returns an empty list (graceful degradation)

## MODIFIED Requirements

### MODIFIED Requirement: FR5 — PlaceRepository (Infrastructure)

`PlaceRepository` MUST depend on `IPlaceExternalService` instead of `IFoursquareApiClient` for external API fallback. The cascade logic (local DB first, then external API) MUST remain in `PlaceRepository`.

- EF Core implementation in `SmartTripPlanner.Infrastructure/Repositories/PlaceRepository.cs`.
- `PlaceConfiguration` in `SmartTripPlanner.Infrastructure/Configurations/PlaceConfiguration.cs` (unchanged).
- Register `IPlaceRepository` in `InfrastructureServiceRegistration` (unchanged).
- **Cascade logic**: `SearchAsync` queries local DB first. If no results, calls `IPlaceExternalService.SearchPlacesAsync`, and returns mapped `Place` list without persisting.
(Previously: PlaceRepository depended on `IFoursquareApiClient` and called it directly for cascade fallback.)

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

### MODIFIED Requirement: FR6 — IFoursquareApiClient (unchanged externally, becomes internal)

`IFoursquareApiClient` remains in Infrastructure and is unchanged. It is now consumed exclusively by `FoursquarePlaceService`. All Foursquare DTOs (`FoursquarePlace`, etc.) and mappers (`FoursquareCategoryHeuristics`) become `internal` — no layer outside Infrastructure may reference them.
(Previously: `IFoursquareApiClient` was injected directly into `PlaceRepository`.)

#### Scenario: Foursquare types are internal

- GIVEN the `SmartTripPlanner.Infrastructure` assembly
- WHEN external assemblies reference `FoursquarePlace`, `FoursquareCategoryHeuristics`, or other Foursquare types
- THEN those types are `internal` and inaccessible from outside Infrastructure

### MODIFIED Requirement: FR8 — Cascade Search Implementation

`PlaceRepository.SearchAsync` must implement:
1. Query local DB via EF Core (unchanged).
2. If local results found (count > 0), return them (unchanged).
3. If no local results, call `IPlaceExternalService.SearchPlacesAsync` (changed from `IFoursquareApiClient`).
4. Results from `IPlaceExternalService` are already mapped to `Place` entities — no additional mapping needed in the repository.
5. Return the mapped `List<Place>`.
6. API results are **ephemeral** — not saved to the database (unchanged).
(Previously: Step 3 called `IFoursquareApiClient.SearchPlacesAsync` and Step 4 mapped results inline in `PlaceRepository`.)

### MODIFIED Requirement: AC7 — Cascade Search

- Local DB results are returned without calling the external service (unchanged).
- No local results → `IPlaceExternalService.SearchPlacesAsync` is called → results are returned (changed).
- External service failure (exception) returns empty list — graceful degradation (unchanged).
- Results from external service are ephemeral (not persisted in DB) (unchanged).
