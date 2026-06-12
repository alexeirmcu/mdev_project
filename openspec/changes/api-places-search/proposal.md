# Proposal: API Places Search

## Intent

Complete Flow 1 by exposing a place search capability via a REST endpoint. This allows clients to search for places in a specific city based on a query string.

## Scope

### In Scope
- Implementation of `PlacesController` in `SmartTripPlanner.API/Controllers/`.
- Integration with `SearchPlacesRequest` / `SearchPlacesHandler` via MediatR `IMediator`.
- Configuration for search constraints (`PlaceSearchOptions`) using the IOptions pattern.
- Implementation of input validation (min query length, cityId check, maxResults) returning 422 errors.
- Global error handling mapping for external service failures (422).
- Unit and integration tests for the controller and validation logic.

### Out of Scope
- Modifications to the Domain layer or Infrastructure (Foursquare client).
- Changes to the Application services (MediatR handlers).
- Implementation of additional search providers.

## Capabilities

### New Capabilities
- `api-places-search`: REST endpoint providing a search interface for places within a city.

### Modified Capabilities
- None

## Approach

- Create a thin `PlacesController` that depends on `IMediator` for request dispatching.
- Implement a dedicated `PlaceSearchOptions` class to hold configurable limits (max results, pilot cities) and register it in `appsettings.json`.
- Use a validation strategy to ensure `query` >= 3 chars, `cityId` is in the pilot list, and `maxResults` is within bounds.
- Return a consistent 422 Unprocessable Entity response for all validation and external service errors, containing a list of `ValidationResult` objects as per the OpenAPI spec.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `SmartTripPlanner.API/Controllers/PlacesController.cs` | New | Controller for place search |
| `SmartTripPlanner.API/Configurations/PlaceSearchOptions.cs` | New | Configuration for search limits |
| `SmartTripPlanner.API/appsettings.json` | Modified | Add search configuration section |
| `SmartTripPlanner.API/Program.cs` | Modified | Register `PlaceSearchOptions` |
| `SmartTripPlanner.API.Tests/Controllers/PlacesControllerTests.cs` | New | Controller unit and integration tests |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Inconsistent error formats between this controller and others | Low | Use a shared validation result helper to ensure 422 responses match the spec |
| Over-reliance on manual validation instead of FluentValidation | Medium | Evaluate if `SearchPlacesRequest` already has validation; if so, trigger it in the controller |

## Rollback Plan

- Remove `PlacesController.cs`.
- Revert `Program.cs` DI registrations.
- Remove `PlaceSearchOptions.cs` and corresponding section in `appsettings.json`.

## Dependencies

- MediatR `IMediator` registered in the application.
- `SearchPlacesRequest` and `SearchPlacesResponse` defined in Application layer.

## Success Criteria

- [ ] `GET /trips/places/search` returns 200 OK with a list of places for valid inputs.
- [ ] Query < 3 characters returns 422 with `MIN_LENGTH_VIOLATION`.
- [ ] Invalid `cityId` returns 422 with appropriate validation error.
- [ ] External service failures return 422 with `EXTERNAL_SERVICE_FAILURE`.
- [ ] All tests pass.
