# Delta for itinerary-generation

## ADDED Requirements

### Requirement: FR16: BlockTimeline persisted as own table

The system MUST map `BlockTimeline` as its own `BlockTimelines` table with a FK to `DayPlans`, replacing the current `OwnsOne` flattening. `DayPlans` table MUST store only identity and date columns (~8 columns), down from ~50.

#### Scenario: BlockTimelines table created with FK to DayPlan
- GIVEN a trip persisted with a DayPlan containing 3 blocks
- THEN `BlockTimelines` table has 3 rows, each with `DayPlanId` FK column

#### Scenario: DayPlans table simplified to ~8 columns
- GIVEN a migration snapshot after schema refactor
- THEN `DayPlans` table columns are `Id`, `TripId`, `DayIndex`, `Date` — no BlockTimeline-embedded columns

### Requirement: FR17: Single Activities table consolidates three tables

The system MUST consolidate `MorningActivities`, `AfternoonActivities`, `EveningActivities` into a single `Activities` table with FK to `BlockTimeline`. `ActivityNode` (with embedded `PlaceLocation`) persists in this same table.

#### Scenario: All activities persisted in one table
- GIVEN activities across Morning, Afternoon, and Evening blocks
- WHEN persisted
- THEN a single `Activities` table contains all rows, each with `BlockTimelineId` FK

### Requirement: NFR6: Migration squash — InitialCreate replaces 13 migrations

The system MUST replace 13 incremental migrations with a single `InitialCreate` reflecting the final schema: `BlockTimelines` table, `Activities` table, and simplified `DayPlans`.

#### Scenario: Clean database applies new schema
- GIVEN a database with zero migrations applied
- WHEN `dotnet ef database update` runs
- THEN all 3 refactored tables are created with correct FK constraints

#### Scenario: 13 prior migrations removed from solution
- GIVEN the solution with 13 prior migrations
- AFTER deleting them and adding InitialCreate
- THEN `dotnet ef migrations list` shows only `InitialCreate`
- AND `dotnet build` succeeds with no migration errors

## MODIFIED Requirements

### Requirement: FR2: Pinned must-sees placed at their PinnedDayIndex/PinnedBlock

The system MUST place must-sees with `PinnedDayIndex` and/or `PinnedBlock` into the exact day and block the user specified. Pinned placement MUST happen before any other distribution step.
(Previously: blocks accessed via `DayPlan.Morning`/`.Afternoon`/`.Evening` properties; now via `DayPlan.GetBlock(BlockType)`)

#### Scenario: Must-see pinned to a specific day and block
- GIVEN a must-see with `PinnedDayIndex = 1`, `PinnedBlock = Afternoon`
- WHEN itinerary generation runs
- THEN the must-see appears in `DayPlan[1].GetBlock(BlockType.Afternoon).Activities`
- AND its `SequenceOrder` is assigned contiguously within the block

#### Scenario: Must-see pinned only to a day (no block preference)
- GIVEN a must-see with `PinnedDayIndex = 0`, `PinnedBlock = null`
- WHEN itinerary generation runs
- THEN the must-see appears in one of the 3 blocks of `DayPlan[0]`, chosen based on opening hours and capacity
(Unchanged from original spec)

#### Scenario: Pinned must-see conflicts with block capacity
- GIVEN a must-see pinned to `DayPlan[2].GetBlock(BlockType.Morning)` where Morning already has `MaxVisitsPerMorningBlock` (3) visits
- WHEN itinerary generation tries to place it
- THEN the system SHALL attempt overflow in adjacent blocks of the same day before falling back (FR8)

## Acceptance Criteria

| ID | Criterion | Scope |
|----|-----------|-------|
| AC11 | All existing itinerary-generation scenarios pass with no behavioral change | Regression |
| AC12 | `DayPlan.GetBlock(BlockType)` returns the correct block for `Morning`, `Afternoon`, `Evening` | New API |
| AC13 | `DayPlan.Blocks` is an `IReadOnlyList<BlockTimeline>` with exactly 3 entries | New API |
| AC14 | `BlockTimelines` table has FK to `DayPlans` | Persistence |
| AC15 | Single `Activities` table replaces 3 old activity tables | Persistence |
| AC16 | `InitialCreate` is the sole migration remaining after squash | Migration |
| AC17 | Grep for `.Morning`, `.Afternoon`, `.Evening` returns 0 matches in production code | Cleanup |
