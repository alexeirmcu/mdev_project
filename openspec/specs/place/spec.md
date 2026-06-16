# Specification: flow1-place-domain

## Overview
Implement the Place domain entity, its local repository, and the Foursquare API cascade for Flow 1 (Place Discovery) — Domain + Infrastructure only. Covers **Paso A** (local DB), **Paso B** (Foursquare API fallback), and **Paso C** (emergency/heuristic mapping) from the cascade pipeline.

## Functional Requirements

### FR1: Place Entity
- Inherits from `Entity` (long auto-generated Id).
- `PlaceId` (string, required) with unique index for domain lookup.
- `Name` (string, required).
- `CityId` (string, required, e.g. "madrid-es").
- `TypicalDurationMinutes` (int) — default 60.
- `IsIndoor` (bool) — default false.
- `IsFamilyFriendly` (bool) — default true.
- `Location` — `PlaceLocation` ValueObject (OwnsOne).
- `OpeningHours` — `List<OpeningHoursWindow>` (OwnsMany).
- `Attributes` — `List<PlaceAttribute>` (OwnsMany, default empty).
- `AddAttribute(PlaceAttribute)` — method that appends to the `Attributes` collection (null check enforced).

### FR2: OpeningHoursWindow ValueObject
- `DayOfWeek` (DayOfWeek).
- `OpenMinutes` (int, range 0-1439 inclusive).
- `CloseMinutes` (int, range 0-1439 inclusive).
- Validation: OpenMinutes <= CloseMinutes.
- Value equality based on all three properties.

### FR3: PlaceLocation ValueObject
- `Latitude` (double, range -90 to 90 inclusive).
- `Longitude` (double, range -180 to 180 inclusive).
- Value equality based on both coordinates.

### FR4: IPlaceRepository
```csharp
namespace SmartTripPlanner.Domain.Repository;

public interface IPlaceRepository : IRepository<Place>
{
    Task<List<Place>> SearchAsync(string query, string cityId, int maxResults = 20);
    Task<Place?> GetByPlaceIdAsync(string placeId);
}
```

### FR5: PlaceRepository (Infrastructure)
- EF Core implementation in `SmartTripPlanner.Infrastructure/Repositories/PlaceRepository.cs`.
- `PlaceConfiguration` in `SmartTripPlanner.Infrastructure/Configurations/PlaceConfiguration.cs`.
- Register `IPlaceRepository` in `InfrastructureServiceRegistration`.
- **Cascade logic**: `SearchAsync` queries local DB first. If no results, calls `IPlaceExternalService.SearchPlacesAsync`, and returns mapped `Place` list without persisting.
- **Attribute search**: `SearchAsync(query, cityCode, maxResults)` MUST match places where `Name.Contains(query)` OR any `Attribute.Value.Contains(query)` (case-insensitive) within the specified city. The query MUST include `Attributes` via EF Core `Include`.

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

#### Scenario: Existing name search preserved
- GIVEN a Place with Name="Hotel California" in city "MAD"
- WHEN SearchAsync("hotel", "MAD") is called
- THEN the Place is returned

#### Scenario: Search matches chain attribute
- GIVEN a Place with Attribute("foursquare","chain","McDonald's") in city "MAD"
- WHEN SearchAsync("mcdonalds", "MAD") is called
- THEN the Place is returned

### FR6: IFoursquareApiClient (internal)
`IFoursquareApiClient` remains in Infrastructure and is unchanged. It is now consumed exclusively by `FoursquarePlaceService`. All Foursquare DTOs (`FoursquarePlace`, etc.) and mappers (`FoursquareCategoryHeuristics`) become `internal` — no layer outside Infrastructure may reference them.

#### Scenario: Foursquare types are internal
- GIVEN the `SmartTripPlanner.Infrastructure` assembly
- WHEN external assemblies reference `FoursquarePlace`, `FoursquareCategoryHeuristics`, or other Foursquare types
- THEN those types are `internal` and inaccessible from outside Infrastructure

### FR7: FoursquareCategoryHeuristics
- Maps Foursquare category IDs and names to heuristic `Place` property values:
  - Museum, Art Gallery, Theme Park → `TypicalDurationMinutes = 120`
  - Historic Site, Monument, Plaza, Park → `TypicalDurationMinutes = 60`
  - Restaurant, Cafe, Food Court → `TypicalDurationMinutes = 90`
  - Nightclub, Strip Club, Adult Entertainment → `IsFamilyFriendly = false`
  - Any indoor category (e.g., Museum) → `IsIndoor = true`
  - Default (unknown category): `TypicalDurationMinutes = 60`, `IsIndoor = true`, `IsFamilyFriendly = true`
- Pure mapping service with no external dependencies (no HttpClient, no DB).
- Lives in `SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/Mapping/`.

### FR8: Cascade Search Implementation
`PlaceRepository.SearchAsync` must implement:
1. Query local DB via EF Core.
2. If local results found (count > 0), return them.
3. If no local results, call `IPlaceExternalService.SearchPlacesAsync`.
4. Results from `IPlaceExternalService` are already mapped to `Place` entities — no additional mapping needed in the repository.
5. Return the mapped `List<Place>`.
6. API results are **ephemeral** — not saved to the database.

### FR9: Configuration
- `appsettings.json` / `appsettings.Development.json`:
  ```json
  {
    "FoursquareApi": {
      "BaseUrl": "https://api.foursquare.com/v3/",
      "ApiKey": ""
    }
  }
  ```
- API Key stored in **User Secrets** for development: `dotnet user-secrets set "FoursquareApi:ApiKey" "<key>"`.
- `FoursquareApiOptions` class in `SmartTripPlanner.Infrastructure/Configuration/` with `IOptions` validation.
- `InfrastructureServiceRegistration` binds options and registers the typed HttpClient.

### FR10: IPlaceExternalService (Port)
The Domain layer MUST define `IPlaceExternalService` as a port for searching places in external providers. The interface SHALL return domain entities (`Place`), not DTOs.

```csharp
namespace SmartTripPlanner.Domain.Repository;

public interface IPlaceExternalService
{
    Task<List<Place>> SearchPlacesAsync(string query, string cityId, int maxResults = 20);
}
```

#### Scenario: Port abstracts external provider lookup
- GIVEN a query, cityId, and maxResults
- WHEN `SearchPlacesAsync` is called
- THEN it returns a `List<Place>` with mapped results from the external provider

### FR11: FoursquarePlaceService (Adapter)
The Infrastructure layer MUST implement `IPlaceExternalService` via a `FoursquarePlaceService` class that:
- Wraps `IFoursquareApiClient` internally
- Maps `FoursquarePlace` → `Place` domain entity using `FoursquareCategoryHeuristics`
- Maps `FoursquarePlace.Categories` to `PlaceAttribute("foursquare", "category", cat.Name)` via `Place.AddAttribute` for each category
- Maps `FoursquarePlace.Chains` to `PlaceAttribute("foursquare", "chain", chain.Name)` for non-empty chain labels
- Is registered via DI as `IPlaceExternalService`

#### Scenario: Adapter returns mapped domain entities
- GIVEN a `FoursquarePlaceService` with a working `IFoursquareApiClient`
- WHEN `SearchPlacesAsync` is called with "Museum" in "madrid-es"
- THEN it returns `Place` entities with `PlaceId`, `Name`, `Location`, `TypicalDurationMinutes`, and `IsIndoor` correctly mapped

#### Scenario: Adapter returns empty list on API failure
- GIVEN a `FoursquarePlaceService` whose `IFoursquareApiClient` throws `HttpRequestException`
- WHEN `SearchPlacesAsync` is called
- THEN it returns an empty list (graceful degradation)

#### Scenario: Categories mapped to attributes
- GIVEN an API response with Categories containing Name="Hotel" and Name="Boutique"
- WHEN MapToPlace creates a Place
- THEN Place.Attributes contains corresponding PlaceAttribute entries with Provider="foursquare", Key="category"

### FR12: PlaceModel Attributes
`PlaceModel` MUST include `IReadOnlyList<PlaceAttributeModel> Attributes`. `PlaceAttributeModel` is a record with `Provider`, `Key`, `Value` (all string). `AutoMapperProfile` MUST map `PlaceAttribute` to `PlaceAttributeModel`.

#### Scenario: Attributes returned in API response
- GIVEN a Place with two attributes
- WHEN mapped to PlaceModel via AutoMapper
- THEN PlaceModel.Attributes contains matching PlaceAttributeModel entries

### FR13: EF Core PlaceAttribute Configuration
`PlaceConfiguration` MUST configure `OwnsMany(p => p.Attributes, ...)` with separate table "PlaceAttributes", foreign key "PlaceId", and properties: Provider (max 100, required), Key (max 100, required), Value (max 500, required). A composite index on `(PlaceId, Value)` SHOULD be applied for search performance.

#### Scenario: Attributes persisted and loaded
- GIVEN a Place with attributes saved via EF Core
- WHEN the Place is retrieved with Include
- THEN all Attributes are loaded with correct values

## Non-Functional Requirements
- Strict TDD — tests define contracts before implementation.
- All existing tests must continue passing.
- No modifications to existing entities (Trip, City, DayPlan, etc.).
- Tests follow mirror directory structure under `tests/SmartTripPlanner.Tests/`.
- Foursquare DTOs must NOT leak outside Infrastructure layer.
- Foursquare API must be mockable in tests (via `HttpMessageHandler`).
- Tests must not require a real Foursquare API key.

## Acceptance Criteria

### AC1: Place Construction (Phase 1 — covered)
- Creating a valid Place with minimal required fields succeeds.
- Creating a Place with null/empty PlaceId throws ArgumentNullException.
- Creating a Place with null/empty Name throws ArgumentNullException.
- Creating a Place with valid Location succeeds.
- Creating a Place with invalid Location (lat out of range) throws validation.

### AC2: OpeningHoursWindow Construction (Phase 1 — covered)
- Creating with valid minutes succeeds.
- Creating with OpenMinutes > CloseMinutes throws ArgumentException.
- Creating with minutes < 0 throws ArgumentOutOfRangeException.
- Creating with minutes > 1439 throws ArgumentOutOfRangeException.
- Two instances with same values are equal.

### AC3: PlaceLocation Construction (Phase 1 — covered)
- Creating with valid lat/lng succeeds.
- Creating with Latitude > 90 throws ArgumentOutOfRangeException.
- Creating with Longitude > 180 throws ArgumentOutOfRangeException.
- Two instances with same coordinates are equal.

### AC4: Repository Operations (Phase 1 — covered)
- SearchAsync with matching query and cityId returns matching Places.
- SearchAsync with non-matching query returns empty list.
- SearchAsync filters by CityId correctly.
- GetByPlaceIdAsync returns the correct Place.
- GetByPlaceIdAsync returns null when PlaceId doesn't exist.
- Saved Place preserves all properties when retrieved.

### AC5: Foursquare API Client
- `FoursquareApiClient` calls the correct Foursquare endpoint with authorization header.
- `SearchPlacesAsync` returns mapped DTOs for a successful response.
- `SearchPlacesAsync` returns empty list when API returns no results.
- `GetPlaceByIdAsync` returns the correct DTO for a valid ID.
- `GetPlaceByIdAsync` returns null when the ID doesn't exist.
- Client propagates HTTP errors (non-2xx) as `HttpRequestException`.

### AC6: Category Heuristics
- Museum category maps to `TypicalDurationMinutes = 120`, `IsIndoor = true`.
- Historic Site maps to `TypicalDurationMinutes = 60`.
- Restaurant maps to `TypicalDurationMinutes = 90`.
- Nightclub maps to `IsFamilyFriendly = false`.
- Unknown category returns default values (60, true, true).

### AC7: Cascade Search
- Local DB results are returned without calling the external service.
- No local results → `IPlaceExternalService.SearchPlacesAsync` is called → results are returned.
- External service failure (exception) returns empty list (graceful degradation).
- Results from external service are ephemeral (not persisted in DB).

### AC8: PlaceAttribute ValueObject (enhance-place-search)
- Creating a valid PlaceAttribute with Provider, Key, Value succeeds.
- Creating with null/empty Provider, Key, or Value throws SmartTripDomainException.
- Two PlaceAttribute instances with same values are equal.

### AC9: Place Attributes Collection (enhance-place-search)
- Place exposes an empty `Attributes` list by default.
- `AddAttribute` appends to the `Attributes` collection.
- Adding null attribute throws ArgumentNullException.

### AC10: Attribute Search (enhance-place-search)
- Searching "hotel" returns places whose attribute Value is "Hotel" even if name doesn't match.
- Existing name-based search still works (regression).
- Searching "mcdonalds" returns places with chain attribute "McDonald's".
- Search is case-insensitive for attribute values.

### AC11: Foursquare Category Mapping (enhance-place-search)
- FoursquarePlaceService maps API categories to PlaceAttribute entries with Provider="foursquare", Key="category".
- Only Pro-tier Foursquare data is used — no premium fields.

### AC12: PlaceModel Attributes (enhance-place-search)
- PlaceModel includes an Attributes collection.
- AutoMapper correctly maps PlaceAttribute to PlaceAttributeModel.

### AC13: Attribute Persistence (enhance-place-search)
- Place attributes are persisted to a separate "PlaceAttributes" table.
- Attributes are correctly loaded when Place is retrieved with Include.

## Infrastructure Dependencies
- `Microsoft.EntityFrameworkCore.InMemory` package for infrastructure tests (already added in Phase 1).
- `Microsoft.Extensions.Http` — typed HttpClient registration (built-in via ASP.NET metapackage).
- `SmartTripPlanner.Tests` already references `SmartTripPlanner.Infrastructure` (Phase 1).
