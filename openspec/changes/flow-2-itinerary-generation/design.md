# Design: Flow 2 — Itinerary Generation (Heuristic Planner)

## 1. Architecture Overview

The heuristic planner follows Clean Architecture layering: domain ports define contracts, domain services contain heuristic logic, application services orchestrate, and infrastructure provides adapters.

```
GenerateTripHandler (ApplicationServices)
       │
       ├── IPlaceRepository.GetManyByCityIdAsync(cityId)
       ├── IWeatherProvider.GetWeatherAsync(cityId, dates)
       ├── IItineraryGenerator.GenerateAsync(trip, places, weather, ct)
       │         │
       │         ├── ICandidateScorer.Score(place, context)
       │         ├── ITransitCalculator.EstimateAsync(from, to, mode)
       │         └── Zone clustering (internal)
       │
       └── ITripRepository.UpdateAsync(trip, ct)

TripPlanResponse (extended with DayPlan[])
```

**Key design choice**: `IItineraryGenerator` is the single orchestration port. The handler calls it after persistence. The generator coordinates zone clustering, must-see placement, candidate scoring, transport, and weather — all through injected domain ports. This keeps the handler thin and the algorithm testable in isolation.

**Dependency flow**: Domain (`IItineraryGenerator`, `ICandidateScorer`, `ITransitCalculator`, `IWeatherProvider`) → no external deps. Infrastructure implements adapters. ApplicationServices wires everything.

## 2. Domain Model Additions

### No new entities or value objects required

The existing domain model already supports the heuristic planner:

- `Trip.GenerateDays(IEnumerable<DayPlan>)` — populates `_days` and sets status to `GENERATED`
- `DayPlan.AddActivity(BlockType, ActivityNode)` — delegates to `BlockTimeline.AddActivity`
- `BlockTimeline.CanFitActivity(durationMinutes)` — validates capacity
- `BlockTimeline.AddActivity(ActivityNode)` — enforces max visits + max duration
- `ActivityNode` — already has `PlaceId`, `Name`, `DurationMinutes`, `IsIndoor`, `Priority`, `TransitToNext`
- `TransitDetails` — already has `TransportMode`, `DurationMinutes`, `BufferMinutes`, `FrictionAlert`
- `MustSee` — already has `PlaceId`, `Priority`, `PinnedDayIndex`, `PinnedBlock`
- `TripPreferences` — already has `CarAvailable`, `MaxWalkingMinutes`, `WeatherAwareEnabled`
- `OverConstrainedRouteException` — already exists with `ConflictingPlaceIds`

**One minor bug fix**: `OverConstrainedRouteException.ConflictingPlaceIds` is typed as `IReadOnlyList<string>` but `MustSee.PlaceId` is `long`. The exception should use `IReadOnlyList<long>` to match the domain. This is a prerequisite fix.

### New enrichments to existing types (not new entities)

- `PlaceLocation`: Add `DistanceKmTo(PlaceLocation other)` method using haversine formula — needed for zone clustering and distance calculations
- `OpeningHoursWindow`: Add `IsOpenOn(DayOfWeek day)` convenience check (the data exists as `DayOfWeek` property but there's no query method)

## 3. Port Definitions

### IItineraryGenerator (Domain Service Port)

```csharp
// SmartTripPlanner.Domain/Ports/IItineraryGenerator.cs
namespace SmartTripPlanner.Domain.Ports;

public interface IItineraryGenerator
{
    /// <summary>
    /// Generates a complete itinerary for the trip, populating Trip.Days
    /// with activities and transit. Sets Trip.Status to GENERATED.
    /// Throws OverConstrainedRouteException if High-priority must-sees cannot fit.
    /// </summary>
    Task GenerateAsync(
        Trip trip,
        IReadOnlyList<Place> candidatePlaces,
        Dictionary<DateOnly, WeatherCondition> weatherData,
        CancellationToken ct);
}
```

The generator receives the trip (with must-sees and preferences), candidate places (from `IPlaceRepository.GetManyByCityIdAsync`), and pre-fetched weather data. It populates `trip.Days` directly and sets status to `GENERATED`.

### ICandidateScorer (Domain Port)

```csharp
// SmartTripPlanner.Domain/Ports/ICandidateScorer.cs
namespace SmartTripPlanner.Domain.Ports;

public interface ICandidateScorer
{
    double Score(Place place, ScoringContext context);
}

public record ScoringContext(
    bool IsFamilyTrip,
    bool IsBadWeather,
    double DistanceFromBlockCenterKm,
    double PopularityRaw); // 0.0–1.0 from external data or fallback
```

Returns a double score. Higher = better candidate. The heuristic implementation uses the scoring formula (see §4.5).

### ITransitCalculator (Domain Port)

```csharp
// SmartTripPlanner.Domain/Ports/ITransitCalculator.cs
namespace SmartTripPlanner.Domain.Ports;

public interface ITransitCalculator
{
    Task<TransitEstimate> EstimateAsync(
        PlaceLocation from, PlaceLocation to, TransportMode mode, CancellationToken ct);
}

public record TransitEstimate(
    int DurationMinutes,
    int BufferMinutes = 10,
    bool FrictionAlert = false);
```

Returns estimated transit time. For MVP, the infrastructure implementation uses haversine + mode-specific speed constants. No real API calls.

### IWeatherProvider (Domain Port)

```csharp
// SmartTripPlanner.Domain/Ports/IWeatherProvider.cs
namespace SmartTripPlanner.Domain.Ports;

public interface IWeatherProvider
{
    Task<Dictionary<DateOnly, WeatherCondition>> GetWeatherAsync(
        long cityId, DateOnly startDate, DateOnly endDate, CancellationToken ct);
}
```

MVP: stubbed implementation returns `Dictionary<DateOnly, WeatherCondition>` with `Clear` for all dates. Real implementation deferred.

### IPlaceRepository addition

```csharp
// Add to existing IPlaceRepository interface
Task<List<Place>> GetManyByCityIdAsync(long cityId, CancellationToken ct);
```

Returns all places for a city (for candidate selection). Includes `OpeningHours` and `Location`.

## 4. Algorithm Design

### 4.1 Phase Overview

The `HeuristicItineraryGenerator.GenerateAsync` method executes 5 sequential phases:

```
┌─────────────┐   ┌──────────────┐   ┌───────────────┐   ┌───────────┐   ┌────────────┐
│ 1. Generate  │──▶│ 2. Place     │──▶│ 3. Zone       │──▶│ 4. Fill   │──▶│ 5. Transit │
│   Days       │   │  Must-Sees   │   │  Cluster &    │   │ Candidates│   │ & Weather  │
│   (empty)    │   │  (pinned     │   │  Reorder      │   │   (scoring)│   │  (enrich)  │
│              │   │   first)     │   │               │   │           │   │            │
└─────────────┘   └──────────────┘   └───────────────┘   └───────────┘   └────────────┘
```

### 4.2 Phase 1 — Generate Empty Days

```
trip.GenerateDays()  // existing method creates N empty DayPlans with 3 blocks each
```

No change from existing behavior. Creates empty scaffolding.

### 4.3 Phase 2 — Place Must-Sees (Pinned → Unpinned)

**Step 2a**: Resolve pinned must-sees.

For each `MustSee` where `PinnedDayIndex` ≠ null:
1. Validate the day index is within range; throw `BusinessRuleException` if not (already validated in handler).
2. Determine target block: if `PinnedBlock` is set, use it; otherwise pick the first block where `CanFitActivity()` returns true and the place is open on that day.
3. Create `ActivityNode` from must-see's place data.
4. Add to the target block via `dayPlan.AddActivity(blockType, activity)`.
5. If block is full, try adjacent blocks of the same day before marking as overflow.

**Step 2b**: Place unpinned must-sees.

1. Group remaining must-sees by zone (see §4.4 for zone clustering).
2. For each zone group, find the best day where most places in the group are open (check `OpeningHoursWindow.DayOfWeek`).
3. Assign must-sees to blocks within that day, respecting capacity.
4. Priority order: `High` → `Medium` → `Low`.

**Fallback chain** (see §4.7): If a `High` must-see cannot fit anywhere after all attempts, throw `OverConstrainedRouteException`.

### 4.4 Phase 3 — Zone Clustering

**Algorithm**: Single-pass distance-based clustering using haversine distance.

```
ZoneId = ClusterId assigned sequentially (0, 1, 2, ...)
ZONE_RADIUS_KM = 2.0  // constant in TripPlanningConstants

For each unclustered place:
    Find existing zone whose centroid is within ZONE_RADIUS_KM
    If found: add place to that zone, update centroid (running average)
    If not found: create new zone with this place as centroid
```

Centroid update uses running average to avoid recomputing all points. This produces O(n²) worst case for n places, acceptable for MVP (typical city: 20–50 candidates).

The zone ID is NOT stored on `Place` — it's a runtime-only grouping via `Dictionary<int, List<Place>>` inside the generator.

### 4.5 Phase 4 — Fill Candidates (Scoring)

For each day, for each block with remaining capacity:

1. Get candidate places not already placed, filtered by opening hours for that day.
2. If `TripPreferences.WeatherAwareEnabled && dayWeather == Bad`: filter to prefer indoor places (outdoor candidates get a penalty, not eliminated — must-sees override weather).
3. Score each candidate using `ICandidateScorer.Score(place, context)`.

**Scoring formula**:

```
score = priorityBonus + familyFriendlyBonus + popularity - distancePenalty + weatherBonus

Where:
  priorityBonus      = MustSee ? { High: 100, Medium: 50, Low: 10 } : 0
  familyFriendlyBonus = (IsFamilyFriendly && travelers.HasChildren) ? 15 : 0
  popularity          = PopularityRaw * 20   (0–20 range)
  distancePenalty     = DistanceFromBlockCenterKm * 5
  weatherBonus        = (IsBadWeather && IsIndoor) ? 20 : 0
                       (IsBadWeather && !IsIndoor) ? -20 : 0
```

Constants chosen to ensure must-sees always rank above candidates, family-friendly wins on family trips, and weather adjusts meaningfully but doesn't override `High` priority.

4. Sort candidates by score (descending).
5. Fill block until `CanFitActivity()` returns false.
6. Move to next block.

### 4.6 Phase 5 — Transit & Weather Enrichment

**Transit assignment**: After all activities are placed, iterate through each block's activities. For each consecutive pair (A → B):

1. Calculate estimated transit using `ITransitCalculator.EstimateAsync(A.Location, B.Location, mode)`.
2. **Transport mode selection rules**:
   - Default: `WALK_AND_PUBLIC_TRANSPORT`
   - Switch to `CAR` when `TripPreferences.CarAvailable == true` AND:
     - PT+walk duration exceeds car duration by 20+ minutes, OR
     - Walking distance exceeds `TripPreferences.MaxWalkingMinutes`
   - Within same zone (distance < ~1.5 km): always `WALK_AND_PUBLIC_TRANSPORT` regardless
3. Set `ActivityNode.TransitToNext` = new `TransitDetails(mode, duration, buffer, friction)`.

**Weather enrichment**: Update each `DayPlan`'s `WeatherSummary` from the weather dictionary. Currently `DayPlan.WeatherSummary` is `init`-only. This design requires changing `WeatherSummary` from `init` to `set` with a private setter, or using a `SetWeather(WeatherCondition)` method on `DayPlan`.

### 4.7 Fallback Chain

When capacity overflows:

1. **Remove all `Low`-priority candidates** from blocks that are at capacity.
2. If still overflowing: **remove `Medium`-priority candidates** one at a time (lowest score first).
3. If still overflowing with only `High` must-sees: **throw `OverConstrainedRouteException`** with the `PlaceId` list of conflicting items.

Must-sees with `High` priority are NEVER dropped. The exception propagates to the handler, which lets it bubble to the API layer.

### 4.8 Block Capacity Enforcement

Use existing `BlockTimeline.CanFitActivity(durationMinutes)` and `BlockTimeline.AddActivity(ActivityNode)` directly. The existing code throws `InvalidOperationException` on overflow. The generator calls `CanFitActivity` before `AddActivity` and skips if capacity is reached (no exception path in normal flow).

**Important**: `CanFitActivity` currently only checks duration, not visit count. `AddActivity` checks BOTH. The generator must check both: first `Activities.Count < maxVisits`, then `CanFitActivity(duration)`.

Actually, re-reading the code: `AddActivity` throws on both conditions. The generator should call `CanFitActivity` first (which checks visits count + duration), and only call `AddActivity` if `CanFitActivity` returns true. This matches the spec's behavior of "not added; generator skips to next candidate."

## 5. Implementation Details

### 5.1 GenerateTripHandler Integration

Add `IItineraryGenerator`, `IWeatherProvider`, and `IPlaceRepository` (already injected) to the handler. Insert after step 6 (persistence):

```csharp
// 6.5 — Generate itinerary
var candidatePlaces = await placeRepository.GetManyByCityIdAsync(city.Id, ct);
var weatherData = await weatherProvider.GetWeatherAsync(city.Id, trip.StartDate, trip.EndDate, ct);
await itineraryGenerator.GenerateAsync(trip, candidatePlaces, weatherData, ct);
await tripRepository.UpdateAsync(trip, ct);
trip.UpdateStatus(TripStatus.GENERATED);
await tripRepository.UpdateAsync(trip, ct); // persist GENERATED status
```

Note: The existing `tripRepository.AddAsync` already persists. We need an `UpdateAsync` call after itinerary generation to save the populated `DayPlan` entities and status change.

### 5.2 Response Mapping — Extend TripPlanResponse

Add `DayPlan[]` to `TripPlanResponse`:

```csharp
public record TripPlanResponse(
    Guid TripId,
    string TripCode,
    long CityId,
    string CityCode,
    string CityName,
    DateOnly StartDate,
    DateOnly EndDate,
    LocationModel BaseHotel,
    TravelersInput Travelers,
    TripPreferencesInput Preferences,
    IReadOnlyList<MustSeeResponse> MustSees,
    string Status,
    string DefaultStartHour,
    IReadOnlyList<DayPlanResponse> Days  // NEW
);

public record DayPlanResponse(
    int DayIndex,
    DateOnly Date,
    string WeatherSummary,
    BlockTimelineResponse Morning,
    BlockTimelineResponse Afternoon,
    BlockTimelineResponse Evening);

public record BlockTimelineResponse(
    string BlockType,
    IReadOnlyList<ActivityNodeResponse> Activities);

public record ActivityNodeResponse(
    int SequenceOrder,
    long PlaceId,
    string Name,
    int DurationMinutes,
    bool IsIndoor,
    string Priority,
    TransitDetailsResponse? TransitToNext);

public record TransitDetailsResponse(
    string TransportMode,
    int DurationMinutes,
    int BufferMinutes,
    bool FrictionAlert);
```

Mapping uses AutoMapper profiles to map domain → response DTOs.

### 5.3 DayPlan WeatherSummary Mutability

Current `DayPlan.WeatherSummary` is `init`-only. Add a method:

```csharp
public void SetWeather(WeatherCondition weather) => WeatherSummary = weather;
```

This follows the same pattern as `UpdateStartTime`. The `WeatherSummary` init setter changes to `private set` with the method accessor.

### 5.4 PlaceLocation.DistanceKmTo Method

```csharp
public double DistanceKmTo(PlaceLocation other)
{
    const double EarthRadiusKm = 6371.0;
    var dLat = ToRadians(other.Latitude - Latitude);
    var dLon = ToRadians(other.Longitude - Longitude);
    var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(ToRadians(Latitude)) * Math.Cos(ToRadians(other.Latitude)) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
    var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    return EarthRadiusKm * c;

    static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
```

### 5.5 OpeningHoursWindow.IsOpenOn Method

```csharp
public bool IsOpenOn(DayOfWeek day) => DayOfWeek == day;
```

Simple equality check. MVP does not handle "closed" (openMinutes == closeMinutes) — that's a future enhancement.

### 5.6 OverConstrainedRouteException Type Fix

Change `ConflictingPlaceIds` from `IReadOnlyList<string>` to `IReadOnlyList<long>` to match `MustSee.PlaceId` type. This is a prerequisite that existing tests must accommodate.

### 5.7 Infrastructure Adapters

| Adapter | File | Description |
|---------|------|-------------|
| `HeuristicItineraryGenerator` | `Domain/Services/HeuristicItineraryGenerator.cs` | Core algorithm — Domain layer |
| `DistanceBasedCandidateScorer` | `Domain/Services/DistanceBasedCandidateScorer.cs` | Scoring using the formula — Domain layer |
| `HaversineTransitCalculator` | `Infrastructure/ExternalServices/Transit/HaversineTransitCalculator.cs` | Haversine-based estimates — Infrastructure |
| `StubbedWeatherProvider` | `Infrastructure/ExternalServices/Weather/StubbedWeatherProvider.cs` | Returns Clear for all dates — Infrastructure |

All Domain-layer implementations depend only on Domain types. No external dependencies.

### 5.8 DI Registration

In `ApplicationServicesRegistration`:
```csharp
services.AddScoped<IItineraryGenerator, HeuristicItineraryGenerator>();
services.AddScoped<ICandidateScorer, DistanceBasedCandidateScorer>();
```

In `InfrastructureServiceRegistration`:
```csharp
services.AddScoped<ITransitCalculator, HaversineTransitCalculator>();
services.AddScoped<IWeatherProvider, StubbedWeatherProvider>();
```

## 6. File Structure

| File | Action | Description |
|------|--------|-------------|
| `Domain/Ports/IItineraryGenerator.cs` | Create | Itinerary generation port |
| `Domain/Ports/ICandidateScorer.cs` | Create | Candidate scoring port + ScoringContext record |
| `Domain/Ports/ITransitCalculator.cs` | Create | Transit estimation port + TransitEstimate record |
| `Domain/Ports/IWeatherProvider.cs` | Create | Weather provider port |
| `Domain/Services/HeuristicItineraryGenerator.cs` | Create | Core heuristic algorithm implementation |
| `Domain/Services/DistanceBasedCandidateScorer.cs` | Create | Scoring formula implementation |
| `Domain/Services/ZoneClusterer.cs` | Create | Haversine-based zone clustering utility |
| `Domain/AggregatesModel/PlaceLocation.cs` | Modify | Add `DistanceKmTo(PlaceLocation)` method |
| `Domain/AggregatesModel/OpeningHoursWindow.cs` | Modify | Add `IsOpenOn(DayOfWeek)` method |
| `Domain/AggregatesModel/DayPlan.cs` | Modify | Add `SetWeather(WeatherCondition)` method, change WeatherSummary to `private set` |
| `Domain/Exceptions/OverConstrainedRouteException.cs` | Modify | Change `ConflictingPlaceIds` type from `IReadOnlyList<string>` to `IReadOnlyList<long>` |
| `Domain/Repository/IPlaceRepository.cs` | Modify | Add `GetManyByCityIdAsync(long, CancellationToken)` |
| `Domain/Constants/TripPlanningConstants.cs` | Modify | Add `ZoneRadiusKm = 2.0`, `CarFasterThresholdMinutes = 20`, `InterZoneThresholdKm = 2.0` |
| `Domain/ApiModels/TripPlanResponse.cs` | Modify | Add `Days` property |
| `Domain/ApiModels/DayPlanResponse.cs` | Create | DTO for DayPlan |
| `Domain/ApiModels/BlockTimelineResponse.cs` | Create | DTO for BlockTimeline |
| `Domain/ApiModels/ActivityNodeResponse.cs` | Create | DTO for ActivityNode |
| `Domain/ApiModels/TransitDetailsResponse.cs` | Create | DTO for TransitDetails |
| `ApplicationServices/Handlers/GenerateTripHandler.cs` | Modify | Add itinerary generation call after persist |
| `ApplicationServices/ApplicationServicesRegistration.cs` | Modify | Register IItineraryGenerator, ICandidateScorer |
| `Infrastructure/ExternalServices/Transit/HaversineTransitCalculator.cs` | Create | Haversine-based transit estimates |
| `Infrastructure/ExternalServices/Weather/StubbedWeatherProvider.cs` | Create | Stubbed weather returning Clear for all dates |
| `Infrastructure/Repositories/PlaceRepository.cs` | Modify | Implement `GetManyByCityIdAsync` |
| `Infrastructure/InfrastructureServiceRegistration.cs` | Modify | Register ITransitCalculator, IWeatherProvider |
| `API/Controllers/TripsController.cs` | No change | Response shape handled by DTO mapping |
| `tests/.../Domain/Services/HeuristicItineraryGeneratorTests.cs` | Create | Unit tests for core algorithm |
| `tests/.../Domain/Services/DistanceBasedCandidateScorerTests.cs` | Create | Unit tests for scoring formula |
| `tests/.../Domain/Services/ZoneClustererTests.cs` | Create | Unit tests for clustering |
| `tests/.../Infrastructure/ExternalServices/Transit/HaversineTransitCalculatorTests.cs` | Create | Unit tests for transit estimates |
| `tests/.../ApplicationServices/Handlers/GenerateTripHandlerTests.cs` | Modify | Update to verify itinerary generation integration |

## 7. Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| **Unit: HeuristicItineraryGenerator** | Pinned must-sees placed correctly; unpinned distributed by zone; fallback chain; over-constrained exception | Create `Trip` with known must-sees, mock `ICandidateScorer`, `ITransitCalculator`, `IWeatherProvider`. Assert block contents. |
| **Unit: DistanceBasedCandidateScorer** | Scoring formula correctness: family bonus, weather bonus, distance penalty, priority stacking | Parameterized tests with varied `ScoringContext` values. |
| **Unit: ZoneClusterer** | Places within 2km grouped; isolated places get own zone; empty input | Pure function tests with known coordinates (Madrid landmarks). |
| **Unit: PlaceLocation.DistanceKmTo** | Known city distances (Madrid Puerta del Sol → Plaza Mayor ~0.6km) | Deterministic haversine assertions. |
| **Unit: BlockTimeline** | Existing tests + add test for `CanFitActivity` with visit count check (already tested, verify) | No new tests needed unless coverage gaps. |
| **Unit: DayPlan.SetWeather** | Weather condition is set correctly | Simple setter test. |
| **Unit: HaversineTransitCalculator** | Walk/PT vs Car mode estimates; reasonable duration ranges | Mock `PlaceLocation` pairs at known distances. |
| **Unit: StubbedWeatherProvider** | Returns Clear for all dates in range | Trivial verification. |
| **Integration: GenerateTripHandler** | End-to-end: handler persists trip, calls generator, returns response with Days | Mock repository; verify `IItineraryGenerator.GenerateAsync` is called; verify response includes `DayPlanResponse[]`. |
| **Regression** | All 172 existing tests pass | Run full suite after changes. |

## 8. Risks & Mitigations

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| `DayPlan` serialization needs EF Core mapping updates for `Days` + nested `ActivityNode` + `TransitDetails` | Medium | Add/verify EF Core Fluent API configurations for owned entities. Test round-trip persistence. |
| `OverConstrainedRouteException` type change (`string` → `long`) breaks existing consumers | Low | Search codebase for `ConflictingPlaceIds` usage. Only used in domain — no API consumers yet. Update any tests. |
| Zone clustering produces unintuitive groupings for widely-spread cities | Medium | Start with 2km radius; document as tuning parameter in `TripPlanningConstants`. Can adjust threshold per city post-MVP. |
| Transit estimates far off from reality | High | Haversine uses speed constants (walk: 5km/h, PT: 15km/h, car: 30km/h city). Document as known MVP limitation. Real routing API integration is post-MVP. |
| `GenerateAsync` is synchronous logic in an async method | Low | Algorithm is CPU-bound and runs in-memory. `async` is for `ITransitCalculator.EstimateAsync` extensibility. No perf issue for <14 days. |
| Existing `GenerateDays(IEnumerable<DayPlan>)` method throws if days already exist | Low | Generator creates `DayPlan` list first, then calls `GenerateDays()`. If `Status != GENERATED`, days list is empty — safe. Add a guard check. |