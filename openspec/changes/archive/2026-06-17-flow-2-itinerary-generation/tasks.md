# SDD Tasks — Flow 2: Itinerary Generation / Heuristic Planner

## Change
**flow-2-itinerary-generation**

## Summary
| Metric | Value |
|--------|-------|
| Total tasks | 14 |
| Estimated LOC | ~1000-1200 |
| Estimated tests | ~25-30 |
| Files changed | ~16 |
| Review workload | ~1200 lines (high) |
| Chained PRs recommended | **Yes** — exceeds 400-line budget |

## Dependencies
```
T1 (Domain fixes) ──> T2 (Ports) ──> T3 (Domain services) ──> T4 (Infrastructure)
                                            │
                                            ▼
T5 (Repository) ──> T6 (Handler) ──> T7 (Response mapping) ──> T8 (EF + Migration)
                                            │
                                            ▼
T9-T14 (Tests)
```

---

## Task Breakdown

### T1: Domain Prerequisite Fixes
**Layer**: Domain
**Priority**: High
**Dependencies**: None

**Description**: Fix 4 domain model issues needed before building the itinerary generator.

**Files to modify**:
- `SmartTripPlanner.Domain/Exceptions/OverConstrainedRouteException.cs`
- `SmartTripPlanner.Domain/AggregatesModel/DayPlan.cs`
- `SmartTripPlanner.Domain/ValueObjects/PlaceLocation.cs`
- `SmartTripPlanner.Domain/ValueObjects/OpeningHoursWindow.cs`

**Changes**:
1. OverConstrainedRouteException.ConflictingPlaceIds: `IReadOnlyList<string>` → `IReadOnlyList<long>`
2. DayPlan.WeatherSummary: `init` → `private set` + add `SetWeather(WeatherCondition)` method
3. PlaceLocation: add `DistanceKmTo(PlaceLocation other)` using haversine formula
4. OpeningHoursWindow: add `IsOpenOn(DayOfWeek day)` convenience method

**Acceptance criteria**:
- All 4 changes compile
- Existing tests still pass
- New unit tests for DistanceKmTo and IsOpenOn

**Estimated LOC**: ~50
**Estimated tests**: 4

---

### T2: Domain Ports
**Layer**: Domain
**Priority**: High
**Dependencies**: T1

**Description**: Define 4 domain ports (interfaces) for the itinerary generation system.

**Files to create**:
- `SmartTripPlanner.Domain/Ports/IItineraryGenerator.cs`
- `SmartTripPlanner.Domain/Ports/ICandidateScorer.cs`
- `SmartTripPlanner.Domain/Ports/ITransitCalculator.cs`
- `SmartTripPlanner.Domain/Ports/IWeatherProvider.cs`

**Interface definitions**:

```csharp
// IItineraryGenerator
public interface IItineraryGenerator
{
    Task<List<DayPlan>> GenerateAsync(
        Trip trip,
        IReadOnlyList<Place> places,
        CancellationToken ct = default);
}

// ICandidateScorer
public interface ICandidateScorer
{
    double Score(CandidateScoringContext context);
}

public record CandidateScoringContext(
    Place Place,
    MustSee? MustSee,
    BlockType TargetBlock,
    WeatherCondition Weather,
    PlaceLocation? PreviousLocation,
    double PopularityRaw = 0.5);

// ITransitCalculator
public interface ITransitCalculator
{
    TransitDetails Calculate(PlaceLocation from, PlaceLocation to);
}

// IWeatherProvider
public interface IWeatherProvider
{
    Task<Dictionary<DateOnly, WeatherCondition>> GetForecastAsync(
        string city,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default);
}
```

**Acceptance criteria**:
- All 4 interfaces compile
- No external dependencies (pure domain)
- ScoringContext record covers all scoring factors

**Estimated LOC**: ~60
**Estimated tests**: 0 (interfaces only)

---

### T3: Domain Services — Heuristic Algorithm
**Layer**: Domain
**Priority**: High
**Dependencies**: T1, T2

**Description**: Implement the core heuristic itinerary generator with zone clustering and scoring.

**Files to create**:
- `SmartTripPlanner.Domain/Services/HeuristicItineraryGenerator.cs`
- `SmartTripPlanner.Domain/Services/ZoneClusteringHelper.cs`
- `SmartTripPlanner.Domain/Services/CandidateScorer.cs`

**Algorithm (5 phases)**:

```
Phase 1: Setup
  - Generate DayPlan[] for each trip day
  - Fetch weather forecast per day (stubbed)
  - Load all Place data for MustSee.PlaceIds
  - Fetch candidate places by CityId

Phase 2: Place Pinned Must-Sees
  - For each MustSee with PinnedDayIndex:
    - Find target DayPlan
    - Find best block (Morning first, then Afternoon, then Evening)
    - Check opening hours for that DayOfWeek
    - AddActivity to block
    - Assign TransitDetails from hotel

Phase 3: Place Unpinned Must-Sees
  - Group by zone proximity (2km radius)
  - For each cluster:
    - Find day with most free capacity
    - Check opening hours for that day's DayOfWeek
    - Distribute across blocks to minimize backtracking
    - AddActivity with TransitDetails

Phase 4: Fill with Candidates
  - For each block with remaining capacity:
    - Filter candidates by weather (indoor if Bad weather)
    - Score candidates: priority_bonus + family_friendly + popularity - distance_penalty
    - Select highest score, check opening hours
    - AddActivity with TransitDetails

Phase 5: Transport & Weather Enrichment
  - For each activity:
    - Calculate transit to next activity (or hotel)
    - Assign TransportMode (Car vs PT+Walking)
    - Set WeatherSummary on DayPlan
```

**Scoring formula**:
```
Score = (PriorityBonus * 100) + (FamilyFriendly * 20) + (Popularity * 10) - (DistanceKm * 30)

PriorityBonus: High = 1.0, Medium = 0.5, Low = 0.2
FamilyFriendly: 1 if IsFamilyFriendly, 0 otherwise
Popularity: 0.5 (constant for MVP)
DistanceKm: distance from previous activity in cluster
```

**Transport rules**:
- Default: PublicTransport + Walking
- Switch to Car when:
  - PT+Walking is >20min slower than Car
  - Walking distance >2km
  - Distance >10km
- Penalize Car in dense zones (if parking friction detected)

**Fallback chain**:
1. Try all must-sees + candidates
2. If over capacity: remove candidates
3. If still over: remove LOW priority must-sees
4. If still over: remove MEDIUM priority must-sees
5. If still over with only HIGH: throw OverConstrainedRouteException

**Acceptance criteria**:
- All 5 phases execute correctly
- Pinned must-sees respect PinnedDayIndex
- Unpinned must-sees respect opening hours
- Zone clustering minimizes backtracking (consecutive places within 2km)
- Block capacity limits enforced
- Weather filter applied (indoor preferred on Bad weather)
- Transport rules followed
- Fallback chain works correctly
- Unit tests for each phase

**Estimated LOC**: ~350
**Estimated tests**: 12

---

### T4: Infrastructure Adapters
**Layer**: Infrastructure
**Priority**: High
**Dependencies**: T2

**Description**: Implement concrete adapters for the domain ports.

**Files to create**:
- `SmartTripPlanner.Infrastructure/Services/HaversineTransitCalculator.cs`
- `SmartTripPlanner.Infrastructure/Services/StubbedWeatherProvider.cs`

**HaversineTransitCalculator**:
- Calculate distance using haversine formula
- Estimate duration by mode:
  - Walking: 5km/h
  - PublicTransport: 15km/h + 10min buffer
  - Car: 30km/h + 5min buffer
- Choose mode based on rules from T3
- Return TransitDetails with TransportMode, DurationMinutes, BufferMinutes

**StubbedWeatherProvider**:
- Return `Clear` for all dates in MVP
- Interface allows future integration with real weather API

**Acceptance criteria**:
- Haversine distance accurate (test with known lat/lng pairs)
- Transport mode selection follows rules
- StubbedWeatherProvider returns Clear for all dates
- Unit tests for both

**Estimated LOC**: ~120
**Estimated tests**: 6

---

### T5: Repository Extension
**Layer**: Infrastructure / Domain
**Priority**: High
**Dependencies**: None

**Description**: Add candidate place query to IPlaceRepository.

**Files to modify**:
- `SmartTripPlanner.Domain/Repository/IPlaceRepository.cs`
- `SmartTripPlanner.Infrastructure/Persistence/PlaceRepository.cs`

**Changes**:
```csharp
// Add to IPlaceRepository
Task<List<Place>> GetManyByCityIdAsync(long cityId, CancellationToken ct = default);
```

**Implementation**:
- Query Places table where CityId == cityId
- Exclude places that are already in the MustSee list
- Return up to 50 candidates (configurable)
- Use ANSI SQL-compatible query (no ILIKE)

**Acceptance criteria**:
- Returns places by city
- Excludes must-sees
- Returns correct count
- Unit tests for repository

**Estimated LOC**: ~30
**Estimated tests**: 3

---

### T6: Handler Integration
**Layer**: ApplicationServices
**Priority**: High
**Dependencies**: T3, T4, T5

**Description**: Integrate IItineraryGenerator into GenerateTripHandler.

**Files to modify**:
- `SmartTripPlanner.ApplicationServices/Handlers/GenerateTripHandler.cs`

**Changes**:
- After `SaveChangesAsync()`:
  1. Fetch all Place data for MustSee.PlaceIds
  2. Call `IItineraryGenerator.GenerateAsync(trip, places, ct)`
  3. Save updated DayPlans
  4. Map to TripPlanResponse
- Add IItineraryGenerator to constructor injection

**Acceptance criteria**:
- Handler invokes generator after persistence
- DayPlans are saved correctly
- Response includes DayPlans
- Integration tests pass

**Estimated LOC**: ~40
**Estimated tests**: 3

---

### T7: Response Mapping
**Layer**: ApplicationServices / API
**Priority**: High
**Dependencies**: T6

**Description**: Extend TripPlanResponse to include DayPlan[] with blocks and activities.

**Files to modify**:
- `SmartTripPlanner.ApplicationServices/DTOs/TripPlanResponse.cs`
- `SmartTripPlanner.ApplicationServices/Mapping/MappingProfile.cs`

**Changes**:
```csharp
// Add to TripPlanResponse
public List<DayPlanResponse> Days { get; set; } = new();

public class DayPlanResponse
{
    public int DayIndex { get; set; }
    public DateOnly Date { get; set; }
    public string WeatherSummary { get; set; } = string.Empty;
    public List<BlockResponse> Blocks { get; set; } = new();
}

public class BlockResponse
{
    public string BlockType { get; set; } = string.Empty;
    public int TotalDurationMinutes { get; set; }
    public List<ActivityResponse> Activities { get; set; } = new();
}

public class ActivityResponse
{
    public string PlaceName { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string TransportMode { get; set; } = string.Empty;
    public int TransitDurationMinutes { get; set; }
}
```

**MappingProfile**: Add AutoMapper mappings for DayPlan → DayPlanResponse, BlockTimeline → BlockResponse, ActivityNode → ActivityResponse.

**Acceptance criteria**:
- Response includes all day plans with blocks
- Each block has activities with transit details
- Mapping tests pass

**Estimated LOC**: ~80
**Estimated tests**: 4

---

### T8: EF Core Mapping & Migration
**Layer**: Infrastructure
**Priority**: Medium
**Dependencies**: T1, T7

**Description**: Configure EF Core owned entities for DayPlan/BlockTimeline/ActivityNode and create migration.

**Files to modify**:
- `SmartTripPlanner.Infrastructure/Persistence/PlannerDbContext.cs`
- `SmartTripPlanner.Infrastructure/Persistence/Configurations/TripConfiguration.cs`

**Changes**:
- Configure DayPlan as owned entity collection of Trip
- Configure BlockTimeline as owned entity of DayPlan
- Configure ActivityNode as owned entity collection of BlockTimeline
- Configure TransitDetails as owned entity of ActivityNode
- Add migration: `Add-Migration Flow2ItineraryGeneration`

**Acceptance criteria**:
- Migration generates correct schema
- DayPlans are persisted correctly
- Querying Trip includes DayPlans
- Integration tests for persistence

**Estimated LOC**: ~60
**Estimated tests**: 2

---

### T9: Unit Tests — HeuristicItineraryGenerator
**Layer**: Tests
**Priority**: Medium
**Dependencies**: T3

**Description**: Comprehensive unit tests for the itinerary generator.

**Files to create**:
- `tests/SmartTripPlanner.Tests/SmartTripPlanner.Domain/Services/HeuristicItineraryGeneratorTests.cs`

**Test cases**:
- Generate with pinned must-sees → correct day/block
- Generate with unpinned must-sees → zone clustering
- Generate with weather Bad → indoor prioritized
- Generate with block overflow → fallback chain
- Generate with over-constrained → exception
- Generate with no candidates → only must-sees
- Generate with mixed priorities → correct order
- Transport mode selection → Car vs PT+Walking

**Estimated LOC**: ~200
**Estimated tests**: 8

---

### T10: Unit Tests — ZoneClusteringHelper
**Layer**: Tests
**Priority**: Medium
**Dependencies**: T3

**Description**: Test zone clustering logic.

**Files to create**:
- `tests/SmartTripPlanner.Tests/SmartTripPlanner.Domain/Services/ZoneClusteringHelperTests.cs`

**Test cases**:
- Cluster places within 2km → same cluster
- Cluster places >2km apart → different clusters
- Cluster with exactly 2km → same cluster
- Single place → single cluster
- Empty list → empty clusters

**Estimated LOC**: ~80
**Estimated tests**: 5

---

### T11: Unit Tests — Transit Calculator
**Layer**: Tests
**Priority**: Medium
**Dependencies**: T4

**Description**: Test haversine transit calculator.

**Files to create**:
- `tests/SmartTripPlanner.Tests/SmartTripPlanner.Infrastructure/Services/HaversineTransitCalculatorTests.cs`

**Test cases**:
- Distance between two known points → accurate
- Transport mode: short distance → Walking
- Transport mode: medium distance → PT+Walking
- Transport mode: long distance → Car
- Transport mode: >20min slower → Car
- Buffer minutes: correct per mode

**Estimated LOC**: ~100
**Estimated tests**: 6

---

### T12: Integration Tests — GenerateTripHandler
**Layer**: Tests
**Priority**: Medium
**Dependencies**: T6, T8

**Description**: End-to-end integration tests for trip generation with itinerary.

**Files to create**:
- `tests/SmartTripPlanner.Tests/SmartTripPlanner.ApplicationServices/Handlers/GenerateTripHandlerItineraryTests.cs`

**Test cases**:
- Generate trip with must-sees → response includes DayPlans
- Generate trip with pinned must-see → correct day
- Generate trip with weather Bad → indoor activities prioritized
- Generate trip with over-constrained → returns 422

**Estimated LOC**: ~120
**Estimated tests**: 4

---

### T13: API Controller Tests
**Layer**: Tests
**Priority**: Low
**Dependencies**: T7

**Description**: Update controller tests for new response format.

**Files to modify**:
- `tests/SmartTripPlanner.Tests/SmartTripPlanner.API/Controllers/TripsControllerTests.cs`

**Changes**:
- Update GetTrip test to assert DayPlans in response
- Add test for itinerary generation endpoint

**Estimated LOC**: ~40
**Estimated tests**: 2

---

### T14: Regression & Validation
**Layer**: Tests
**Priority**: High
**Dependencies**: T9-T13

**Description**: Ensure all existing tests pass and no regressions.

**Command**:
```bash
dotnet test --verbosity minimal
```

**Acceptance criteria**:
- All 172 existing tests pass
- All new tests pass
- Total tests >= 197
- Build succeeds with 0 errors

**Estimated LOC**: 0
**Estimated tests**: 0 (validation)

---

## Review Workload Forecast

| Phase | Files | LOC | Tests |
|-------|-------|-----|-------|
| Domain fixes + ports | 8 | 110 | 4 |
| Domain services | 3 | 350 | 12 |
| Infrastructure | 3 | 150 | 9 |
| Handler + Response | 3 | 120 | 7 |
| EF Core + Migration | 2 | 60 | 2 |
| **Total** | **19** | **~790** | **34** |

**Review Budget**: 400 lines exceeded
**Recommendation**: **Chained PRs** or **size:exception**

---

## Next Steps

1. Review tasks and approve
2. Begin implementation (T1 → T14)
3. Verify after each task group
