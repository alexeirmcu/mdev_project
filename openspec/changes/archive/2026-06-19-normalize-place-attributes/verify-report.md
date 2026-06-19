# Verification Report: normalize-place-attributes

**Change**: normalize-place-attributes
**Mode**: openspec (file persistence)
**Date**: 2026-06-19
**Verifier**: sdd-verify (automated) + manual remediation

---

## 1. Completeness Table

| Artifact | Exists | Status |
|----------|--------|--------|
| Proposal | ✅ | Read and verified |
| Design | ✅ | Read and verified |
| Specs (4) | ✅ | place-attributes, place, trip-interests, city-interests-endpoint |
| Tasks (14) | ✅ | All 14 tasks marked [x] complete |

## 2. Build Evidence

| Command | Result | Details |
|---------|--------|---------|
| `dotnet build SmartTripPlanner.slnx --no-restore` | **PASS** | 0 errors, 0 warnings |
| `dotnet test SmartTripPlanner.slnx --no-build` | **PASS** | 292/292 tests passed |

## 3. Spec Compliance Matrix

### place-attributes

| Spec Requirement | Status | Evidence |
|------------------|--------|----------|
| PlaceAttribute inherits from Entity with `long Id` | ✅ COMPLIANT | `PlaceAttribute : Entity`; `Entity` has `virtual long Id` |
| Provider, Key, Value are immutable (no public setters) | ✅ COMPLIANT | `private set`; no mutation methods exist. |
| Case-insensitive unique constraint on (Provider, Key, Value) | ✅ COMPLIANT | PostgreSQL functional unique index: `CREATE UNIQUE INDEX ... ON (LOWER(Provider), LOWER(Key), LOWER(Value))` |
| PlaceAttribute is shared entity (not owned) | ✅ COMPLIANT | `HasMany...WithMany().UsingEntity<PlacePlaceAttribute>()` with separate `DbSet<PlaceAttribute>` |
| Orphaned attributes retained (no cascade delete) | ✅ COMPLIANT | FK from `PlacePlaceAttributes` to `PlaceAttributes` uses CASCADE on join row only; PlaceAttribute row survives |
| Null/empty validation preserved | ✅ COMPLIANT | Constructor throws `SmartTripDomainException` for null/empty Provider, Key, Value |
| Identity-based equality (Entity.Equals) | ✅ COMPLIANT | Transient objects (Id=0) are NOT equal by `Entity.Equals`; same-Id objects ARE equal |

### place

| Spec Requirement | Status | Evidence |
|------------------|--------|----------|
| Place.Attributes is ICollection<PlaceAttribute> via many-to-many | ✅ COMPLIANT | `ICollection<PlaceAttribute> Attributes` with `HasMany...WithMany()` join table |
| AddAttribute(PlaceAttribute) with null check | ✅ COMPLIANT | `Attributes.Add(attribute ?? throw new SmartTripDomainException(...))` |
| PlaceConfiguration uses HasMany...WithMany via PlacePlaceAttribute | ✅ COMPLIANT | Configured inline in `PlaceConfiguration` using `UsingEntity<PlacePlaceAttribute>()` |
| UpsertRangeAsync finds existing attribute and links | ✅ COMPLIANT | `ResolveAttributesAsync` queries DB + falls back to `Local` for unsaved entities |
| UpsertRangeAsync creates new attribute when not found | ✅ COMPLIANT | Falls through to `new PlaceAttribute(...)` + `_context.PlaceAttributes.Add(created)` |
| SearchAsync matches via attribute value through join table | ✅ COMPLIANT | `.Include(p => p.Attributes).Where(...)` with `ToLower()` for case-insensitive search |
| Interest filtering via join table in SQL | ✅ COMPLIANT | `GetManyByCityIdAsync` uses case-insensitive comparison via `lowerInterests.Contains(a.Value.ToLower())` |
| Distinct attribute values via join table in SQL | ✅ COMPLIANT | `GetDistinctInterestsByCityIdAsync` uses `SelectMany().Distinct()` — SQL-translatable |
| PlaceAttributeModel exposes only Key and Value | ✅ COMPLIANT | `public record PlaceAttributeModel(string Key, string Value);` |
| AutoMapper maps PlaceAttribute → PlaceAttributeModel (Key+Value only) | ✅ COMPLIANT | Convention-based mapping ignores Provider since target has no Provider property |
| Single InitialCreate migration | ✅ COMPLIANT | Only `20260619063606_InitialCreate.cs` exists with snapshot |
| PlacePlaceAttributes join table with composite PK | ✅ COMPLIANT | Migration: `PK_PlacePlaceAttributes(PlaceId, PlaceAttributeId)` with FK indexes |

### trip-interests

| Spec Requirement | Status | Evidence |
|------------------|--------|----------|
| Interest filtering via join table (no in-memory filtering) | ✅ COMPLIANT | Uses `.Include(p => p.Attributes).Where(p => p.Attributes.Any(...))` — SQL-translatable |
| Interests are case-insensitive | ✅ COMPLIANT | `lowerInterests.Contains(a.Value.ToLower())` normalizes both sides to lowercase |
| Interfaces preserve backward compatibility | ✅ COMPLIANT | `GetManyByCityIdAsync` has optional `interests` param defaulting to null |

### city-interests-endpoint

| Spec Requirement | Status | Evidence |
|------------------|--------|----------|
| GET /api/cities/{cityCode}/interests exists | ✅ COMPLIANT | `CitiesController` with `[HttpGet("{cityCode}/interests")]` |
| Delegates to GetDistinctInterestsByCityIdAsync via join | ✅ COMPLIANT | Handler resolves city by code, then calls `placeRepository.GetDistinctInterestsByCityIdAsync(city.Id)` |
| City not found throws CityNotFoundException (→ 404) | ✅ COMPLIANT | Handler throws `CityNotFoundException` for null city |
| Returns interests as JSON array | ✅ COMPLIANT | Returns `IReadOnlyList<string>` |

## 4. Task Completeness

| Task | Status | Evidence |
|------|--------|----------|
| TASK-01 (Promote PlaceAttribute to Entity) | ✅ COMPLETE | Inherits `Entity`, `Id` from base, private setters, validation preserved |
| TASK-02 (Add PlacePlaceAttribute join entity) | ✅ COMPLETE | `PlacePlaceAttribute.cs` with `PlaceId` + `PlaceAttributeId` FKs |
| TASK-03 (Update Place for many-to-many) | ✅ COMPLETE | `ICollection<PlaceAttribute>`, `AddAttribute`, `UpdateFromExternalProvider` accepts `ICollection` |
| TASK-04 (PlaceAttributeConfiguration + update PlaceConfiguration) | ✅ COMPLETE | Separate config with unique index; `HasMany...WithMany` in PlaceConfiguration |
| TASK-05 (Add DbSets) | ✅ COMPLETE | `DbSet<PlaceAttribute>` and `DbSet<PlacePlaceAttribute>` in PlannerDbContext |
| TASK-06 (Find-or-create in UpsertRangeAsync) | ✅ COMPLETE | `ResolveAttributesAsync` with DB query + `Local` fallback + batch dedup dictionary |
| TASK-07 (Update repository queries) | ✅ COMPLETE | `SearchAsync`, `GetManyByCityIdAsync`, `GetDistinctInterestsByCityIdAsync` all use `.Include(p => p.Attributes)` |
| TASK-08 (Reduce PlaceAttributeModel) | ✅ COMPLETE | `record PlaceAttributeModel(string Key, string Value)` |
| TASK-09 (Delete migrations, create InitialCreate) | ✅ COMPLETE | Single `20260619063606_InitialCreate.cs` with full schema |
| TASK-10 (Update PlaceAttributeTests) | ✅ COMPLETE | Entity equality, validation, identity tests |
| TASK-11 (Update PlaceRepositoryTests) | ✅ COMPLETE | 5 new tests: shared attribute dedup, find-or-create, distinct values |
| TASK-12 (Update PlaceTests) | ✅ COMPLETE | Uses `.First()`/`.ElementAt()` for collection |
| TASK-13 (Update test fixtures) | ✅ COMPLETE | `PlaceFixture` creates `PlaceAttribute` with 3-arg constructor |
| TASK-14 (Update mapping/handler tests) | ✅ COMPLETE | `PlaceMappingProfileTests` asserts Key+Value, no Provider |

## 5. Correctness Table

| Dimension | Status | Notes |
|-----------|--------|-------|
| Behavioral correctness | ✅ | All 292 tests pass; domain logic preserved |
| Data model correctness | ✅ | PlaceAttribute as shared Entity; join table; single InitialCreate |
| API contract correctness | ✅ | Interest filtering case-insensitive; API returns Key+Value only |
| Migration correctness | ✅ | InitialCreate creates all tables with correct FKs, unique indexes, and seed data |

## 6. Design Coherence

| Design Decision | Implementation | Compliant? |
|-----------------|----------------|------------|
| `Entity` with `long Id` for PlaceAttribute | `PlaceAttribute : Entity` | ✅ |
| Many-to-many via explicit join entity | `UsingEntity<PlacePlaceAttribute>()` | ✅ |
| Case-insensitive unique index at DB level | PostgreSQL functional index `LOWER(Provider), LOWER(Key), LOWER(Value)` | ✅ |
| Keep orphans (no cascade delete on PlaceAttribute) | FK uses CASCADE on join deletion only; no deletion path for PlaceAttribute | ✅ |
| Immutability via private setters | `private set` on Provider/Key/Value | ✅ |
| API model exposes only (Key, Value) | `record PlaceAttributeModel(string Key, string Value)` | ✅ |
| Single InitialCreate migration | Only one migration file exists | ✅ |
| No separate PlacePlaceAttributeConfiguration | Inline in PlaceConfiguration via UsingEntity | ✅ |

## 7. Issues

### CRITICAL — ALL RESOLVED

1. ~~CI unique constraint not enforced at DB level~~ → **FIXED**: Migration now uses `CREATE UNIQUE INDEX ... ON (LOWER(Provider), LOWER(Key), LOWER(Value))` which is a true PostgreSQL functional unique index.

2. ~~Interest filtering is case-sensitive in `GetManyByCityIdAsync`~~ → **FIXED**: Now uses `lowerInterests.Contains(a.Value.ToLower())` for case-insensitive matching, consistent with `SearchAsync`.

### WARNING — NONE REMAIN

### SUGGESTION

3. **Consider adding a test for case-insensitive interest filtering**: No test currently verifies that `GetManyByCityIdAsync` with interests=["museum"] matches `PlaceAttribute.Value="Museum"`. This was verified manually but not automated.

## 8. Test Results

```
292 tests total
292 passed
  0 failed
  0 skipped
  0 errors
Build: 0 warnings, 0 errors
```

Key test coverage:
- `PlaceAttributeTests`: 9 tests — constructor, validation, identity equality ✅
- `PlaceTests`: 7 tests — AddAttribute, null check, collection ✅
- `PlaceRepositoryTests`: 22 tests — search, attributes, upsert find-or-create, distinct interests ✅
- `PlaceMappingProfileTests`: 5 tests — attribute mapping (Key+Value only) ✅

## 9. Remediation Log

| Issue | Fix Applied | Date |
|-------|-------------|------|
| Duplicate key violation (23505) in ResolveAttributesAsync | Added `seen` dictionary for batch dedup + `_context.PlaceAttributes.Local` fallback for unsaved entities | 2026-06-19 |
| Case-insensitive unique index not at DB level | Replaced standard `CreateIndex` with raw SQL: `CREATE UNIQUE INDEX ... ON (LOWER(...), LOWER(...), LOWER(...))` | 2026-06-19 |
| Case-sensitive interest filtering | Changed `interests.Contains(a.Value)` to `lowerInterests.Contains(a.Value.ToLower())` | 2026-06-19 |

## 10. Final Verdict

**PASS**

All 14 tasks complete, all specs satisfied, 292/292 tests passing, 0 build errors. The two originally flagged CRITICAL issues have been remediated and verified. The implementation is ready for archival.
