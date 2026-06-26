# Tasks: Refactor DayPlan Database Schema

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | ~524 |
| 400-line budget risk | Medium |
| Chained PRs recommended | No |
| Suggested split | Single PR |
| Delivery strategy | single-pr |
| Chain strategy | pending |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Medium

## Phase 1: Domain Model

- [x] 1.1 **DayPlan.cs**: Replace `Morning`/`Afternoon`/`Evening` properties with `IReadOnlyList<BlockTimeline> Blocks` + backing field; make `GetBlock()` public; add constructor validating BlockType ordinals
- [x] 1.2 **Trip.cs**: Update `GenerateDaysFrom()` to use `new DayPlan(dayIndex, date, morning, afternoon, evening)` constructor

## Phase 2: Persistence Layer

- [x] 2.1 **TripConfiguration.cs**: Rewrite DayPlan mapping — remove OwnsOne for blocks; add `HasMany(d => d.Blocks).WithOne()` + `Entity<BlockTimeline>` config with unique index on (DayPlanId, BlockType)
- [x] 2.2 **TripConfiguration.cs**: Consolidate 3 OwnsMany activity tables into single `OwnsMany` → table `Activities` with FK `BlockTimelineId`
- [x] 2.3 **TripRepository.cs**: Add `.ThenInclude(d => d.Blocks).ThenInclude(b => b.Activities)` to all query methods

## Phase 3: Application Layer

- [x] 3.1 **ToggleActivityCompletionHandler.cs**: Replace `.Morning`/`.Afternoon`/`.Evening` with `.Blocks.SelectMany(b => b.Activities)` and `Blocks`-based `FindActivityAcrossBlocks`
- [x] 3.2 **GenerateTripItineraryHandler.cs**: Replace `new[] { d.Morning, d.Afternoon, d.Evening }` with `d.Blocks`
- [x] 3.3 **ItineraryGeneratorHelpers.cs**: Replace `dayPlan.Morning.Activities.Count` etc. with `GetBlock(BlockType.X).Activities.Count`
- [x] 3.4 **AutoMapperProfile.cs**: Map `DayPlan→DayPlanResponse` via `src.Blocks` directly; update `Trip→TripSummaryResponse` to use `d.Blocks`

## Phase 4: Test Updates

- [x] 4.1 **DayPlanTests.cs, TripTests.cs**: Update DayPlan construction and block assertions
- [x] 4.2 **TimelineSchedulerTests.cs**: Update `CreateTripWithDay()` to use new DayPlan constructor
- [x] 4.3 **TransitEnricherTests.cs, UnpinnedMustSeePlacerTests.cs, PinnedMustSeePlacerTests.cs**: Replace `day.Morning`/`.Afternoon`/`.Evening` with `GetBlock()` or `Blocks` enumeration
- [x] 4.4 **HeuristicItineraryGeneratorTests.cs, CandidateFillerTests.cs**: Replace direct property access in assertions
- [x] 4.5 **ToggleActivityCompletionHandlerTests.cs, ListTripsHandlerTests.cs**: Replace `trip.Days[0].Morning.ForceAddActivity` with `.GetBlock(BlockType.Morning).ForceAddActivity`

## Phase 5: Migration + Verification

- [x] 5.1 Delete all 23 migration files; run `dotnet ef migrations add InitialCreate`
- [x] 5.2 Run `dotnet test` — all 172+ tests pass with zero behavioral changes
- [x] 5.3 Verify: grep `.Morning`/`.Afternoon`/`.Evening` → 0 matches in production code; verify migration SQL shows 3 clean tables
