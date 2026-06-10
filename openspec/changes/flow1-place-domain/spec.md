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
- `PlaceConfiguration` in `SmartTripPlanner.Infrastructure/Configurations/PlaceConfiguration.cs`:
  - HasKey on Id (auto-generated).
  - Unique index on PlaceId.
  - OwnsOne for Location (columns: Location_Latitude, Location_Longitude).
  - OwnsMany for OpeningHours (separate table `PlaceOpeningHours`).
- Register `DbSet<Place>` in `PlannerDbContext`.
- Register `IPlaceRepository` in `InfrastructureServiceRegistration`.
- **Cascade logic**: `SearchAsync` queries local DB first. If no results, calls `IFoursquareApiClient`, maps results via `FoursquareCategoryHeuristics`, and returns mapped `Place` list without persisting.

### FR6: IFoursquareApiClient
```csharp
namespace SmartTripPlanner.Infrastructure.ExternalServices.Foursquare;

public interface IFoursquareApiClient
{
    Task<List<FoursquarePlace>> SearchPlacesAsync(string query, string near, int limit = 20);
    Task<FoursquarePlace?> GetPlaceByIdAsync(string fsqId);
}
```
- Lives in Infrastructure layer only — no other layer may reference Foursquare DTOs.
- Implements a typed HttpClient wired via `IHttpClientFactory`.
- Configuration via `IOptions<FoursquareApiOptions>`:
  - `BaseUrl` (string, default `"https://api.foursquare.com/v3/"`)
  - `ApiKey` (string, loaded from User Secrets in development)
- Communicates with Foursquare Places API `/places/search` and `/places/{fsq_id}` endpoints.
- Returns Infrastructure-level DTOs that are NOT shared with any other layer.

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
- `PlaceRepository.SearchAsync` must implement:
  1. Query local DB via EF Core (existing Phase 1 logic).
  2. If local results found (count > 0), return them.
  3. If no local results, call `IFoursquareApiClient.SearchPlacesAsync`.
  4. For each Foursquare result, map to `Place` domain entity:
     - `PlaceId` = Foursquare `fsq_id`
     - `Name` = Foursquare `name`
     - `CityId` = original `cityId` parameter
     - `Location` = Foursquare `geocodes.main` (lat/lng)
     - `OpeningHours` = Foursquare `hours.regular` (if available)
     - `TypicalDurationMinutes` + `IsIndoor` + `IsFamilyFriendly` = from `FoursquareCategoryHeuristics`
  5. Return the mapped `List<Place>`.
- API results are **ephemeral** — not saved to the database.

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
- Local DB results are returned without calling the Foursquare API.
- No local results → Foursquare API is called → results are mapped and returned.
- Foursquare API failure (HTTP error) returns empty list (graceful degradation).
- Results from Foursquare API are ephemeral (not persisted in DB).

## Infrastructure Dependencies
- `Microsoft.EntityFrameworkCore.InMemory` package for infrastructure tests (already added in Phase 1).
- `Microsoft.Extensions.Http` — typed HttpClient registration (built-in via ASP.NET metapackage).
- `SmartTripPlanner.Tests` already references `SmartTripPlanner.Infrastructure` (Phase 1).
