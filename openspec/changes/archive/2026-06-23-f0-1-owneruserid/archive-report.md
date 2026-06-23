# Archive Report: f0-1-owneruserid (Trip Ownership via JWT Bearer)

**Archived**: 2026-06-23
**Archive path**: `openspec/changes/archive/2026-06-23-f0-1-owneruserid/`
**Artifact store mode**: openspec

## Change Summary

Added identity-based trip ownership via JWT Bearer token validation. `Trip` carries a non-nullable `OwnerUserId` set from the JWT `sub` claim. All `TripsController` endpoints are protected by `[Authorize]`. Handlers enforce ownership (403 on mismatch, 404 before 403). A DELETE endpoint was added. An EF Core migration adds the NOT NULL column with an index.

## Verdict from Verify Report

- **Final verdict**: PASS WITH WARNINGS (APPROVED_WITH_WARNINGS)
- **Requirements**: 5/5 PASS
- **Design decisions**: 8/8 PASS
- **Tasks**: 29/29 complete
- **Test count**: 425/425 passing (416 unit + 9 integration)
- **CRITICAL issues**: 0 (both prior CRITICALs resolved)
- **WARNING issues**: 3 (non-blocking — see verify-report.md for details)

## Specs Synced

| Domain | Action | Details |
|--------|--------|---------|
| `trip-ownership` | Created (NEW) | Full spec with R1-R5 requirements, API contract, migration, test strategy |
| `trip-interests` | Updated (ADDED) | Appended requirement: GenerateTrip command/handler carry OwnerUserId |
| `itinerary-generation` | Updated (ADDED) | Appended requirement: GenerateTripItineraryHandler enforces ownership before regeneration |

## Source of Truth Updated

- `openspec/specs/trip-ownership/spec.md` — New domain spec created
- `openspec/specs/trip-interests/spec.md` — OwnerUserId requirement appended
- `openspec/specs/itinerary-generation/spec.md` — Ownership enforcement requirement appended

## Archive Contents

| Artifact | Status |
|----------|--------|
| `proposal.md` | ✅ Archived |
| `spec.md` | ✅ Archived (combined delta spec) |
| `design.md` | ✅ Archived (with Applied Deviations documented) |
| `tasks.md` | ✅ Archived (29/29 tasks complete) |
| `apply-progress.md` | ✅ Archived (TDD Cycle Evidence table present) |
| `verify-report.md` | ✅ Archived (APPROVED_WITH_WARNINGS) |
| `archive-report.md` | ✅ This file |

## Design Deviations (Documented)

1. **Middleware ordering** (W4): `ExceptionHandlingMiddleware` placed OUTERMOST (before `UseAuthentication`/`UseAuthorization`) — wraps the full pipeline including auth. Accepted deviation.
2. **HttpUserContext claim resolution**: Added fallback to `ClaimTypes.NameIdentifier` because ASP.NET Core maps `sub` → `NameIdentifier` by default with `MapInboundClaims = true`.

## Post-Implementation Notes

1. **Migration `defaultValue: ""`** (W2): The migration uses `defaultValue: ""` which silently backfills empty string instead of failing loudly on a non-empty table. If strict R5 "fail loudly" behavior is desired, scaffold the migration without `defaultValue`.
2. **TDD reporting gap** (W1): The TDD Cycle Evidence table in `apply-progress.md` is missing TRIANGULATE and SAFETY NET columns. Independently corroborated by 425/425 runtime pass.
3. **Base `appsettings.json` `Jwt:Secret` is empty**: Non-Development environments must override via env variable or the startup crashes (fail-fast, by design).
4. **Integration tests use EF InMemory**: True migration realism (NOT NULL enforcement) is not tested — would require Testcontainers Postgres.

## SDD Cycle Complete

This change has been fully planned (propose → spec → design → tasks), implemented (apply), verified (verify), and archived. Ready for the next change.
