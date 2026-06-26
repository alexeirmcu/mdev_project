# Delta for api-places-search

## MODIFIED Requirements

### FR1: POST /trips/places/search

`POST /trips/places/search` accepts a JSON body with `PlaceSearchRequest` schema: `query` (string?, nullable), `cityId` (string, required), `category` (string?, nullable), `filters` (object?, nullable, reserved for future use), `fetchFromExternalIfInsufficient` (bool?, default true), and optional `maxResults` (int, 1–10). The controller MUST inject `IMediator` (MediatR) to dispatch `SearchPlacesRequest`. Response MUST be serialized as JSON.
(Previously: query required with min 3, no category/filters/fetchFromExternalIfInsufficient fields)

### FR2: Request Validation

- At least one of `query`, `category`, or `filters` MUST be non-null and non-empty.
- If `query` is provided, it MUST be at least 3 characters long.
- `cityId` is required and MUST be present in the configured `AllowedCities` list.
- `maxResults` is optional (default 10), MUST be >= 1 and <= `PlaceSearchOptions.MaxResults`.
- `fetchFromExternalIfInsufficient` defaults to `true` when omitted.
- If validation fails, return `422 Unprocessable Entity` with `List<ValidationResult>`.
(Previously: query was required with min 3, no at-least-one guard clause)

### FR6 (previously FR4): External Service Failure

If the search pipeline returns no results (local + external), the controller MUST return 422 with `EXTERNAL_SERVICE_FAILURE`.
(Previously: if cascade search returns no results due to external API failure)

## ADDED Requirements

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
