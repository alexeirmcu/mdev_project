# Design: Flow 2 — Timeline Scheduling and Hotel Transit

## Technical Approach

Extend the existing 5-phase itinerary generator with two coordinated changes:

1. **Phase 5 extension** — `TransitEnricher` now also computes `TransitFromHotel` and `TransitToHotel` per non-empty block using `Trip.BaseHotel.Location`, storing them on `BlockTimeline`.
2. **Phase 6 (new)** — `TimelineScheduler` walks each block's activities after all transit is assigned, computing `EstimatedArrival` / `EstimatedDeparture` as pure synchronous math starting from `DayPlan.StartTime`.

Response DTOs and AutoMapper mappings expose the new fields. No changes to the Application handler layer or the generator's public interface.

## Architecture Decisions

### Decision: Hotel transit lives on BlockTimeline, not ActivityNode

| Option | Tradeoff | Decision |
|--------|----------|----------|
| `BlockTimeline.TransitFromHotel/ToHotel` | Natural ownership (one per block), clean model, requires EF Core owned-entity update | ✅ Chosen |
| `ActivityNode.TransitFromHotel/ToHotel` on first/last | Adjacent to activity, but conceptually awkward ("from hotel" is not an activity transit) | ❌ Rejected |
| Compute on-the-fly during mapping | Zero model changes, but violates Clean Architecture (domain logic leaks to API layer) | ❌ Rejected |

**Rationale**: Hotel transit is block-level, not activity-level. Two nullable `TransitDetails?` per block is structurally clear and keeps `ActivityNode` unchanged.

### Decision: TimelineScheduler is a separate synchronous phase, not merged into TransitEnricher

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Separate `ITimelineScheduler` / `TimelineScheduler` class | SRP, testable independently, no async dependencies | ✅ Chosen |
| Inline into `TransitEnricher` | Fewer files, but violates SRP and mixes async I/O with pure math | ❌ Rejected |

**Rationale**: Timeline scheduling is deterministic math with zero I/O. Separating it makes testing trivial and keeps `TransitEnricher` focused on transit computation.

### Decision: Arrival/departure format in API — int (minutes-from-midnight)

| Option | Tradeoff | Decision |
|--------|----------|----------|
| `int` minutes-from-midnight | Simple, no culture/format issues, client formats | ✅ Chosen |
| `string` "HH:mm" | UX-friendly, but domain leakage + culture risk | ❌ Rejected |

**Rationale**: Domain stores minutes-from-midnight. API returns raw ints; clients format. Post-MVP can add a formatted string alongside.

### Decision: Each block starts at DayPlan.StartTime (MVP)

| Option | Tradeoff | Decision |
|--------|----------|----------|
| All blocks start at `DayPlan.StartTime` | Simple, no implicit block-chaining, documented MVP limitation | ✅ Chosen |
| Afternoon starts after Morning ends | More realistic, but couples blocks and requires cross-block state | ❌ Deferred |

**Rationale**: Documented as MVP scope. Code comment explains the decision. Post-MVP can chain blocks sequentially.

### Decision: BlockTotalDurationMinutes unchanged; BlockWallClockDurationMinutes added

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Keep existing property, add computed property | Zero breaking change, capacity logic intact | ✅ Chosen |
| Modify existing property | Breaks capacity validation, regression risk | ❌ Rejected |

**Rationale**: `BlockTotalDurationMinutes` is used in `CanFitActivity()` and `AddActivity()`. Changing its semantics breaks capacity checks.

## Data Flow

```
HeuristicItineraryGenerator.GenerateAsync()
  │
  ├─ Phases 1–4: unchanged (Place activities into blocks)
  │
  ├─ Phase 5: TransitEnricher.EnrichAsync()
  │    ├─ For each block, for each activity pair [i]→[i+1]:
  │    │    AssignTransitAsync(from.Location, to.Location) → ActivityNode.TransitToNext
  │    │
  │    └─ NEW: For each non-empty block where Trip.BaseHotel ≠ null:
  │         AssignTransitAsync(hotel → first.Location) → BlockTimeline.TransitFromHotel
  │         AssignTransitAsync(last.Location → hotel)    → BlockTimeline.TransitToHotel
  │
  ├─ Phase 6 (NEW): TimelineScheduler.Schedule()
  │    └─ For each day, for each block type:
  │         currentTime = day.StartTime.Hour*60 + day.StartTime.Minute
  │         currentTime += block.TransitFromHotel?.DurationMinutes ?? 0
  │                            + block.TransitFromHotel?.BufferMinutes ?? 0
  │         For each activity:
  │           activity.EstimatedArrival   = currentTime
  │           activity.EstimatedDeparture = currentTime + activity.DurationMinutes
  │           currentTime = activity.EstimatedDeparture
  │                        + activity.TransitToNext?.DurationMinutes ?? 0
  │                        + activity.TransitToNext?.BufferMinutes ?? 0
  │         // Reset currentTime to DayPlan.StartTime for next block (MVP)
  │
  └─ Fallback chain: unchanged
```

Mapping path (response):

```
Trip → TripPlanResponse (AutoMapper)
  ├─ DayPlan → DayPlanResponse
  │    └─ BlockTimeline → BlockResponse
  │         ├─ .TransitFromHotel → TransitResponse?      ← NEW
  │         ├─ .TransitToHotel   → TransitResponse?      ← NEW
  │         └─ Activities → ActivityResponse[]
  │              ├─ .EstimatedArrival   → int             ← NEW
  │              └─ .EstimatedDeparture → int             ← NEW
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `SmartTripPlanner.Domain/AggregatesModel/BlockTimeline.cs` | Modify | Add `TransitFromHotel`, `TransitToHotel` (`TransitDetails?`), `BlockWallClockDurationMinutes` computed property |
| `SmartTripPlanner.Domain/Ports/ITimelineScheduler.cs` | Create | Single method `void Schedule(Trip trip)` |
| `SmartTripPlanner.Domain/Services/TimelineScheduler.cs` | Create | Pure synchronous implementation |
| `SmartTripPlanner.Domain/Services/TransitEnricher.cs` | Modify | Add hotel-transit computation per block after inter-activity loop |
| `SmartTripPlanner.Domain/Services/HeuristicItineraryGenerator.cs` | Modify | Inject `ITimelineScheduler`, call `Schedule(trip)` after Phase 5 |
| `SmartTripPlanner.ApplicationServices/ApplicationServicesRegistration.cs` | Modify | Register `ITimelineScheduler → TimelineScheduler` |
| `SmartTripPlanner.Domain/ApiModels/TripPlanResponse.cs` | Modify | Add `TransitFromHotel/ToHotel` on `BlockResponse`, `EstimatedArrival/Departure` on `ActivityResponse`, new `TransitResponse` record |
| `SmartTripPlanner.API/Configurations/AutoMapperProfile.cs` | Modify | Add mappings for new response fields |
| `SmartTripPlanner.Infrastructure/Configurations/TripConfiguration.cs` | Modify | Add `OwnsOne` for `TransitFromHotel` and `TransitToHotel` on each block owned-entity nesting |
| `tests/.../TransitEnricherTests.cs` | Modify | Add hotel-transit test cases |
| `tests/.../TimelineSchedulerTests.cs` | Create | Unit tests for scheduling logic |
| `tests/.../HeuristicItineraryGeneratorTests.cs` | Modify | Assert `EstimatedArrival`/`EstimatedDeparture` set; add `ITimelineScheduler` mock/instance |

## Interfaces / Contracts

### ITimelineScheduler (new)

```csharp
namespace SmartTripPlanner.Domain.Ports;

public interface ITimelineScheduler
{
    void Schedule(Trip trip);
}
```

### BlockTimeline additions

```csharp
// Existing BlockTimeline.cs — additions only:
public TransitDetails? TransitFromHotel { get; set; }
public TransitDetails? TransitToHotel { get; set; }

public int BlockWallClockDurationMinutes =>
    (TransitFromHotel?.DurationMinutes ?? 0) +
    BlockTotalDurationMinutes +
    (TransitToHotel?.DurationMinutes ?? 0);
```

### TransitResponse (new DTO)

```csharp
public record TransitResponse(
    string TransportMode,
    int DurationMinutes,
    int BufferMinutes,
    bool FrictionAlert);
```

### ActivityResponse additions

```csharp
public int? EstimatedArrival { get; set; }   // minutes from midnight, null if unset
public int? EstimatedDeparture { get; set; }  // minutes from midnight, null if unset
```

### BlockResponse additions

```csharp
public TransitResponse? TransitFromHotel { get; set; }
public TransitResponse? TransitToHotel { get; set; }
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | `TimelineScheduler.Schedule()` — happy path, empty block, single activity, multiple with transit | Create `Trip` with pre-populated `DayPlan` blocks (activities + transit), assert arrival/departure values |
| Unit | `TimelineScheduler` — block without hotel transit starts at `DayPlan.StartTime` directly | Trip with `BaseHotel = null`, verify first activity arrival = startTime |
| Unit | `TransitEnricher` — hotel legs computed when `BaseHotel` present | Mock `ITransitCalculator`, assert `block.TransitFromHotel` and `TransitToHotel` non-null |
| Unit | `TransitEnricher` — hotel legs null when `BaseHotel` null or block empty | Verify nulls, verify existing inter-activity tests still pass |
| Integration | `HeuristicItineraryGenerator` end-to-end with Phase 6 | Add `TimelineScheduler` to constructor, generate itinerary, assert all `EstimatedArrival` / `EstimatedDeparture` populated |
| Integration | All 295+ existing tests pass unchanged | Regression suite run — no existing test modifications needed |
| Controller | `TripsController` response includes hotel transit and time fields | API test with seeded trip, assert JSON structure |

## Migration / Rollout

**EF Core migration required.** `BlockTimeline.TransitFromHotel` and `TransitToHotel` are owned entities (value objects) of type `TransitDetails`. The existing `TripConfiguration.cs` already uses `OwnsOne` for `TransitToNext` on each activity block — the same pattern applies for the two new owned references. A new migration adds nullable columns for the hotel transit legs within the owned `BlockTimeline` tables.

**Additive change.** All new properties are nullable (`TransitDetails?`, `int?`). No existing columns change. Rollback = revert the PR; no data loss.

**DI registration**: Add `services.AddScoped<ITimelineScheduler, TimelineScheduler>()` to `ApplicationServicesRegistration.cs`.

## Open Questions

- [x] ~~Format of arrival/departure in API: int vs formatted string~~ → **Decision: int (minutes-from-midnight). Clients format.**
- [x] ~~Whether hotel transit duration in block total~~ → **Decision: `BlockTotalDurationMinutes` unchanged; separate `BlockWallClockDurationMinutes` added. Added to design.**
- [ ] EF Core owned-entity mapping: `TransitDetails` on `BlockTimeline` needs `OwnsOne` in `TripConfiguration.cs`. The existing pattern uses `OwnsOne(ac => ac.TransitToNext)` inside `OwnsMany(b => b.Activities, ...)`. Hotel transit needs `OwnsOne(b => b.TransitFromHotel)` and `OwnsOne(b => b.TransitToHotel)` at the block level, inside the `OwnsOne(d => d.Morning/Afternoon/Evening, ...)` nesting. This should work but needs verification at implementation time.