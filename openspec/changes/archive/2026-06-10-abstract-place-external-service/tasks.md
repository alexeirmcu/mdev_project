# Tasks: Abstract Place External Service

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~250-300 |
| 400-line budget risk | Medium |
| Chained PRs recommended | No |
| Suggested split | Single PR |
| Delivery strategy | ask-always |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Medium

## Phase 1: Port Interface & Adapter Test Setup (RED)

- [x] 1.1 Create `SmartTripPlanner.Domain/Repository/IPlaceExternalService.cs` with `SearchPlacesAsync` returning `List<Place>`
- [x] 1.2 Write `FoursquarePlaceServiceTests` (RED) — mock `IFoursquareApiClient`, verify correct Place mapping + empty list on `HttpRequestException`

## Phase 2: Adapter & Internal Visibility (GREEN)

- [x] 2.1 Implement `FoursquarePlaceService` wrapping `IFoursquareApiClient`, mapping `FoursquarePlace` → `Place` via `FoursquareCategoryHeuristics`
- [x] 2.2 Set all Foursquare models (6 files) + `FoursquareCategoryHeuristics` to `internal`

## Phase 3: Repository Wiring & Test Refactor

- [x] 3.1 Modify `PlaceRepository.cs` — replace `IFoursquareApiClient?` with `IPlaceExternalService?`, remove `MapToPlace`, drop Foursquare usings
- [x] 3.2 Add `services.AddScoped<IPlaceExternalService, FoursquarePlaceService>()` to `InfrastructureServiceRegistration.cs`
- [x] 3.3 Refactor `PlaceRepositoryCascadeTests` — swap inline `MockApiClient` for Moq mock of `IPlaceExternalService`

## Phase 4: Verify

- [x] 4.1 Run all tests — confirm `PlaceRepositoryTests`, `PlaceRepositoryCascadeTests`, `FoursquarePlaceServiceTests`, `FoursquareCategoryHeuristicsTests` pass
- [x] 4.2 Verify no Foursquare types remain public outside Infrastructure
