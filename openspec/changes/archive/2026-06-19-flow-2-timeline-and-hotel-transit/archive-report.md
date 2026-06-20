# Archive Report: flow-2-timeline-and-hotel-transit

**Archived:** 2026-06-19
**Commit:** `abe1053` — `feat(itinerary): add timeline scheduling and hotel transit legs`
**Mode:** Single PR (all additive, ~260 lines changed)
**Artifact store:** openspec

---

## What Was Implemented

Two coordinated features to make the generated itinerary human-readable with exact minute-level timings and complete transit coverage:

1. **Timeline Scheduling (Phase 6)** — New `ITimelineScheduler` / `TimelineScheduler` domain service that walks each block's activities and computes `EstimatedArrival` / `EstimatedDeparture` (minutes from midnight) by advancing a time cursor through hotel transit, activity duration, inter-activity transit, and buffers. Pure synchronous math — no I/O.

2. **Hotel Transit Legs** — Extended `TransitEnricher` to compute `TransitFromHotel` (hotel → first activity) and `TransitToHotel` (last activity → hotel) per non-empty block when `Trip.BaseHotel` is set. Stored as `TransitDetails?` on `BlockTimeline`.

### Supporting Changes

- Added `BlockWallClockDurationMinutes` computed property to `BlockTimeline` (sums hotel legs + block duration for display; `BlockTotalDurationMinutes` unchanged)
- Added `TransitResponse` DTO; extended `BlockResponse` with `TransitFromHotel`/`TransitToHotel` and `ActivityResponse` with `EstimatedArrival`/`EstimatedDeparture`
- Updated AutoMapper mappings
- Added EF Core `OwnsOne` configuration for hotel transit on each block
- DI registration for `ITimelineScheduler`

## Key Decisions

| Decision | Rationale |
|----------|-----------|
| Hotel transit lives on `BlockTimeline`, not `ActivityNode` | Natural block-level ownership; keeps `ActivityNode` unchanged |
| TimelineScheduler is a separate synchronous phase (Phase 6) | SRP, independently testable, no async dependencies |
| Arrival/departure as `int` (minutes-from-midnight) in API | Simple, no culture/format issues; clients format |
| All blocks start at `DayPlan.StartTime` (MVP) | Simple; no implicit block-chaining. Post-MVP can chain sequentially |
| `BlockTotalDurationMinutes` unchanged; `BlockWallClockDurationMinutes` added | Zero breaking change; capacity logic intact |

## Test Results

| Metric | Value |
|--------|-------|
| **Total tests** | 308 |
| **Passed** | 308 |
| **Failed** | 0 |
| **Skipped** | 0 |
| **New tests added** | 13 |

New test classes:
- `TimelineSchedulerTests` — 6 cases (single activity, multiple activities, empty block, multiple blocks, no hotel transit, sync-only)
- `TransitEnricherTests` — 4 new hotel transit cases (populated, null when BaseHotel null, null for empty block, transport mode rules)
- `HeuristicItineraryGeneratorTests` — Phase 6 assertions (EstimatedArrival/Departure populated)
- `BlockTimelineTests` — 3 new cases (hotel transit properties, duration semantics)

## Files Changed (20 files, +1634 / −35)

| File | Action |
|------|--------|
| `Domain/AggregatesModel/BlockTimeline.cs` | Modified |
| `Domain/Ports/ITimelineScheduler.cs` | Created |
| `Domain/Services/TimelineScheduler.cs` | Created |
| `Domain/Services/TransitEnricher.cs` | Modified |
| `Domain/Services/HeuristicItineraryGenerator.cs` | Modified |
| `Domain/ApiModels/TripPlanResponse.cs` | Modified |
| `ApplicationServices/ApplicationServicesRegistration.cs` | Modified |
| `Infrastructure/Configurations/TripConfiguration.cs` | Modified |
| `API/Configurations/AutoMapperProfile.cs` | Modified |
| Various test files | Modified/Created |

## Known Limitations / Deferred Work

- **Domain model uses `int` (not `int?`)** for `EstimatedArrival`/`EstimatedDeparture`. The scheduler always populates values for non-empty blocks; empty blocks have no activities to serialize. No functional impact. Consider making them `int?` in a future cleanup.
- **Block chaining** (Afternoon starting after Morning ends) is deferred to post-MVP. All blocks currently start at `DayPlan.StartTime`.
- **Real routing API integration** continues using Haversine heuristic.
- **OR-Tools VRP solver** remains deferred.

## Specs Synced

| Domain | Action | Details |
|--------|--------|---------|
| `timeline-scheduling` | **Created** | New domain spec — 1 FR with 7 scenarios |
| `itinerary-generation` | **Updated** | FR6 extended with 6 hotel transit scenarios; FR10 extended with 3 time/hotel-transit scenarios; FR14 added (BlockTimeline hotel transit properties, 2 scenarios) |

## Architecture Decisions Preserved in Memory

- TimelineScheduler as separate synchronous Phase 6 (not merged into TransitEnricher)
- Hotel transit on BlockTimeline (not ActivityNode or computed on-the-fly)
- Minutes-from-midnight format in API
- Block chaining deferred to post-MVP
