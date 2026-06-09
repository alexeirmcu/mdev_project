# Design: Align Domain with Requirements

## Technical Approach
Refactor the domain core to use domain-native types: replace the API-layer `MustSeeInput` with a `SelectedAttraction` value object, swap `int` minutes for `TimeOnly` in `DayPlan`, add `Priority` to `ActivityNode`, and encapsulate completion state via `MarkAsCompleted()`. The solver adapter handles `TimeOnly` ↔ `int` conversion at the boundary.

## Architecture Decisions

| Decision | Choice | Alternatives | Rationale |
|----------|--------|-------------|-----------|
| SelectedAttraction type | Class extending ValueObject | record struct, simple DTO | Consistent with existing `ValueObject` base class; equality by Attraction ID via `GetEqualityComponents()` |
| Time in DayPlan | `TimeOnly` | `int` minutes, `DateTime` | .NET 8 TimeOnly is domain-native for wall-clock time; solver adapter converts to `int` minutes |
| Priority default | Constructor parameter | Post-construction setter | Constructor guarantees invariant — every `ActivityNode` always has a priority |
| Completion mutation | `MarkAsCompleted()` method | Public setter, auto-property | Encapsulates the state change and expresses business intent (one-way transition) |
| Trip must-see collection | `List<SelectedAttraction>` | `IReadOnlyList`, array | Needs `Add`/`Remove` mutations; `List<T>` is simplest |
| Default start time | `TimeOnly` (09:00) | Config file, DB setting | Domain constant referenced from `TripPlanningConstants`; can be promoted later |

## Data Flow

```
Trip
 ├── CityId, StartDate, EndDate, BaseHotel
 ├── DefaultStartTime: TimeOnly (09:00)
 ├── SelectedAttractions: List<SelectedAttraction>
 │    └── SelectedAttraction ──► Attraction (reference, equality by Id)
 └── Days: List<DayPlan>
      └── DayPlan
           ├── StartTime: TimeOnly (defaults 09:00)
           ├── BlockTimeline (Morning / Afternoon / Evening)
           │    └── ActivityNode[]
           │         ├── Priority: Priority (default Medium)
           │         └── IsCompleted: bool ──► MarkAsCompleted()
           └── ...
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `Domain/AggregatesModel/SelectedAttraction.cs` | Create | Value Object wrapping `Attraction`; validates non-null; equality by Attraction ID |
| `Domain/AggregatesModel/Trip.cs` | Modify | Replace `OriginalMustSees` with `SelectedAttractions`; add `AddSelectedAttraction()` / `RemoveSelectedAttraction()`; add `DefaultStartTime` |
| `Domain/AggregatesModel/DayPlan.cs` | Modify | Add `TimeOnly StartTime` defaulting to 09:00; add `UpdateStartTime()` |
| `Domain/AggregatesModel/ActivityNode.cs` | Modify | Add `Priority` property (default `Medium`); add `MarkAsCompleted()`; remove import of `MustSeeInput` |
| `Domain/ApiModels/MustSeeInput.cs` | Keep | Still needed by API input layer — no reference to domain types |

## Interfaces / Contracts

```csharp
// SelectedAttraction
public class SelectedAttraction : ValueObject
{
    public Attraction Attraction { get; }
    public SelectedAttraction(Attraction attraction);  // throws ArgumentNullException if null
    protected override IEnumerable<object> GetEqualityComponents();
}

// Trip
public class Trip : Entity, IAggregateRoot
{
    public List<SelectedAttraction> SelectedAttractions { get; }
    public TimeOnly DefaultStartTime { get; }
    public void AddSelectedAttraction(Attraction attraction);
    public bool RemoveSelectedAttraction(string placeId);
}

// DayPlan
public class DayPlan : Entity
{
    public TimeOnly StartTime { get; private set; }
    public void UpdateStartTime(TimeOnly newStart);
}

// ActivityNode
public class ActivityNode : Entity
{
    public Priority Priority { get; }
    public bool IsCompleted { get; private set; }
    public void MarkAsCompleted();
    public ActivityNode(/* existing params */, Priority priority = Priority.MEDIUM);
}
```

## Testing Strategy

| Layer | What | How |
|-------|------|-----|
| Domain Unit | `SelectedAttraction`: null guard, equality by Attraction ID, inequality for different IDs | MSTest |
| Domain Unit | `Trip`: add/remove SelectedAttraction, default start time is 09:00 | MSTest |
| Domain Unit | `DayPlan`: StartTime defaults to 09:00, `UpdateStartTime` updates the value | MSTest |
| Domain Unit | `ActivityNode`: Priority defaults to `Medium`, `MarkAsCompleted` flips `IsCompleted` | MSTest |

## Migration / Rollout
No data migration required. Domain-only change — existing API models remain at the boundary. Infrastructure adapter will convert `TimeOnly` to `int` minutes for the OR-Tools solver.

## Open Questions
None.
