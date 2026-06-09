# Proposal: Align Domain with Requirements

## Intent
Align the core domain model with business requirements for trip planning. This addresses the need for precise time representation (using .NET 8 `TimeOnly`), a unified value object for selected attractions (`SelectedAttraction`), and a mechanism to prioritize filler activities versus user-defined must-sees.

## Scope

### In Scope
- Creation of `SelectedAttraction` value object in the domain.
- Migration of `Trip.OriginalMustSees` from `MustSeeInput` to `SelectedAttraction`.
- Implementation of `TimeOnly StartTime` in `DayPlan` (default 09:00).
- Introduction of `Priority` enum/property in `ActivityNode` (default `Priority.Medium` for fillers).
- Domain-level logic for dynamic preference mutation in `Trip`.

### Out of Scope
- Implementation of the OR-Tools solver logic (handled by infrastructure adapters).
- Versioned history of user preference changes.
- UI changes for time selection.

## Capabilities

### New Capabilities
- `activity-prioritization`: Allows the system to distinguish between must-see attractions and suggested filler activities via a priority level.

### Modified Capabilities
- `trip-planning`: Updated to support `TimeOnly` for day start times and `SelectedAttraction` for must-see tracking.

## Approach
1. **Domain Refactoring**: 
   - Introduce `SelectedAttraction` as a Value Object to encapsulate attraction selection data.
   - Update `Trip` to use `SelectedAttraction` and implement logic to update must-sees on preference changes.
   - Update `DayPlan` to use `TimeOnly` for `StartTime`, ensuring the domain doesn't leak infrastructure-specific (integer minutes) representations.
   - Add `Priority` to `ActivityNode` to allow the solver to prioritize activities.
2. **Infrastructure Adapter**: The infrastructure layer will be responsible for converting `TimeOnly` to the integer minutes required by OR-Tools and vice versa.
3. **Verification**: Use Strict TDD. Create domain tests for `Trip` and `DayPlan` before implementing the changes.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `SmartTripPlanner.Domain/AggregatesModel/SelectedAttraction.cs` | New | Value object for selected attractions. |
| `SmartTripPlanner.Domain/AggregatesModel/Trip.cs` | Modified | Uses `SelectedAttraction` instead of `MustSeeInput`. |
| `SmartTripPlanner.Domain/AggregatesModel/DayPlan.cs` | Modified | Adds `TimeOnly StartTime`. |
| `SmartTripPlanner.Domain/AggregatesModel/ActivityNode.cs` | Modified | Adds `Priority` property. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Breaking changes in Solver Adapter | Medium | Thoroughly test the conversion logic between `TimeOnly` and integer minutes. |
| Domain logic complexity in `Trip` | Low | Keep preference mutation simple and direct. |

## Rollback Plan
Revert the domain model to use `MustSeeInput` and `int` (minutes) for time. Restore `ActivityNode` to its previous state without the `Priority` property.

## Dependencies
- .NET 8 (for `TimeOnly` support).

## Success Criteria
- [ ] `Trip` correctly manages a list of `SelectedAttraction`.
- [ ] `DayPlan` defaults to 09:00 `StartTime` and allows modification.
- [ ] `ActivityNode` correctly assigns `Priority.Medium` to filler activities.
- [ ] Domain tests pass without relying on infrastructure.
