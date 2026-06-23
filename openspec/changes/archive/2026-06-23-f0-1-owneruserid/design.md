# Design: Trip Ownership via JWT Bearer (f0-1-owneruserid)

## Technical Approach

Implements the **NOT NULL** variant of spec `f0-1-owneruserid` (this overrides the proposal's nullable stance per orchestrator directive). Add `string OwnerUserId` (`required init`) to the `Trip` aggregate; introduce the `IUserContext` Domain port resolved by `HttpUserContext` from the JWT `sub` claim; protect the whole `TripsController` with `[Authorize]` wired to HS256 JWT Bearer. Create stamping flows through the `GenerateTrip` command (sourced from `IUserContext.UserId`); read/update/itinerary/delete handlers inject `IUserContext` and enforce ownership by comparing the loaded `trip.OwnerUserId` to the caller, raising typed exceptions mapped centrally to 404/403. An EF migration adds the non-nullable column + index assuming an empty `Trips` table (R5).

## Architecture Decisions

| # | Decision | Choice | Rejected alternative | Rationale |
|---|----------|--------|----------------------|-----------|
| D1 | `OwnerUserId` mutability | `required string OwnerUserId { get; init; }` on `Trip` | factory `Trip.Create(...)`; plain `set` | Matches the existing `init`-only pattern (`TripCode`, `TripId`, `CreatedAt`); `required` forces every caller (incl. tests) to supply it → satisfies "rejects null owner at construction" at **compile time**. EF materialization uses the backing field like other init props — no factory refactor. |
| D2 | Owner source on **create** | Controller reads `IUserContext.UserId` and stores it in `GenerateTrip.OwnerUserId`; handler injects `IUserContext` and asserts `request.OwnerUserId == _userContext.UserId` | handler reads `IUserContext` only | Spec R-trip-interests mandates the command carries `OwnerUserId`; R4 mandates handlers inject `IUserContext`. Cross-check prevents any other command producer from stamping a different owner → throws `BusinessRuleException` on mismatch. |
| D3 | Owner enforcement on read/update/itinerary/delete | Handler injects `IUserContext`, loads trip unfiltered; null → `TripNotFoundException` (404); `trip.OwnerUserId != caller` → `TripForbiddenException` (403) | repo filters owner in the query | S8 mandates **404 precedes 403**; collapsing both into "not found" via the query hides ownership leakage. Handler distinguishes missing trip from other-owner. |
| D4 | `ListAsync` owner filter | Add **required** `string ownerUserId` first param to `ITripRepository.ListAsync`; filter `.Where(t => t.OwnerUserId == ownerUserId)` | keep city/date-only | Spec R-migration requires `ListAsync` filter by `OwnerUserId`. No handler consumes it today → safe signature change. `GetByIdAsync`/`GetByTripCodeAsync` stay unfiltered (see D3). |
| D5 | Exception → HTTP mapping | Extend `ExceptionHandlingMiddleware` switch with `TripForbiddenException → 403` and `TripNotFoundException → 404` **before** the generic `DomainException → 422` case | controller `try/catch` returning status | Centralized mapping matches the existing pattern; keeps controllers thin. **Today `TripNotFoundException` is wrongly folded into 422** — this design corrects it. Specific types must precede the base in the switch. |
| D6 | `IUserContext` lifetime | Scoped; depends on `IHttpContextAccessor` | Singleton/Transient | `HttpContext` is per-request; lifetime must track the request scope. |
| D7 | DELETE endpoint | Add `DELETE /api/trips/{tripId}` + `DeleteTripHandler` reusing existing `ITripRepository.DeleteAsync(Guid)` after handler-side 404/403 checks | new repo `DeleteAsync(Trip)` overload | Satisfies S7 with minimal surface change. Accepts a minor double-load (handler loads to check, repo re-loads to remove) and a racy idempotent no-op for MVP. |
| D8 | JWT configuration | `Jwt:{Secret,Issuer,Audience}` in `appsettings`; HS256 symmetric; validate issuer + audience + lifetime + signing key | per-user keys / RS256 / IdP | Spec R2 mandates HS256 symmetric + external token generation. Secret ≥ 32 bytes to clear HS256. |

## Data Flow

```
Client ──[Bearer JWT]──► TripsController [Authorize]
   │  HttpUserContext.UserId ──► injected into Controller
   ▼
GenerateTrip(OwnerUserId=user) ──► GenerateTripHandler (+ IUserContext equality guard)
   │                                             │
   ▼                                             ▼
   Trip{OwnerUserId=user} ── AddAsync ──► Postgres (Trips.OwnerUserId NOT NULL, IX_Trips_OwnerUserId)

Read / Update / Itinerary / Delete path:
   Handler loads Trip (UNFILTERED)
     │
     ├─ null            ──► TripNotFoundException  ──► 404
     ├─ owner != caller ──► TripForbiddenException ──► 403   (BEFORE any mutation/outbox)
     └─ ok              ──► mutate/return           ──► 200/204
   ExceptionHandlingMiddleware maps 403/404/422.
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `Domain/AggregatesModel/Trip.cs` | Modify | Add `required string OwnerUserId { get; init; }` |
| `Domain/Ports/IUserContext.cs` | Create | `string UserId { get; }` (non-null; throws if `sub` absent) |
| `Domain/Exceptions/TripForbiddenException.cs` | Create | `SmartTripDomainException` subclass → 403; cites tripId + caller |
| `ApplicationServices/Commands/GenerateTrip.cs` | Modify | Append `string OwnerUserId` to record |
| `ApplicationServices/Commands/DeleteTrip.cs` | Create | `record DeleteTrip(Guid TripId) : IRequest` |
| `ApplicationServices/Handlers/GenerateTripHandler.cs` | Modify | Inject `IUserContext`; assert `request.OwnerUserId == _userContext.UserId`; set `trip.OwnerUserId` |
| `ApplicationServices/Handlers/GetTripHandler.cs` | Modify | Inject `IUserContext`; enforce ownership → `TripForbiddenException` |
| `ApplicationServices/Handlers/UpdateTripHandler.cs` | Modify | Inject `IUserContext`; enforce ownership **before mutation** |
| `ApplicationServices/Handlers/GenerateTripItineraryHandler.cs` | Modify | Inject `IUserContext`; enforce **before** `IItineraryGenerator.GenerateAsync` / outbox enqueue |
| `ApplicationServices/Handlers/DeleteTripHandler.cs` | Create | Load → 404/403 → `repo.DeleteAsync(tripId)`; returns `Unit` |
| `Domain/Repository/ITripRepository.cs` | Modify | `ListAsync` gains **required** `string ownerUserId` first param |
| `Infrastructure/Repositories/TripRepository.cs` | Modify | Apply `OwnerUserId` filter in `ListAsync` |
| `Infrastructure/Configurations/TripConfiguration.cs` | Modify | `.Property(t=>t.OwnerUserId).IsRequired().HasMaxLength(100); .HasIndex(t=>t.OwnerUserId);` |
| `Infrastructure/Migrations/<ts>_AddTripOwnerUserId.cs` | Create | `AddColumn(nullable:false)` + `CreateIndex IX_Trips_OwnerUserId` |
| `API/Services/HttpUserContext.cs` | Create | `IHttpContextAccessor` → reads `sub` → throws `InvalidOperationException` if absent |
| `API/Controllers/TripsController.cs` | Modify | `[Authorize]` class-level; inject `IUserContext`; stamp `GenerateTrip.OwnerUserId`; add `DELETE` endpoint returning `NoContent()` |
| `API/Program.cs` | Modify | `AddHttpContextAccessor`; `AddAuthentication().AddJwtBearer(HS256)`; `AddScoped<IUserContext,HttpUserContext>`; `UseAuthentication` → `UseAuthorization` before `MapControllers` (after `UseHttpsRedirection`, before `UseMiddleware<ExceptionHandlingMiddleware>`) |
| `API/appsettings.json`, `appsettings.Development.json` | Modify | Add `Jwt:{Secret,Issuer,Audience}` |
| `API/SmartTripPlanner.API.csproj` | Modify | Add `Microsoft.AspNetCore.Authentication.JwtBearer` package |

## Interfaces / Contracts

```csharp
// Domain port — SmartTripPlanner.Domain/Ports/IUserContext.cs
namespace SmartTripPlanner.Domain.Ports;
public interface IUserContext { string UserId { get; } } // non-null; throws if 'sub' absent

// API impl — SmartTripPlanner.API/Services/HttpUserContext.cs (internal sealed)
internal sealed class HttpUserContext(IHttpContextAccessor accessor) : IUserContext
{
    public string UserId => accessor.HttpContext?.User.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("JWT 'sub' claim is missing.");
}

// Domain exception — SmartTripPlanner.Domain/Exceptions/TripForbiddenException.cs
public class TripForbiddenException : SmartTripDomainException
{
    public TripForbiddenException(Guid tripId, string caller)
        : base($"Trip '{tripId}' does not belong to caller '{caller}'.") { }
}
```

`ExceptionHandlingMiddleware.GetStatusCode` excerpt (ordering matters — specific first):
```csharp
TripForbiddenException => StatusCodes.Status403Forbidden,
TripNotFoundException  => StatusCodes.Status404NotFound,   // currently folded into 422; fixed here
DomainException         => StatusCodes.Status422UnprocessableEntity,
```

## Testing Strategy

| Layer | What | How |
|-------|------|-----|
| Unit (Domain) | `Trip` cannot be built without `OwnerUserId` | `required` ⇒ compile-error; existing `TripTests` updated to supply `OwnerUserId` in every initializer |
| Unit (Handlers) | Create stamps owner; Get/Update/Itinerary/Delete enforce 403 on mismatch, 404 on missing; itinerary never calls generator/outbox on mismatch | Moq `IUserContext` with fixed `UserId`; **update every existing handler-test constructor** to inject the mock; **every existing `Trip` initializer** gains `OwnerUserId` |
| Unit (Controllers) | `[Authorize]` present; `GenerateTrip.OwnerUserId` populated from `IUserContext`; DELETE → 204 | Inject mock `IUserContext`; set a `ControllerContext` with a `ClaimsPrincipal` carrying `sub = "user-42"` |
| Integration (NEW infra) | 401 no-token, 403 wrong-owner, 404 missing, 201/200 happy paths | `WebApplicationFactory<Program>` + real HS256 JWT via a `TestJwtTokenFactory`; configure `Jwt:Secret` in test `appsettings`; target test Postgres/Testcontainers |
| Repository / Migration | `ListAsync` filters by owner; migration adds column + index | existing repository tests + migration run against an empty table (R5 scenario) |

## Applied Deviations (from apply-fix)

### W4 — Middleware Pipeline Ordering
The file-changes table above states `UseAuthentication`/`UseAuthorization` should run **before** `UseMiddleware<ExceptionHandlingMiddleware>`. The actual implementation in `Program.cs` places them **after** the exception handler middleware:

```
UseHttpsRedirection → UseMiddleware<ExceptionHandlingMiddleware> → UseAuthentication → UseAuthorization → MapControllers
```

**Rationale**: `ExceptionHandlingMiddleware` should wrap the entire HTTP pipeline — including middleware that may throw (auth, model binding, etc.) — so all unhandled exceptions are caught and mapped consistently. Spec R2 only requires `UseAuthentication` → `UseAuthorization` before `MapControllers`, which is satisfied regardless of where the exception handler sits relative to auth.

The design table entry is **amended** to match the implementation. No functional impact.

### HttpUserContext Claim Resolution
The `HttpUserContext.UserId` property originally only read the raw JWT `"sub"` claim. Because ASP.NET Core's JWT Bearer has `MapInboundClaims = true` by default, the `"sub"` claim is mapped to `ClaimTypes.NameIdentifier`. The implementation adds a fallback:

```csharp
user.FindFirst("sub")?.Value
    ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
    ?? throw new InvalidOperationException("JWT 'sub' claim is missing.");
```

This ensures compatibility with the default claim mapping behavior. No change to the interface or controller logic.

## Migration / Rollout

Migration `<timestamp>_AddTripOwnerUserId` adds `OwnerUserId` (`varchar(100)`, **NOT NULL**) + non-unique `IX_Trips_OwnerUserId`. Developer precondition: delete all `Trips` rows before applying (R5). **No backfill**, no default value.

**Phased rollout (400-line review-budget mitigation):** recommend chained PRs —
- **PR1**: Domain + Application + Infrastructure — entity `OwnerUserId`, exceptions, `IUserContext`, command/handler ownership enforcement, repo `ListAsync` filter, `TripConfiguration`, migration, and all updated **unit** tests.
- **PR2**: API wiring — `HttpUserContext`, `[Authorize]`, `Program.cs` JWT Bearer, `appsettings`, the new **integration-test** infrastructure + `TestJwtTokenFactory`.

## Open Questions

- [ ] Integration-test data store: Testcontainers Postgres vs EF InMemory / SQLite. EF InMemory won't enforce NOT NULL and `text[]` columns behave differently — recommend **Testcontainers** for migration realism.
- [ ] `DeleteTripHandler` return type — `Unit` (MediatR) vs a dedicated result. Recommended: `Unit`; controller returns `NoContent()`.
- [ ] Should the JWT validation also require a `ClockSkew` of <= 30s, or default 5 min? Recommend default 5 min for MVP.