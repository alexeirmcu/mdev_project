# Tasks: Normalize Place Attributes

## Domain

- [x] **TASK-01** (M) Promote `PlaceAttribute` from ValueObject to Entity
  - **Description**: Convert `PlaceAttribute` to inherit from `Entity` (add `long Id`). Keep Provider, Key, Value immutable (private setters + constructor). Preserve null/empty validation. Remove `ValueObject` base and `GetEqualityComponents`. Add case-insensitive equality helper if needed for repository lookups.
  - **Files**: `SmartTripPlanner.Domain/AggregatesModel/PlaceAttribute.cs`
  - **AC**: `PlaceAttribute` inherits from `Entity`; has `long Id`; Provider/Key/Value immutable with same validation; `PlaceAttributeTests` updated
  - **Deps**: none

- [x] **TASK-02** (S) Add `PlacePlaceAttribute` join entity
  - **Description**: Create a simple join entity class `PlacePlaceAttribute` with `PlaceId` and `PlaceAttributeId` foreign keys. No extra payload needed.
  - **Files**: `SmartTripPlanner.Domain/AggregatesModel/PlacePlaceAttribute.cs`
  - **AC**: Class compiles; has both FK properties
  - **Deps**: none

- [x] **TASK-03** (S) Update `Place` aggregate for many-to-many
  - **Description**: Change `Place.Attributes` from `List<PlaceAttribute>` to `ICollection<PlaceAttribute>`. Update `AddAttribute` to accept `PlaceAttribute`. Update `UpdateFromExternalProvider` to clear and re-add attributes (clearing removes join rows only, not definitions).
  - **Files**: `SmartTripPlanner.Domain/AggregatesModel/Place.cs`
  - **AC**: `Place.Attributes` is a collection navigable by EF Core many-to-many; `AddAttribute` and `UpdateFromExternalProvider` compile and pass tests
  - **Deps**: TASK-01, TASK-02

## Infrastructure

- [x] **TASK-04** (M) Add `PlaceAttributeConfiguration` and update `PlaceConfiguration`
  - **Description**: Create `PlaceAttributeConfiguration` with `ToTable("PlaceAttributes")`, `HasKey(a => a.Id)`, required/maxlength on Provider/Key/Value, and case-insensitive unique index on `(Provider, Key, Value)`. Update `PlaceConfiguration`: replace `OwnsMany` with `HasMany(p => p.Attributes).WithMany().UsingEntity<PlacePlaceAttribute>(...)` mapping to table `PlacePlaceAttributes` with composite PK `(PlaceId, PlaceAttributeId)`.
  - **Files**: `SmartTripPlanner.Infrastructure/Configurations/PlaceAttributeConfiguration.cs`, `SmartTripPlanner.Infrastructure/Configurations/PlaceConfiguration.cs`
  - **AC**: `dotnet build` succeeds; `PlaceConfiguration` no longer uses `OwnsMany`
  - **Deps**: TASK-01, TASK-02, TASK-03

- [x] **TASK-05** (M) Add `DbSet<PlaceAttribute>` and `DbSet<PlacePlaceAttribute>` to `PlannerDbContext`
  - **Description**: Register new entities in `PlannerDbContext`. Ensure `OnModelCreating` applies the new configuration.
  - **Files**: `SmartTripPlanner.Infrastructure/PlannerDbContext.cs`
  - **AC**: `dotnet build` succeeds; `PlannerDbContext` exposes `PlaceAttributes` and `PlacePlaceAttributes` sets
  - **Deps**: TASK-04

- [x] **TASK-06** (L) Implement find-or-create in `PlaceRepository.UpsertRangeAsync`
  - **Description**: Before calling `UpdateFromExternalProvider`, resolve all incoming attributes by normalized `(Provider, Key, Value)`. If an attribute exists in DB, use the tracked entity; if not, create a new one. Then pass the resolved list to `UpdateFromExternalProvider`.
  - **Files**: `SmartTripPlanner.Infrastructure/Repositories/PlaceRepository.cs`
  - **AC**: `UpsertRangeAsync` tests pass with duplicate attributes across places; no unique constraint violations; only distinct rows in `PlaceAttributes` table
  - **Deps**: TASK-01, TASK-03, TASK-05

- [x] **TASK-07** (S) Update repository queries for join table correctness
  - **Description**: Verify `SearchAsync`, `GetManyByCityIdAsync`, `GetDistinctInterestsByCityIdAsync` still work. Add `.Include(p => p.Attributes)` if needed.
  - **Files**: `SmartTripPlanner.Infrastructure/Repositories/PlaceRepository.cs`
  - **AC**: All existing repository tests pass; `GetDistinctInterestsByCityIdAsync` returns correct distinct values via join table
  - **Deps**: TASK-04, TASK-05

## API / Application

- [x] **TASK-08** (S) Reduce `PlaceAttributeModel` to `(Key, Value)`
  - **Description**: Remove `Provider` from `PlaceAttributeModel` record. Update `AutoMapperProfile` mapping.
  - **Files**: `SmartTripPlanner.Domain/ApiModels/PlaceAttributeModel.cs`, `SmartTripPlanner.API/Configurations/AutoMapperProfile.cs`
  - **AC**: `PlaceAttributeModel` compiles with only `Key` and `Value`; AutoMapper tests pass
  - **Deps**: TASK-01

## Migration

- [x] **TASK-09** (L) Delete all migrations and create single `InitialCreate`
  - **Description**: Delete all files under `SmartTripPlanner.Infrastructure/Migrations/`. Ensure database is dropped or schema is clean. Run `dotnet ef migrations add InitialCreate`. Verify `Up()` creates all tables including `PlaceAttributes` and `PlacePlaceAttributes`.
  - **Files**: `SmartTripPlanner.Infrastructure/Migrations/*`
  - **AC**: Single migration file created; `dotnet ef database update` applies cleanly; schema matches design
  - **Deps**: TASK-04, TASK-05

## Tests

- [x] **TASK-10** (M) Update `PlaceAttributeTests` for Entity semantics
  - **Description**: Remove value-equality tests. Add tests for: construction with valid values, null/empty validation throws, `Id` is generated/default.
  - **Files**: `tests/SmartTripPlanner.Tests/Domain/PlaceAttributeTests.cs`
  - **AC**: All tests pass; no references to `ValueObject` equality
  - **Deps**: TASK-01

- [x] **TASK-11** (M) Update `PlaceRepositoryTests` for normalized persistence
  - **Description**: Update `SavePlace_PreservesAttributes` to verify attributes are linked, not duplicated. Update `UpsertRangeAsync` tests to verify find-or-create behavior. Update `SearchAsync_WithAttributeValueMatch`. Update `GetDistinctInterestsByCityIdAsync` test.
  - **Files**: `tests/SmartTripPlanner.Tests/Infrastructure/Repositories/PlaceRepositoryTests.cs`
  - **AC**: All repository tests pass; no duplicate `PlaceAttribute` rows after upsert
  - **Deps**: TASK-06, TASK-07, TASK-09

- [x] **TASK-12** (S) Update `PlaceTests` for collection changes
  - **Description**: Ensure `AddAttribute` and `UpdateFromExternalProvider` tests still compile and pass. Verify clearing attributes only removes join rows.
  - **Files**: `tests/SmartTripPlanner.Tests/Domain/PlaceTests.cs`
  - **AC**: All tests pass
  - **Deps**: TASK-03

- [x] **TASK-13** (S) Update test fixtures and helpers
  - **Description**: Update `PlaceFixture` and any other helpers that instantiate `PlaceAttribute`.
  - **Files**: `tests/SmartTripPlanner.Tests/Helpers/PlaceFixture.cs`, other helper files
  - **AC**: All test projects compile
  - **Deps**: TASK-01

- [x] **TASK-14** (S) Update mapping and handler tests
  - **Description**: Verify `PlaceMappingProfileTests` pass with trimmed `PlaceAttributeModel`. Verify `SearchPlacesHandlerTests` pass.
  - **Files**: `tests/SmartTripPlanner.Tests/Mapping/PlaceMappingProfileTests.cs`, `tests/SmartTripPlanner.Tests/ApplicationServices/Handlers/SearchPlacesHandlerTests.cs`
  - **AC**: All tests pass
  - **Deps**: TASK-08

---

## Review Workload Forecast

| Metric | Value |
|--------|-------|
| Estimated changed lines | ~600–800 |
| Files touched | 15+ |
| New files | 3 (join entity, PlaceAttributeConfiguration, InitialCreate migration) |
| Deleted files | 6+ (old migrations) |
| **Chained PRs recommended** | **Yes** |
| **400-line budget risk** | **High** |
| **Decision needed before apply** | **Yes** |

### Decision
Single PR with `size:exception` accepted by user.
