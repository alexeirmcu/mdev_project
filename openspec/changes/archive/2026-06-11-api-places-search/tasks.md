# Tasks: API Places Search

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~280 |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | Single PR |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: No
Chained PRs recommended: No
400-line budget risk: Low

## Phase 1: PlaceSearchOptions + Configuration

- [ ] 1.1 Create `Configurations/PlaceSearchOptions.cs` — class with `AllowedCities` (string[], default `["madrid-es"]`) and `MaxResults` (int, default `10`)
- [ ] 1.2 Add `"PlaceSearch"` section to `appsettings.json` with `AllowedCities` and `MaxResults`
- [ ] 1.3 Register `PlaceSearchOptions` in `Program.cs` via `builder.Services.Configure<PlaceSearchOptions>()`
- [ ] 1.4 Verify build compiles

## Phase 2: PlacesController Tests — TDD (RED)

- [ ] 2.1 Write `PlacesControllerTests` with mocks for `IMediator` and `IOptions<PlaceSearchOptions>`:
  - `Search_ValidRequest_Returns200WithPlaces`
  - `Search_ShortQuery_Returns422WithMinLengthViolation`
  - `Search_InvalidCity_Returns422WithInvalidCity`
  - `Search_MaxResultsExceeded_Returns422WithMaxResultsExceeded`
  - `Search_ExternalServiceFailure_Returns422WithExternalServiceFailure`

## Phase 3: PlacesController Implementation — TDD (GREEN)

- [ ] 3.1 Create `PlacesController` with constructor injection of `IMediator` and `IOptions<PlaceSearchOptions>`
- [ ] 3.2 Implement `ValidateRequest()` helper method for input validation
- [ ] 3.3 Implement `Search` action: validate → send MediatR → return results
- [ ] 3.4 Implement error handling for external service failures → 422
- [ ] 3.5 Verify all tests pass (GREEN)

## Phase 4: Refactor + Final Verification

- [ ] 4.1 Clean up test setup (shared mock factory), verify green
- [ ] 4.2 Manual smoke test: verify `GET /trips/places/search` returns expected response shape
- [ ] 4.3 Verify OpenAPI alignment with `doc/architecture/endpoints.yaml`
