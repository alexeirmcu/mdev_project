# Archive Report: generator-refactor-interests

**Archived**: 2026-06-18
**Change Name**: generator-refactor-interests
**Commit**: 76066c1
**Mode**: openspec

## Change Summary

Refactored `HeuristicItineraryGenerator` into 5 collaborator classes (PinnedPlacementPhase, UnpinnedPlacementPhase, CandidateFillingPhase, TransitEnrichmentPhase, WeatherEnrichmentPhase). Added `ActivityNode.Location` for real Haversine distance scoring (replacing the `return 1.0` stub). Added `TripPreferences.Interests` with PostgreSQL `text[]` persistence and interest-based candidate filtering in `IPlaceRepository`. Created `GET /api/cities/{cityCode}/interests` endpoint for city interest discovery.

## Files Changed

~90 files modified/created across 2 PRs (Model+API, Generator Refactor).

### Key Files
- **Domain**: ActivityNode.cs, TripPreferences.cs, IPlaceRepository.cs, IPinnedPlacementPhase.cs, IUnpinnedPlacementPhase.cs, ICandidateFillingPhase.cs, ITransitEnrichmentPhase.cs, IWeatherEnrichmentPhase.cs
- **Domain Services**: PinnedPlacementPhase.cs, UnpinnedPlacementPhase.cs, CandidateFillingPhase.cs, TransitEnrichmentPhase.cs, WeatherEnrichmentPhase.cs
- **Infrastructure**: HeuristicItineraryGenerator.cs (refactored), PlaceRepository.cs (new methods), TripConfiguration.cs, ApplicationServicesRegistration.cs
- **API**: CitiesController.cs, GetCityInterests.cs, GetCityInterestsHandler.cs, GenerateTripValidator.cs
- **Tests**: HeuristicItineraryGeneratorTests.cs, + unit tests for all 5 phase classes, + integration tests

### EF Core Migrations
1. `AddTripCityForeignKey` — Added FK constraint between Trip and City
2. `AddActivityNodeLocation` — Added `Location_Latitude`/`Location_Longitude` to 3 activity tables (MorningActivities, AfternoonActivities, EveningActivities)
3. `AddTripPreferencesInterests` — Added `Interests` text[] column to Preferences

## Test Results

- **287/287 tests passing** (up from 268 baseline)
- 19 new tests added for: phase collaborators, Haversine distance, interest filtering, city interests endpoint
- All existing tests continue to pass (regression-free)

## Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Phase extraction pattern | 5 DI-injected collaborator classes | Independently testable; no artificial `IPhase` abstraction |
| `ActivityNode.Location` | `OwnsOne` on 3 activity tables | Matches existing EF Core pattern |
| `TripPreferences.Interests` persistence | PostgreSQL `text[]` | Native array support; server-side `ANY()` queries |
| Interest filtering | EF Core `Any` + `Contains` → SQL EXISTS subquery | Server-side filtering; no in-memory materialization |
| Distance scoring | Reused `PlaceLocation.DistanceKmTo()` | Already had Haversine; removed `_placesById` mutable state |
| City interests endpoint | CQRS query via MediatR + controller action | Matches project pattern |
| Validator scope | `GenerateTripValidator` only (new trips) | Existing trips backward compatible |

## Stale Checkbox Reconciliation

The archived `tasks.md` contains 2 unchecked verification items. These are confirmed complete per orchestrator-provided proof:
- `[ ] Endpoint tests for city interests (PR #1 scope)` — **Confirmed**: 287/287 tests passing includes endpoint integration tests
- `[ ] EF migrations apply cleanly (PR #1 scope)` — **Confirmed**: 3 migrations (AddTripCityForeignKey, AddActivityNodeLocation, AddTripPreferencesInterests) applied successfully

These were stale checkboxes from `sdd-apply` tracking that were never marked done; the work was verified complete by `sdd-verify` (287/287 passing) and the orchestrator.

## Known Issues / Deferred Work

- **Haversine distance for short urban distances**: MVP limitation. Haversine works at city scale but may be inaccurate for very short distances (<100m). Deferred: real routing API integration.
- **Interest fallback edge case**: When interest filtering yields zero candidates, the system falls back to `GetManyByCityIdAsync`. This means a trip with obscure interests could get the same itinerary as one without interests. Acceptable for MVP.
- **Zone clustering heuristic**: Uses simple lat/lng distance threshold rather than real neighborhood boundaries. Iterate based on user feedback.

## Specs Synced to Source of Truth

| Domain | Action | Details |
|--------|--------|---------|
| `itinerary-generation` | Updated (MODIFIED + ADDED) | FR4, FR9 updated; FR11, FR12, FR13 added (ActivityNode.Location, Haversine scoring, generator refactor) |
| `place` | Updated (MODIFIED) | FR4 replaced with full current interface; FR5 updated with interest-filtering requirements |
| `city-interests-endpoint` | Created (new domain) | Full spec copied to source of truth |
| `trip-interests` | Created (new domain) | Full spec copied to source of truth |

## Archive Contents

- `proposal.md` ✅
- `spec.md` ✅
- `design.md` ✅
- `tasks.md` ✅ (13/13 implementation sections; 2 stale checkboxes reconciled)
- `specs/city-interests-endpoint/spec.md` ✅
- `specs/itinerary-generation/spec.md` ✅
- `specs/place/spec.md` ✅
- `specs/trip-interests/spec.md` ✅
