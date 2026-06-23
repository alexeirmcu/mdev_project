# Proposal: Trip Ownership via JWT Bearer

## Intent

Add `OwnerUserId` to `Trip` and enforce ownership using JWT Bearer token validation. Currently, trips are anonymous — anyone with a `TripId` can access or modify any trip. This change introduces identity-based authorization while preserving backward compatibility for existing trips.

## Scope

### In Scope
- Add `string? OwnerUserId` to `Trip` aggregate (nullable for backward compat)
- Add `Microsoft.AspNetCore.Authentication.JwtBearer` package
- Configure JWT validation (HS256, symmetric key) in `Program.cs`
- Add `[Authorize]` to `TripsController`
- Extract `UserId` from `sub` claim, pass to handlers
- Enforce ownership in `GenerateTrip`, `GetTrip`, `UpdateTrip`, `GenerateTripItinerary` handlers
- EF Core migration for nullable `OwnerUserId` column
- Update all tests to include JWT tokens

### Out of Scope
- Login/register endpoints (tokens generated externally via jwt.io/script for MVP)
- Role-based access control
- Token refresh or revocation

## Capabilities

### New Capabilities
- `trip-ownership`: JWT-based identity extraction, `OwnerUserId` enforcement, anonymous-trip backward compatibility

### Modified Capabilities
- `trip-interests`: `GenerateTrip` handler sets `OwnerUserId` from JWT `sub` claim
- `itinerary-generation`: `GenerateTripItinerary` handler enforces ownership before regeneration

## Approach

JWT Bearer middleware validates externally-generated tokens (HS256 symmetric key). Introduce `IUserContext` domain port with `string? UserId`; `HttpUserContext` reads `sub` claim from `ClaimsPrincipal`. `OwnerUserId` is nullable so existing anonymous trips remain accessible. New trips capture the caller's `sub`. On read/update/generate, if `OwnerUserId` is non-null and mismatches the caller, return 403.

**Key decisions:**
- **JWT vs header**: Cryptographic validation prevents spoofing; `IUserContext` keeps handlers decoupled from auth mechanics.
- **Nullable `OwnerUserId`**: We cannot retroactively assign owners to existing trips.
- **Anonymous trips**: `null` owner means no enforcement (public access preserved).
- **`sub` claim**: Standard OIDC claim for user identifier.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Domain/AggregatesModel/Trip.cs` | Modified | Add `string? OwnerUserId` |
| `Domain/Ports/IUserContext.cs` | New | Domain abstraction for caller identity |
| `API/Services/HttpUserContext.cs` | New | JWT claim extraction |
| `API/Controllers/TripsController.cs` | Modified | Add `[Authorize]`, pass `UserId` to commands |
| `API/Program.cs` | Modified | `AddAuthentication().AddJwtBearer()` |
| `ApplicationServices/Handlers/*` | Modified | Set/enforce `OwnerUserId` |
| `Infrastructure/Repositories/TripRepository.cs` | Modified | Filter by owner where applicable |
| `tests/*` | Modified | JWT token generation in test setup |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| 405+ tests break | High | Generate test JWTs with known secret; update all controller tests |
| Migration conflicts | Low | Nullable column addition is safe |
| Client breakage | Med | Document that external token generation is required |

## Rollback Plan

1. Revert migration (drop `OwnerUserId` column)
2. Remove `[Authorize]` attributes
3. Remove JWT configuration from `Program.cs`
4. Remove `Microsoft.AspNetCore.Authentication.JwtBearer` package reference

## Dependencies

- `Microsoft.AspNetCore.Authentication.JwtBearer` NuGet package

## Success Criteria

- [ ] All trips created after change have `OwnerUserId` set from JWT `sub` claim
- [ ] Existing trips with `null` `OwnerUserId` remain accessible (no 403)
- [ ] Unauthenticated requests return 401
- [ ] Authenticated requests for trips owned by another user return 403
- [ ] All 405+ tests pass

## Proposal Question Round

Before finalizing specs, confirm or correct the following assumptions:

1. **Business problem**: Is the primary driver regulatory/compliance, or is it user-expected privacy (e.g., "I don't want others editing my trip")?
2. **Anonymous trip sunset**: Should we eventually require all trips to have an owner, or is anonymous access a permanent feature?
3. **Claim choice**: Is `sub` the right claim, or does the external token generator use `nameid`?
4. **Scope of 403**: Should `GET /api/trips/{tripId}` return 404 (not found) instead of 403 for unauthorized trips, to avoid leaking trip existence?
5. **Token secret management**: For MVP, is a single hardcoded symmetric key acceptable, or should it come from environment/configuration?
