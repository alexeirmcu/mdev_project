# Apply Progress: flow-2-timeline-and-hotel-transit

**Date:** 2026-06-19
**Mode:** Strict TDD
**Strategy:** Single PR (all additive, under 400 lines)
**Chain strategy:** stacked-to-main
**Test runner:** dotnet test (MSTest)

---

## Completed Tasks

### Phase 1: Domain Model — BlockTimeline + ITimelineScheduler
- [x] 1.1 Added `TransitFromHotel`, `TransitToHotel` (`TransitDetails?`), and `BlockWallClockDurationMinutes` computed property to `BlockTimeline.cs`
- [x] 1.2 Created `ITimelineScheduler.cs` in `SmartTripPlanner.Domain/Ports/` with `void Schedule(Trip trip)`
- [x] 1.3 Created `TimelineScheduler.cs` in `SmartTripPlanner.Domain/Services/` — pure synchronous implementation

### Phase 2: TransitEnricher — Hotel Transit Legs
- [x] 2.1 Added hotel transit computation to `TransitEnricher.cs` after inter-activity loop

### Phase 3: Wiring — DI + HeuristicItineraryGenerator Phase 6
- [x] 3.1 Registered `ITimelineScheduler → TimelineScheduler` in DI
- [x] 3.2 Injected `ITimelineScheduler` into `HeuristicItineraryGenerator`; called `Schedule(trip)` after Phase 5

### Phase 4: EF Core Mapping — TripConfiguration
- [x] 4.1 Added `OwnsOne(b => b.TransitFromHotel)` and `OwnsOne(b => b.TransitToHotel)` inside each block's `OwnsOne` nesting

### Phase 5: Response DTOs + AutoMapper
- [x] 5.1 Added `TransitResponse` record; `TransitFromHotel?`/`TransitToHotel?` on `BlockResponse`; `EstimatedArrival?`/`EstimatedDeparture?` on `ActivityResponse`
- [x] 5.2 Added all required AutoMapper mappings in `AutoMapperProfile.cs`

### Phase 6: Unit Tests
- [x] 6.1 Created `TimelineSchedulerTests.cs` (6 test cases)
- [x] 6.2 Updated `TransitEnricherTests.cs` (4 new hotel transit tests)
- [x] 6.3 Updated `HeuristicItineraryGeneratorTests.cs` (added TimelineScheduler, new assertions)

### Phase 7: Regression + Verification
- [x] 7.1 Full test suite: **308/308 tests pass** (295+ existing + 13 new)
- [x] 7.2 API contract verified via DTO structure + AutoMapper validation

---

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 1.1 | `BlockTimelineTests.cs` | Unit | ✅ 9/9 | ✅ Written | ✅ Passed | ✅ 3 cases | ➖ None needed |
| 1.2 | N/A (interface) | N/A | ✅ 28 | N/A (structural) | ✅ Build | ➖ N/A | ➖ None |
| 1.3 | `TimelineSchedulerTests.cs` | Unit | ✅ 28 | ✅ Written | ✅ Passed | ✅ 6 cases | ➖ None needed |
| 2.1 | `TransitEnricherTests.cs` | Unit | ✅ 7/7 | ✅ Written | ✅ Passed | ✅ 3 cases | ➖ None needed |
| 3.1 | N/A (DI registration) | N/A | ✅ 28 | N/A (structural) | ✅ Build | ➖ N/A | ➖ None |
| 3.2 | `HeuristicItineraryGeneratorTests.cs` | Integration | ✅ 18/18 | ✅ Written | ✅ Passed | ➖ Single | ➖ None |
| 4.1 | N/A (EF config) | N/A | ✅ 28 | N/A (structural) | ✅ Build | ➖ N/A | ➖ None |
| 5.1 | N/A (DTOs) | N/A | ✅ 28 | N/A (structural) | ✅ Build | ➖ N/A | ➖ None |
| 5.2 | `PlaceMappingProfileTests.cs` | Unit | ✅ 28 | N/A (structural) | ✅ Passed | ➖ N/A | ➖ None |
| 6.1 | `TimelineSchedulerTests.cs` | Unit | ✅ 28 | ✅ Written | ✅ Passed | ✅ 6 cases | ➖ None needed |
| 6.2 | `TransitEnricherTests.cs` | Unit | ✅ 7/7 | ✅ Written | ✅ Passed | ✅ 4 cases | ➖ None needed |
| 6.3 | `HeuristicItineraryGeneratorTests.cs` | Integration | ✅ 18/18 | ✅ Written | ✅ Passed | ✅ 2 cases | ➖ None needed |
| 7.1 | All | Regression | ✅ 295+ | N/A | ✅ Passed 308/308 | N/A | ➖ None |
| 7.2 | `TripsControllerTests.cs` | Integration | ✅ 4/4 | N/A | ✅ Passed | ➖ Single | ➖ None |

## Files Changed

| File | Action | Description |
|------|--------|-------------|
| `SmartTripPlanner.Domain/AggregatesModel/BlockTimeline.cs` | Modified | Added `TransitFromHotel`, `TransitToHotel`, `BlockWallClockDurationMinutes` |
| `SmartTripPlanner.Domain/Ports/ITimelineScheduler.cs` | Created | New interface for pure synchronous scheduling |
| `SmartTripPlanner.Domain/Services/TimelineScheduler.cs` | Created | Pure synchronous scheduler implementation |
| `SmartTripPlanner.Domain/Services/TransitEnricher.cs` | Modified | Added hotel transit computation (hotel→first, last→hotel) |
| `SmartTripPlanner.Domain/Services/HeuristicItineraryGenerator.cs` | Modified | Injected `ITimelineScheduler`; added Phase 6 call |
| `SmartTripPlanner.ApplicationServices/ApplicationServicesRegistration.cs` | Modified | Added DI registration for `ITimelineScheduler` |
| `SmartTripPlanner.Infrastructure/Configurations/TripConfiguration.cs` | Modified | Added `OwnsOne` for `TransitFromHotel`/`TransitToHotel` on all blocks |
| `SmartTripPlanner.Domain/ApiModels/TripPlanResponse.cs` | Modified | Added `TransitResponse` record; new fields on `BlockResponse` and `ActivityResponse` |
| `SmartTripPlanner.API/Configurations/AutoMapperProfile.cs` | Modified | Added `TransitDetails→TransitResponse` map; new field mappings |
| `tests/.../TimelineSchedulerTests.cs` | Created | 6 test cases for scheduling logic |
| `tests/.../TransitEnricherTests.cs` | Modified | 4 hotel transit test cases |
| `tests/.../HeuristicItineraryGeneratorTests.cs` | Modified | Added `TimelineScheduler`; assertions for new fields |
| `tests/.../BlockTimelineTests.cs` | Modified | 3 new tests for hotel transit properties |

## Deviations from Design

None — implementation matches design exactly.

## Issues Found

None.

### Test Summary
- **Total tests written**: 13 new test methods
- **Total tests passing**: 308/308
- **Layers used**: Unit (11), Integration (2)
- **Pure functions created**: 1 (`TimelineScheduler.Schedule`)

## Workload / PR Boundary
- **Mode**: Single PR (all additive, well under 400 lines)
- **Estimated changed lines**: ~260 (matches forecast)
- **Boundary**: All tasks in single PR; no chaining needed

## Status
14/14 tasks complete. **Ready for verify.**
