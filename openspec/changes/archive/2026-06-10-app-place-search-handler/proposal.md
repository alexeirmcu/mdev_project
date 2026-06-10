# Proposal: App Place Search Handler

## Intent

Bootstraps the empty Application Services layer with a MediatR handler for place search. Domain + Infrastructure already support the cascade search via `IPlaceRepository`/`IPlaceExternalService`, but no application-layer entry point exists — any consumer must talk directly to Infrastructure. This fills that gap.

## Scope

### In Scope
- MediatR `SearchPlaces` request → `PlaceSearchResponse` handler
- `PlaceModel` response record in `Domain/ApiModels/`
- AutoMapper profile (`PlaceMappingProfile`) for `Place` → `PlaceModel`
- DI registration extension (`ApplicationServicesRegistration`)
- All Place fields exposed: PlaceId, Name, CityId, Location, TypicalDurationMinutes, IsIndoor, IsFamilyFriendly, OpeningHours

### Out of Scope
- `PlacesController` (separate change)
- FluentValidation (deferred unless pager params justify it)
- Domain entity / Infrastructure changes — existing specs untouched

## Capabilities

### New Capabilities
- `place-search-handler`: Application-layer MediatR handler that orchestrates the existing `IPlaceRepository` cascade and maps results to `PlaceModel` via AutoMapper.

### Modified Capabilities
- None — existing `place` spec (Domain + Infrastructure) is unchanged

## Approach

1. Create `SearchPlaces` record (`IRequest<PlaceSearchResponse>`) with `Query`, `CityId`, `MaxResults`
2. Create `PlaceModel` record in `Domain/ApiModels/` with all Place fields
3. Create `PlaceSearchResponse` record wrapping `List<PlaceModel>`
4. Create `PlaceMappingProfile` (AutoMapper) for `Place` → `PlaceModel`
5. Create `SearchPlacesHandler` — injects `IPlaceRepository`, delegates to `SearchAsync`, maps via AutoMapper
6. Create `ApplicationServicesRegistration` — registers MediatR + AutoMapper profiles

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/ApplicationServices/` | New | First code in the empty project |
| `Domain/ApiModels/` | New | Folder + response model records |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Handler bypasses external cascade for local results | Low | Uses same `IPlaceRepository.SearchAsync` — cascade logic untouched |

## Rollback Plan

Revert all files in `ApplicationServices/` and `Domain/ApiModels/`. Remove `ApplicationServicesRegistration` call from API composition root.

## Dependencies

- `MediatR` NuGet in `ApplicationServices.csproj`
- `AutoMapper.Extensions.Microsoft.DependencyInjection` NuGet
- Existing `SmartTripPlanner.Domain` project reference

## Success Criteria

- [ ] `SearchPlaces` handler returns mapped results from local DB
- [ ] Handler falls through to external service when local is empty
- [ ] `PlaceModel` exposes all Place fields including `OpeningHours`
- [ ] `MaxResults` is passed through to the repository
- [ ] All existing Domain + Infrastructure tests still pass
