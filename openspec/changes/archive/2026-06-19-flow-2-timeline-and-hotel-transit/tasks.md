# Tasks: flow-2-timeline-and-hotel-transit

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~240–260 |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | Single PR (all additive, well under 400 lines) |
| Delivery strategy | ask-always |
| Chain strategy | stacked-to-main |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: stacked-to-main
400-line budget risk: Low

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Domain model + scheduler + enricher + wiring + DTOs + tests | Single PR | All additive; regression suite validates |

## Phase 1: Domain Model — BlockTimeline + ITimelineScheduler

- [x] 1.1 Add `TransitFromHotel`, `TransitToHotel` (`TransitDetails?`), and `BlockWallClockDurationMinutes` computed property to `BlockTimeline.cs`. **Verify:** `dotnet build` succeeds; existing tests pass. (~12 lines)
- [x] 1.2 Create `ITimelineScheduler.cs` in `SmartTripPlanner.Domain/Ports/` with `void Schedule(Trip trip)`. **Verify:** `dotnet build` succeeds. (~6 lines)
- [x] 1.3 Create `TimelineScheduler.cs` in `SmartTripPlanner.Domain/Services/` — pure synchronous implementation walking each day/block, advancing `currentTime` by hotel transit + buffer + activity duration + inter-activity transit. Reset `currentTime` to `DayPlan.StartTime` per block (MVP). **Verify:** compiles; no I/O calls. (~50 lines)

## Phase 2: TransitEnricher — Hotel Transit Legs

- [x] 2.1 In `TransitEnricher.cs`, after the inter-activity `AssignTransitAsync` loop, add hotel transit computation: `Hotel → firstActivity.Location` → `block.TransitFromHotel` and `lastActivity.Location → Hotel` → `block.TransitToHotel`. Guard on `trip.BaseHotel != null` and `block.Activities.Count > 0`. **Verify:** `dotnet build` succeeds. (~25 lines)

## Phase 3: Wiring — DI + HeuristicItineraryGenerator Phase 6

- [x] 3.1 Register `ITimelineScheduler → TimelineScheduler` in `ApplicationServicesRegistration.cs` (add `services.AddScoped<ITimelineScheduler, TimelineScheduler>()`). **Verify:** `dotnet build` succeeds. (~2 lines)
- [x] 3.2 In `HeuristicItineraryGenerator.cs`, inject `ITimelineScheduler` into constructor; call `_timelineScheduler.Schedule(trip)` after Phase 5 (`_transitEnricher.EnrichAsync`). **Verify:** `dotnet build` succeeds. (~8 lines)

## Phase 4: EF Core Mapping — TripConfiguration

- [x] 4.1 In `TripConfiguration.cs`, add `OwnsOne(b => b.TransitFromHotel)` and `OwnsOne(b => b.TransitToHotel)` inside each block's `OwnsOne` nesting (Morning, Afternoon, Evening) — at the block level, parallel to `OwnsMany(b => b.Activities, ...)`. **Verify:** `dotnet build` succeeds; EF Core migration can be added. (~15 lines)

## Phase 5: Response DTOs + AutoMapper

- [x] 5.1 In `TripPlanResponse.cs`, add `TransitResponse` record (`TransportMode`, `DurationMinutes`, `BufferMinutes`, `FrictionAlert`); add `TransitFromHotel?` and `TransitToHotel?` to `BlockResponse`; add `EstimatedArrival?` and `EstimatedDeparture?` to `ActivityResponse`. **Verify:** `dotnet build` succeeds. (~20 lines)
- [x] 5.2 In `AutoMapperProfile.cs`, add `CreateMap<TransitDetails, TransitResponse>()`; extend `ActivityNode → ActivityResponse` mapping for `EstimatedArrival`/`EstimatedDeparture`; extend `BlockTimeline → BlockResponse` mapping for `TransitFromHotel`/`TransitToHotel`. **Verify:** `dotnet build` succeeds. (~15 lines)

## Phase 6: Unit Tests

- [x] 6.1 Create `TimelineSchedulerTests.cs` — test: single activity block with hotel transit, multiple activities with inter-activity transit, empty block skipped, block without hotel transit starts at `DayPlan.StartTime`, multiple blocks each reset to `DayPlan.StartTime`. **Verify:** `dotnet test --filter "TimelineSchedulerTests"` passes. (~60 lines)
- [x] 6.2 Update `TransitEnricherTests.cs` — add tests: hotel legs populated when `BaseHotel` present; hotel legs null when `BaseHotel` null; hotel legs null for empty block. **Verify:** `dotnet test --filter "TransitEnricherTests"` passes. (~40 lines)
- [x] 6.3 Update `HeuristicItineraryGeneratorTests.cs` — add `ITimelineScheduler` (real instance or mock) to constructor; assert `EstimatedArrival`/`EstimatedDeparture` populated after `GenerateAsync`; assert hotel transit fields set on blocks. **Verify:** `dotnet test --filter "HeuristicItineraryGeneratorTests"` passes. (~25 lines)

## Phase 7: Regression + Verification

- [x] 7.1 Run full test suite: `dotnet test` — all 295+ existing tests pass without modification. **Verify:** zero failures. (~0 lines changed)
- [x] 7.2 API contract check — seed a trip, call generate endpoint, assert JSON response includes `transitFromHotel`, `transitToHotel` on blocks and `estimatedArrival`/`estimatedDeparture` on activities. **Verify:** AutoMapper AssertConfigurationIsValid() passes; controller tests return correct DTO structure. Full JSON verification requires running the API. (~15 lines)
