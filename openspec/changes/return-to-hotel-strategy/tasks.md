# Tasks: ReturnToHotelStrategy (Inter-Block Transit Optimization)

## Overview
Add configurable `ReturnToHotelStrategy` to `TripPreferences` with three options: Always, Never, ProximityBased. When Never or ProximityBased are selected, skip hotel return between blocks and compute direct inter-block transit legs instead.

## Task 1: Domain Model & Enum
**File:** `SmartTripPlanner.Domain/Enums/ReturnToHotelStrategy.cs` (new)
**Work:** Create enum `ReturnToHotelStrategy { Always, Never, ProximityBased }`.
**Depends on:** None
**Verify:** Compiles, enum values are distinct.
- [x] 1.1 Enum created with `Always`, `Never`, `ProximityBased`
- [x] 1.2 Enum tests verify 3 distinct values and default to `Always`
- [x] 1.3 Build succeeds, 2 tests pass

## Task 2: TripPreferences Update
**Files:** `SmartTripPlanner.Domain/AggregatesModel/TripPreferences.cs`, `SmartTripPlanner.Domain/ApiModels/TripPreferencesInput.cs`
**Work:** Add `ReturnToHotelStrategy` property/parameter with default `Always` for backward compatibility. Update constructor, equality components, and input DTO.
**Depends on:** Task 1
**Verify:** Existing `TripPreferences` tests pass.
- [x] 2.1 `ReturnToHotelStrategy` property added with default `Always`
- [x] 2.2 `TripPreferencesInput` record updated with new field
- [x] 2.3 Equality components include new field
- [x] 2.4 No regression — 314 total tests pass (308→314)

## Task 3: EF Core Mapping
**File:** `SmartTripPlanner.Infrastructure/Configurations/TripConfiguration.cs`
**Work:** Add property mapping for `ReturnToHotelStrategy` (enum→string conversion, default `Always`) and `InterBlockTransit` (owned entity) to Morning/Afternoon/Evening blocks.
**Depends on:** Task 1
**Verify:** `dotnet ef migrations add` succeeds.
- [x] 3.1 `ReturnToHotelStrategy` mapped with `HasConversion<string>()` and `HasDefaultValue(ReturnToHotelStrategy.Always)`
- [x] 3.2 `InterBlockTransit` mapped as owned entity in Morning, Afternoon, Evening blocks
- [x] 3.3 Build succeeds, all 317 tests pass

## Task 4: BlockTimeline InterBlockTransit Property
**File:** `SmartTripPlanner.Domain/AggregatesModel/BlockTimeline.cs`
**Work:** Add `public TransitDetails? InterBlockTransit { get; set; }`.
**Depends on:** None
**Verify:** Compiles.
- [x] 4.1 `InterBlockTransit` property added with nullable default
- [x] 4.2 `BlockTotalDurationMinutes` excludes InterBlockTransit
- [x] 4.3 Tests verify default null, can be set, doesn't affect total duration

## Task 5: TransitEnricher Strategy Logic
**File:** `SmartTripPlanner.Domain/Services/TransitEnricher.cs`
**Work:**
- After computing hotel transit for all blocks, apply strategy to optimize inter-block transit.
- **Always:** keep current behavior (no second pass).
- **Never:** if both adjacent blocks have activities, compute InterBlockTransit between them and null out boundary hotel legs. Evening always returns to hotel.
- **ProximityBased:** compute both direct and via-hotel totals; choose the shorter. Tie-breaker favors hotel.
**Depends on:** Tasks 1, 4
**Verify:** `TransitEnricherTests` pass (new tests in Task 10).
- [x] 5.1 Strategy logic implemented in `ApplyStrategyAsync` method
- [x] 5.2 Never: InterBlockTransit on destination block, hotel legs null at boundaries
- [x] 5.3 ProximityBased: both routes computed, shorter chosen, tie to hotel
- [x] 5.4 Evening Always returns to hotel regardless of strategy

## Task 6: TimelineScheduler Block Chaining
**File:** `SmartTripPlanner.Domain/Services/TimelineScheduler.cs`
**Work:**
- If `block.TransitFromHotel != null`, block starts at `DayPlan.StartTime` (reset).
- If `block.InterBlockTransit != null`, block starts at `previousBlockEnd + interBlockTransit.DurationMinutes + interBlockTransit.BufferMinutes`.
- If both are null (empty previous block), reset to `StartTime`.
**Depends on:** Task 4
**Verify:** `TimelineSchedulerTests` pass (new tests in Task 11).
- [x] 6.1 Chaining logic added: `TransitFromHotel` → reset, `InterBlockTransit` → chain, else → reset
- [x] 6.2 `previousBlockEnd` tracks across blocks for chaining
- [x] 6.3 Existing behavior preserved for Always strategy (regression safe)

## Task 7: Response DTO Updates
**File:** `SmartTripPlanner.Domain/ApiModels/TripPlanResponse.cs` (BlockResponse)
**Work:** Add `public TransitResponse? InterBlockTransit { get; set; }`.
**Depends on:** Task 4
**Verify:** Compiles.
- [x] 7.1 `InterBlockTransit` property added to `BlockResponse`

## Task 8: AutoMapper Mapping
**File:** `SmartTripPlanner.API/Configurations/AutoMapperProfile.cs`
**Work:** Map `BlockTimeline.InterBlockTransit` → `BlockResponse.InterBlockTransit`.
**Depends on:** Tasks 4, 7
**Verify:** `AssertConfigurationIsValid()` passes.
- [x] 8.1 Mapping added in `BlockTimeline → BlockResponse` profile
- [x] 8.2 `AssertConfigurationIsValid()` passes (verified by existing mapping tests)

## Task 9: ApplicationServicesRegistration
**File:** `SmartTripPlanner.ApplicationServices/ApplicationServicesRegistration.cs`
**Work:** No new services needed (TimelineScheduler and TransitEnricher already registered). Verify DI container resolves correctly.
**Depends on:** None
**Verify:** `dotnet build` succeeds.
- [x] 9.1 Full solution builds without errors

## Task 10: TransitEnricher Tests
**File:** `tests/.../TransitEnricherTests.cs`
**Work:** Add tests:
- Always strategy: hotel legs present, no InterBlockTransit
- Never strategy: InterBlockTransit present, hotel legs null at boundaries
- ProximityBased: computes both routes, makes a choice
- Empty block boundaries (keep hotel transit)
- Evening always returns to hotel (all strategies)
**Depends on:** Tasks 1, 5
**Verify:** `dotnet test --filter "TransitEnricherTests"` passes.
- [x] 10.1 `EnrichAsync_AlwaysStrategy_HotelLegsPresentNoInterBlockTransit`
- [x] 10.2 `EnrichAsync_NeverStrategy_InterBlockTransitPresentHotelLegsNullAtBoundaries`
- [x] 10.3 `EnrichAsync_NeverStrategy_EveningAlwaysReturnsToHotel`
- [x] 10.4 `EnrichAsync_NeverStrategy_EmptyBlockBoundary_KeepsHotelTransit`
- [x] 10.5 `EnrichAsync_ProximityBased_MechanismRunsAndMakesChoice`
- [x] 10.6 `EnrichAsync_ProximityBased_EveningAlwaysReturnsToHotel`

## Task 11: TimelineScheduler Tests
**File:** `tests/.../TimelineSchedulerTests.cs`
**Work:** Add tests:
- Block with InterBlockTransit chains from previous
- Block with TransitFromHotel takes priority over InterBlockTransit
- Mixed boundaries (some chain, some reset)
- Empty block followed by non-empty block resets
- Multiple InterBlockTransit chains correctly
**Depends on:** Tasks 4, 6
**Verify:** `dotnet test --filter "TimelineSchedulerTests"` passes.
- [x] 11.1 `Schedule_BlockWithInterBlockTransit_ChainsFromPreviousBlockEnd`
- [x] 11.2 `Schedule_BlockWithTransitFromHotel_ResetsToStartTime`
- [x] 11.3 `Schedule_InterBlockTransitChainsMixedWithTransitFromHotel`
- [x] 11.4 `Schedule_EmptyBlockFollowedByNonEmpty_ResetsToStartTime`
- [x] 11.5 `Schedule_MultipleInterBlockTransit_ChainsCorrectly`

## Task 12: Integration Tests
**File:** `tests/.../HeuristicItineraryGeneratorTests.cs`
**Work:** Add end-to-end tests:
- Generate with Always strategy (baseline)
- Generate with Never strategy (verify InterBlockTransit populated)
- Generate with ProximityBased (verify correct choice at boundaries)
- Evening always returns to hotel with Never strategy
**Depends on:** Tasks 5, 6
**Verify:** `dotnet test --filter "HeuristicItineraryGeneratorTests"` passes.
- [x] 12.1 `GenerateAsync_AlwaysStrategy_HotelLegsPresentNoInterBlockTransit`
- [x] 12.2 `GenerateAsync_NeverStrategy_InterBlockTransitPopulated`
- [x] 12.3 `GenerateAsync_ProximityBased_MechanismRuns`
- [x] 12.4 `GenerateAsync_NeverStrategy_EveningAlwaysReturnsToHotel`

## Task 13: EF Migration
**Files:** `SmartTripPlanner.Infrastructure/Migrations/20260621193200_AddReturnToHotelStrategy.cs`
**Work:** Run `dotnet ef migrations add AddReturnToHotelStrategy`.
**Depends on:** Tasks 2, 3
**Verify:** Migration script contains `PrefReturnToHotelStrategy`, `*_InterBlockTransit_*` columns.
- [x] 13.1 Migration created with `PrefReturnToHotelStrategy` (text, default "Always")
- [x] 13.2 Migration includes `InterBlockTransit` columns for all three blocks
- [x] 13.3 Migration up/down scripts symmetrical

## Task 14: Full Regression
**Work:** Run entire test suite.
**Depends on:** All prior tasks
**Verify:** `dotnet test` — all tests pass (308+).
- [x] 14.1 Build succeeds (0 errors, warnings only pre-existing)
- [x] 14.2 **333 tests passing** (308 original + 25 new = 333, 0 failures)

---

## Review Workload Forecast
- **Estimated changed lines:** 350–400
- **Budget risk:** Medium (at boundary)
- **Chained PRs recommended:** No (user requested single PR)
- **Delivery strategy:** single-pr-default with size:exception implied by user choice
