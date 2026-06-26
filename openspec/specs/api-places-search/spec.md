# Spec: API Places Search

## Overview

REST endpoint exposing place search functionality via a thin API controller. Consumes the existing MediatR pipeline (`SearchPlacesRequest` → `SearchPlacesHandler`) and validates input before dispatching. All errors return `422 Unprocessable Entity` with `List<ValidationResult>`.

---

## Functional Requirements

### FR1: POST /trips/places/search

`POST /trips/places/search` accepts a JSON body with `PlaceSearchRequest` schema: `query` (string?, nullable), `cityId` (string, required), `category` (string?, nullable), `filters` (object?, nullable, reserved for future use), `fetchFromExternalIfInsufficient` (bool?, default true), and optional `maxResults` (int, 1–10). The controller MUST inject `IMediator` (MediatR) to dispatch `SearchPlacesRequest`. Response MUST be serialized as JSON.

### FR2: Request Validation

- At least one of `query`, `category`, or `filters` MUST be non-null and non-empty.
- If `query` is provided, it MUST be at least 3 characters long.
- `cityId` is required and MUST be present in the configured `AllowedCities` list.
- `maxResults` is optional (default 10), MUST be >= 1 and <= `PlaceSearchOptions.MaxResults`.
- `fetchFromExternalIfInsufficient` defaults to `true` when omitted.
- If validation fails, return `422 Unprocessable Entity` with `List<ValidationResult>`.

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

### FR4: Configuration (PlaceSearchOptions)

- `PlaceSearchOptions` MUST be registered via `IOptions<PlaceSearchOptions>`.
- Namespace: `SmartTripPlanner.API.Configurations`.
- Configuration section key: `"PlaceSearch"`.
- Properties:
  - `AllowedCities` (string[]): list of valid city IDs, default `["madrid-es"]`.
  - `MaxResults` (int): maximum allowed results per query, default `10`.
- Stored in `appsettings.json` under `"PlaceSearch"`.

### FR5: Successful Response

- HTTP 200 with body: array of `PlaceResponse` objects matching the OpenAPI schema.

### FR6: External Service Failure

If the search pipeline returns no results (local + external), the controller MUST return 422 with `EXTERNAL_SERVICE_FAILURE`.

### FR7: At-Least-One-Input Error

When `query`, `category`, and `filters` are all null or empty, the controller MUST return 422 with `AT_LEAST_ONE_REQUIRED` error code.

#### Scenario: Empty search rejected
- GIVEN `query`=null, `category`=null, `filters`=null
- WHEN the controller validates the request
- THEN it returns 422 with `AT_LEAST_ONE_REQUIRED`

#### Scenario: Query-only accepted (regression)
- GIVEN `query`="Museum", `category`=null
- WHEN the controller validates the request
- THEN validation passes

#### Scenario: Category-only accepted
- GIVEN `query`=null, `category`="Museum"
- WHEN the controller validates the request
- THEN validation passes

#### Scenario: Short query still rejected
- GIVEN `query`="Mu", `category`=null
- WHEN the controller validates the request
- THEN it returns 422 with `MIN_LENGTH_VIOLATION`

#### Scenario: External fallback disabled
- GIVEN `query`="Museum", `fetchFromExternalIfInsufficient`=false
- WHEN the controller handles the request
- THEN it dispatches with `FetchFromExternalIfInsufficient`=false

#### Scenario: All inputs provided
- GIVEN `query`="Museum", `category`="Art", `fetchFromExternalIfInsufficient`=true
- WHEN the controller validates the request
- THEN validation passes

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
- AND the search pipeline (local + external) returns no results
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
