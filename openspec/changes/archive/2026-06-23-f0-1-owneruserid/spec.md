# Delta Spec: Trip Ownership via JWT Bearer

**Change**: `f0-1-owneruserid`
**Mode**: Hybrid (OpenSpec + Engram)

> **Divergence from proposal**: The proposal specified a **nullable** `OwnerUserId` with anonymous-trip backward compatibility. This spec **overrides** that decision per orchestrator directive: `OwnerUserId` is **NOT NULL**, there is **no anonymous fallback**, and a mismatched owner returns **403**. The user is responsible for deleting all existing trips **before** applying the migration (see R5). No backward compatibility is provided for pre-existing trips.

---

## Domain: trip-ownership (NEW)

### Requirement: R1 — Trip aggregate stores a non-nullable OwnerUserId

`Trip` (`SmartTripPlanner.Domain/AggregatesModel/Trip.cs`) MUST carry a `string OwnerUserId` property (C# non-nullable reference type). The column SHALL be `NOT NULL` in PostgreSQL. `OwnerUserId` MUST be set at creation time from the authenticated caller and MUST NOT be mutable by any update path (no setter exposed beyond aggregate construction / `GenerateTripHandler`).

#### Scenario: S1 — Create trip with valid JWT sets OwnerUserId from `sub`

- GIVEN a `POST /api/trips` request with a valid Bearer token whose `sub` claim is `"user-42"`
- WHEN `GenerateTripHandler` persists the new `Trip`
- THEN `Trip.OwnerUserId == "user-42"`
- AND the response is `201 Created` with the new `TripId`

#### Scenario: Trip entity rejects null owner at construction

- GIVEN a `Trip` being created without an `OwnerUserId`
- WHEN the aggregate is instantiated by the handler
- THEN an `ArgumentNullException` (or domain guard) is thrown
- AND no trip is persisted

### Requirement: R2 — JWT Bearer middleware validates tokens (HS256, symmetric key)

`Program.cs` MUST register `AddAuthentication().AddJwtBearer()` (package `Microsoft.AspNetCore.Authentication.JwtBearer`) validating HS256-signed tokens with a symmetric key from configuration (`Jwt:Secret`), issuer `Jwt:Issuer`, and audience `Jwt:Audience`. The pipeline MUST call `UseAuthentication()` then `UseAuthorization()` before `MapControllers()`. Token generation is **out of scope** (external, e.g. jwt.io/script, for MVP).

#### Scenario: S4 — Request without JWT is rejected

- GIVEN a `GET /api/trips/{tripId}` request with no `Authorization` header
- WHEN it reaches the middleware
- THEN the response is `401 Unauthorized`

#### Scenario: Request with malformed/expired token is rejected

- GIVEN a request with a Bearer token whose signature is invalid or `exp` has passed
- WHEN the JWT handler validates it
- THEN the response is `401 Unauthorized`

### Requirement: R3 — Controller is authorized and extracts UserId from the `sub` claim

`TripsController` (`SmartTripPlanner.API/Controllers/TripsController.cs`) MUST be decorated with `[Authorize]` (class level). The `sub` claim (RFC 7519 standard subject — NOT a custom claim) MUST be read via `IUserContext` and propagated into every command/query as `OwnerUserId`. `IUserContext` MUST be a Domain port (`SmartTripPlanner.Domain/Ports/IUserContext.cs`) with `string UserId { get; }` (non-nullable; throws if absent). Its API implementation `HttpUserContext` (`SmartTripPlanner.API/Services/HttpUserContext.cs`) MUST resolve `UserId` from `HttpContext.User.Claims` `sub` claim.

#### Scenario: Controller populates command OwnerUserId from `sub`

- GIVEN an authenticated request with `sub = "user-42"`
- WHEN `TripsController.CreateTrip` builds the `GenerateTrip` command
- THEN the command carries `OwnerUserId = "user-42"`

### Requirement: R4 — Handlers enforce ownership on every read/write operation

`GenerateTripHandler`, `GetTripHandler`, `UpdateTripHandler`, and `GenerateTripItineraryHandler` (`SmartTripPlanner.ApplicationServices/Handlers/`) MUST receive `IUserContext`. On any operation that loads an existing `Trip`, the handler MUST compare `trip.OwnerUserId` to `userContext.UserId`; a mismatch MUST throw an exception mapped to `403 Forbidden`. A missing trip MUST return `404 Not Found`. Ownership check MUST occur **before** any mutation or itinerary generation.

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

---

## Domain: trip-interests (ADDED Requirements)

### Requirement: GenerateTrip command and handler carry OwnerUserId

`GenerateTrip` (`SmartTripPlanner.ApplicationServices/Commands/GenerateTrip.cs`) MUST include an `OwnerUserId` parameter. `GenerateTripHandler` MUST set `Trip.OwnerUserId` from the command (sourced from `IUserContext.UserId`) **before** `ITripRepository.AddAsync`. No request DTO change is required (owner is never client-supplied).

#### Scenario: Owner captured on create from user context

- GIVEN `IUserContext.UserId == "user-42"` and a valid `GenerateTrip` command
- WHEN `GenerateTripHandler` constructs the `Trip`
- THEN `Trip.OwnerUserId == "user-42"` before persistence

---

## Domain: itinerary-generation (ADDED Requirements)

### Requirement: GenerateTripItineraryHandler enforces ownership before regeneration

`GenerateTripItineraryHandler` MUST load the trip, verify `trip.OwnerUserId == IUserContext.UserId`, and throw an ownership exception (→ `403`) **before** invoking `IItineraryGenerator.GenerateAsync` or enqueuing outbox messages (FR15). The existing outbox trigger behavior (FR15) is otherwise unchanged.

#### Scenario: Regeneration blocked for non-owner

- GIVEN a trip owned by `"user-42"` and a regenerate request with `sub = "user-99"`
- WHEN `GenerateTripItineraryHandler` runs
- THEN a `403 Forbidden` is returned
- AND `IItineraryGenerator.GenerateAsync` is never called
- AND no outbox messages are enqueued

---

## Domain: trip-ownership — API Contract Changes

### Requirement: All TripsController endpoints require authorization

Every endpoint on `TripsController` (`POST`, `GET`, `PATCH`, `POST .../generate`) MUST be reached only when `[Authorize]` passes. Request models (`TripGenerationRequest`, `TripUpdateRequest`) SHALL NOT carry `OwnerUserId` (never client-supplied). Response models (`TripPlanResponse`) remain unchanged. A `DELETE /api/trips/{tripId}` endpoint MAY be added to satisfy S7; if absent at spec time, the apply phase MUST add it.

#### Scenario: S4 — All endpoints reject anonymous requests

- GIVEN a request to any `TripsController` endpoint without a valid Bearer token
- WHEN the request is processed
- THEN the response is `401 Unauthorized`

---

## Domain: trip-ownership — Database Migration

### Requirement: Migration adds NOT NULL OwnerUserId with index

An EF Core migration MUST add `OwnerUserId` (`varchar`, NOT NULL) to the `Trips` table via `TripConfiguration` (`SmartTripPlanner.Infrastructure/Configurations/TripConfiguration.cs`) and `PlannerDbContext`. A non-unique index on `OwnerUserId` MUST be created to support owner-filtered queries. The migration assumes no existing data (R5). Repository queries (`TripRepository`) MUST be database-agnostic (no provider-specific syntax per `dotnet-clean-arch`); `ITripRepository.GetByIdAsync`/`GetByTripCodeAsync` MAY add an optional owner filter parameter, and `ListAsync` MUST filter by `OwnerUserId`.

#### Scenario: Column and index created

- GIVEN an empty `Trips` table and R5 satisfied
- WHEN the migration runs
- THEN `OwnerUserId` is added as NOT NULL `varchar`
- AND an index `IX_Trips_OwnerUserId` exists

---

## Domain: trip-ownership — Test Strategy

### Requirement: tests authenticate with generated JWTs and mock IUserContext

Integration tests MUST generate HS256 JWTs with a known test secret and send `Authorization: Bearer <token>`. Handler unit tests MUST mock `IUserContext` (via Moq) returning a fixed `UserId`. All existing 405+ tests MUST continue to pass after updating controller/handler construction. Strict TDD is active: write failing tests for S1–S8 first, then implement.

#### Scenario: Existing tests pass after auth wiring

- GIVEN the full test suite (405+ tests) updated with token generation and `IUserContext` mocks
- WHEN `dotnet test` runs
- THEN all tests pass (no anonymous-access assumptions remain)

---

## Coverage

- Happy paths (S1, S2, S5, S6, S7): **covered**
- Edge cases (non-owner S3, non-existent S8, null owner guard, malformed token, empty vs non-empty table at migration): **covered**
- Error states (401 S4, 403, 404 S8): **covered**

## Next Step
Ready for design (`sdd-design`) to define `IUserContext`/`HttpUserContext` contracts, JWT configuration schema, exception→HTTP mapping (403/401), and the `GenerateTrip` command reshape. If design exists, ready for tasks (`sdd-tasks`).