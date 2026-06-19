# Delta for Itinerary Generation

## MODIFIED Requirements

### Requirement: FR4: Candidate places fill remaining block capacity

After all must-sees are placed, the system SHALL fill remaining block slots with candidate places ranked by a composite score: `family-friendly bonus + popularity − distance penalty`. The system MUST use `ICandidateScorer` (Domain port) for score computation. Candidate places SHALL be filtered by the trip's interests via `IPlaceRepository.GetCandidatesByCityAndInterestsAsync` when interests are present; unfiltered candidates SHALL be used as fallback.

(Previously: FR4 did not include interest-based filtering; candidates were fetched unconditionally from `GetManyByCityIdAsync`.)

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

### Requirement: FR9: GenerateTripHandler invokes itinerary generation after persistence

The `GenerateTripHandler` SHALL call `IItineraryGenerator.GenerateAsync(trip, weatherData, ct)` after step 6 (persistence) and before step 7 (response mapping). The generator populates `Trip.Days` with activities and transit, and the handler saves the updated trip. When the trip carries `TripPreferences.Interests`, the handler SHALL validate that at least one interest value exists among the city's known interests before invoking generation; invalid interests SHALL produce a validation error, not a runtime exception.

(Previously: FR9 did not include interest validation; interests were not part of TripPreferences.)

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

## ADDED Requirements

### Requirement: ActivityNode stores PlaceLocation for distance calculation

`ActivityNode` MUST carry a `PlaceLocation` property so that the itinerary generator can compute real Haversine distance between consecutive activities without looking up `Place` entities from a dictionary. The `PlaceLocation` is set at creation time from the source `Place.Location` and stored as an EF Core owned entity (3 separate owned tables: `ActivityNode_Location`, `ActivityNode_OpeningHours` if retained, etc.).

#### Scenario: ActivityNode created with PlaceLocation

- GIVEN a Place at Location(40.4168, -3.7038)
- WHEN an ActivityNode is created from that Place
- THEN `ActivityNode.Location` equals `PlaceLocation(40.4168, -3.7038)`

#### Scenario: ActivityNode without location produces null

- GIVEN a Place with null Location
- WHEN an ActivityNode is created from that Place
- THEN `ActivityNode.Location` is null

### Requirement: Haversine distance scoring replaces stub

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

### Requirement: Generator refactored into testable phase collaborators

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