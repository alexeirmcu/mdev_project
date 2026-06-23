# Trip Ownership Specification

## 1. Summary

Add identity-based trip ownership via JWT Bearer token validation. `Trip` carries a non-nullable `OwnerUserId` set from the JWT `sub` claim. All `TripsController` endpoints are protected by `[Authorize]`. Handlers enforce ownership by comparing `trip.OwnerUserId` to the caller, returning 403 on mismatch and 404 when the trip doesn't exist. A migration adds a NOT NULL `OwnerUserId` column with an index. This is a breaking change — existing trips MUST be deleted before applying the migration (no backward compatibility).

## 2. Requirements

### Requirement: R1 — Trip aggregate stores a non-nullable OwnerUserId

`Trip` MUST carry a `string OwnerUserId` property (C# non-nullable reference type). The column SHALL be `NOT NULL` in PostgreSQL. `OwnerUserId` MUST be set at creation time from the authenticated caller and MUST NOT be mutable by any update path (no setter exposed beyond aggregate construction).

#### Scenario: S1 — Create trip with valid JWT sets OwnerUserId from `sub`

- GIVEN a `POST /api/trips` request with a valid Bearer token whose `sub` claim is `"user-42"`
- WHEN the `GenerateTripHandler` persists the new `Trip`
- THEN `Trip.OwnerUserId == "user-42"`
- AND the response is `201 Created` with the new `TripId`

#### Scenario: Trip entity rejects null owner at construction

- GIVEN a `Trip` being created without an `OwnerUserId`
- WHEN the aggregate is instantiated by the handler
- THEN an `ArgumentNullException` (or domain guard) is thrown
- AND no trip is persisted

### Requirement: R2 — JWT Bearer middleware validates tokens (HS256, symmetric key)

`Program.cs` MUST register `AddAuthentication().AddJwtBearer()` validating HS256-signed tokens with a symmetric key from configuration (`Jwt:Secret`), issuer `Jwt:Issuer`, and audience `Jwt:Audience`. The pipeline MUST call `UseAuthentication()` then `UseAuthorization()` before `MapControllers()`. Token generation is **out of scope** (external, e.g. jwt.io/script, for MVP).

#### Scenario: S4 — Request without JWT is rejected

- GIVEN a `GET /api/trips/{tripId}` request with no `Authorization` header
- WHEN it reaches the middleware
- THEN the response is `401 Unauthorized`

#### Scenario: Request with malformed/expired token is rejected

- GIVEN a request with a Bearer token whose signature is invalid or `exp` has passed
- WHEN the JWT handler validates it
- THEN the response is `401 Unauthorized`

### Requirement: R3 — Controller is authorized and extracts UserId from the `sub` claim

`TripsController` MUST be decorated with `[Authorize]` (class level). The `sub` claim (RFC 7519 standard subject) MUST be read via `IUserContext` and propagated into every command/query as `OwnerUserId`. `IUserContext` MUST be a Domain port with `string UserId { get; }` (non-nullable; throws if absent). Its API implementation `HttpUserContext` MUST resolve `UserId` from `HttpContext.User.Claims` `sub` claim.

#### Scenario: Controller populates command OwnerUserId from `sub`

- GIVEN an authenticated request with `sub = "user-42"`
- WHEN `TripsController.CreateTrip` builds the `GenerateTrip` command
- THEN the command carries `OwnerUserId = "user-42"`

### Requirement: R4 — Handlers enforce ownership on every read/write operation

`GenerateTripHandler`, `GetTripHandler`, `UpdateTripHandler`, and `GenerateTripItineraryHandler` MUST receive `IUserContext`. On any operation that loads an existing `Trip`, the handler MUST compare `trip.OwnerUserId` to `userContext.UserId`; a mismatch MUST throw an exception mapped to `403 Forbidden`. A missing trip MUST return `404 Not Found`. Ownership check MUST occur **before** any mutation or itinerary generation.

#### Scenario: S2 — Get trip with matching owner returns 200

- GIVEN a trip where `OwnerUserId == "user-42"` and a request with `sub = "user-42"`
- WHEN `GetTripHandler` runs
- THEN the response is `200 OK` with the trip

#### Scenario: S3 — Get trip with non-matching owner returns 403

- GIVEN a trip where `OwnerUserId == "user-42"` and a request with `sub = "user-99"`
- WHEN `GetTripHandler` runs
- THEN the response is `403 Forbidden`
- AND no trip data is returned

#### Scenario: S5 — Update trip with matching owner returns 200

- GIVEN a trip owned by `"user-42"` and a `PATCH` request with `sub = "user-42"`
- WHEN `UpdateTripHandler` runs
- THEN the trip is updated and the response is `200 OK`

#### Scenario: S6 — Generate itinerary with matching owner returns 200

- GIVEN a trip owned by `"user-42"` and a `POST .../generate` with `sub = "user-42"`
- WHEN `GenerateTripItineraryHandler` runs
- THEN the itinerary is regenerated and the response is `200 OK`

#### Scenario: S7 — Delete trip with matching owner returns 204

- GIVEN a trip owned by `"user-42"` and a `DELETE` with `sub = "user-42"`
- WHEN the delete handler runs
- THEN the trip is removed and the response is `204 No Content`

#### Scenario: S8 — Any operation on a non-existent trip returns 404

- GIVEN a request for `tripId` that does not exist in the database
- WHEN any trip handler runs
- THEN the response is `404 Not Found`
- AND no ownership check is performed (404 takes precedence over 403)

#### Scenario: Update/Generate/Delete with non-matching owner returns 403

- GIVEN a trip owned by `"user-42"` and a request with `sub = "user-99"`
- WHEN `UpdateTripHandler`, `GenerateTripItineraryHandler`, or the delete handler runs
- THEN the response is `403 Forbidden`
- AND no mutation occurs

### Requirement: R5 — Existing trips MUST be deleted before applying the migration

Because `OwnerUserId` is NOT NULL and tokens are generated externally (no retroactive owner assignment), the database MUST contain **zero** `Trip` rows when the migration is applied. This is a **user responsibility** precondition, not enforced by the migration. The migration MUST NOT attempt to backfill a default owner.

#### Scenario: Migration applied to empty table succeeds

- GIVEN the `Trips` table has zero rows (user deleted all existing trips)
- WHEN the `OwnerUserId` NOT NULL migration is applied
- THEN the migration succeeds and the column is added as NOT NULL

#### Scenario: Migration applied to non-empty table fails loudly

- GIVEN the `Trips` table still has rows lacking `OwnerUserId`
- WHEN the NOT NULL migration is applied
- THEN the migration fails (PostgreSQL NOT NULL violation)
- AND the user is instructed to delete existing trips and retry

## 3. API Contract

### All TripsController endpoints require authorization

Every endpoint on `TripsController` (`POST`, `GET`, `PATCH`, `POST .../generate`) MUST be reached only when `[Authorize]` passes. Request models (`TripGenerationRequest`, `TripUpdateRequest`) SHALL NOT carry `OwnerUserId` (never client-supplied). Response models (`TripPlanResponse`) remain unchanged. A `DELETE /api/trips/{tripId}` endpoint MUST be added.

#### Scenario: All endpoints reject anonymous requests

- GIVEN a request to any `TripsController` endpoint without a valid Bearer token
- WHEN the request is processed
- THEN the response is `401 Unauthorized`

## 4. Database Migration

### Migration adds NOT NULL OwnerUserId with index

An EF Core migration MUST add `OwnerUserId` (`varchar`, NOT NULL) to the `Trips` table via `TripConfiguration` and `PlannerDbContext`. A non-unique index on `OwnerUserId` MUST be created to support owner-filtered queries. The migration assumes no existing data (R5). Repository queries (`TripRepository`) MUST be database-agnostic; `ITripRepository.GetByIdAsync`/`GetByTripCodeAsync` MAY add an optional owner filter parameter, and `ListAsync` MUST filter by `OwnerUserId`.

#### Scenario: Column and index created

- GIVEN an empty `Trips` table and R5 satisfied
- WHEN the migration runs
- THEN `OwnerUserId` is added as NOT NULL `varchar`
- AND an index `IX_Trips_OwnerUserId` exists

## 5. Test Strategy

### Tests authenticate with generated JWTs and mock IUserContext

Integration tests MUST generate HS256 JWTs with a known test secret and send `Authorization: Bearer <token>`. Handler unit tests MUST mock `IUserContext` returning a fixed `UserId`. All existing 405+ tests MUST continue to pass after updating controller/handler construction. Strict TDD is active: write failing tests first, then implement.

#### Scenario: Existing tests pass after auth wiring

- GIVEN the full test suite (425+ tests) updated with token generation and `IUserContext` mocks
- WHEN `dotnet test` runs
- THEN all tests pass (no anonymous-access assumptions remain)

## 6. Coverage

- Happy paths (S1, S2, S5, S6, S7): **covered**
- Edge cases (non-owner S3, non-existent S8, null owner guard, malformed token, empty vs non-empty table at migration): **covered**
- Error states (401 S4, 403, 404 S8): **covered**
