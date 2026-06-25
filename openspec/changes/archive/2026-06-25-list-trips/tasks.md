# Tasks: List Trips

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~440 |
| 400-line budget risk | Medium |
| Chained PRs recommended | Yes |
| Suggested split | PR 1: data model → PR 2: core + controller → PR 3: tests |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: Medium

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Fix `TripSummaryResponse` + AutoMapper mapping | PR 1 | base=feature/list-trips; ~20 lines |
| 2 | `ListTrips` command, handler, validator | PR 2 | base=PR 1 branch; ~80 lines |
| 3 | Controller action + handler/controller/validator tests | PR 3 | base=PR 2 branch; ~340 lines |

## Phase 1: Data Model

- [x] 1.1 Change `string CityId` → `long CityId` + add `string CityCode` in `TripSummaryResponse`
- [x] 1.2 Add `Trip → TripSummaryResponse` mapping in `AutoMapperProfile` using `ForCtorParam` for `CityCode` (from `src.City.CityCode`)

## Phase 2: Core Logic

- [x] 2.1 Create `ListTrips` record in `Commands/` with optional `CityCode`, `StartDate`, `EndDate` implementing `IRequest<List<TripSummaryResponse>>`
- [x] 2.2 Create `ListTripsHandler` injecting `ITripRepository`, `ICityRepository`, `IMapper`, `ILogger<ListTripsHandler>`, `IUserContext`; resolve cityCode→cityId via `GetByCodeAsync` (short-circuit to empty if null), call `ListAsync`, map via AutoMapper with computed aggregate fields
- [x] 2.3 Create `ListTripsQueryValidator` in `Validators/` with rule: if both `StartDate` and `EndDate` provided, require `StartDate <= EndDate`

## Phase 3: API Wiring

- [x] 3.1 Add `GET /api/trips` action in `TripsController` binding `[FromQuery]` params, send `ListTrips` via `_mediator.Send`, return `Ok()`
- [x] 3.2 Update `doc/architecture/endpoints.yaml` with `GET /api/trips` entry if missing — already present

## Phase 4: Testing

- [x] 4.1 **Handler tests**: empty results, cityCode resolution, date filters, CREATED trip counts=0, GENERATED trip with computed counts, TotalMustSees, other owner exclusion
- [x] 4.2 **Validator tests**: no filters passes, startDate only passes, endDate only passes, startDate <= endDate passes, startDate > endDate fails
- [x] 4.3 **Controller tests**: `ListTrips` returns Ok with response, query params passed through to `ListTrips` record, `_mediator.Send` verified
