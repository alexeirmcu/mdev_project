# List Trips Specification

## 1. Summary

Implement the missing `GET /api/trips` endpoint returning the authenticated user's trips as `TripSummaryResponse[]`. The handler filters by `OwnerUserId` (via repository), supports optional `cityCode`/`startDate`/`endDate` query parameters, and includes CREATED-status trips with zero activity counts. The `TripSummaryResponse` record is fixed: `CityId` becomes `long` and `CityCode` is added.

## 2. Requirements

### Requirement: R1 — GET /api/trips returns owner-scoped trip summaries

The system MUST expose `GET /api/trips` returning `200 OK` with `TripSummaryResponse[]`. The handler MUST call `ITripRepository.ListAsync(ownerUserId, cityId, startDate, endDate, ct)` which already filters by `OwnerUserId`. Results MUST include all trip statuses (CREATED, GENERATED, COMPLETED). The endpoint MUST be decorated with `[Authorize]` (inherited from class-level attribute).

#### Scenario: User with trips receives 200 with summaries

- GIVEN the authenticated user owns 2 trips (one CREATED, one GENERATED)
- WHEN `GET /api/trips` is called without filters
- THEN the response is `200 OK`
- AND the body is a non-empty `TripSummaryResponse[]`
- AND each item contains `tripId`, `cityId`, `cityCode`, `cityName`, `startDate`, `endDate`, `totalMustSees`, `completedActivitiesCount`, `totalActivitiesCount`

#### Scenario: User with no trips receives empty array

- GIVEN the authenticated user has zero trips
- WHEN `GET /api/trips` is called
- THEN the response is `200 OK`
- AND the body is an empty array `[]`

#### Scenario: Another user's trips are invisible

- GIVEN user A owns 3 trips and user B owns 2 trips
- WHEN user A calls `GET /api/trips`
- THEN only user A's 3 trips are returned
- AND none of user B's trips appear

#### Scenario: CREATED trips (no itinerary) are included with counts = 0

- GIVEN a trip with status CREATED (no `DayPlan[]` populated)
- WHEN `GET /api/trips` is called
- THEN the response includes that trip
- AND `completedActivitiesCount = 0` and `totalActivitiesCount = 0`

### Requirement: R2 — Optional cityCode filter resolves to cityId

When the `cityCode` query parameter is provided, the handler MUST resolve it to a `cityId` via `ICityRepository.GetByCodeAsync(cityCode, ct)`. If found, the `cityId` is passed to `ITripRepository.ListAsync`. The endpoint SHALL accept `cityCode` as a string (e.g. "madrid-es") matching the YAML contract.

#### Scenario: Filter by valid cityCode returns only matching trips

- GIVEN user owns 2 trips: one in "madrid-es" and one in "barcelona-es"
- WHEN `GET /api/trips?cityCode=madrid-es` is called
- THEN the response contains only the "madrid-es" trip
- AND `cityId` in the response matches the resolved city's ID

#### Scenario: cityCode not found returns empty array

- GIVEN `ICityRepository.GetByCodeAsync("invalid-city")` returns null
- WHEN `GET /api/trips?cityCode=invalid-city` is called
- THEN the response is `200 OK` with an empty array `[]`
- AND `ITripRepository.ListAsync` is never called (short-circuit)

#### Scenario: cityCode omitted returns all owner trips

- GIVEN no `cityCode` query parameter
- WHEN `GET /api/trips` is called
- THEN `ListAsync` is called with `cityId = null`
- AND all owner trips are returned regardless of city

### Requirement: R3 — Optional startDate / endDate filter by date range

The system MUST accept optional `startDate` and `endDate` query parameters (ISO 8601 `YYYY-MM-DD`). When provided, they are passed as-is to `ITripRepository.ListAsync` which filters trips where `startDate >= startDate` and `endDate <= endDate`. Both parameters MUST be optional independently.

#### Scenario: Filter by date range returns trips within range

- GIVEN a trip from 2026-06-15 to 2026-06-18 and another from 2026-07-01 to 2026-07-05
- WHEN `GET /api/trips?startDate=2026-06-01&endDate=2026-06-30` is called
- THEN only the June trip is returned

#### Scenario: startDate without endDate filters from date forward

- GIVEN trips starting before and after 2026-07-01
- WHEN `GET /api/trips?startDate=2026-07-01` is called
- THEN only trips with `startDate >= 2026-07-01` are returned

#### Scenario: endDate without startDate filters up to date

- GIVEN trips ending before and after 2026-06-30
- WHEN `GET /api/trips?endDate=2026-06-30` is called
- THEN only trips with `endDate <= 2026-06-30` are returned

### Requirement: R4 — Combined filters (cityCode + dates) are applied together

When multiple query parameters are provided, the system MUST apply all filters conjunctively. The `ownerUserId` filter is always applied (mandatory).

#### Scenario: City and date filters combined narrow results

- GIVEN a "madrid-es" trip in June and a "barcelona-es" trip in July
- WHEN `GET /api/trips?cityCode=madrid-es&startDate=2026-06-01&endDate=2026-06-30` is called
- THEN only the "madrid-es" June trip is returned
- AND the "barcelona-es" July trip is excluded

### Requirement: R5 — Invalid query parameters return 422

The system MUST validate query parameters and return `422 Unprocessable Entity` with `ValidationResult[]` when parameters fail format validation (e.g. invalid date format).

#### Scenario: Invalid date format returns 422

- GIVEN `startDate=not-a-date`
- WHEN `GET /api/trips?startDate=not-a-date` is called
- THEN the response is `422 Unprocessable Entity`
- AND the body contains a `ValidationResult` with the relevant error

### Requirement: R6 — TripSummaryResponse fixes CityId to long and adds CityCode

The `TripSummaryResponse` record MUST be updated: `CityId` from `string` to `long`, and `CityCode` (`string`) added. The YAML-defined schema in `endpoints.yaml` is authoritative. AutoMapper MUST map `Trip.CityId` (long) and `Trip.City.CityCode` to the response.

#### Scenario: Response contains correct types

- GIVEN a trip with `CityId = 1` and `CityCode = "madrid-es"`
- WHEN `GET /api/trips` returns a `TripSummaryResponse`
- THEN `cityId` is a JSON number (int64) with value 1
- AND `cityCode` is a JSON string `"madrid-es"`

## 3. API Contract

| Method | Path | Auth | Request | Response |
|--------|------|------|---------|----------|
| GET | `/api/trips` | Required (Bearer) | Query: `cityCode`, `startDate`, `endDate` | `200` → `TripSummaryResponse[]`, `422` → `ValidationResult[]` |

Refer to `doc/architecture/endpoints.yaml` for the authoritative OpenAPI schema.

## 4. Out of Scope

- Pagination (deferred)
- Sorting (deferred)
- Token generation or management

## 5. Coverage

- Happy paths (owner with trips, empty list, CREATED trips with 0 counts): **covered**
- Edge cases (cityCode not found → `[]`, no filters → all trips, combined filters, invisible other-owner trips): **covered**
- Error states (invalid date format → 422): **covered**
