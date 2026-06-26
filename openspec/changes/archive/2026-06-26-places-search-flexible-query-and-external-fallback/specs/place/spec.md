# Delta for place

## MODIFIED Requirements

### FR4: IPlaceRepository

```csharp
Task<List<Place>> SearchAsync(string? query, string cityCode, string? category, int maxResults = 20);
Task<string?> GetProviderIdForCategoryAsync(string categoryName, CancellationToken ct = default);
// All other methods unchanged
```
(Previously: `SearchAsync(string query, ...)` — query was required; no `category` param, no `GetProviderIdForCategoryAsync`)

#### Scenario: Search with null query
- GIVEN a repository with places in city "madrid-es"
- WHEN `SearchAsync(query: null, "madrid-es", category: null)` is called
- THEN it returns local results without name/attribute filtering (city-wide search)
- AND no exception is thrown

#### Scenario: Search by category filters on attribute value
- GIVEN places linked to PlaceAttribute with Key="category", Value="Museum"
- WHEN `SearchAsync(query: null, "madrid-es", category: "Museum")` is called
- THEN only places with that attribute value are returned

### FR5: PlaceRepository (Infrastructure)

- `SearchAsync` MUST handle nullable `query`: if null, skip name/attribute `Contains` filter; if provided, apply case-insensitive match as before.
- `SearchAsync` MUST accept `category` parameter: when non-null, filter by `Attribute.Value.Contains(category)` with Provider="foursquare", Key="category".
- **Cascade logic REMOVED** from repository — `SearchAsync` returns local results only. External fallback is now the handler's responsibility.
- `GetProviderIdForCategoryAsync(name)` MUST query `PlaceAttribute` where (Provider="foursquare", Key="category", Value=name) and return the `ProviderId` of the first match, or null.
- `UpsertRangeAsync` MUST deduplicate places by `ProviderReferenceId`: if a Place with the same `ProviderReferenceId` exists, update basic fields (Name, Location, OpeningHours) but preserve enrichment fields (FamilyFriendlyScore, Popularity, IsEnriched, IsIndoor, TypicalDurationMinutes).
(Previously: cascade logic existed; SearchAsync query was required; no category filter; UpsertRangeAsync only resolved attributes, not deduped by ProviderReferenceId)

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

### FR8: Cascade Search Implementation (REMOVED)

The cascade logic in `PlaceRepository` (steps 1-6) is REMOVED. External fallback is now orchestrated by the handler, not the repository.
(Reason: handler now controls external fallback and merge)

### FR11: FoursquarePlaceService (Adapter)

- Maps `FoursquarePlace.Categories` to `PlaceAttribute("foursquare", "category", cat.Name)` via `Place.AddAttribute` — unchanged.
- **Chain attribute mapping REMOVED** — `FoursquarePlace.Chains` is no longer mapped to `PlaceAttribute("foursquare", "chain", ...)`.
(Previously: chains were mapped to PlaceAttribute entries with Provider="foursquare", Key="chain")

#### Scenario: Chains no longer persisted as attributes
- GIVEN an API response with Chains=[{Name="McDonald's"}]
- WHEN `MapToPlace` creates a Place
- THEN Place.Attributes does NOT contain any chain-related PlaceAttribute entries

## ADDED Requirements

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
