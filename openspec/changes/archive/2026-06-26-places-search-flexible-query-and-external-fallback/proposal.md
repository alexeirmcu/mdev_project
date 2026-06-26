# Proposal: Flexible Query & External Fallback for Place Search

## Intent

Make place search flexible enough for browse-style queries (no text) and refresh-style queries (pull external data). Currently, `query` is required and external fallback is hardcoded — users can't search by category alone or control whether the system hits Foursquare when local results exist.

## Scope

### In Scope
- **Query optional**: `query` nullable, guard: at least one of query/category/filters must be provided
- **External fallback parameter**: `FetchFromExternalIfInsufficient` (bool?, default true) — only calls external when `localCount < maxResults`
- **Deduplication by `ProviderReferenceId`**: external wins for basic fields (name, location, hours, categories); preserves LLM enrichment fields (`FamilyFriendlyScore`, `Popularity`, `IsEnriched`, `IsIndoor`, duration estimates)
- **`ProviderId` on `PlaceAttribute`**: nullable, filled over time as places refresh
- **Category filter push**: resolve category name → `fsq_category_ids` from local DB, send to Foursquare API
- **Chain removal**: stop persisting chain attributes
- **Cold start**: if no `PlaceAttribute` matches category, skip external call, return local results

### Out of Scope
- Rate-limit handling (learning project, not critical)
- Timeout configuration (use HttpClient default 100s)
- Multiple external providers
- Backfill of `ProviderId` for existing attributes
- Foursquare premium/Pro-tier-only fields

## Capabilities

### New Capabilities
- `external-fallback-dedup`: Deduplication logic — `ProviderReferenceId` match, field merge preserving LLM enrichment
- `foursquare-category-filter`: Resolve categories to `fsq_category_ids`, push to Foursquare API

### Modified Capabilities
- `api-places-search`: Query becomes optional, add `category` and `FetchFromExternalIfInsufficient` to request schema
- `place-search-handler`: Accept `FetchFromExternalIfInsufficient`, delegate category resolution + external fallback
- `place`: Add `ProviderId` to `PlaceAttribute`, remove chain attributes, update `UpsertRangeAsync` dedup logic
- `place-attributes`: Add `ProviderId` field to spec, remove chain-related behavior

## Approach

**Search flow**: validate at least one input (query/category/filters). Query local DB; if `localCount < maxResults` AND `FetchFromExternalIfInsufficient` is true (default), call Foursquare with resolved `fsq_category_ids`. Merge external results by `ProviderReferenceId` — external fields overwrite basic fields, enrichment fields are kept.

**Dedup**: incoming external `Place` with `ProviderReferenceId` → match existing local. If found, update basic fields only (name, location, hours, categories); skip `FamilyFriendlyScore`, `Popularity`, `IsEnriched`, `IsIndoor`, `TypicalDurationMinutes`. If not found, insert new.

**`ProviderId`**: added to `PlaceAttribute` entity (nullable `string`). Example: `Provider="foursquare"`, `ProviderId="10000"`, `Key="category"`, `Value="Museum"`. Populated during dedup/upsert.

**Category filter**: resolve category name to `fsq_category_ids` from local `PlaceAttribute` values. Send to Foursquare API. Cold start: no match → skip external call.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Domain/Entities/PlaceAttribute.cs` | Modified | Add nullable `ProviderId` (string) |
| `Domain/Repository/IPlaceRepository.cs` | Modified | Add `FetchFromExternalIfInsufficient` to `SearchAsync` |
| `ApplicationServices/Handlers/SearchPlacesHandler.cs` | Modified | Pass new params, category resolution |
| `API/Controllers/PlacesController.cs` | Modified | Validate at least one input, new request fields |
| `API/Models/PlaceSearchRequest.cs` | Modified | Query nullable, add category/filters/fallback fields |
| `Infrastructure/Repositories/PlaceRepository.cs` | Modified | New cascade + dedup logic, chain removal |
| `Infrastructure/ExternalServices/Foursquare/FoursquarePlaceService.cs` | Modified | Category filter push, dedup-aware mapping |
| `Infrastructure/Configurations/PlaceConfiguration.cs` | Modified | `ProviderId` column |
| `openspec/specs/place-attributes/spec.md` | Modified | Add `ProviderId` requirement |
| `openspec/specs/api-places-search/spec.md` | Modified | Query optional, new fields |
| `openspec/specs/place-search-handler/spec.md` | Modified | External fallback parameter |
| `openspec/specs/place/spec.md` | Modified | Dedup logic, chain removal, `ProviderId` |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| External rate limits (learning project) | Low | Document as risk; no hard block |
| Cold start: no attributes → no external | Low | Expected behavior, not an error |
| Dedup overwrites human-curated data | Low | Enrichment fields always preserved; external only touches basic fields |
| Http timeout on Foursquare API | Low | HttpClient default 100s; existing `HttpRequestException` catch handles it |

## Rollback Plan

- Remove `ProviderId` from `PlaceAttribute` + revert migration
- Restore chain attribute persistence in `FoursquarePlaceService`
- Revert `SearchAsync` signature to original (no `FetchFromExternalIfInsufficient`)
- Revert `PlaceSearchRequest` validation — `query` required again
- Restore old specs from previous change archive

## Dependencies

- `IFoursquareApiClient` with `fsq_category_ids` parameter support
- `IPlaceRepository.SearchAsync` signature change

## Success Criteria

- [ ] Search with `query=null` + `category="Museum"` returns places (local + external)
- [ ] External results deduplicated by `ProviderReferenceId`, enrichment fields preserved
- [ ] `ProviderId` populated on attributes from Foursquare results
- [ ] Chain attributes no longer persisted
- [ ] Cold start with unknown category returns local results only (no external call)
- [ ] All existing tests pass
