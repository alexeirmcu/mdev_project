# Delta Spec: flow-2-timeline-and-hotel-transit

---

## Domain: timeline-scheduling (NEW)

### Requirements

#### Requirement: Timeline Scheduler computes activity arrival and departure times

The system SHALL provide `ITimelineScheduler` / `TimelineScheduler` as a Phase 6 step in `HeuristicItineraryGenerator`, running after `TransitEnricher`. It MUST compute `EstimatedArrival` and `EstimatedDeparture` (minutes from midnight) for every `ActivityNode` in every non-empty block by walking the activity list sequentially.

##### Scenario: Single activity block gets arrival and departure

- GIVEN a Morning block with one activity (DurationMinutes=60) at DayPlan.StartTime=09:00
- AND TransitFromHotel.DurationMinutes=15, BufferMinutes=5
- WHEN `TimelineScheduler.Schedule(trip)` runs
- THEN `ActivityNode[0].EstimatedArrival = 555` (09:00 + 15 + 5)
- AND `ActivityNode[0].EstimatedDeparture = 615` (555 + 60)

##### Scenario: Multiple activities advance time by duration plus transit

- GIVEN a block with Activity[A](60min), TransitA→B(10min, Buffer=5), Activity[B](45min)
- AND TransitFromHotel(15min, Buffer=0), startTime=540 (09:00)
- WHEN `Schedule` runs
- THEN A.EstimatedArrival=555 (540+15), A.EstimatedDeparture=615
- AND B.EstimatedArrival=630 (615+10+5), B.EstimatedDeparture=675

##### Scenario: Empty block is skipped

- GIVEN an Afternoon block with zero activities
- WHEN `Schedule` runs
- THEN no `ActivityNode` in that block has arrival/departure set

##### Scenario: Each block starts at DayPlan.StartTime (MVP)

- GIVEN a day with Morning (non-empty) and Afternoon (non-empty)
- WHEN `Schedule` runs
- THEN both blocks' first activities have `EstimatedArrival` offset from the same `DayPlan.StartTime`

##### Scenario: Hotel transit NOT included in activity time advancement between blocks

- GIVEN TransitToHotel exists on Morning block with DurationMinutes=20
- WHEN `Schedule` advances to Afternoon block
- THEN Afternoon first activity starts at `DayPlan.StartTime + TransitFromHotel.DurationMinutes + TransitFromHotel.BufferMinutes`
- AND Morning's `TransitToHotel.DurationMinutes` is NOT deducted from Afternoon's start

##### Scenario: Block without hotel transit starts at DayPlan.StartTime directly

- GIVEN a block where `TransitFromHotel` is null
- WHEN `Schedule` runs
- THEN first activity `EstimatedArrival = DayPlan.StartTime.Hour*60 + DayPlan.StartTime.Minute`

##### Scenario: No I/O — pure synchronous computation

- GIVEN a fully transit-enriched `Trip`
- WHEN `TimelineScheduler.Schedule(trip)` is called
- THEN the method completes synchronously with no async calls, no service resolution, and no database access

---

## Domain: itinerary-generation (MODIFIED)

### MODIFIED Requirements

#### Requirement: FR6 Transport mode assignment per leg (now includes hotel transit)

The system MUST assign `TransportMode` per transit leg between activities AND per hotel transit leg (`Hotel → FirstActivity`, `LastActivity → Hotel`) using `ITransitCalculator`. Default: `WALK_AND_PUBLIC_TRANSPORT`; switch to `CAR` when car is available and PT+walk > 20 min slower. Hotel transit legs MUST be stored on `BlockTimeline` as `TransitFromHotel` and `TransitToHotel` (both `TransitDetails?`), computed by `TransitEnricher` using `Trip.BaseHotel.Location`.

(Previously: FR6 only covered inter-activity transit. Now extends to hotel→first and last→hotel legs.)

##### Scenario: Hotel to first activity — transit computed and stored on block

- GIVEN a non-empty block and `Trip.BaseHotel` is set
- WHEN `TransitEnricher.EnrichAsync` runs
- THEN `block.TransitFromHotel` is a `TransitDetails` with `TransportMode`, `DurationMinutes`, `BufferMinutes`, `FrictionAlert` computed from `BaseHotel.Location` to `Activities[0].Location`

##### Scenario: Last activity to hotel — transit computed and stored on block

- GIVEN a non-empty block and `Trip.BaseHotel` is set
- WHEN `TransitEnricher.EnrichAsync` runs
- THEN `block.TransitToHotel` is a `TransitDetails` computed from `Activities[^1].Location` to `BaseHotel.Location`

##### Scenario: Hotel transit null when BaseHotel absent

- GIVEN `Trip.BaseHotel` is null
- WHEN `TransitEnricher.EnrichAsync` runs
- THEN `TransitFromHotel` and `TransitToHotel` remain null

##### Scenario: Hotel transit null for empty block

- GIVEN a block with zero activities
- WHEN `TransitEnricher.EnrichAsync` runs
- THEN `TransitFromHotel` and `TransitToHotel` remain null

##### Scenario: Hotel transit respects transport mode rules

- GIVEN hotel is 5 km from first activity and `CarAvailable = true` and car is 25+ min faster
- WHEN hotel transit is computed
- THEN `TransitFromHotel.TransportMode = CAR`
- GIVEN same distance but `CarAvailable = false`
- THEN `TransitFromHotel.TransportMode = WALK_AND_PUBLIC_TRANSPORT`

##### Scenario: Short-distance hotel transit defaults to walk+PT

- GIVEN hotel is 0.8 km from first activity
- WHEN hotel transit is computed
- THEN `TransitFromHotel.TransportMode = WALK_AND_PUBLIC_TRANSPORT` regardless of `CarAvailable`

#### Requirement: FR10 Response includes full DayPlan[] with blocks, activities, transit, hotel transit, and times

`TripPlanResponse` SHALL expose all itinerary data including hotel transit legs and exact times. `BlockResponse` MUST add `TransitFromHotel` and `TransitToHotel` (nullable `TransitResponse`). `ActivityResponse` MUST add `EstimatedArrival` and `EstimatedDeparture` (nullable `int`, minutes from midnight). `AutoMapperProfile` MUST include: `TransitDetails → TransitResponse` mapping; `BlockTimeline.TransitFromHotel/ToHotel → BlockResponse.TransitFromHotel/ToHotel`; `ActivityNode.EstimatedArrival/Departure → ActivityResponse.EstimatedArrival/Departure`.

(Previously: FR10 only included inter-activity transit and basic activity fields. Now adds hotel transit on BlockResponse and exact times on ActivityResponse.)

##### Scenario: Block response includes hotel transit details

- GIVEN a block with `TransitFromHotel` and `TransitToHotel` set
- WHEN `TripPlanResponse` is mapped from `Trip`
- THEN `BlockResponse.TransitFromHotel` and `BlockResponse.TransitToHotel` contain `TransportMode`, `DurationMinutes`, `BufferMinutes`, `FrictionAlert`

##### Scenario: Activity response includes arrival and departure times

- GIVEN a scheduled activity with `EstimatedArrival=555`, `EstimatedDeparture=615`
- WHEN `TripPlanResponse` is mapped
- THEN `ActivityResponse.EstimatedArrival = 555` and `ActivityResponse.EstimatedDeparture = 615`

##### Scenario: Null hotel transit and times map to null in response

- GIVEN a block where `TransitFromHotel` and `TransitToHotel` are null
- WHEN mapped to `BlockResponse`
- THEN both fields are null (not default/zero)

---

## Domain: itinerary-generation (ADDED)

### ADDED Requirements

#### Requirement: BlockTimeline hotel transit properties

`BlockTimeline` MUST expose `TransitFromHotel` (`TransitDetails?`) and `TransitToHotel` (`TransitDetails?`) representing hotel→first-activity and last-activity→hotel transit legs. `BlockTotalDurationMinutes` semantics MUST remain unchanged (sum of activities + inter-activity transit only). A computed `BlockWallClockDurationMinutes` MAY be added summing hotel legs + block duration for display.

##### Scenario: BlockTotalDurationMinutes excludes hotel transit

- GIVEN a block with one 60-min activity, TransitFromHotel(15min), TransitToHotel(20min)
- WHEN `BlockTotalDurationMinutes` is computed
- THEN the result is 60 (activity duration only, no hotel legs)

##### Scenario: BlockWallClockDurationMinutes includes hotel transit

- GIVEN same block
- WHEN `BlockWallClockDurationMinutes` is computed
- THEN the result is 95 (15 + 60 + 20)

---

## Backward Compatibility

| Existing Behavior | Change Impact | Guarantee |
|---|---|---|
| `BlockTimeline.BlockTotalDurationMinutes` | Semantics unchanged — activities + inter-activity transit only | MUST pass all existing capacity tests |
| `CanFitActivity()` / `AddActivity()` | No modification — hotel transit not included in capacity check | MUST pass all existing tests |
| `TransitEnricher.EnrichAsync()` signature | Unchanged | Existing mock-based tests pass unchanged |
| `ActivityResponse` existing fields | No removal or retyping; new fields are additive (nullable) | Clients ignoring new fields unaffected |
| 295+ existing tests | Zero modification needed | All pass without changes |

---

## Acceptance Criteria

| ID | Criterion |
|----|-----------|
| AC1 | `TransitFromHotel` and `TransitToHotel` populated on every non-empty block when `Trip.BaseHotel` is set; null otherwise |
| AC2 | Hotel transit respects same transport mode rules (walk/PT vs car) as inter-activity transit |
| AC3 | `EstimatedArrival` and `EstimatedDeparture` set on every `ActivityNode` in every non-empty block |
| AC4 | Timeline scheduling starts each block at `DayPlan.StartTime`; hotel transit buffers included; inter-block hotel transit NOT carried forward |
| AC5 | `BlockTotalDurationMinutes` unchanged — does NOT include hotel transit legs |
| AC6 | `BlockResponse.TransitFromHotel/ToHotel` and `ActivityResponse.EstimatedArrival/Departure` present in API response |
| AC7 | All 295+ existing tests pass without modification |
| AC8 | `TimelineScheduler.Schedule()` is pure synchronous — no I/O, no async |