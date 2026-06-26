# Design: Flexible Query & External Fallback for Place Search

## Technical Approach

Three-part change: (1) make `query` optional with at-least-one-input guard, (2) add `FetchFromExternalIfInsufficient` parameter with field-aware dedup merge, (3) push category filters to Foursquare via resolved `fsq_category_ids` from local `PlaceAttribute`. Cold start detection and chain attribute removal complete the picture.

## Architecture Decisions

| Decision | Choice | Alternatives | Rationale |
|----------|--------|--------------|-----------|
| Fallback ownership | Handler orchestrates local→external flow, not repo | Put logic in FoursquarePlaceService | Handler has access to both repo and external service; Clean Architecture keeps domain/repo agnostic of external providers |
| Dedup merge location | New `PlaceService` helper in Application layer | In repo's `UpsertRangeAsync` or in handler inline | Handler is the orchestrator; dedup is application logic (which fields to preserve), not persistence logic |
| Category resolution | New `GetProviderAttributeIdsAsync` on `IPlaceRepository` | Resolve from external service config | Resolution needs local DB (existing PlaceAttributes); repo is the right abstraction |
| ProviderId on PlaceAttribute | Nullable string, populated during external mapping | Separate table, new entity | Minimal schema change; `ProviderId` is metadata on an existing attribute, not a new concept |
| When external is called | `localCount < maxResults` AND `FetchFromExternalIfInsufficient != false` | Always call, always merge | Avoids unnecessary API calls when local data is sufficient |
| Cold start | Skip external if no `PlaceAttribute` with matching category | Always call with no category filter | Calling without category filter returns irrelevant results; user intent was category-browse |
| Chain persistence | Remove from `FoursquarePlaceService.MapToPlace` | Filter at query time | Chains aren't used by any consumer; simpler to stop producing them |

## Data Flow

```
Client                   Handler                 Repo              Foursquare
  │                        │                      │                   │
  │ POST /places/search    │                      │                   │
  │━━━━━━━━━━━━━━━━━━━━━━━▶│                      │                   │
  │ { query?, category,    │                      │                   │
  │   FetchFromExternal? } │                      │                   │
  │                        │ Validate: at least   │                   │
  │                        │ one of query/        │                   │
  │                        │ category/filters >0  │                   │
  │                        │                      │                   │
  │                        │──SearchAsync(q,city)─▶│                   │
  │                        │◀─────localPlaces─────│                   │
  │                        │                      │                   │
  │                        │ if localCount >=     │                   │
  │                        │ maxResults OR        │                   │
  │                        │ FetchFromExternal==  │                   │
  │                        │ false → return local │                   │
  │                        │                      │                   │
  │                        │ if category set:     │                   │
  │                        │──GetProviderAttrIds─▶│                   │
  │                        │◀─── [fsq_cat_ids] ──│                   │
  │                        │                      │                   │
  │                        │ if cold start (no    │                   │
  │                        │ ids) → return local  │                   │
  │                        │                      │                   │
  │                        │──SearchPlacesAsync───│──────────────────▶│
  │                        │   (query, city,      │                   │
  │                        │    fsq_cat_ids)      │                   │
  │                        │◀──── externalPlaces ─│──────────────────│
  │                        │                      │                   │
  │                        │ Dedup merge:         │                   │
  │                        │ by ProviderReference │                   │
  │                        │ basic ← external     │                   │
  │                        │ enrichment ← local   │                   │
  │                        │                      │                   │
  │                        │──UpsertRangeAsync────▶│                   │
  │                        │◀─────────────────────│                   │
  │◀━━━━━ merged results ━━━│                      │                   │
```

## Interfaces / Contracts

### PlaceSearchRequest (API Model) — ADDED field
```csharp
public record PlaceSearchRequest(
    string? Query,
    string CityCode,
    int? MaxResults,
    string? Category = null,
    bool? IsIndoor = null,
    bool? IsFamilyFriendly = null,
    int? MaxDurationMinutes = null,
    bool? FetchFromExternalIfInsufficient = null);
```

### IPlaceRepository — MODIFIED signature + new method
```csharp
public interface IPlaceRepository : IRepository<Place>
{
    Task<List<Place>> SearchAsync(string? query, string cityCode,
        int maxResults = 20, PlaceSearchFilter? filter = null);
    // NEW: resolves category name → ProviderId list for a provider
    Task<List<string>> GetProviderAttributeIdsAsync(
        string provider, string key, string value);
    // ... rest unchanged
}
```

### IFoursquareApiClient — MODIFIED signature
```csharp
internal interface IFoursquareApiClient
{
    Task<List<FoursquarePlace>> SearchPlacesAsync(
        string? query, string near, int limit = 20,
        List<string>? fsqCategoryIds = null);
}
```

### PlaceAttribute — ADDED field
```csharp
public class PlaceAttribute : Entity
{
    public string Provider { get; private set; }
    public string? ProviderId { get; private set; }  // NEW: nullable
    public string Key { get; private set; }
    public string Value { get; private set; }

    public PlaceAttribute(string provider, string key, string value,
        string? providerId = null);  // NEW optional param
}
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `Domain/AggregatesModel/PlaceAttribute.cs` | Modify | Add nullable `ProviderId` property, update ctors |
| `Domain/Repository/IPlaceRepository.cs` | Modify | `SearchAsync` query nullable; add `GetProviderAttributeIdsAsync` |
| `Domain/ApiModels/PlaceSearchRequest.cs` | Modify | Add `FetchFromExternalIfInsufficient` |
| `Domain/ApiModels/PlaceAttributeModel.cs` | Modify | Add `ProviderId` to model |
| `ApplicationServices/Handlers/SearchPlacesHandler.cs` | Modify | Fallback flow, dedup merge, category resolution, enrichment preservation |
| `ApplicationServices/Validators/SearchPlacesRequestValidator.cs` | Modify | Remove query required+minLength; add at-least-one-input guard |
| `ApplicationServices/Commands/SearchPlacesRequest.cs` | Modify | Carry `FetchFromExternalIfInsufficient` from request |
| `API/Controllers/PlacesController.cs` | No change | Handled by validator |
| `Infrastructure/Repositories/PlaceRepository.cs` | Modify | Null-safe query; add `GetProviderAttributeIdsAsync`; chain-aware ResolveAttributes no change needed |
| `Infrastructure/ExternalServices/Foursquare/FoursquarePlaceService.cs` | Modify | Remove chain mapping; populate `ProviderId` on category attributes; pass `fsqCategoryIds` to client |
| `Infrastructure/ExternalServices/Foursquare/IFoursquareApiClient.cs` | Modify | Add `fsqCategoryIds` param |
| `Infrastructure/ExternalServices/Foursquare/FoursquareApiClient.cs` | Modify | Build URL with `categories` param when `fsqCategoryIds` provided |
| `Infrastructure/Configurations/PlaceAttributeConfiguration.cs` | Modify | Add `ProviderId` column (nullable, max 100) |
| `API/Configurations/AutoMapperProfile.cs` | Modify | Map `ProviderId` from `PlaceAttribute` |
| `tests/.../SearchPlacesHandlerTests.cs` | Modify | New tests for fallback, dedup, cold start, null query |
| `tests/.../PlaceRepositoryTests.cs` | Modify | Tests for null-query safety, `GetProviderAttributeIdsAsync`, fix chain test |
| `tests/.../FoursquarePlaceServiceTests.cs` | Modify | Remove chain test; add `ProviderId` verification |
| `tests/.../SearchPlacesRequestValidatorTests.cs` | Modify | Tests for new validation rules |
| `tests/.../PlaceAttributeTests.cs` | Modify | Test `ProviderId` constructor |

## Dedup Merge Algorithm

```
For each externalPlace:
  local = repo.GetByProviderReferenceId(externalPlace.ProviderReferenceId)
  if local == null:
    insert externalPlace as new
  else:
    // External wins for basic fields
    local.Name = externalPlace.Name
    local.Location = externalPlace.Location
    local.OpeningHours = externalPlace.OpeningHours
    local.Attributes = externalPlace.Attributes  (with ProviderId)
    local.IsFamilyFriendly = externalPlace.IsFamilyFriendly
    local.TypicalDurationMinutes = externalPlace.TypicalDurationMinutes
    local.IsIndoor = externalPlace.IsIndoor

    // Local enrichment fields are PRESERVED (never overwritten):
    // FamilyFriendlyScore, Popularity, IsEnriched
    // IsAutoUpdateEnabled
```

## Testing Strategy

| Layer | What | Approach |
|-------|------|----------|
| Unit | PlaceAttribute ProviderId | Constructor with/without providerId, null-safety |
| Unit | Handler: fallback flow | Mock: local < maxResults calls external; local >= maxResults skips; FetchFromExternal=false skips |
| Unit | Handler: cold start | Mock: GetProviderAttributeIdsAsync returns empty → no external call |
| Unit | Handler: dedup merge | Mock: local+external with same ProviderReferenceId → verify enrichment fields preserved |
| Unit | Handler: null query | Mock: query=null + category set → searches by category |
| Unit | Validator: at-least-one-input | Empty query + null category + no filters → fails; empty query + category → passes |
| Unit | Repo: null query safety | SearchAsync(null, cityCode) → no NRE |
| Unit | Repo: GetProviderAttributeIdsAsync | Seed attributes with ProviderId, verify resolved |
| Unit | FoursquarePlaceService: ProviderId | Category attribute has ProviderId = FsqCategoryId |
| Unit | FoursquarePlaceService: no chains | Chain attributes no longer created |
| Unit | FoursquareApiClient: categories param | URL includes categories=10000,10024 |
| Integration | Full search flow | InMemory DB: category browse triggers external → merge → response |

## Migration / Rollout

1. Add `ProviderId` column to `PlaceAttributes` table (nullable `character varying(100)`)
2. No backfill needed — `ProviderId` populated gradually as places refresh
3. Existing `chain` attributes remain in DB (not removed) but no new ones created
4. Deployment: code + migration in same PR; backward compatible (query remains optional in request model already)

## Open Questions

- None — all decisions mapped from proposal and user input.
