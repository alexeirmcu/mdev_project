# Proposal: F2-2 — MaxWalkingMinutes efectivo

## Intent

`Trip.Preferences.MaxWalkingMinutes` exists (default 30, range 5–120) but `TransitEnricher.AssignTransitAsync` never reads it. A user who sets `maxWalkingMinutes: 15` still gets `WALK_AND_PUBLIC_TRANSPORT` even when the walk between activities takes 30 min. This makes the preference meaningless and hurts UX for families with small children or users with limited mobility.

## Scope

### In Scope
- Modify `TransitEnricher.AssignTransitAsync` to check walking duration against `MaxWalkingMinutes`
- Walking duration = distance / `TripPlanningConstants.WalkingSpeedKmh` (5 km/h)
- If walking duration > `MaxWalkingMinutes` AND `CarAvailable` → switch mode to `CAR`
- If walking duration > `MaxWalkingMinutes` AND `!CarAvailable` → set `FrictionAlert = true`
- Update existing tests; add new test scenarios

### Out of Scope
- No API, handler, controller, or DB changes
- No new properties or endpoints
- `MaxWalkingMinutes` validation already exists (5–120 in `GenerateTripValidator`)
- No TransportMode changes to the enum itself

## Capabilities

### New Capabilities
None — this modifies existing transport mode assignment behavior.

### Modified Capabilities
- `itinerary-generation` (FR6): Transport mode assignment MUST check `MaxWalkingMinutes` before defaulting to `WALK_AND_PUBLIC_TRANSPORT`

## Approach

1. In `AssignTransitAsync`, after computing `distanceKm`, calculate `estimatedWalkingMinutes = (distanceKm / WalkingSpeedKmh) * 60 * WalkingPortionFactor`
   - `WalkingSpeedKmh` = 5 km/h (existing constant)
   - `WalkingPortionFactor` = 0.3 — since `WALK_AND_PUBLIC_TRANSPORT` includes PT, only ~30% of the distance is actual walking (to/from stations)
2. When mode resolves to `WALK_AND_PUBLIC_TRANSPORT`, check `estimatedWalkingMinutes > preferences.MaxWalkingMinutes`
3. If exceeded:
   - **If `CarAvailable`**: switch mode to `CAR`, re-estimate via `_transitCalculator`
   - **If `!CarAvailable`**: keep `WALK_AND_PUBLIC_TRANSPORT` but force `frictionAlert = true` in `TransitDetails`
4. Apply via `AssignTransitAsync` — shared by both `EnrichAsync` and `EnrichScopedAsync`

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `SmartTripPlanner.Domain/Services/TransitEnricher.cs` | Modified | Add MaxWalkingMinutes check in `AssignTransitAsync` |
| `tests/.../TransitEnricherTests.cs` | Modified | Add scenarios for exceed/no-exceed, car/no-car |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Walking-only calculation may not match actual PT walking portion | Low | Haversine distance is a reasonable proxy; frictionAlert catches misestimation |

## Rollback Plan

Revert the single method `AssignTransitAsync` in `TransitEnricher.cs` and revert test additions. No migration needed — this is purely behavioral, no new data.

## Dependencies

None. Pure domain service change.

## Success Criteria

- [x] `AssignTransitAsync` switches to `CAR` when walking exceeds `MaxWalkingMinutes` and car is available
- [x] `AssignTransitAsync` sets `FrictionAlert = true` when walking exceeds `MaxWalkingMinutes` and car is NOT available
- [x] Walking below `MaxWalkingMinutes` keeps existing behavior unchanged
- [x] All existing 172+ tests continue to pass
- [ ] Backlog item F2-2 is verifiably closed
