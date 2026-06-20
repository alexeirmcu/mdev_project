# Timeline Scheduling Specification

## 1. Summary

Synchronous timeline scheduling for itinerary generation. After transit enrichment (Phase 5), the Timeline Scheduler (Phase 6) walks each block's activities and computes exact `EstimatedArrival` and `EstimatedDeparture` (minutes from midnight) by advancing a time cursor through hotel transit, activity duration, inter-activity transit, and buffers. Each block independently starts at `DayPlan.StartTime` (MVP limitation — post-MVP can chain blocks sequentially).

## 2. Functional Requirements

### FR1: Timeline Scheduler computes activity arrival and departure times

The system SHALL provide `ITimelineScheduler` / `TimelineScheduler` as a Phase 6 step in `HeuristicItineraryGenerator`, running after `TransitEnricher`. It MUST compute `EstimatedArrival` and `EstimatedDeparture` (minutes from midnight) for every `ActivityNode` in every non-empty block by walking the activity list sequentially.

#### Scenario: Single activity block gets arrival and departure

- GIVEN a Morning block with one activity (DurationMinutes=60) at DayPlan.StartTime=09:00
- AND TransitFromHotel.DurationMinutes=15, BufferMinutes=5
- WHEN `TimelineScheduler.Schedule(trip)` runs
- THEN `ActivityNode[0].EstimatedArrival = 555` (09:00 + 15 + 5)
- AND `ActivityNode[0].EstimatedDeparture = 615` (555 + 60)

#### Scenario: Multiple activities advance time by duration plus transit

- GIVEN a block with Activity[A](60min), TransitA→B(10min, Buffer=5), Activity[B](45min)
- AND TransitFromHotel(15min, Buffer=0), startTime=540 (09:00)
- WHEN `Schedule` runs
- THEN A.EstimatedArrival=555 (540+15), A.EstimatedDeparture=615
- AND B.EstimatedArrival=630 (615+10+5), B.EstimatedDeparture=675

#### Scenario: Empty block is skipped

- GIVEN an Afternoon block with zero activities
- WHEN `Schedule` runs
- THEN no `ActivityNode` in that block has arrival/departure set

#### Scenario: Each block starts at DayPlan.StartTime (MVP)

- GIVEN a day with Morning (non-empty) and Afternoon (non-empty)
- WHEN `Schedule` runs
- THEN both blocks' first activities have `EstimatedArrival` offset from the same `DayPlan.StartTime`

#### Scenario: Hotel transit NOT included in activity time advancement between blocks

- GIVEN TransitToHotel exists on Morning block with DurationMinutes=20
- WHEN `Schedule` advances to Afternoon block
- THEN Afternoon first activity starts at `DayPlan.StartTime + TransitFromHotel.DurationMinutes + TransitFromHotel.BufferMinutes`
- AND Morning's `TransitToHotel.DurationMinutes` is NOT deducted from Afternoon's start

#### Scenario: Block without hotel transit starts at DayPlan.StartTime directly

- GIVEN a block where `TransitFromHotel` is null
- WHEN `Schedule` runs
- THEN first activity `EstimatedArrival = DayPlan.StartTime.Hour*60 + DayPlan.StartTime.Minute`

#### Scenario: No I/O — pure synchronous computation

- GIVEN a fully transit-enriched `Trip`
- WHEN `TimelineScheduler.Schedule(trip)` is called
- THEN the method completes synchronously with no async calls, no service resolution, and no database access

## 3. Acceptance Criteria

| ID | Criterion |
|----|-----------|
| AC1 | `EstimatedArrival` and `EstimatedDeparture` set on every `ActivityNode` in every non-empty block |
| AC2 | Timeline scheduling starts each block at `DayPlan.StartTime`; hotel transit buffers included; inter-block hotel transit NOT carried forward |
| AC3 | `TimelineScheduler.Schedule()` is pure synchronous — no I/O, no async |

## 4. Non-Functional Requirements

- **NFR1**: Pure synchronous computation — no async calls, no service resolution, no database access
- **NFR2**: Unit-testable without mocking frameworks (pure domain logic on in-memory Trip objects)
- **NFR3**: No external dependencies beyond the domain model (`Trip`, `DayPlan`, `BlockTimeline`, `ActivityNode`)

## 5. Integration Points

| Integration Point | Direction | Description |
|-------------------|-----------|-------------|
| `ITimelineScheduler` (Domain port) | HeuristicItineraryGenerator → TimelineScheduler | Called as Phase 6 after TransitEnricher completes |
| `TimelineScheduler` (Domain service) | Self-contained | Operates on `Trip.Days` in-memory; no I/O |

## 6. Out of Scope

- Block chaining (Afternoon starting after Morning ends) — deferred to post-MVP
- Real-time clock / timezone handling
- User-configurable start times per block
