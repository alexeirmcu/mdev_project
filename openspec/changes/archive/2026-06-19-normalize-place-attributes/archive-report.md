# Archive Report: normalize-place-attributes

**Change**: normalize-place-attributes
**Date**: 2026-06-19
**Archiver**: sdd-archive (v2.0)
**Mode**: openspec
**Final Status**: COMPLETE

---

## Change Summary

Promoted `PlaceAttribute` from a `ValueObject` (owned via `OwnsMany`) to a standalone `Entity` with identity-based equality. Introduced a many-to-many join table `PlacePlaceAttributes` so multiple places share the same attribute row. Trimmed the API model to expose only `Key` and `Value`. Consolidated all migrations into a single `InitialCreate`.

## What Was Accomplished

### Key Deliverables

| Area | Deliverable |
|------|-------------|
| **Domain** | `PlaceAttribute` inherits from `Entity` with `long Id`, identity-based equality, immutable `Provider/Key/Value` |
| **Domain** | `PlacePlaceAttribute` join entity with `PlaceId` and `PlaceAttributeId` FKs |
| **Domain** | `Place` updated to `ICollection<PlaceAttribute>` via many-to-many relationship |
| **Domain** | `PlaceAttributeModel` reduced to `record PlaceAttributeModel(string Key, string Value)` |
| **Infrastructure** | `PlaceAttributeConfiguration` with case-insensitive unique index on `(Provider, Key, Value)` |
| **Infrastructure** | `PlaceConfiguration` updated from `OwnsMany` to `HasMany...WithMany().UsingEntity<PlacePlaceAttribute>()` |
| **Infrastructure** | `PlannerDbContext` registers `DbSet<PlaceAttribute>` and `DbSet<PlacePlaceAttribute>` |
| **Infrastructure** | `PlaceRepository.ResolveAttributesAsync` with find-or-create by normalized `(Provider, Key, Value)` |
| **Infrastructure** | `PlaceRepository` queries updated to work through join table (SearchAsync, GetManyByCityIdAsync, GetCandidatesByCityAndInterestsAsync, GetDistinctInterestsByCityIdAsync) |
| **Infrastructure** | Single `20260619063606_InitialCreate` migration replacing 6 legacy migrations |
| **Tests** | 14 tasks completed; 292/292 tests passing; 0 build errors |

## Files Changed

### New Files
- `SmartTripPlanner.Domain/AggregatesModel/PlacePlaceAttribute.cs` — join entity
- `SmartTripPlanner.Infrastructure/Configurations/PlaceAttributeConfiguration.cs` — entity config + CI unique index
- `SmartTripPlanner.Infrastructure/PlannerDbContextFactory.cs` — design-time migration factory
- `SmartTripPlanner.Infrastructure/Migrations/20260619063606_InitialCreate.cs` — consolidated migration
- `SmartTripPlanner.Infrastructure/Migrations/PlannerDbContextModelSnapshot.cs` — updated snapshot

### Modified Files
- `SmartTripPlanner.Domain/AggregatesModel/PlaceAttribute.cs` — promoted to Entity
- `SmartTripPlanner.Domain/AggregatesModel/Place.cs` — ICollection, method signatures
- `SmartTripPlanner.Domain/ApiModels/PlaceAttributeModel.cs` — reduced to (Key, Value)
- `SmartTripPlanner.Domain/SmartTripPlanner.Domain.csproj` — InternalsVisibleTo
- `SmartTripPlanner.Infrastructure/Configurations/PlaceConfiguration.cs` — OwnsMany → HasMany...WithMany
- `SmartTripPlanner.Infrastructure/PlannerDbContext.cs` — added DbSets
- `SmartTripPlanner.Infrastructure/Repositories/PlaceRepository.cs` — ResolveAttributesAsync, updated queries
- `tests/.../PlaceAttributeTests.cs` — identity-based equality
- `tests/.../PlaceTests.cs` — LINQ indexing
- `tests/.../PlaceRepositoryTests.cs` — 5 new tests for normalized persistence
- `tests/.../PlaceMappingProfileTests.cs` — removed Provider assertions
- `tests/.../SearchPlacesHandlerTests.cs` — updated constructor calls
- `tests/.../FoursquarePlaceServiceTests.cs` — `.First()` instead of `[0]`

### Deleted Files
- `SmartTripPlanner.Infrastructure/Migrations/*` — 6 legacy migration files removed

## Decisions Made

| Decision | Choice | Rationale |
|----------|--------|-----------|
| PlaceAttribute identity | `Entity` with `long Id` | Shared rows require identity; eliminates duplication |
| Relationship type | Explicit many-to-many via `PlacePlaceAttributes` | Gives FK indexes and query control |
| Uniqueness | Case-insensitive unique index on `(Provider, Key, Value)` | Prevents "museum" vs "Museum" duplicates |
| Orphan handling | Keep orphans (no cascade delete) | Orphans stay as catalog |
| Immutability | Private setters, no mutation | Values never change once created |
| API model | `PlaceAttributeModel(Key, Value)` only | API consumers only need Key+Value |
| Migration strategy | Delete all, single `InitialCreate` | Project early-stage, no production data |

## Specs Synced

| Domain | Action | Details |
|--------|--------|---------|
| place-attributes | Updated (FR1 modified, FR2-3 added, FR2 removed) | Entity semantics, unique constraint, shared entity; old ValueObject FR removed (moved to place) |
| place | Updated (FR1, FR4, FR5, FR12, FR13, FR14 added) | HasMany-through-join-table, find-or-create, trimmed model, migration cleanup |
| trip-interests | Updated | Interest filtering via join table |
| city-interests-endpoint | Updated | Distinct values queried through join table |

## Issues Encountered and Resolution

1. **Duplicate key violation (23505) in `ResolveAttributesAsync`** — Fixed by adding a `seen` dictionary for batch dedup + `_context.PlaceAttributes.Local` fallback for unsaved entities.
2. **CI unique constraint not at DB level** — Fixed by replacing standard `CreateIndex` with raw SQL: `CREATE UNIQUE INDEX ... ON (LOWER(Provider), LOWER(Key), LOWER(Value))` — a true PostgreSQL functional unique index.
3. **Case-sensitive interest filtering** — Fixed by changing `interests.Contains(a.Value)` to `lowerInterests.Contains(a.Value.ToLower())` for case-insensitive matching.

## Verification

| Metric | Result |
|--------|--------|
| Build | **PASS** — 0 errors, 0 warnings |
| Tests | **PASS** — 292/292 passed (0 failed, 0 skipped) |
| Spec compliance | **PASS** — All 4 domain specs verified against implementation |
| Design coherence | **PASS** — All design decisions implemented and confirmed |
| Final verdict | **PASS** — Ready for archival |

## Archive Contents

- `proposal.md` ✅
- `design.md` ✅
- `specs/` ✅ (4 domains: place-attributes, place, trip-interests, city-interests-endpoint)
- `tasks.md` ✅ (14/14 tasks complete)
- `apply-progress.md` ✅
- `verify-report.md` ✅ (PASS — no CRITICAL issues)
- `archive-report.md` ✅ (this file)

## SDD Cycle Complete

The change has been fully planned, implemented, verified, and archived. Delta specs have been synced into main specs at `openspec/specs/{domain}/spec.md`. The source of truth is updated and reflects the new normalized Place Attribute behavior.
