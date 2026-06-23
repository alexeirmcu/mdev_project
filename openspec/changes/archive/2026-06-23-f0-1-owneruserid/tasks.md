# Tasks: Trip Ownership via JWT Bearer (f0-1-owneruserid)

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 550–700 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (Domain+App+Infra+unit tests) → PR 2 (API wiring+JWT+integration tests) |
| Delivery strategy | ask-always |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Domain model, exceptions, IUserContext port, command/handler ownership, repo filter, migration, all updated unit tests | PR 1 | Base = main; self-contained; all unit tests pass |
| 2 | HttpUserContext, [Authorize], Program.cs JWT, appsettings, DELETE endpoint, integration test infra | PR 2 | Base = PR 1 branch (feature-branch-chain) or main (stacked) |

## Phase 1: Domain Foundation (TDD: RED first)

- [x] 1.1 **RED** — Create `Domain/Exceptions/TripForbiddenException.cs` extending `SmartTripDomainException`; test in `tests/.../Exceptions/TripForbiddenExceptionTests.cs`.
- [x] 1.2 **RED** — Create `Domain/Ports/IUserContext.cs` with `string UserId { get; }`; test getter throws when absent.
- [x] 1.3 **GREEN** — Add `required string OwnerUserId { get; init; }` to `Domain/AggregatesModel/Trip.cs`.
- [x] 1.4 **REFACTOR** — Update `tests/.../AggregatesModel/TripTests.cs` — every `Trip` initializer gains `OwnerUserId = "user-42"`.

## Phase 2: Application Layer — Commands & Handlers (TDD)

- [x] 2.1 **RED→GREEN** — Add `string OwnerUserId` to `ApplicationServices/Commands/GenerateTrip.cs` record.
- [x] 2.2 **RED→GREEN** — Create `ApplicationServices/Commands/DeleteTrip.cs`: `record DeleteTrip(Guid TripId) : IRequest<Unit>`.
- [x] 2.3 **GREEN** — Modify `ApplicationServices/Handlers/GenerateTripHandler.cs`: inject `IUserContext`; assert `request.OwnerUserId == _userContext.UserId` (throw `BusinessRuleException` on mismatch); set `trip.OwnerUserId` in `CreateTripAggregate`.
- [x] 2.4 **GREEN** — Modify `ApplicationServices/Handlers/GetTripHandler.cs`: inject `IUserContext`; after load, if `trip.OwnerUserId != _userContext.UserId` throw `TripForbiddenException`.
- [x] 2.5 **GREEN** — Modify `ApplicationServices/Handlers/UpdateTripHandler.cs`: inject `IUserContext`; enforce ownership **before** any mutation (same 403 pattern).
- [x] 2.6 **GREEN** — Modify `ApplicationServices/Handlers/GenerateTripItineraryHandler.cs`: inject `IUserContext`; enforce ownership **before** `IItineraryGenerator.GenerateAsync` and outbox enqueue.
- [x] 2.7 **GREEN** — Create `ApplicationServices/Handlers/DeleteTripHandler.cs`: load → 404/403 → `repo.DeleteAsync(tripId)` → return `Unit.Value`.
- [x] 2.8 **REFACTOR** — Update `Domain/Repository/ITripRepository.cs`: `ListAsync` gains required `string ownerUserId` first param.

## Phase 3: Infrastructure — EF Config, Migration, Repository

- [x] 3.1 Modify `Infrastructure/Configurations/TripConfiguration.cs`: add `.Property(t => t.OwnerUserId).IsRequired().HasMaxLength(100)` and `.HasIndex(t => t.OwnerUserId)`.
- [x] 3.2 Run `dotnet ef migrations add AddTripOwnerUserId` → verify `AddColumn(nullable: false)` + `CreateIndex IX_Trips_OwnerUserId`.
- [x] 3.3 Modify `Infrastructure/Repositories/TripRepository.cs`: apply `OwnerUserId` filter in `ListAsync` (first param).

## Phase 4: API Layer — Auth Wiring & DELETE Endpoint

- [x] 4.1 Add `Microsoft.AspNetCore.Authentication.JwtBearer` package to `API/SmartTripPlanner.API.csproj`.
- [x] 4.2 Create `API/Services/HttpUserContext.cs`: `internal sealed` implementing `IUserContext`; reads `sub` claim via `IHttpContextAccessor`; throws `InvalidOperationException` if absent.
- [x] 4.3 Modify `API/Controllers/TripsController.cs`: add `[Authorize]` class-level; inject `IUserContext`; stamp `GenerateTrip.OwnerUserId` from `_userContext.UserId`; add `DELETE /{tripId}` → `_mediator.Send(new DeleteTrip(tripId))` → `NoContent()`.
- [x] 4.4 Modify `API/Program.cs`: `AddHttpContextAccessor`; `AddAuthentication().AddJwtBearer(HS256)` with `Jwt:{Secret,Issuer,Audience}`; `AddScoped<IUserContext, HttpUserContext>`; `UseAuthentication` → `UseAuthorization` before `MapControllers`.
- [x] 4.5 Add `Jwt:{Secret,Issuer,Audience}` to `API/appsettings.json` and `appsettings.Development.json`.

## Phase 5: Exception Mapping Fix

- [x] 5.1 Modify `API/Middleware/ExceptionHandlingMiddleware.cs` `GetStatusCode` switch: add `TripForbiddenException => 403` and `TripNotFoundException => 404` **before** the generic `DomainException => 422` case.

## Phase 6: Test Updates (TDD — update all 405+ tests)

- [x] 6.1 Update `tests/.../Handlers/GenerateTripHandlerTests.cs`: inject mock `IUserContext` (UserId="user-42"); add `OwnerUserId` to every `Trip` initializer; add test: mismatched owner throws `BusinessRuleException`.
- [x] 6.2 Update `tests/.../Handlers/GetTripHandlerTests.cs`: inject mock `IUserContext`; add tests: matching owner → 200, mismatched → `TripForbiddenException`, missing → `TripNotFoundException`.
- [x] 6.3 Update `tests/.../Handlers/UpdateTripHandlerTests.cs`: inject mock `IUserContext`; add 403/404 ownership tests.
- [x] 6.4 Update `tests/.../Handlers/GenerateTripItineraryHandlerTests.cs`: inject mock `IUserContext`; add test: mismatched owner → 403, generator never called.
- [x] 6.5 Create `tests/.../Handlers/DeleteTripHandlerTests.cs`: mock `IUserContext` + `ITripRepository`; test 204 (owner match), 403 (mismatch), 404 (missing).
- [x] 6.6 Update `tests/.../Controllers/TripsControllerTests.cs`: inject mock `IUserContext`; set `ControllerContext` with `ClaimsPrincipal(sub="user-42")`; add DELETE → 204 test; verify `GenerateTrip.OwnerUserId` populated.
- [x] 6.7 Create integration test infra: `TestJwtTokenFactory` (HS256 with known test secret); configure `Jwt:Secret` in test `appsettings`; test 401 no-token, 403 wrong-owner, 201/200 happy paths.
- [x] 6.8 Run `dotnet test` — all 405+ tests pass.
