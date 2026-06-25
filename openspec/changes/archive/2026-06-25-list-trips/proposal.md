# Proposal: List Trips

## Intent

Implement the missing `GET /api/trips` endpoint to list the authenticated user's trips with optional city/date filters.

## Scope

### In Scope
- Fix `TripSummaryResponse` — `CityId` to `long`, add `CityCode`
- Create `ListTrips` MediatR query + `ListTripsHandler` using `ITripRepository.ListAsync`
- Add `Trip → TripSummaryResponse` mapping in `AutoMapperProfile`
- Add `GET /api/trips` action in `TripsController`
- Resolve `cityCode`(string) → `cityId`(long) via `ICityRepository.GetByCodeAsync`
- Controller tests + handler tests

### Out of Scope
- Pagination (deferred)
- Sorting options

## Capabilities

### New Capabilities
- `list-trips`: `GET /api/trips` returning `TripSummaryResponse[]` with owner-scoped results and optional `cityCode`/`startDate`/`endDate` filters

### Modified Capabilities
- None

## Approach

Three-layer flow: Controller → Handler → Repository. Handler resolves optional `cityCode` to `cityId` via `ICityRepository`, then calls existing `ListAsync(ownerUserId, cityId, startDate, endDate, ct)`. No per-trip ownership check needed — repository filters by `OwnerUserId`. AutoMapper maps `Trip` → `TripSummaryResponse`, reading `CityCode`/`CityName` from `City` navigation. CREATED trips (no itinerary) are included with `CompletedActivitiesCount`/`TotalActivitiesCount` = 0.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Domain/ApiModels/TripSummaryResponse.cs` | Modified | `CityId` to `long`, add `CityCode` |
| `ApplicationServices/Commands/ListTrips.cs` | New | MediatR query |
| `ApplicationServices/Handlers/ListTripsHandler.cs` | New | Query handler |
| `API/Configurations/AutoMapperProfile.cs` | Modified | `Trip → TripSummaryResponse` |
| `API/Controllers/TripsController.cs` | Modified | New `ListTrips` action |
| `tests/.../Handlers/ListTripsHandlerTests.cs` | New | Handler tests |
| `tests/.../Controllers/TripsControllerTests.cs` | Modified | Controller tests |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| City code not found → no results | Low | Return `[]` — city code is optional |
| `TripSummaryResponse` change breaks consumers | Low | No existing consumers |

## Rollback Plan

Remove controller action, revert `TripSummaryResponse`, delete command/handler files, remove AutoMapper mapping.

## Dependencies

- `ITripRepository.ListAsync` (exists)
- `ICityRepository.GetByCodeAsync` (exists)

## Success Criteria

- [ ] `GET /api/trips` returns 200 with `TripSummaryResponse[]`
- [ ] Filters by `cityCode`, `startDate`, `endDate` work correctly
- [ ] Only current user's trips are returned
- [ ] CREATED trips (no itinerary) included with counts = 0
- [ ] All existing tests pass; new handler + controller tests are green
