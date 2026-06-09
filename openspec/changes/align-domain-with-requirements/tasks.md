# Tasks: Align Domain with Requirements

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | < 100 |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | single PR |
| Delivery strategy | ask-on-risk |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: Low

## Phase 1: SelectedAttraction Value Object

- [ ] 1.1 Create `SmartTripPlanner.Domain/AggregatesModel/SelectedAttraction.cs` extending `ValueObject`.
- [ ] 1.2 Implement constructor that accepts an `Attraction` and throws `ArgumentNullException` if null.
- [ ] 1.3 Implement `GetEqualityComponents()` using the `Attraction`'s identity for equality.

## Phase 2: Trip Updates

- [ ] 2.1 Replace `IReadOnlyList<MustSeeInput> OriginalMustSees` with `List<SelectedAttraction> SelectedAttractions`.
- [ ] 2.2 Add `AddSelectedAttraction(Attraction attraction)` method.
- [ ] 2.3 Add `RemoveSelectedAttraction(string placeId)` method.
- [ ] 2.4 Add `TimeOnly DefaultStartTime` property defaulting to 09:00 (use `TripPlanningConstants.DefaultStartHourMinutes`).

## Phase 3: DayPlan Updates

- [ ] 3.1 Add `TimeOnly StartTime` property with private setter, default 09:00.
- [ ] 3.2 Add `UpdateStartTime(TimeOnly newStart)` method.

## Phase 4: ActivityNode Updates

- [ ] 4.1 Add `Priority Priority` property with default `Priority.MEDIUM`.
- [ ] 4.2 Replace public `IsCompleted` setter with `MarkAsCompleted()` method.

## Phase 5: Domain Tests

- [ ] 5.1 Tests for `SelectedAttraction`: null guard, equality, inequality.
- [ ] 5.2 Tests for `Trip`: add/remove `SelectedAttraction`, default start time.
- [ ] 5.3 Tests for `DayPlan`: default start time, `UpdateStartTime`.
- [ ] 5.4 Tests for `ActivityNode`: default priority, `MarkAsCompleted`.
