# Proposal: Enhance Place Search

## Intent

Current `PlaceRepository.SearchAsync` only searches by `Name.Contains(query)`. A place named "Gran Palace" categorized as "Hotel" by Foursquare won't appear when searching "hotel" because provider category data is discarded after heuristics. We need to persist and search across provider metadata (categories, chains, location) at zero extra cost using only Pro-tier Foursquare data.

## Scope

### In Scope
- `PlaceAttribute` value object (provider-agnostic key-value)
- `Place.Attributes` collection
- `PlaceRepository.SearchAsync` updated to search attributes
- `FoursquarePlaceService` mapping updated to capture categories
- EF Core `PlaceConfiguration` for owned attributes
- `PlaceModel` and AutoMapper mapping updated
- xUnit tests for attribute search

### Out of Scope
- Premium Foursquare fields (tastes, rating, photos, etc.)
- External API query changes (cascade stays as-is)
- UI/API controller changes
- Other providers (Google, etc.) beyond generic abstraction

## Capabilities

### New Capabilities
- `place-attributes`: Generic provider-agnostic key-value attribute system for Place entities, enabling search across external provider metadata

### Modified Capabilities
- `place`: Place entity gains `Attributes` collection; EF config updated to own `PlaceAttribute`; Foursquare mapping populates attributes on creation

## Approach

Introduce `PlaceAttribute` as a `ValueObject` with `Provider` (string), `Key` (string), and `Value` (string). Add it to `Place` as an owned collection via `OwnsMany`. Update `PlaceRepository.SearchAsync` to match `Name.Contains(query)` OR any `Attribute.Value.Contains(query)`. Update `FoursquarePlaceService.MapToPlace` to iterate `apiPlace.Categories` and emit `AddAttribute(new PlaceAttribute("foursquare", "category", cat.Name))`. This is generic enough for future providers (Google, Yelp) without Foursquare-specific code in the domain.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `SmartTripPlanner.Domain/AggregatesModel/PlaceAttribute.cs` | New | ValueObject for provider-agnostic attributes |
| `SmartTripPlanner.Domain/AggregatesModel/Place.cs` | Modified | Add `List<PlaceAttribute> Attributes` and `AddAttribute` method |
| `SmartTripPlanner.Domain/ApiModels/PlaceModel.cs` | Modified | Add `IReadOnlyList<PlaceAttributeModel> Attributes` |
| `SmartTripPlanner.Infrastructure/Repositories/PlaceRepository.cs` | Modified | Update `SearchAsync` to include attribute search |
| `SmartTripPlanner.Infrastructure/Configurations/PlaceConfiguration.cs` | Modified | Add `OwnsMany` for `PlaceAttribute` with separate table |
| `SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/FoursquarePlaceService.cs` | Modified | Map categories into `Place.Attributes` |
| `SmartTripPlanner.ApplicationServices/Mapping/PlaceMappingProfile.cs` | Modified | Map `PlaceAttribute` → `PlaceAttributeModel` |
| `tests/SmartTripPlanner.Tests/Domain/PlaceAttributeTests.cs` | New | Construction, equality, validation tests |
| `tests/SmartTripPlanner.Tests/Infrastructure/PlaceRepositoryTests.cs` | Modified | Attribute search scenario tests |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| `Contains` on attributes causes full table scan | Med | Add composite index on `PlaceAttribute.Value` with `PlaceId`; benchmark before merge |
| Existing seeded data has no attributes | Med | Backfill script not needed — external cascade still works; attributes populate on new API fetches |
| Migration complexity with owned collection | Low | Use EF Core `OwnsMany` → separate table; migration is additive only |

## Rollback Plan

1. Revert `PlaceRepository.SearchAsync` to `Name.Contains` only.
2. Drop `PlaceAttribute` table via EF Core migration rollback.
3. Revert `Place` entity constructor and `FoursquarePlaceService` mapping.
4. No breaking API changes — contract remains identical.

## Dependencies

- EF Core `OwnsMany` support (available in current EF8 setup)
- Existing `FoursquareCategory` model already exposes `Name` — no API schema changes

## Success Criteria

- [ ] Searching "hotel" returns places whose Foursquare category is "Hotel" even if name doesn't match
- [ ] All existing `place` spec tests pass unchanged
- [ ] New `PlaceAttribute` unit tests cover construction, equality, and validation
- [ ] `PlaceRepository` tests verify attribute search works with seeded data
- [ ] AutoMapper maps `PlaceAttribute` correctly in `PlaceModel`
- [ ] No premium Foursquare fields introduced
- [ ] Build and test command pass: `dotnet test`
