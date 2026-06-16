# Archive Report: enhance-place-search

**Status**: COMPLETE ✅
**Archived**: 2026-06-16
**Archive Path**: `openspec/changes/archive/2026-06-16-enhance-place-search/`

---

## Change Summary

Introduced a generic, provider-agnostic attribute system (`PlaceAttribute` ValueObject) for the Place entity, enabling search across external provider metadata (Foursquare categories and chains) at zero extra API cost using only Pro-tier Foursquare data.

**Why**: The existing `PlaceRepository.SearchAsync` only searched by `Name.Contains(query)`. A place named "Gran Palace" categorized as "Hotel" by Foursquare would never appear when searching "hotel" because the provider category data was discarded after heuristic mapping.

**What was done**:
- Created `PlaceAttribute` ValueObject (Provider, Key, Value) with validation and equality
- Added `Attributes` collection and `AddAttribute` to the `Place` entity
- Created `PlaceAttributeModel` and updated `PlaceModel` to expose attributes in the API
- Updated `PlaceConfiguration` with `OwnsMany` for attribute persistence
- Updated `PlaceRepository.SearchAsync` to match `Name.Contains(query)` OR `Attributes.Any(a.Value.Contains(query))`
- Updated `FoursquarePlaceService.MapToPlace` to map categories and chains to attributes
- Added EF Core migration for the `PlaceAttributes` table
- Added comprehensive test coverage (all layers)

---

## Final Test Results

| Metric | Value |
|--------|-------|
| Total tests | 107 |
| Passed | 107 |
| Failed | 0 |
| Skipped | 0 |
| Build | Passed |

---

## Acceptance Criteria

| AC | Description | Status |
|----|-------------|--------|
| AC1 | Place Construction | ✅ PASS (unchanged) |
| AC2 | OpeningHoursWindow Construction | ✅ PASS (unchanged) |
| AC3 | PlaceLocation Construction | ✅ PASS (unchanged) |
| AC4 | Repository Operations | ✅ PASS (regression preserved) |
| AC5 | Foursquare API Client | ✅ PASS (unchanged) |
| AC6 | Category Heuristics | ✅ PASS (unchanged) |
| AC7 | Cascade Search | ✅ PASS (unchanged) |
| AC8 | PlaceAttribute ValueObject | ✅ PASS |
| AC9 | Place Attributes Collection | ✅ PASS |
| AC10 | Attribute Search (incl. case-insensitive, chain search) | ✅ PASS |
| AC11 | Foursquare Category Mapping | ✅ PASS |
| AC12 | PlaceModel Attributes | ✅ PASS |
| AC13 | Attribute Persistence (EF Core round-trip) | ✅ PASS |

All 13 acceptance criteria pass.

---

## Files Created

| File | Layer |
|------|-------|
| `SmartTripPlanner.Domain/AggregatesModel/PlaceAttribute.cs` | Domain — New ValueObject |
| `SmartTripPlanner.Domain/ApiModels/PlaceAttributeModel.cs` | Domain — New API model record |
| `Migrations/{timestamp}_AddPlaceAttributes.cs` | Infrastructure — EF migration |
| `Migrations/{timestamp}_AddPlaceAttributes.Designer.cs` | Infrastructure — Migration designer |
| `tests/SmartTripPlanner.Tests/Domain/PlaceAttributeTests.cs` | Tests — Unit tests |
| `tests/SmartTripPlanner.Tests/Domain/PlaceTests.cs` (new scenarios) | Tests — Unit tests |

## Files Modified

| File | Change |
|------|--------|
| `SmartTripPlanner.Domain/AggregatesModel/Place.cs` | Added `Attributes` collection + `AddAttribute` method |
| `SmartTripPlanner.Domain/ApiModels/PlaceModel.cs` | Added `Attributes` parameter |
| `SmartTripPlanner.Infrastructure/Configurations/PlaceConfiguration.cs` | Added `OwnsMany` for `PlaceAttribute` |
| `SmartTripPlanner.Infrastructure/Repositories/PlaceRepository.cs` | Extended `SearchAsync` with attribute search |
| `SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/FoursquarePlaceService.cs` | Mapped categories and chains to attributes |
| `SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/Models/FoursquarePlace.cs` | Added `Chains` collection |
| `API/Configurations/AutoMapperProfile.cs` | Added `PlaceAttribute` → `PlaceAttributeModel` mapping |
| `tests/SmartTripPlanner.Tests/Infrastructure/PlaceRepositoryTests.cs` | Added attribute search scenarios |
| `tests/SmartTripPlanner.Tests/Infrastructure/FoursquarePlaceServiceTests.cs` | Added attribute mapping tests |
| `tests/SmartTripPlanner.Tests/Mapping/PlaceMappingProfileTests.cs` | Added attribute mapping assertion |
| `tests/SmartTripPlanner.Tests/Helpers/PlaceFixture.cs` | Added attributes to populated places |
| `tests/SmartTripPlanner.Tests/ApplicationServices/SearchPlacesHandlerTests.cs` | Added end-to-end attribute search tests |

---

## Specs Synced

| Domain | Action | Details |
|--------|--------|---------|
| `place-attributes` | **Created** (new) | Full spec created at `openspec/specs/place-attributes/spec.md` |
| `place` | **Updated** | FR1 (Attributes collection), FR5 (attribute search + 3 new scenarios), FR11 (category/chain mapping + 1 new scenario), FR12 (PlaceModel Attributes), FR13 (EF Core config) added; AC8-AC13 added |

---

## Key Architectural Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| PlaceAttribute identity | ValueObject (no Id) | Follows existing `OpeningHoursWindow` pattern; equality by (Provider, Key, Value) is correct semantics |
| Attribute key naming | Short keys (`"category"`, `"chain"`) | Provider field in separate column disambiguates; namespaced keys (`"foursquare.category"`) add coupling |
| Search implementation | LINQ `Any(a => a.Value.Contains(query))` | EF Core translates to SQL JOIN; composite index `(PlaceId, Value)` mitigates table scan |
| Case-insensitive search | `EF.Functions.Like()` | InMemory provider is case-sensitive for `Contains()`; `Like()` is consistently case-insensitive across all providers |
| Chain mapping | `PlaceAttribute("foursquare", "chain", name)` | Spec includes chain search scenario; Foursquare Pro-tier exposes chain labels |

---

## Lessons Learned

1. **EF Core InMemory vs production providers**: The InMemory database provider uses case-sensitive string comparison for `Contains()`, while SQLite and SQL Server are case-insensitive by default. Solution: use `EF.Functions.Like()` which behaves consistently across all providers.

2. **Foursquare API Pro-tier data**: The Foursquare Places API returns `chains[{id, name}]` in Pro tier by default — we initially missed it because the `FoursquarePlace` model didn't expose it. The model had to be updated to capture chain data before it could be mapped to attributes.

3. **Additive-only migrations**: The `PlaceAttributes` table via `OwnsMany` is purely additive — existing Places get empty collections automatically. No data migration needed.

4. **Composite index placement**: The `(PlaceId, Value)` composite index on `PlaceAttributes` was added during configuration to support the `WHERE` clause in `SearchAsync` without full table scans.

---

## Verification History

- **v1** (initial): 103/107 passing — 4 tests failing due to chain mapping gap, case-insensitive search, and missing round-trip persistence test
- **v2** (final): 107/107 passing — all gaps closed: chain model added, `EF.Functions.Like()` for case-insensitive, round-trip persistence verified

---

## Source of Truth Updated

- `openspec/specs/place/spec.md` — Now reflects attribute search, PlaceModel attributes, and Foursquare category mapping
- `openspec/specs/place-attributes/spec.md` — New spec for the provider-agnostic attribute capability

---

## SDD Cycle Complete

The change has been fully planned, implemented, verified, and archived. Ready for the next change.
