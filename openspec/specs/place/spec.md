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
- `Attributes` — `List<PlaceAttribute>` (HasMany-through-join-table, default empty). Relationship is many-to-many via explicit join entity `PlacePlaceAttributes`. `PlaceAttribute` is a shared entity, not owned.
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
    Task<List<Place>> SearchAsync(string? query, string cityCode, string? category, int maxResults = 20);
    Task<string?> GetProviderIdForCategoryAsync(string categoryName, CancellationToken ct = default);
    Task<Place?> GetByProviderReferenceIdAsync(string providerReferenceId);
    Task<IEnumerable<Place>> GetManyByIdsAsync(IEnumerable<long> placeIds, CancellationToken ct);
    Task<List<Place>> GetManyByCityIdAsync(long cityId, CancellationToken ct = default);
    Task<List<Place>> GetCandidatesByCityAndInterestsAsync(long cityId, IReadOnlyList<string> interests, CancellationToken ct = default);
    Task<List<string>> GetDistinctAttributeValuesByCityCodeAsync(string cityCode, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<Place> places);
    Task UpsertRangeAsync(IEnumerable<Place> places);
}
```

#### Scenario: Search with null query
- GIVEN a repository with places in city "madrid-es"
- WHEN `SearchAsync(query: null, "madrid-es", category: null)` is called
- THEN it returns local results without name/attribute filtering (city-wide search)
- AND no exception is thrown

#### Scenario: Search by category filters on attribute value
- GIVEN places linked to PlaceAttribute with Key="category", Value="Museum"
- WHEN `SearchAsync(query: null, "madrid-es", category: "Museum")` is called
- THEN only places with that attribute value are returned

#### Scenario: Search matches attribute value through join table
- GIVEN a Place with Name="Gran Palace" linked to PlaceAttribute(Value="Hotel") via join table in city "MAD"
- WHEN `SearchAsync("hotel", "MAD", null)` is called
- THEN the Place is returned

#### Scenario: Interest-filtered candidate retrieval returns matching places via join table
- GIVEN a city with places linked to shared PlaceAttribute entities "museum", "history", "food" via join table
- WHEN `GetCandidatesByCityAndInterestsAsync(cityId, ["museum", "food"])` is called
- THEN only places linked to at least one PlaceAttribute whose Value matches any interest are returned
- AND matching is inclusive (place matches if ANY linked attribute matches ANY interest)

#### Scenario: Interest filtering falls back gracefully
- GIVEN a city with places having no attributes matching ["underwater"]
- WHEN `GetCandidatesByCityAndInterestsAsync(cityId, ["underwater"])` is called
- THEN an empty list is returned (not an exception)

#### Scenario: Distinct attribute values per city code via join table
- GIVEN a city with places linked to shared PlaceAttribute entities via join table
- WHEN `GetDistinctAttributeValuesByCityCodeAsync("madrid-es")` is called
- THEN distinct PlaceAttribute.Value strings are returned (no duplicates)

### FR5: PlaceRepository (Infrastructure)
- EF Core implementation in `SmartTripPlanner.Infrastructure/Repositories/PlaceRepository.cs`.
- `PlaceConfiguration` in `SmartTripPlanner.Infrastructure/Configurations/PlaceConfiguration.cs` configures `HasMany(p => p.Attributes).WithMany()` using explicit join entity `PlacePlaceAttributes`.
- A case-insensitive unique index is applied on `PlaceAttribute(Provider, Key, Value)`.
- Register `IPlaceRepository` in `InfrastructureServiceRegistration`.
- **SearchAsync**: MUST handle nullable `query`: if null, skip name/attribute `Contains` filter; if provided, apply case-insensitive match as before. MUST accept `category` parameter: when non-null, filter by `Attribute.Value.Contains(category)` with Provider="foursquare", Key="category". **Cascade logic REMOVED** — `SearchAsync` returns local results only. External fallback is now the handler's responsibility.
- **GetProviderIdForCategoryAsync(name)**: MUST query `PlaceAttribute` where (Provider="foursquare", Key="category", Value=name) and return the `ProviderId` of the first match, or null.
- **Attribute search**: `SearchAsync(query, cityCode, maxResults)` MUST match places where `Name.Contains(query)` OR any `Attribute.Value.Contains(query)` (case-insensitive) within the specified city. The query MUST include `Attributes` via EF Core `Include` through join table.
- **Interest-filtered query**: `GetCandidatesByCityAndInterestsAsync` MUST filter candidates in SQL through the join table linking Places to PlaceAttributes. `GetDistinctAttributeValuesByCityCodeAsync` MUST query distinct PlaceAttribute.Value strings through the join table in SQL.
- **UpsertRangeAsync attribute resolution**: `UpsertRangeAsync` MUST resolve attributes using find-or-create: for each attribute on an incoming Place, check if a PlaceAttribute with matching (Provider, Key, Value) already exists (case-insensitive). If found, link the existing entity to the Place. If not found, create and persist a new PlaceAttribute entity, then link it.
- **UpsertRangeAsync dedup**: MUST deduplicate places by `ProviderReferenceId`: if a Place with the same `ProviderReferenceId` exists, update basic fields (Name, Location, OpeningHours, Attributes) but preserve enrichment fields (FamilyFriendlyScore, Popularity, IsEnriched, IsIndoor, TypicalDurationMinutes).

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

#### Scenario: UpsertRangeAsync finds existing attribute and links
- GIVEN PlaceAttribute (Provider="foursquare", Key="category", Value="Museum") already exists in DB
- WHEN `UpsertRangeAsync` processes a new Place with the same attribute
- THEN the existing PlaceAttribute row is linked via join table (no duplicate row created)

#### Scenario: UpsertRangeAsync creates new attribute when not found
- GIVEN no PlaceAttribute with (Provider="foursquare", Key="category", Value="Aquarium") exists
- WHEN `UpsertRangeAsync` processes a Place with this attribute
- THEN a new PlaceAttribute row is created and linked to the Place via join table

#### Scenario: Search with null query returns all city places
- GIVEN 5 places in city "madrid-es"
- WHEN `SearchAsync(null, "madrid-es", null)` is called
- THEN all 5 places are returned

#### Scenario: Search by category filters correctly
- GIVEN Place A with category "Museum", Place B with category "Restaurant"
- WHEN `SearchAsync(null, "madrid-es", "Museum")` is called
- THEN only Place A is returned

#### Scenario: Upsert dedup by ProviderReferenceId
- GIVEN a persisted Place with ProviderReferenceId="fsq_123", Name="Old Name", FamilyFriendlyScore=4
- WHEN `UpsertRangeAsync` processes an incoming Place with same ProviderReferenceId, Name="New Name", FamilyFriendlyScore=2
- THEN the persisted Place's Name becomes "New Name" (external wins)
- AND FamilyFriendlyScore remains 4 (enrichment preserved)

#### Scenario: Upsert inserts new place when no match
- GIVEN no persisted Place with ProviderReferenceId="fsq_999"
- WHEN `UpsertRangeAsync` processes a Place with ProviderReferenceId="fsq_999"
- THEN a new Place row is inserted

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

### FR8: Cascade Search Implementation (REMOVED)

The cascade logic in `PlaceRepository` (steps 1-6) is REMOVED. External fallback is now orchestrated by the handler, not the repository.
(Reason: handler now controls external fallback and merge)

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
- **Chain attribute mapping REMOVED** — `FoursquarePlace.Chains` is no longer mapped to `PlaceAttribute("foursquare", "chain", ...)`.
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

#### Scenario: Chains no longer persisted as attributes
- GIVEN an API response with Chains=[{Name="McDonald's"}]
- WHEN `MapToPlace` creates a Place
- THEN Place.Attributes does NOT contain any chain-related PlaceAttribute entries

### FR12: PlaceModel Attributes
`PlaceModel` MUST include `IReadOnlyList<PlaceAttributeModel> Attributes`. `PlaceAttributeModel` is a record with `Key` and `Value` (string). `Provider` and `Id` MUST NOT be exposed in `PlaceAttributeModel`. `AutoMapperProfile` MUST map `PlaceAttribute` to `PlaceAttributeModel` projecting only `Key` and `Value`.

#### Scenario: Attributes returned in API response without Provider or Id
- GIVEN a Place with a linked PlaceAttribute (Provider="foursquare", Key="category", Value="Hotel", Id=42)
- WHEN mapped to PlaceModel via AutoMapper
- THEN PlaceModel.Attributes contains a PlaceAttributeModel with Key="category", Value="Hotel"
- AND the PlaceAttributeModel does NOT contain Provider or Id fields

### FR13: EF Core PlaceAttribute Configuration
`PlaceConfiguration` MUST configure the many-to-many relationship between `Place` and `PlaceAttribute` using an explicit join entity `PlacePlaceAttributes` with foreign keys `PlaceId` and `PlaceAttributeId`. `PlaceAttribute` MUST be configured as a separate entity with its own `DbSet<PlaceAttribute>` in `PlannerDbContext` and a dedicated `PlaceAttributeConfiguration` class. A case-insensitive unique index MUST be applied on `(Provider, Key, Value)`.

#### Scenario: Attributes persisted through join table and loaded
- GIVEN a Place linked to shared PlaceAttribute entities via join table
- WHEN the Place is retrieved with Include on Attributes
- THEN all linked Attributes are loaded with correct values

### FR14: Delete and recreate migrations
All existing EF Core migration files MUST be deleted. A single `InitialCreate` migration MUST be generated that captures the full schema including the `PlacePlaceAttributes` join table, `PlaceAttribute` as an entity with `Id`, and the case-insensitive unique index on `(Provider, Key, Value)`. The `__EFMigrationsHistory` table MUST start fresh with no prior migration history.

#### Scenario: Fresh database applies InitialCreate
- GIVEN a blank database
- WHEN the InitialCreate migration is applied
- THEN all tables (Places, PlaceAttributes, PlacePlaceAttributes, Cities, etc.) are created with correct schema
- AND the migration history contains only the InitialCreate entry

#### Scenario: No legacy migrations remain
- GIVEN the source code after migration cleanup
- WHEN the Migrations folder is inspected
- THEN only the InitialCreate migration file exists (plus snapshot)

### FR15: Place Enrichment Fields

The `Place` entity MUST carry enrichment metadata populated by the LLM background enricher. New fields: `FamilyFriendlyScore` (int, range 1-5, default 3), `Popularity` (double, range 0.0-1.0, default 0.5), `IsEnriched` (bool, default false). These fields MUST be persisted as EF Core columns with configured defaults in `PlaceConfiguration`. The existing `IsFamilyFriendly` (bool) SHALL remain alongside `FamilyFriendlyScore` (graded score) — they serve different purposes.

#### Scenario: New Place has default enrichment values

- GIVEN a Place created via existing constructors
- WHEN inspected
- THEN `FamilyFriendlyScore = 3`, `Popularity = 0.5`, `IsEnriched = false`

#### Scenario: Enrichment fields persisted and retrieved

- GIVEN a Place with `FamilyFriendlyScore = 4`, `Popularity = 0.8`, `IsEnriched = true`
- WHEN saved and reloaded via EF Core
- THEN all three fields are correctly persisted with their values

### FR16: Place.MarkEnriched Method

The `Place` entity MUST expose `MarkEnriched(int typicalDurationMinutes, bool isIndoor, int familyFriendlyScore, double popularity)` that validates all inputs and sets `IsEnriched = true`. `FamilyFriendlyScore` MUST be 1-5; `Popularity` MUST be 0.0-1.0; `TypicalDurationMinutes` MUST be > 0. Out-of-range values SHALL throw `SmartTripDomainException` and MUST NOT mutate any field.

#### Scenario: Valid enrichment marks Place as enriched

- GIVEN a Place with `IsEnriched = false`
- WHEN `MarkEnriched(120, true, 4, 0.8)` is called
- THEN `IsEnriched = true`, `TypicalDurationMinutes = 120`, `IsIndoor = true`, `FamilyFriendlyScore = 4`, `Popularity = 0.8`

#### Scenario: Out-of-range FamilyFriendlyScore throws

- GIVEN a Place
- WHEN `MarkEnriched(60, true, 6, 0.5)` is called
- THEN `SmartTripDomainException` is thrown
- AND `IsEnriched` remains false and no field is mutated

#### Scenario: Out-of-range Popularity throws

- GIVEN a Place
- WHEN `MarkEnriched(60, true, 3, 1.5)` is called
- THEN `SmartTripDomainException` is thrown
- AND `IsEnriched` remains false and no field is mutated

### FR17: Category ProviderId Resolution

The system MUST provide `GetProviderIdForCategoryAsync(string categoryName)` on `IPlaceRepository` to resolve a category name to its Foursquare ProviderId from local `PlaceAttribute` data.

#### Scenario: Category resolved to ProviderId
- GIVEN a PlaceAttribute with (Provider="foursquare", Key="category", Value="Museum", ProviderId="10000")
- WHEN `GetProviderIdForCategoryAsync("Museum")` is called
- THEN it returns "10000"

#### Scenario: Unknown category returns null (cold start)
- GIVEN no PlaceAttribute with Value="Aquarium" exists
- WHEN `GetProviderIdForCategoryAsync("Aquarium")` is called
- THEN it returns null

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

### AC7: External Fallback (Handler-Orchestrated)
- Handler calls `SearchAsync` for local results first.
- If `localCount < maxResults` AND `FetchFromExternalIfInsufficient != false`, handler resolves category and calls external service.
- External service failure (exception) returns local results only (graceful degradation).
- External results are merged with local data by `ProviderReferenceId` and persisted via `UpsertRangeAsync`.
- Enrichment fields (FamilyFriendlyScore, Popularity, IsEnriched) are preserved during merge.

### AC8: PlaceAttribute Entity (normalize-place-attributes)
- Creating a valid PlaceAttribute with Provider, Key, Value succeeds.
- Creating with null/empty Provider, Key, or Value throws SmartTripDomainException.
- Two transient PlaceAttribute instances are NOT equal (identity-based equality; same-Id instances are equal).
- PlaceAttribute is immutable — no public setters on Provider, Key, Value.

### AC9: Place Attributes Collection (enhance-place-search)
- Place exposes an empty `Attributes` list by default.
- `AddAttribute` appends to the `Attributes` collection.
- Adding null attribute throws ArgumentNullException.

### AC10: Attribute Search (enhance-place-search)
- Searching "hotel" returns places whose attribute Value is "Hotel" even if name doesn't match.
- Existing name-based search still works (regression).
- Search with null query returns all city places (category-based browse).
- Search is case-insensitive for attribute values.
- Category filter on search is supported via `SearchAsync` `category` parameter.

### AC11: Foursquare Category Mapping (enhance-place-search)
- FoursquarePlaceService maps API categories to PlaceAttribute entries with Provider="foursquare", Key="category".
- Only Pro-tier Foursquare data is used — no premium fields.

### AC12: PlaceModel Attributes (normalize-place-attributes)
- PlaceModel includes an Attributes collection.
- PlaceAttributeModel exposes only Key and Value (no Provider or Id).
- AutoMapper maps PlaceAttribute to PlaceAttributeModel projecting Key+Value only.

### AC13: Attribute Persistence (normalize-place-attributes)
- Place attributes are persisted as shared entities with a join table (not owned).
- A case-insensitive unique index on (Provider, Key, Value) is enforced at the database level.
- Attributes are correctly loaded when Place is retrieved with Include through join table.
- Duplicate attributes across places result in one PlaceAttribute row linked via the join table.
- Orphaned PlaceAttribute rows (with no Place references) are retained.

## Infrastructure Dependencies
- `Microsoft.EntityFrameworkCore.InMemory` package for infrastructure tests (already added in Phase 1).
- `Microsoft.Extensions.Http` — typed HttpClient registration (built-in via ASP.NET metapackage).
- `SmartTripPlanner.Tests` already references `SmartTripPlanner.Infrastructure` (Phase 1).
