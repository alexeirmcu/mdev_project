# Tasks: Enhance Place Search

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 350–400 |
| 400-line budget risk | Medium |
| Chained PRs recommended | No |
| Suggested split | Single PR |
| Delivery strategy | ask-always |
| Chain strategy | pending |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Medium

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Full feature | PR 1 | Domain + EF + Repo + Service + Mapping + Tests |

## Phase 1: Domain Model

- [x] **TASK-01** (S) Create `Domain/AggregatesModel/PlaceAttribute.cs` — ValueObject with Provider/Key/Value, validation, equality. Files: `Domain/AggregatesModel/PlaceAttribute.cs`. AC: `PlaceAttributeTests` pass construction/equality/validation. Deps: none.
- [x] **TASK-02** (S) Add `Attributes` collection and `AddAttribute` to `Place.cs` — `List<PlaceAttribute>` initialized empty, method appends with null check. Files: `Domain/AggregatesModel/Place.cs`. AC: `PlaceTests` verify collection and method. Deps: TASK-01.
- [x] **TASK-03** (S) Create `PlaceAttributeModel` record and update `PlaceModel` — Add `IReadOnlyList<PlaceAttributeModel> Attributes` to `PlaceModel`. Files: `Domain/ApiModels/PlaceAttributeModel.cs`, `Domain/ApiModels/PlaceModel.cs`. AC: Model compiles; record properties correct. Deps: none.

## Phase 2: Repository & EF Configuration

- [x] **TASK-04** (S) Update `PlaceConfiguration` with `OwnsMany` for `PlaceAttribute` — Separate table `PlaceAttributes`, FK `PlaceId`, index on `PlaceId`+`Value`. Files: `Infrastructure/Configurations/PlaceConfiguration.cs`. AC: `PlaceRepositoryTests` persist/round-trip attributes. Deps: TASK-02.
- [x] **TASK-05** (M) Create EF Core migration for `PlaceAttributes` table — Additive migration only. Files: `Migrations/*`. AC: `dotnet ef migrations add` succeeds; `dotnet ef database update` works. Deps: TASK-04.
- [x] **TASK-06** (S) Update `PlaceRepository.SearchAsync` — Include `Attributes`, extend `Where` to match `Name.Contains(query)` OR `Attributes.Any(a => a.Value.Contains(query))`. Files: `Infrastructure/Repositories/PlaceRepository.cs`. AC: `PlaceRepositoryTests` verify attribute search and name search regression. Deps: TASK-04.

## Phase 3: External Service & Mapping

- [x] **TASK-07** (S) Update `FoursquarePlaceService.MapToPlace` — Map `Categories` to `PlaceAttribute("foursquare","category",cat.Name)`. Chain not available in current FoursquarePlace model (no ChainLabel field). Files: `Infrastructure/ExternalServices/Foursquare/FoursquarePlaceService.cs`. AC: `FoursquarePlaceServiceTests` verify attributes populated. Deps: TASK-02.
- [x] **TASK-08** (S) Update AutoMapper profile — Add `PlaceAttribute` → `PlaceAttributeModel` mapping; update `PlaceMappingProfileTests` and `PlaceFixture` to include attributes. Files: `API/Configurations/AutoMapperProfile.cs`, `tests/.../Mapping/PlaceMappingProfileTests.cs`, `tests/.../Helpers/PlaceFixture.cs`. AC: `PlaceMappingProfileTests` verify mapping. Deps: TASK-03.

## Phase 4: Testing

- [x] **TASK-09** (S) Create `PlaceAttributeTests` — Construction, equality, null/empty validation. Files: `tests/SmartTripPlanner.Tests/Domain/PlaceAttributeTests.cs`. AC: All tests pass. Deps: TASK-01.
- [x] **TASK-10** (S) Create/update `PlaceTests` — Verify `AddAttribute` appends and initial empty collection. Files: `tests/SmartTripPlanner.Tests/Domain/PlaceTests.cs`. AC: All tests pass. Deps: TASK-02.
- [x] **TASK-11** (M) Update `PlaceRepositoryTests` — Add attribute search scenarios (match category, match chain, name regression). Files: `tests/SmartTripPlanner.Tests/Infrastructure/PlaceRepositoryTests.cs`. AC: All tests pass. Deps: TASK-06.
- [x] **TASK-12** (S) Update `SearchPlacesHandlerTests` — End-to-end attribute search via handler. Files: `tests/SmartTripPlanner.Tests/ApplicationServices/SearchPlacesHandlerTests.cs`. AC: All tests pass. Deps: TASK-06, TASK-08.

## Phase 5: Verification

- [x] **TASK-13** (S) Run full test suite — `dotnet test` passes with zero failures; all acceptance criteria from spec verified. Files: none. AC: Build succeeds; all tests green. Deps: TASK-09, TASK-10, TASK-11, TASK-12.
