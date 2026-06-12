# Spec: API Places Search

## Overview

REST endpoint exposing place search functionality via a thin API controller. Consumes the existing MediatR pipeline (`SearchPlacesRequest` → `SearchPlacesHandler`) and validates input before dispatching. All errors return `422 Unprocessable Entity` with `List<ValidationResult>`.

---

## Functional Requirements

### FR1: GET /trips/places/search
- `GET /trips/places/search` accepts query parameters: `query`, `cityId`, and optional `maxResults`.
- The controller MUST inject `IMediator` (MediatR) to dispatch `SearchPlacesRequest`.
- Response MUST be serialized as JSON.

### FR2: Request Validation
- `query` is required and MUST be at least 3 characters long.
- `cityId` is required and MUST be present in the configured `AllowedCities` list.
- `maxResults` is optional (default 10), MUST be >= 1 and <= `PlaceSearchOptions.MaxResults`.
- If validation fails, return `422 Unprocessable Entity`.

### FR3: 422 Error Format
All validation and service errors MUST return HTTP 422 with body:
```json
[
  {
    "errorCode": "MIN_LENGTH_VIOLATION",
    "description": "The search query must be at least 3 characters long."
  }
]
```
- `errorCode`: string — machine-readable error code
- `description`: string — human-readable error message in English

### FR4: External Service Failure
- If `PlaceRepository` cascade (local DB → Foursquare) returns no results due to external API failure, the controller MUST return 422 with `EXTERNAL_SERVICE_FAILURE`.

### FR5: Configuration (PlaceSearchOptions)
- `PlaceSearchOptions` MUST be registered via `IOptions<PlaceSearchOptions>`.
- Namespace: `SmartTripPlanner.API.Configurations`.
- Configuration section key: `"PlaceSearch"`.
- Properties:
  - `AllowedCities` (string[]): list of valid city IDs, default `["madrid-es"]`.
  - `MaxResults` (int): maximum allowed results per query, default `10`.
- Stored in `appsettings.json` under `"PlaceSearch"`.

### FR6: Successful Response
- HTTP 200 with body: array of `PlaceResponse` objects matching the OpenAPI schema.

---

## Acceptance Criteria

### AC1: Valid query returns places
- GIVEN a valid `query` ("Museo"), `cityId` ("madrid-es"), and optional `maxResults`
- WHEN the controller handles the request
- THEN it dispatches `SearchPlacesRequest` via `IMediator`
- AND returns 200 with `List<PlaceResponse>`

### AC2: Short query rejected
- GIVEN a `query` with fewer than 3 characters ("Mu")
- WHEN the controller handles the request
- THEN it returns 422 with `MIN_LENGTH_VIOLATION`

### AC3: Invalid city rejected
- GIVEN a `cityId` not in the allowed list ("london-gb")
- WHEN the controller handles the request
- THEN it returns 422 with `INVALID_CITY`

### AC4: MaxResults exceeds limit
- GIVEN a `maxResults` exceeding the configured maximum
- WHEN the controller handles the request
- THEN it returns 422 with `MAX_RESULTS_EXCEEDED`

### AC5: External service failure
- GIVEN a valid request
- AND the cascade search (local → external) fails with no results
- WHEN the controller handles the request
- THEN it returns 422 with `EXTERNAL_SERVICE_FAILURE`

### AC6: Configuration loaded from IOptions
- GIVEN `PlaceSearchOptions` registered via `IOptions`
- WHEN `AllowedCities` and `MaxResults` are accessed
- THEN they match the values in `appsettings.json`

---

## Testing Requirements (Strict TDD)

- Unit tests MUST be written BEFORE implementation.
- Controller tests: mock `IMediator` and `IOptions<PlaceSearchOptions>`, verify:
  - Valid request dispatches and returns mapped results
  - Each validation rule triggers correct 422 error
  - External service failure returns 422
- Configuration tests: verify `PlaceSearchOptions` binds correctly from appsettings.

---

## Non-Goals

- No changes to Domain, Infrastructure, or ApplicationServices layers.
- No FluentValidation integration (manual validation at controller level).
- No additional search providers.
- No persistence of search results.
