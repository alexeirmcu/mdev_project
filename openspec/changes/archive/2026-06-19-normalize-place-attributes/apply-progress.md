# Apply Progress: Normalize Place Attributes

**Delivery Strategy**: single-pr-default (size:exception accepted by user)
**Date**: 2026-06-19

## Summary

All 14 tasks implemented. 292 tests pass (287 original + 5 new). Build succeeds with 0 errors.

## Completed Tasks

### Domain
- [x] **TASK-01** Promote `PlaceAttribute` from ValueObject to Entity — inherits `Entity`, has `long Id`, immutable `Provider/Key/Value`, validation preserved, `GetEqualityComponents` removed
- [x] **TASK-02** Add `PlacePlaceAttribute` join entity — `PlaceId` + `PlaceAttributeId` FKs
- [x] **TASK-03** Update `Place` for many-to-many — `ICollection<PlaceAttribute>`, `UpdateFromExternalProvider` accepts `ICollection`

### Infrastructure
- [x] **TASK-04** `PlaceAttributeConfiguration` + updated `PlaceConfiguration` — `HasMany...WithMany` via `PlacePlaceAttribute`, CI unique index, separate table
- [x] **TASK-05** `DbSet<PlaceAttribute>` + `DbSet<PlacePlaceAttribute>` in `PlannerDbContext`
- [x] **TASK-06** `ResolveAttributesAsync` in `PlaceRepository` — find-or-create by normalized `(Provider, Key, Value)` using case-insensitive matching
- [x] **TASK-07** Repository queries verified — `SearchAsync`, `GetManyByCityIdAsync`, `GetDistinctInterestsByCityIdAsync` work through join table

### API / Application
- [x] **TASK-08** `PlaceAttributeModel` reduced to `(Key, Value)` record; AutoMapper mapping works by convention

### Migration
- [x] **TASK-09** All 6 legacy migrations deleted; single `InitialCreate` generated with correct schema including join table and unique index

### Tests
- [x] **TASK-10** `PlaceAttributeTests` updated — identity-based equality, validation preserved, transient inequality
- [x] **TASK-11** `PlaceRepositoryTests` updated — 5 new tests: shared attribute dedup, find-or-create, distinct values
- [x] **TASK-12** `PlaceTests` updated — indexing replaced with `.First()`/`.ElementAt()`
- [x] **TASK-13** Test fixtures verified — no changes needed
- [x] **TASK-14** Mapping/handler tests updated — Provider removed from assertions/constructors

## Files Changed

| File | Action | Description |
|------|--------|-------------|
| `SmartTripPlanner.Domain/AggregatesModel/PlaceAttribute.cs` | Modified | Promoted to Entity; removed ValueObject base |
| `SmartTripPlanner.Domain/AggregatesModel/PlacePlaceAttribute.cs` | **Created** | Join entity with PlaceId + PlaceAttributeId |
| `SmartTripPlanner.Domain/AggregatesModel/Place.cs` | Modified | ICollection<PlaceAttribute>, method signature change |
| `SmartTripPlanner.Domain/ApiModels/PlaceAttributeModel.cs` | Modified | Reduced to (Key, Value) |
| `SmartTripPlanner.Domain/SmartTripPlanner.Domain.csproj` | Modified | Added InternalsVisibleTo for tests |
| `SmartTripPlanner.Infrastructure/Configurations/PlaceAttributeConfiguration.cs` | **Created** | Entity config with CI unique index |
| `SmartTripPlanner.Infrastructure/Configurations/PlaceConfiguration.cs` | Modified | Replaced OwnsMany with HasMany...WithMany |
| `SmartTripPlanner.Infrastructure/PlannerDbContext.cs` | Modified | Added DbSet<PlaceAttribute> and DbSet<PlacePlaceAttribute> |
| `SmartTripPlanner.Infrastructure/PlannerDbContextFactory.cs` | **Created** | Design-time factory for migrations |
| `SmartTripPlanner.Infrastructure/Repositories/PlaceRepository.cs` | Modified | Added ResolveAttributesAsync; updated UpsertRangeAsync |
| `SmartTripPlanner.Infrastructure/Migrations/*` | Deleted | All 6 legacy migrations removed |
| `SmartTripPlanner.Infrastructure/Migrations/20260619063606_InitialCreate.cs` | **Created** | Single migration with full schema |
| `SmartTripPlanner.Infrastructure/Migrations/PlannerDbContextModelSnapshot.cs` | **Created** | Updated model snapshot |
| `tests/.../PlaceAttributeTests.cs` | Modified | Entity equality tests |
| `tests/.../PlaceTests.cs` | Modified | LINQ indexing |
| `tests/.../PlaceRepositoryTests.cs` | Modified | 5 new tests for normalized persistence |
| `tests/.../PlaceMappingProfileTests.cs` | Modified | Removed Provider assertions |
| `tests/.../SearchPlacesHandlerTests.cs` | Modified | Updated PlaceAttributeModel constructor |
| `tests/.../FoursquarePlaceServiceTests.cs` | Modified | Replaced `[0]` with `.First()` |

## Deviations from Design

- **CI unique index**: The migration creates a standard B-tree unique index (case-sensitive at DB level). Full case-insensitive enforcement would require PostgreSQL citext extension or ICU collations. The application layer (`ResolveAttributesAsync`) handles case-insensitive matching via `ToLowerInvariant()`, preventing case-variant duplicates through normal API flow.
- **No separate `PlacePlaceAttributeConfiguration`**: The join table config is defined inline in `PlaceConfiguration.UsingEntity()` which is cleaner and the EF Core idiomatic approach.

## Issues Found

- None blocking. All 292 tests pass.

## Risks

- The CI unique index relies on application-layer normalization. A direct SQL insert bypassing the API could create case-variant duplicates. Mitigation: consider adding a case-insensitive collation index as a follow-up.

## Next Steps

Ready for verify phase (`sdd-verify`).

**Status**: 14/14 tasks complete.
