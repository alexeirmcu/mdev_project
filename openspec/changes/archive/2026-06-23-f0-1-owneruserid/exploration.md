## Exploration: F0-1 — OwnerUserId in Trip and Authorization

### Current State
The SmartTripPlanner backend has **no authentication or authorization system** whatsoever. The `Trip` aggregate is entirely anonymous — anyone with a `TripId` or `TripCode` can create, read, update, or regenerate itineraries without any identity checks.

Key architectural observations:
- `Trip` (`Domain/AggregatesModel/Trip.cs`) is a pure planning aggregate with properties for dates, city, hotel, travelers, preferences, must-sees, and day plans. No user identity exists.
- `ITripRepository` / `TripRepository` provide CRUD and listing methods. Queries do not filter by user.
- `TripsController` exposes 4 endpoints: `POST /api/trips`, `GET /api/trips/{tripId}`, `PATCH /api/trips/{tripId}`, `POST /api/trips/{tripId}/generate`. None have auth attributes.
- Handlers (`GenerateTripHandler`, `GetTripHandler`, `UpdateTripHandler`, `GenerateTripItineraryHandler`) are pure MediatR pipelines with no identity context.
- The project does **not** reference `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.Identity`, or any auth packages.
- `PlannerDbContext` has no `Users` DbSet.
- 405+ existing tests (MSTest + Moq) assume anonymous access.

### Affected Areas
- `SmartTripPlanner.Domain/AggregatesModel/Trip.cs` — add `OwnerUserId`
- `SmartTripPlanner.Domain/Repository/ITripRepository.cs` — add owner-filtered query methods or owner parameter to existing ones
- `SmartTripPlanner.Infrastructure/Configurations/TripConfiguration.cs` — map `OwnerUserId` column (nullable)
- `SmartTripPlanner.Infrastructure/Repositories/TripRepository.cs` — apply owner filtering in `GetByIdAsync`, `GetByTripCodeAsync`, `ListAsync`
- `SmartTripPlanner.Infrastructure/PlannerDbContext.cs` — migration needed
- `SmartTripPlanner.ApplicationServices/Commands/*.cs` — `GenerateTrip`, `GetTrip`, `UpdateTrip`, `GenerateTripItinerary` need user context propagation
- `SmartTripPlanner.ApplicationServices/Handlers/*.cs` — set ownership on create; enforce ownership on read/update/generate
- `SmartTripPlanner.API/Controllers/TripsController.cs` — extract caller identity and pass into commands/queries
- `SmartTripPlanner.API/Program.cs` — register `IUserContext` implementation
- `tests/SmartTripPlanner.Tests/` — all handler and controller tests need `IUserContext` mocks
- **New**: `Domain/Ports/IUserContext.cs` — abstraction for the current user's identity
- **New**: `API/Services/HttpUserContext.cs` — HTTP-based implementation

### Approaches

1. **Minimal Header-Based Identity + Nullable OwnerUserId (Recommended)**
   - Add `string? OwnerUserId` to `Trip` (nullable to preserve backward compatibility for existing trips).
   - Introduce `IUserContext` domain abstraction with `string? UserId`.
   - Implement `HttpUserContext` in API layer reading `X-User-Id` header (or `ClaimsPrincipal` if claims exist).
   - In `GenerateTripHandler`, set `trip.OwnerUserId = userContext.UserId`.
   - In read/write handlers, after loading the trip, check: if `trip.OwnerUserId` is not null and does not match `userContext.UserId`, throw `UnauthorizedAccessException` (or a domain exception mapped to 403).
   - Existing trips with `null` `OwnerUserId` remain publicly accessible.
   - Pros: No new NuGet packages, no JWT infrastructure, low complexity, fully backward compatible, tests are easy to adapt with a mocked `IUserContext`, future-proof (can swap `HttpUserContext` to read from JWT claims later without touching handlers).
   - Cons: Not cryptographically secure — relies on client-sent header. However, this is acceptable as an authorization enforcement layer assuming an upstream gateway or future JWT middleware will validate identity before the header reaches the API.
   - Effort: Medium

2. **Full JWT Authentication + Non-Nullable OwnerUserId**
   - Add `Microsoft.AspNetCore.Authentication.JwtBearer`, configure JWT validation in `Program.cs`, add `[Authorize]` to `TripsController`.
   - Add `string OwnerUserId` to `Trip` (non-nullable).
   - Create a migration that assigns a default system user ID to all existing trips (or deletes them — risky).
   - Extract `sub` claim as `OwnerUserId`.
   - Pros: Real security, industry standard, no reliance on client headers.
   - Cons: Massive scope creep — key management, token issuance, client breakage, 405 tests break immediately, violates "no UI changes" and "maintain backward compatibility" constraints. Would require a separate auth service or identity provider.
   - Effort: High

3. **Separate Ownership Aggregate**
   - Keep `Trip` pure. Create `TripOwnership { TripId, OwnerUserId }` as a separate entity/table.
   - Repository joins `Trip` and `TripOwnership` on every query.
   - Pros: Trip aggregate remains focused on planning logic.
   - Cons: Unnecessary indirection, violates YAGNI, inconsistent with existing repository patterns, complicates every query with joins, no clear domain benefit.
   - Effort: Medium-High

### Recommendation
**Adopt Approach 1 (Minimal Header-Based Identity + Nullable OwnerUserId).**

Rationale: The project currently has zero auth infrastructure. Introducing full JWT is a multi-feature effort that dwarfs the ownership requirement. The `IUserContext` abstraction gives us a clean seam: today it reads a header, tomorrow it reads from a JWT claim — the handlers and domain remain unchanged. Making `OwnerUserId` nullable is the only viable backward-compatible strategy because we cannot retroactively determine owners for existing anonymous trips.

### Risks
- **Breaking existing tests**: All 405+ tests instantiate trips and call handlers without a user context. Every handler test will need a mocked `IUserContext`. This is mechanical but widespread.
- **Security misunderstanding**: Future developers might think `X-User-Id` header alone is "authentication." We must document clearly that this is **authorization enforcement** assuming trusted identity propagation from an upstream gateway or future auth middleware.
- **ListAsync ambiguity**: `ITripRepository.ListAsync` currently has no controller consumer, but if one is added later, it must filter by `OwnerUserId`. We should add an overload or parameter now to avoid forgetting.
- **EF Core migration**: Adding a nullable string column is safe, but we must generate a migration and ensure it applies cleanly on startup (the app already runs `MigrateAsync()` in `Program.cs`).
- **Forget to enforce on new endpoints**: Any future trip endpoint must receive `IUserContext` and enforce ownership. A consistent pattern (e.g., a domain service like `TripAccessGuard`) reduces this risk.

### Ready for Proposal
Yes. The orchestrator should tell the user:
> "Exploration complete. The recommended path is to add a nullable `OwnerUserId` to `Trip`, introduce a lightweight `IUserContext` abstraction (header-based for now), and enforce ownership in all handlers. This preserves backward compatibility for existing trips and avoids pulling in heavy auth infrastructure. Ready to proceed to proposal."
