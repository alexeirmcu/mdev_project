# Exploration: flow-2-timeline-and-hotel-transit

## Current State

The `HeuristicItineraryGenerator` runs a 5-phase algorithm that populates `Trip.Days` with activities and inter-activity transit, but **two key gaps remain** explicitly documented in `spec-trip-generation-flow-2.md` §7.1 and §7.4:

1. **`ActivityNode.EstimatedArrival` / `EstimatedDeparture`** are `int` properties (minutes from midnight) that exist on the entity but are **never set** by any phase. The spec states they were added for Flow 4 but the MVP generator only assigns `SequenceOrder` and `DurationMinutes`.

2. **Hotel transit legs are missing.** `TransitDetails` only connects `ActivityNode[i]` → `ActivityNode[i+1]` within a block. There is no representation of:
   - `Hotel → FirstActivity` (start of each block)
   - `LastActivity → Hotel` (end of each block)

### Key Architectural Findings

- **`BlockTimeline.BlockTotalDurationMinutes`** currently sums `Activity.DurationMinutes + Activity.TransitToNext.DurationMinutes`. It does **not** account for hotel transit legs. Adding hotel legs would change the conceptual "total block duration" if we want it to represent wall-clock time from leaving the hotel to returning.

- **`DayPlan.StartTime`** is a `TimeOnly` (default 09:00) stored per day, not per block. For MVP, each block can be treated as independently starting at `DayPlan.StartTime` (or a computed offset), but the spec notes "block boundaries implicitly start after previous block ends."

- **`TransitEnricher`** already has access to `ITransitCalculator`, `Trip.Preferences`, and `ActivityNode.Location`. It is the natural home for hotel transit calculation, but the current interface (`ITransitEnricher`) only accepts `Trip`, `placesById`, and `weatherData`.

- **`Trip.BaseHotel`** is a `Location` (name + lat/lng), while activity locations are `PlaceLocation` (lat/lng only). `PlaceLocation.DistanceKmTo()` expects another `PlaceLocation`, so a conversion or overload is needed for hotel transit.

- **`ActivityNode.TransitToNext`** is modeled as a single `TransitDetails` per activity. Hotel legs are **not** "from an activity to the next activity" — they are from/to the hotel. This structural mismatch is the core design decision.

---

## Affected Areas

| File | Why Affected |
|------|-------------|
| `SmartTripPlanner.Domain/AggregatesModel/BlockTimeline.cs` | `BlockTotalDurationMinutes` semantics; needs `HotelTransitToFirst` / `HotelTransitFromLast` properties (or similar) |
| `SmartTripPlanner.Domain/AggregatesModel/ActivityNode.cs` | `EstimatedArrival` / `EstimatedDeparture` need to be populated |
| `SmartTripPlanner.Domain/AggregatesModel/TransitDetails.cs` | May need factory method or overload for hotel→activity legs |
| `SmartTripPlanner.Domain/Services/TransitEnricher.cs` | Natural place to compute hotel transit; needs `Trip.BaseHotel` access |
| `SmartTripPlanner.Domain/Services/HeuristicItineraryGenerator.cs` | Needs new Phase 6 (Timeline Scheduling) or extension of Phase 5 |
| `SmartTripPlanner.Domain/Ports/ITransitEnricher.cs` | Interface signature may need to change if hotel transit requires more context |
| `SmartTripPlanner.Domain/ApiModels/TripPlanResponse.cs` | `BlockResponse` and/or `ActivityResponse` need new fields for hotel transit and exact times |
| `SmartTripPlanner.API/Configurations/AutoMapperProfile.cs` | New response fields require mapping rules |
| `tests/.../TransitEnricherTests.cs` | Existing tests mock `ITransitCalculator`; hotel transit adds new call patterns |
| `tests/.../HeuristicItineraryGeneratorTests.cs` | Tests assert on activity counts and transit modes; timeline fields need assertions |

---

## Approaches

### Approach A: Extend `TransitEnricher` + Add `TimelineScheduler`

**Description:**
- Keep `TransitEnricher` responsible for **all transit** (inter-activity + hotel legs).
- Add a new `ITimelineScheduler` / `TimelineScheduler` phase that runs **after** `TransitEnricher` and computes `EstimatedArrival` / `EstimatedDeparture` by walking each block's activity list.
- Hotel transit is stored as two new properties on `BlockTimeline`: `TransitFromHotel` and `TransitToHotel`.

**Pros:**
- Clean separation: `TransitEnricher` does transit, `TimelineScheduler` does time math.
- `BlockTimeline` owns the hotel legs naturally (one per block).
- Minimal change to `ActivityNode` structure.
- Easy to test independently.

**Cons:**
- Requires new interface + class + DI registration.
- `BlockTimeline` changes affect EF Core mapping (owned entity or table change).

**Effort:** Medium (~60 lines new scheduler + 30 lines enricher changes + 20 lines model changes = **~110 lines**)

---

### Approach B: Hotel Transit as Virtual `ActivityNode` Wrappers in Response Only

**Description:**
- Do **not** change `BlockTimeline` or `ActivityNode`.
- `TransitEnricher` computes hotel legs but stores them only in a transient lookup or computes them during mapping.
- Response DTO (`BlockResponse`) gets `HotelDepartureTransit` / `HotelReturnTransit` properties populated by AutoMapper logic that calls `ITransitCalculator`.

**Pros:**
- Zero domain model changes — no EF Core migration risk.
- Fits within tight line budget.

**Cons:**
- Violates Clean Architecture: domain transit logic leaks into API/Mapper layer.
- Hotel transit is not persisted or testable at domain layer.
- AutoMapper should not call async services (`ITransitCalculator`).

**Effort:** Low (~40 lines mapper changes + DTO changes)

**Verdict:** Rejected — violates architecture boundaries.

---

### Approach C: Extend `ActivityNode` with `TransitFromHotel` and `TransitToHotel`

**Description:**
- Add `TransitFromHotel?` to the first `ActivityNode` of each block.
- Add `TransitToHotel?` to the last `ActivityNode` of each block.
- `TimelineScheduler` uses these to compute arrival/departure times.

**Pros:**
- Hotel transit is adjacent to the activity it relates to.
- No changes to `BlockTimeline`.

**Cons:**
- Conceptually awkward: "from hotel" is a property of the first activity, not the activity itself.
- Complicates `ActivityNode` (which already has `TransitToNext`).
- Logic to "find first/last activity" is scattered.

**Effort:** Medium

**Verdict:** Rejected — poorer domain model than Approach A.

---

### Approach D: Inline Timeline Scheduling into `TransitEnricher`

**Description:**
- Merge both features into `TransitEnricher` (rename to `TransitAndTimelineEnricher` or keep name).
- After assigning `TransitToNext` for each activity, compute:
  - `Hotel → FirstActivity` transit
  - `LastActivity → Hotel` transit
  - `EstimatedArrival` / `EstimatedDeparture` for each activity

**Pros:**
- Single point of change — no new phase class.
- Fewer files touched.

**Cons:**
- `TransitEnricher` grows beyond single responsibility.
- Harder to unit-test in isolation (more assertions per test).
- Timeline scheduling is inherently synchronous/time-math; mixing with async `ITransitCalculator` calls is messy.

**Effort:** Low-Medium (~80 lines in enricher + model changes)

---

## Recommendation

**Adopt Approach A** with a hybrid twist: inline hotel transit calculation into `TransitEnricher` (it already calculates transit), but extract timeline scheduling into a dedicated `TimelineScheduler` class that runs as Phase 6 in `HeuristicItineraryGenerator`.

### Rationale

1. **Transit logic stays together.** `TransitEnricher` already iterates blocks and calls `ITransitCalculator`. Adding hotel→first and last→hotel legs there is natural and reuses the existing `AssignTransitAsync` helper pattern.

2. **Timeline scheduling is pure math.** Once all transit durations are known, computing `EstimatedArrival` / `EstimatedDeparture` is deterministic and synchronous — perfect for a separate class.

3. **Domain model clarity.** `BlockTimeline` is the right owner for hotel legs (one per block). `ActivityNode` keeps its existing shape.

4. **Testability.** `TimelineScheduler` can be unit-tested with in-memory `BlockTimeline` graphs without mocking `ITransitCalculator`.

---

## Detailed Implementation Plan

### Step 1: Extend `BlockTimeline` (Domain Model)

```csharp
public class BlockTimeline : Entity
{
    public BlockType BlockType { get; init; }
    public int BlockTotalDurationMinutes => Activities.Sum(a => a.DurationMinutes + (a.TransitToNext?.DurationMinutes ?? 0));
    public List<ActivityNode> Activities { get; private set; } = new();
    
    // NEW: Hotel transit legs
    public TransitDetails? TransitFromHotel { get; set; }
    public TransitDetails? TransitToHotel { get; set; }
    
    // Computed: wall-clock block duration including hotel legs
    public int BlockWallClockDurationMinutes => 
        (TransitFromHotel?.DurationMinutes ?? 0) +
        BlockTotalDurationMinutes +
        (TransitToHotel?.DurationMinutes ?? 0);
}
```

> **Note:** `BlockTotalDurationMinutes` is kept unchanged for backward compatibility with capacity checks. `BlockWallClockDurationMinutes` is additive.

### Step 2: Extend `TransitEnricher`

In `EnrichAsync`, after the inner `for` loop that assigns `TransitToNext`, add:

```csharp
var block = dayPlan.GetBlock(blockType);
if (block.Activities.Count > 0)
{
    var first = block.Activities[0];
    var last = block.Activities[^1];

    if (trip.BaseHotel is not null && first.Location is not null)
    {
        block.TransitFromHotel = await AssignTransitAsync(
            new PlaceLocation(trip.BaseHotel.Latitude, trip.BaseHotel.Longitude),
            first.Location,
            trip.Preferences, ct);
    }

    if (trip.BaseHotel is not null && last.Location is not null)
    {
        block.TransitToHotel = await AssignTransitAsync(
            last.Location,
            new PlaceLocation(trip.BaseHotel.Latitude, trip.BaseHotel.Longitude),
            trip.Preferences, ct);
    }
}
```

> **Risk:** `PlaceLocation` constructor validates lat/lng ranges. `Location` already validates in its own constructor, so this is safe.

### Step 3: Create `TimelineScheduler`

```csharp
public interface ITimelineScheduler
{
    void Schedule(Trip trip);
}

public class TimelineScheduler : ITimelineScheduler
{
    public void Schedule(Trip trip)
    {
        foreach (var day in trip.Days)
        {
            int currentTime = day.StartTime.Hour * 60 + day.StartTime.Minute;
            
            foreach (var blockType in new[] { BlockType.Morning, BlockType.Afternoon, BlockType.Evening })
            {
                var block = day.GetBlock(blockType);
                if (block.Activities.Count == 0) continue;

                // Account for transit from hotel
                currentTime += block.TransitFromHotel?.DurationMinutes ?? 0;
                currentTime += block.TransitFromHotel?.BufferMinutes ?? 0;

                for (int i = 0; i < block.Activities.Count; i++)
                {
                    var activity = block.Activities[i];
                    activity.EstimatedArrival = currentTime;
                    activity.EstimatedDeparture = currentTime + activity.DurationMinutes;

                    // Advance to next activity
                    if (activity.TransitToNext is not null)
                    {
                        currentTime = activity.EstimatedDeparture 
                                    + activity.TransitToNext.DurationMinutes 
                                    + activity.TransitToNext.BufferMinutes;
                    }
                }

                // After last activity, transit back to hotel does NOT advance currentTime
                // for the *next block* because blocks are treated as independent in MVP.
                // Reset to a computed block start or keep DayPlan.StartTime for all blocks.
                // DECISION: For MVP, each block starts at DayPlan.StartTime.
                currentTime = day.StartTime.Hour * 60 + day.StartTime.Minute;
            }
        }
    }
}
```

> **Open Question:** Should Afternoon/Evening blocks start after Morning ends, or all start at 09:00? The spec says "for MVP we can treat each block as independent starting at DayPlan.StartTime or a computed block start time." **Recommendation:** Start all blocks at `DayPlan.StartTime` for MVP to avoid implicit scheduling assumptions. A comment in code documents this decision.

### Step 4: Wire into `HeuristicItineraryGenerator`

```csharp
public class HeuristicItineraryGenerator : IItineraryGenerator
{
    private readonly IPinnedMustSeePlacer _pinnedPlacer;
    private readonly IUnpinnedMustSeePlacer _unpinnedPlacer;
    private readonly ICandidateFiller _candidateFiller;
    private readonly ITransitEnricher _transitEnricher;
    private readonly ITimelineScheduler _timelineScheduler; // NEW

    // ... constructor injection ...

    public async Task GenerateAsync(...)
    {
        // Phases 1-5 unchanged
        // ...
        await _transitEnricher.EnrichAsync(trip, placesById, weatherData, ct);
        
        // NEW Phase 6
        _timelineScheduler.Schedule(trip);

        // Fallback chain unchanged
    }
}
```

### Step 5: Response DTO & AutoMapper

Add to `BlockResponse`:
```csharp
public TransitResponse? TransitFromHotel { get; set; }
public TransitResponse? TransitToHotel { get; set; }
```

Add to `ActivityResponse`:
```csharp
public int EstimatedArrival { get; set; }   // minutes from midnight
public int EstimatedDeparture { get; set; } // minutes from midnight
```

Add new `TransitResponse` record (or reuse `TransitDetails` mapping).

Update `AutoMapperProfile`:
```csharp
CreateMap<TransitDetails, TransitResponse>();
CreateMap<ActivityNode, ActivityResponse>()
    .ForMember(dest => dest.EstimatedArrival, opt => opt.MapFrom(src => src.EstimatedArrival))
    .ForMember(dest => dest.EstimatedDeparture, opt => opt.MapFrom(src => src.EstimatedDeparture));
CreateMap<BlockTimeline, BlockResponse>()
    .ForMember(dest => dest.TransitFromHotel, opt => opt.MapFrom(src => src.TransitFromHotel))
    .ForMember(dest => dest.TransitToHotel, opt => opt.MapFrom(src => src.TransitToHotel));
```

---

## Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| **EF Core mapping break** | High if `BlockTimeline` table already migrated | `TransitFromHotel` / `TransitToHotel` are reference types (owned entities or nullable FKs). Verify existing migration handles owned `TransitDetails` or add new migration. |
| `BlockTotalDurationMinutes` semantic drift | Medium | Keep original property unchanged for capacity validation. Add new `BlockWallClockDurationMinutes` for display. Document in spec. |
| **Async in AutoMapper rejected** | Low | Approach A keeps all async `ITransitCalculator` calls in `TransitEnricher` (Domain layer). Mapping is synchronous. |
| **Test explosion** | Medium | Existing `TransitEnricherTests` need ~2 new test cases (hotel to first, last to hotel). `TimelineScheduler` needs ~3-4 focused tests. |
| **Line count exceeds 400** | Medium | Estimated total: ~220-250 lines across 6 files. Well within 400-line budget. |
| **MVP block start time assumption** | Low | Document that all blocks start at `DayPlan.StartTime`. Post-MVP can chain blocks sequentially. |

---

## Line Count Estimate

| File | Lines Changed | Nature |
|------|---------------|--------|
| `BlockTimeline.cs` | +12 | New properties |
| `TransitEnricher.cs` | +25 | Hotel transit logic |
| `TimelineScheduler.cs` | +45 | New class |
| `ITimelineScheduler.cs` | +6 | New interface |
| `HeuristicItineraryGenerator.cs` | +8 | Inject + call scheduler |
| `TripPlanResponse.cs` / DTOs | +20 | New fields |
| `AutoMapperProfile.cs` | +15 | New mappings |
| `TransitEnricherTests.cs` | +40 | Hotel transit tests |
| `TimelineSchedulerTests.cs` | +55 | New test class |
| `HeuristicItineraryGeneratorTests.cs` | +20 | Assert timeline fields |
| **TOTAL** | **~246 lines** | |

---

## Ready for Proposal

**Yes.** The exploration is complete and the recommendation is clear:

1. **Add hotel transit legs to `BlockTimeline`** (computed by `TransitEnricher`).
2. **Add `TimelineScheduler` as Phase 6** to compute `EstimatedArrival` / `EstimatedDeparture`.
3. **Extend response DTOs and AutoMapper** to expose the new data.
4. **Add focused unit tests** for hotel transit and timeline scheduling.

All changes fit within the ~250-line estimate, well under the 400-line review budget. No breaking changes to existing capacity logic. The orchestrator should proceed to `sdd-propose` with this exploration as backing context.
