# Specification: place-search-handler

## Purpose

Application-layer MediatR handler that orchestrates `IPlaceRepository` cascade search and maps results to `PlaceModel` via AutoMapper. Provides the single entry point for place search between API controllers and domain/infrastructure.

## Requirements

### R1: SearchPlaces Request & Response

`SearchPlaces : IRequest<PlaceSearchResponse>` MUST expose:

| Field | Type | Required | Default |
|-------|------|----------|---------|
| `Query` | `string?` | No | null |
| `CityId` | `string` | Yes | — |
| `MaxResults` | `int` | No | 20 |

`PlaceSearchResponse` MUST wrap `IReadOnlyList<PlaceModel> Results`.

#### Scenario: Valid request passes through to repository
- GIVEN a `SearchPlaces` with Query="Museum", CityId="madrid-es", MaxResults=10
- WHEN the handler processes the request
- THEN it passes (Query, CityId, MaxResults) to `IPlaceRepository.SearchAsync`

#### Scenario: MaxResults defaults to 20
- GIVEN a `SearchPlaces` with CityId="madrid-es" and no MaxResults specified
- WHEN the handler processes the request
- THEN `SearchAsync` receives maxResults=20

### R2: PlaceModel

`PlaceModel` record MUST expose all `Place` entity fields:

| Field | Type |
|-------|------|
| `PlaceId` | `string` |
| `Name` | `string` |
| `CityId` | `string` |
| `Location` | `PlaceLocationModel` |
| `TypicalDurationMinutes` | `int` |
| `IsIndoor` | `bool` |
| `IsFamilyFriendly` | `bool` |
| `OpeningHours` | `IReadOnlyList<OpeningHoursWindowModel>` |

`PlaceLocationModel` is a dedicated domain record with `Latitude` and `Longitude` fields — distinct from the existing `LocationModel` (which has a `Name` field).

#### Scenario: All fields mapped from Place entity
- GIVEN a `Place` entity with all fields populated
- WHEN mapped via AutoMapper to `PlaceModel`
- THEN every field (PlaceId, Name, CityId, Location.Latitude, Location.Longitude, TypicalDurationMinutes, IsIndoor, IsFamilyFriendly, OpeningHours) matches the source

### R3: SearchPlacesHandler

The handler MUST inject `IPlaceRepository` and `IMapper`, call `SearchAsync(query, cityId, maxResults)`, and map results via `IMapper.Map<List<PlaceModel>>`.

#### Scenario: Returns local DB results (happy path)
- GIVEN `SearchAsync` returns 3 local `Place` entities matching the query
- WHEN the handler processes a `SearchPlaces` request
- THEN `PlaceSearchResponse.Results` contains 3 `PlaceModel` items with correct values

#### Scenario: Cascade to external service transparently
- GIVEN `SearchAsync` returns results from the external service fallback (no local matches)
- WHEN the handler processes a `SearchPlaces` request
- THEN `PlaceSearchResponse.Results` contains the external results — the handler does not distinguish source

#### Scenario: Empty results for non-matching query
- GIVEN `SearchAsync` returns an empty list
- WHEN the handler processes a `SearchPlaces` request
- THEN `PlaceSearchResponse.Results` is an empty (non-null) list

#### Scenario: Null Query is passed through to repository
- GIVEN a `SearchPlaces` with Query=null
- WHEN the handler processes the request
- THEN it calls `SearchAsync(query: null, cityId, maxResults)` — the repository decides behavior

### R4: PlaceMappingProfile

An AutoMapper `Profile` MUST map `Place` → `PlaceModel`, mapping the nested `PlaceLocation` ValueObject to `PlaceLocationModel` and the `OpeningHours` list to `List<OpeningHoursWindowModel>`.

#### Scenario: Nested Location is mapped to PlaceLocationModel
- GIVEN a `Place` with `Location.Latitude = 40.4168` and `Location.Longitude = -3.7038`
- WHEN mapped via `PlaceMappingProfile`
- THEN `PlaceModel.Location.Latitude` = 40.4168 and `PlaceModel.Location.Longitude` = -3.7038

### R5: DI Registration

`ApplicationServicesRegistration` MUST register MediatR (scanning the ApplicationServices assembly) and add `PlaceMappingProfile` to AutoMapper.

#### Scenario: Handler is resolved through MediatR pipeline
- GIVEN `ApplicationServicesRegistration` has been invoked
- WHEN `IMediator.Send(new SearchPlaces { CityId = "madrid-es" })` is called
- THEN `SearchPlacesHandler` processes the request and returns `PlaceSearchResponse`

## Constraints

- MUST NOT modify any existing Domain entity, `IPlaceRepository`, or Infrastructure code
- MUST NOT introduce new external service or repository dependencies
- All existing `place` spec tests MUST continue passing unchanged
