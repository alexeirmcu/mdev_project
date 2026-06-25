# Apply Progress: F2-2 — MaxWalkingMinutes

## Completed Tasks

- [x] **Modify `AssignTransitAsync`** — added MaxWalkingMinutes guard after mode resolution
  - Calculates `estimatedWalkingMinutes = (distanceKm / 5.0) * 60 * 0.3`
  - If exceeding `MaxWalkingMinutes` AND `CarAvailable`: switches to `CAR`
  - If exceeding `MaxWalkingMinutes` AND `!CarAvailable`: keeps `WALK_AND_PUBLIC_TRANSPORT` with `FrictionAlert = true`
- [x] **Add test: Walking below MaxWalkingMinutes** — verifies no behavioral change when walking is within limit
- [x] **Add test: Walking exceeds + car available** — verifies mode switches to CAR
- [x] **Add test: Walking exceeds + no car** — verifies FrictionAlert = true
- [x] **Add test: Walking below default MaxWalkingMinutes** — verifies default (30) does not trigger guard for ~5km trips

## Files Changed

| File | Action | What Was Done |
|------|--------|---------------|
| `SmartTripPlanner.Domain/Services/TransitEnricher.cs` | Modified | Added MaxWalkingMinutes guard in `AssignTransitAsync` |
| `tests/SmartTripPlanner.Tests/SmartTripPlanner.Domain/Services/TransitEnricherTests.cs` | Modified | Added 4 new test scenarios |

## Test Results

- **Total**: 20 passed, 0 failed, 0 skipped
- **New tests**: 4 (all passing)
- **Existing tests**: 16 (all passing, no regressions)

## Deviations from Design

None — implementation matches the proposal exactly.

## Status

4/4 tasks complete. Ready for verify.
