# Itinerary Generation Specification

## 1. Summary

Heuristic multi-day itinerary builder for Smart Trip Planner. Generates `DayPlan[]` with 3 blocks per day (Morning/Afternoon/Evening), placing must-sees by priority and pin constraints, filling remaining slots with scored candidates, applying weather and transport heuristics, and enforcing block capacity limits. This is the PRD v1 MVP approach — OR-Tools is deferred.

## 2. Functional Requirements

### FR1: Trip.GenerateDays() creates empty DayPlans with 3 blocks

The system SHALL call `Trip.GenerateDays()` to initialize one `DayPlan` per trip date, each containing `Morning`, `Afternoon`, and `Evening` blocks with default `WeatherSummary = Clear`.

#### Scenario: N-day trip creates N DayPlans

- GIVEN a trip from StartDate to EndDate spanning N days
- WHEN `GenerateDays()` is called
- THEN N `DayPlan` entities are created, each with `DayIndex` 0..N-1 and `Date` matching each calendar day
- AND each `DayPlan` has exactly 3 `BlockTimeline` instances: Morning, Afternoon, Evening

#### Scenario: Blocks start empty with default start time

- GIVEN a newly generated DayPlan
- WHEN generated
- THEN each block has zero activities
- AND the `StartTime` equals `Trip.DefaultStartTime`

### FR2: Pinned must-sees placed at their PinnedDayIndex/PinnedBlock

The system MUST place must-sees with `PinnedDayIndex` and/or `PinnedBlock` into the exact day and block the user specified. Pinned placement MUST happen before any other distribution step.

#### Scenario: Must-see pinned to a specific day and block

- GIVEN a must-see with `PinnedDayIndex = 1`, `PinnedBlock = Afternoon`
- WHEN itinerary generation runs
- THEN the must-see appears in `DayPlan[1].Afternoon` activities
- AND its `SequenceOrder` is assigned contiguously within the block

#### Scenario: Must-see pinned only to a day (no block preference)

- GIVEN a must-see with `PinnedDayIndex = 0`, `PinnedBlock = null`
- WHEN itinerary generation runs
- THEN the must-see appears in one of the 3 blocks of `DayPlan[0]`, chosen based on opening hours and capacity

#### Scenario: Pinned must-see conflicts with block capacity

- GIVEN a must-see pinned to `DayPlan[2].Morning` where Morning already has `MaxVisitsPerMorningBlock` (3) visits
- WHEN itinerary generation tries to place it
- THEN the system SHALL attempt overflow in adjacent blocks of the same day before falling back (FR8)

### FR3: Unpinned must-sees distributed by zone proximity and opening hours

The system MUST place must-sees without `PinnedDayIndex` using zone-proximity clustering and opening-hours feasibility. Placement MUST respect `OpeningHoursWindow.DayOfWeek` for each must-see's assigned day.

#### Scenario: Unpinned must-see placed on a day it is open

- GIVEN an unpinned must-see that is closed on Mondays and open Tuesday-Sunday
- AND the trip includes a Monday (DayIndex 0)
- WHEN itinerary generation runs
- THEN the must-see is NOT placed on Monday and IS placed on a day it is open

#### Scenario: Zone clustering reduces backtracking

- GIVEN two unpinned must-sees in the same barrio/zone with close proximity
- WHEN itinerary generation assigns them
- THEN both SHOULD appear in the same day to minimize transit between zones

### FR4: Candidate places fill remaining block capacity

After all must-sees are placed, the system SHALL fill remaining block slots with candidate places ranked by a composite score: `family-friendly bonus + popularity − distance penalty`. The system MUST use `ICandidateScorer` (Domain port) for score computation. Candidate places SHALL be filtered by the trip's interests via `IPlaceRepository.GetCandidatesByCityAndInterestsAsync` when interests are present; unfiltered candidates SHALL be used as fallback.

#### Scenario: Block has 2 of 3 slots filled after must-see placement

- GIVEN Morning block with 2 must-sees (capacity 3)
- WHEN candidate filling runs
- THEN 1 scored candidate is added to Morning
- AND `CanFitActivity()` returns false for additional activities

#### Scenario: No candidates available for a block

- GIVEN all candidate places are already placed or filtered out
- WHEN candidate filling runs for an empty Evening block
- THEN the block remains empty with zero activities (not an error)

#### Scenario: Candidates filtered by interests yield sufficient results

- GIVEN trip preferences include interests ["museum", "history"]
- AND the city has 8 candidate places tagged "museum" or "history"
- WHEN candidate filling runs
- THEN only interest-matching candidates are scored and placed
- AND at least one slot per block is filled if matching candidates exist

#### Scenario: Interest filtering yields too few results — fallback to unfiltered

- GIVEN trip preferences include interests ["underwater"]
- AND the city has 0 candidate places matching "underwater"
- WHEN candidate filling runs
- THEN the system falls back to using unfiltered candidates from `GetManyByCityIdAsync`
- AND blocks are filled with the best available candidates regardless of interests

### FR5: Weather filter per day adjusts activity selection

When `TripPreferences.WeatherAwareEnabled = true` and a day's `WeatherSummary = Bad`, the system MUST deprioritize outdoor (`IsIndoor = false`) activities and prefer indoor candidates.

#### Scenario: Rainy day prefers indoor activities

- GIVEN a day with `WeatherSummary = Bad`
- AND candidates include both indoor and outdoor places
- WHEN candidate scoring runs
- THEN indoor candidates receive a scoring bonus over outdoor ones
- AND outdoor must-sees still appear (priority overrides weather)

#### Scenario: Weather-aware disabled by preference

- GIVEN `TripPreferences.WeatherAwareEnabled = false`
- WHEN itinerary generation runs on a rainy day
- THEN no weather-based reordering or filtering occurs

### FR6: Transport mode assignment per leg (now includes hotel transit)

The system MUST assign `TransportMode` per transit leg between activities AND per hotel transit leg (`Hotel → FirstActivity`, `LastActivity → Hotel`) using `ITransitCalculator` (Domain port). Default: `WALK_AND_PUBLIC_TRANSPORT`. Switch to `CAR` when car is available and either: (a) PT+walk exceeds 20 minutes longer than car, or (b) the trip involves long-distance inter-zone transit. Hotel transit legs MUST be stored on `BlockTimeline` as `TransitFromHotel` and `TransitToHotel` (both `TransitDetails?`, null when `Trip.BaseHotel` is null or block is empty), computed by `TransitEnricher` using `Trip.BaseHotel.Location`.

#### Scenario: PT+Walking is reasonably fast

- GIVEN `TripPreferences.CarAvailable = false`
- AND PT+walk transit between two consecutive activities takes 15 min
- WHEN transit is assigned
- THEN `TransitDetails.TransportMode = WALK_AND_PUBLIC_TRANSPORT`

#### Scenario: Car significantly faster than PT+walk

- GIVEN `TripPreferences.CarAvailable = true`
- AND PT+walk takes 45 min while car takes 15 min (30+ min difference)
- WHEN transit is assigned
- THEN `TransitDetails.TransportMode = CAR`

#### Scenario: Short walking distance within zone

- GIVEN two activities in the same zone with 8 min walk
- AND `TripPreferences.CarAvailable = true`
- WHEN transit is assigned
- THEN `TransitDetails.TransportMode = WALK_AND_PUBLIC_TRANSPORT` (car is not justified)

#### Scenario: Hotel to first activity — transit computed and stored on block

- GIVEN a non-empty block and `Trip.BaseHotel` is set
- WHEN `TransitEnricher.EnrichAsync` runs
- THEN `block.TransitFromHotel` is a `TransitDetails` with `TransportMode`, `DurationMinutes`, `BufferMinutes`, `FrictionAlert` computed from `BaseHotel.Location` to `Activities[0].Location`

#### Scenario: Last activity to hotel — transit computed and stored on block

- GIVEN a non-empty block and `Trip.BaseHotel` is set
- WHEN `TransitEnricher.EnrichAsync` runs
- THEN `block.TransitToHotel` is a `TransitDetails` computed from `Activities[^1].Location` to `BaseHotel.Location`

#### Scenario: Hotel transit null when BaseHotel absent

- GIVEN `Trip.BaseHotel` is null
- WHEN `TransitEnricher.EnrichAsync` runs
- THEN `TransitFromHotel` and `TransitToHotel` remain null

#### Scenario: Hotel transit null for empty block

- GIVEN a block with zero activities
- WHEN `TransitEnricher.EnrichAsync` runs
- THEN `TransitFromHotel` and `TransitToHotel` remain null

#### Scenario: Hotel transit respects transport mode rules

- GIVEN hotel is 5 km from first activity and `CarAvailable = true` and car is 25+ min faster
- WHEN hotel transit is computed
- THEN `TransitFromHotel.TransportMode = CAR`
- GIVEN same distance but `CarAvailable = false`
- THEN `TransitFromHotel.TransportMode = WALK_AND_PUBLIC_TRANSPORT`

#### Scenario: Short-distance hotel transit defaults to walk+PT

- GIVEN hotel is 0.8 km from first activity
- WHEN hotel transit is computed
- THEN `TransitFromHotel.TransportMode = WALK_AND_PUBLIC_TRANSPORT` regardless of `CarAvailable`

### FR7: Block capacity validation

The system MUST enforce `TripPlanningConstants` capacity limits per block type. `BlockTimeline.CanFitActivity()` and `BlockTimeline.AddActivity()` already validate these constraints.

| Block | Max Visits | Max Duration |
|-------|-----------|-------------|
| Morning | 3 | 210 min |
| Afternoon | 3 | 180 min |
| Evening | 2 | 105 min |

#### Scenario: Activity fits within block capacity

- GIVEN an Evening block with 1 activity (60 min)
- WHEN adding a 30-min activity with 15-min transit
- THEN the activity is accepted (1+1 visits ≤ 2, 60+30+15 ≤ 105)

#### Scenario: Block capacity exceeded — overflow trimming

- GIVEN a Morning block at max visits (3)
- WHEN the heuristic attempts to add a 4th activity
- THEN the activity is not added; the generator skips to next candidate or next block

### FR8: Fallback by priority on capacity overflow

When not all places fit, the system MUST drop in priority order: first `Low`, then `Medium`. If a `High` priority must-see cannot be placed, the system SHALL throw `OverConstrainedRouteException` with the conflicting place IDs.

#### Scenario: Low-priority candidate dropped to fit must-sees

- GIVEN a Morning block at capacity with 1 High and 2 Medium must-sees
- AND a Low-priority candidate also targets Morning
- WHEN overflow trimming runs
- THEN the Low-priority candidate is removed first

#### Scenario: Medium must-see dropped when Low already gone

- GIVEN only Medium and High must-sees remain, and capacity is insufficient
- WHEN overflow trimming runs
- THEN Medium-priority items are dropped before High-priority items

#### Scenario: High must-see cannot fit — exception

- GIVEN a High-priority must-see that cannot fit in any block on any day
- WHEN fallback has already dropped all Low and Medium candidates
- THEN `OverConstrainedRouteException` is thrown with the High must-see's `PlaceId` in `ConflictingPlaceIds`

### FR9: GenerateTripHandler invokes itinerary generation after persistence

The `GenerateTripHandler` SHALL call `IItineraryGenerator.GenerateAsync(trip, weatherData, ct)` after step 6 (persistence) and before step 7 (response mapping). The generator populates `Trip.Days` with activities and transit, and the handler saves the updated trip. When the trip carries `TripPreferences.Interests`, the handler SHALL validate that at least one interest value exists among the city's known interests before invoking generation; invalid interests SHALL produce a validation error, not a runtime exception.

#### Scenario: Successful itinerary generation after trip creation

- GIVEN a valid `GenerateTrip` command with must-sees and dates
- WHEN the handler processes the command
- THEN after `tripRepository.AddAsync()`, it calls `itineraryGenerator.GenerateAsync()`
- AND the trip status is updated to `GENERATED`
- AND the response includes the filled `DayPlan[]`

#### Scenario: Itinerary generation fails gracefully

- GIVEN `IItineraryGenerator.GenerateAsync()` throws `OverConstrainedRouteException`
- WHEN the handler catches it
- THEN the exception propagates to the API layer (no silent swallow)

#### Scenario: Valid interests pass validation

- GIVEN a trip with interests ["museum", "food"]
- AND the city has places with attributes matching "museum" and "food"
- WHEN the handler validates before generation
- THEN generation proceeds normally with interest-filtered candidates

#### Scenario: Invalid interests cause validation error

- GIVEN a trip with interests ["nonexistent_interest"]
- AND the city has NO places with matching attribute values
- WHEN the handler validates before generation
- THEN a validation error is returned (not a runtime exception)

### FR10: Response includes full DayPlan[] with blocks, activities, transit, hotel transit, and times

`TripPlanResponse` SHALL expose all itinerary data including hotel transit legs and exact times. `BlockResponse` MUST add `TransitFromHotel` and `TransitToHotel` (nullable `TransitResponse`). `ActivityResponse` MUST add `EstimatedArrival` and `EstimatedDeparture` (nullable `int`, minutes from midnight). `AutoMapperProfile` MUST include: `TransitDetails → TransitResponse` mapping; `BlockTimeline.TransitFromHotel/ToHotel → BlockResponse.TransitFromHotel/ToHotel`; `ActivityNode.EstimatedArrival/Departure → ActivityResponse.EstimatedArrival/Departure`.

#### Scenario: API response contains full itinerary

- GIVEN a generated trip with 3 days
- WHEN the client receives `TripPlanResponse`
- THEN the response includes each day's Morning, Afternoon, and Evening blocks
- AND each block contains its `ActivityNode` list with `SequenceOrder`, `PlaceId`, `Name`, `DurationMinutes`, `IsIndoor`, `Priority`
- AND each activity with a next-activity has `TransitToNext` with `TransportMode`, `DurationMinutes`, `BufferMinutes`

#### Scenario: Block response includes hotel transit details

- GIVEN a block with `TransitFromHotel` and `TransitToHotel` set
- WHEN `TripPlanResponse` is mapped from `Trip`
- THEN `BlockResponse.TransitFromHotel` and `BlockResponse.TransitToHotel` contain `TransportMode`, `DurationMinutes`, `BufferMinutes`, `FrictionAlert`

#### Scenario: Activity response includes arrival and departure times

- GIVEN a scheduled activity with `EstimatedArrival=555`, `EstimatedDeparture=615`
- WHEN `TripPlanResponse` is mapped
- THEN `ActivityResponse.EstimatedArrival = 555` and `ActivityResponse.EstimatedDeparture = 615`

#### Scenario: Null hotel transit and times map to null in response

- GIVEN a block where `TransitFromHotel` and `TransitToHotel` are null
- WHEN mapped to `BlockResponse`
- THEN both fields are null (not default/zero)

### FR11: ActivityNode stores PlaceLocation for distance calculation

`ActivityNode` MUST carry a `PlaceLocation` property so that the itinerary generator can compute real Haversine distance between consecutive activities without looking up `Place` entities from a dictionary. The `PlaceLocation` is set at creation time from the source `Place.Location` and stored as an EF Core owned entity (3 separate owned tables: `ActivityNode_Location`, `ActivityNode_OpeningHours` if retained, etc.).

#### Scenario: ActivityNode created with PlaceLocation

- GIVEN a Place at Location(40.4168, -3.7038)
- WHEN an ActivityNode is created from that Place
- THEN `ActivityNode.Location` equals `PlaceLocation(40.4168, -3.7038)`

#### Scenario: ActivityNode without location produces null

- GIVEN a Place with null Location
- WHEN an ActivityNode is created from that Place
- THEN `ActivityNode.Location` is null

### FR12: Haversine distance scoring replaces stub

`HeuristicItineraryGenerator.EstimateDistanceFromNearestActivity` currently returns a fixed `1.0`. The system MUST replace this stub with real Haversine distance calculation using `ActivityNode.Location` from the previous activity in the block. The `ICandidateScorer` receives `DistanceFromBlockCenterKm` computed from actual coordinates.

#### Scenario: Haversine distance computed from previous activity

- GIVEN a block with activities at Location(40.4168, -3.7038) and a candidate at Location(40.4520, -3.6900)
- WHEN candidate scoring runs
- THEN `DistanceFromBlockCenterKm` equals the Haversine distance in km between those two coordinates
- AND the value is NOT the stub 1.0

#### Scenario: Empty block yields zero distance

- GIVEN a Morning block with zero existing activities
- WHEN candidate scoring runs for the first slot
- THEN `DistanceFromBlockCenterKm` is 0 (no previous activity to measure from)

### FR13: Generator refactored into testable phase collaborators

`HeuristicItineraryGenerator` (395 lines) MUST be decomposed into 5 phase classes that are independently testable. Each phase class implements a single responsibility and is injectable via DI. The mutable `_placesById` dictionary MUST be removed; all place lookups use `ActivityNode.Location` instead.

| Phase | Responsibility | Extracted From |
|-------|---------------|----------------|
| `PinnedPlacementPhase` | Place pinned must-sees at their exact day/block | Lines 60-70, 113-150 |
| `UnpinnedPlacementPhase` | Zone-clustered placement of unpinned must-sees | Lines 73-98, 152-183 |
| `CandidateFillingPhase` | Scored candidate filling of remaining capacity | Lines 100-248 |
| `TransitEnrichmentPhase` | Transit mode assignment between consecutive activities | Lines 250-289 |
| `WeatherEnrichmentPhase` | Weather summary assignment per day | Lines 258-259 |

#### Scenario: Each phase is independently unit-testable

- GIVEN the `CandidateFillingPhase` class
- WHEN a test constructs it with a mocked `ICandidateScorer`
- THEN the phase produces correctly scored and placed candidates without requiring the full generator

#### Scenario: No mutable shared state in generator

- GIVEN a `HeuristicItineraryGenerator` instance
- WHEN `GenerateAsync` is called twice concurrently
- THEN no shared mutable dictionary (_placesById) causes race conditions
- AND ActivityNode.Location is used for distance lookups instead of dictionary lookups

### FR14: BlockTimeline hotel transit properties

`BlockTimeline` MUST expose `TransitFromHotel` (`TransitDetails?`) and `TransitToHotel` (`TransitDetails?`) representing hotel→first-activity and last-activity→hotel transit legs. `BlockTotalDurationMinutes` semantics MUST remain unchanged (sum of activities + inter-activity transit only). A computed `BlockWallClockDurationMinutes` MAY be added summing hotel legs + block duration for display.

#### Scenario: BlockTotalDurationMinutes excludes hotel transit

- GIVEN a block with one 60-min activity, TransitFromHotel(15min), TransitToHotel(20min)
- WHEN `BlockTotalDurationMinutes` is computed
- THEN the result is 60 (activity duration only, no hotel legs)

#### Scenario: BlockWallClockDurationMinutes includes hotel transit

- GIVEN same block
- WHEN `BlockWallClockDurationMinutes` is computed
- THEN the result is 95 (15 + 60 + 20)

### FR15: Outbox Trigger After Itinerary Generation

After `IItineraryGenerator.GenerateAsync` populates `Trip.Days`, the `GenerateTripItineraryHandler` MUST extract unique `PlaceId`s from all activities across all days and filter to those where `Place.IsEnriched == false`. The handler MUST call `IOutboxWriter.EnqueueAsync` with the unenriched `PlaceProviderReferenceId`s BEFORE returning the response. The Outbox enqueue MUST participate in the same EF Core transaction as the trip save (`ITripRepository.UpdateAsync`).

#### Scenario: Unenriched places queued after generation

- GIVEN a generated itinerary with 5 unique places, 3 of which have `IsEnriched = false`
- WHEN the handler completes generation
- THEN 3 Outbox messages are enqueued for the unenriched places

#### Scenario: Already enriched places are skipped

- GIVEN a generated itinerary where all places have `IsEnriched = true`
- WHEN the handler completes generation
- THEN zero Outbox messages are enqueued

#### Scenario: Duplicate PlaceIds deduplicated

- GIVEN an itinerary where the same unenriched Place appears in 3 activities across 2 days
- WHEN the handler extracts unique PlaceIds
- THEN only 1 Outbox message is enqueued for that Place

#### Scenario: Outbox enqueue is atomic with trip save

- GIVEN the handler saves the trip and enqueues Outbox messages in one transaction
- WHEN the transaction commits
- THEN both the trip update and Outbox inserts are persisted
- WHEN the transaction rolls back
- THEN neither the trip update nor Outbox inserts are persisted

#### Scenario: Outbox trigger failure does not block itinerary response

- GIVEN `IOutboxWriter.EnqueueAsync` throws
- WHEN the handler catches the exception
- THEN the itinerary response SHALL still be returned (enrichment is best-effort, non-blocking)
- AND the exception is logged

### Requirement: GenerateTripItineraryHandler enforces ownership before regeneration

`GenerateTripItineraryHandler` MUST load the trip, verify `trip.OwnerUserId == IUserContext.UserId`, and throw an ownership exception (→ `403`) **before** invoking `IItineraryGenerator.GenerateAsync` or enqueuing outbox messages (FR15). The existing outbox trigger behavior (FR15) is otherwise unchanged.

#### Scenario: Regeneration blocked for non-owner

- GIVEN a trip owned by `"user-42"` and a regenerate request with `sub = "user-99"`
- WHEN `GenerateTripItineraryHandler` runs
- THEN a `403 Forbidden` is returned
- AND `IItineraryGenerator.GenerateAsync` is never called
- AND no outbox messages are enqueued

## 3. Acceptance Criteria

| ID | Criterion |
|----|-----------|
| AC1 | Pinned must-sees appear in the correct day and block per their `PinnedDayIndex` and `PinnedBlock` |
| AC2 | Unpinned must-sees respect `OpeningHoursWindow.DayOfWeek` for the day they are assigned |
| AC3 | Zone clustering minimizes backtracking: consecutive activities in the same block have geographically close locations |
| AC4 | When `WeatherSummary = Bad` and `WeatherAwareEnabled = true`, outdoor activities are deprioritized in scoring |
| AC5 | Transport mode follows rules: default `WALK_AND_PUBLIC_TRANSPORT`; switch to `CAR` when car available and PT+walk > 20 min slower |
| AC6 | Block capacity limits enforced per `TripPlanningConstants`: Morning 3/210min, Afternoon 3/180min, Evening 2/105min |
| AC7 | `OverConstrainedRouteException` thrown when a `High` priority must-see cannot fit after dropping all `Low` and `Medium` items |
| AC8 | `TripPlanResponse` includes `DayPlan[]` with blocks, activities, transit details, duration estimates |
| AC9 | All must-sees are included in the itinerary unless physically impossible (reason surfaced in exception) |
| AC10 | All 172+ existing tests continue passing after the change |

## 4. Non-Functional Requirements

- **NFR1**: No database-specific syntax in domain services or ports (`IItineraryGenerator`, `ICandidateScorer`, `ITransitCalculator` are Domain-layer interfaces with no EF Core or SQL dependencies)
- **NFR2**: Domain-agnostic ports — `ICandidateScorer` and `ITransitCalculator` are injectable ports; implementations live in Infrastructure
- **NFR3**: All heuristic logic is unit-testable without external API calls (no HTTP clients in domain logic)
- **NFR4**: Heuristic algorithm runs synchronously within the handler; no background jobs for MVP
- **NFR5**: `IItineraryGenerator` is swappable — future OR-Tools implementation can be injected without changing the handler

## 5. Integration Points

| Integration Point | Direction | Description |
|-------------------|-----------|-------------|
| `IItineraryGenerator` (new Domain port) | ApplicationServices → Domain | Called by `GenerateTripHandler` after persistence |
| `ICandidateScorer` (new Domain port) | Domain → Infrastructure | Scores candidate places for slot filling |
| `ITransitCalculator` (new Domain port) | Domain → Infrastructure | Estimates transit duration/mode between places |
| `IWeatherProvider` (new Domain port) | Domain → Infrastructure | Provides weather forecast per date (stubbed for MVP) |
| `IPlaceRepository.GetManyByCityIdAsync` (new method) | Domain → Infrastructure | Fetches candidate places for a city |
| `GenerateTripHandler` (modified) | ApplicationServices | Adds itinerary generation call after `AddAsync` |
| `TripPlanResponse` (modified) | Domain → API | Adds `DayPlan[]` to response DTO |
| `Trip.GenerateDays(IEnumerable<DayPlan>)` (existing) | Domain | Already accepts pre-built days; generator uses this |

## 6. Out of Scope

- Google OR-Tools VRP solver (explicitly deferred to post-MVP)
- Real-time routing API integration (Google Maps, HERE, etc.)
- Exact per-visit start times (only sequence order + duration)
- Multi-city / hotel-switch trips
- Automatic replanning engine
- User preference learning / ML-based scoring
- Budget or cost optimization
- Restaurant / meal slot management

## 7. Risks & Assumptions

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| Transit time estimates inaccurate without real routing API | Medium | Use buffered heuristic estimates; document as known MVP limitation |
| Zone clustering produces suboptimal groupings | Medium | Start with simple lat/lng distance threshold; iterate based on user feedback |
| Candidate place source may lack coverage | High | Use Place repository seeded with Madrid data; Foursquare fallback for gaps |
| Opening hours edge cases (closed days, seasonality) cause empty blocks | Low | Validate must-see feasibility before generation; surface clear error messages |
| Heuristic performance on long trips (14 days) | Low | Complexity is O(n·d) where n=places, d=days; fast enough for MVP |

**Assumptions**:

- `Place.OpeningHours` is populated for must-sees and candidates (even if approximate)
- Weather data is available as `Dictionary<DateOnly, WeatherCondition>` (stubbed for MVP)
- Zone/barrio can be derived from `PlaceLocation` proximity (simple lat/lng distance)
- `TripPreferences.CarAvailable` and `MaxWalkingMinutes` are provided in the request
- The existing 172+ test suite covers domain invariants thoroughly