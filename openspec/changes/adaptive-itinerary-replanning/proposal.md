# Proposal: Adaptive Itinerary Replanning

## Intent

Enable travelers to adapt their itinerary mid-trip when weather changes or plans shift. Provide explicit controls to refresh weather, regenerate single days, and smart-replan from any point forward — while preserving completed activities and must-see commitments.

## Scope

### In Scope
- Weather refresh endpoint with stale-day detection
- Single day regeneration with completed/must-see preservation
- Smart replan from current block/day/remaining trip driven by request scope
- Checklist API to toggle `IsCompleted`
- `ForceIncludeDespiteWeather` flag on `MustSee`
- `DayPlan.IsStale` persistence + 2 EF migrations

### Out of Scope
- Automatic background replanning
- Real-time routing API integration
- Multi-city / hotel-switch replanning
- Budget or cost optimization

## Capabilities

### New
- `weather-refresh`: Re-fetch forecast, diff against stored summaries, mark stale days
- `day-regeneration`: Regenerate one day preserving completed activities and must-sees
- `smart-replan`: Replan from current point forward with weather-aware swaps and transit recalculation
- `checklist-api`: Toggle `IsCompleted` on an ActivityNode

### Modified
- `itinerary-generation`: New `IItineraryReplanningEngine` domain service; `CandidateScorer` / `CandidateFiller` respect `ForceIncludeDespiteWeather`; generator phases support partial execution

## Approach

Create `IItineraryReplanningEngine` as a domain service that operates on existing `Trip.Days` — never clearing all days. It locks completed activities, preserves must-sees (pinned and unpinned), and re-runs CandidateFiller + TransitEnricher + TimelineScheduler for affected blocks only.

**Key Decisions**
- Partial regeneration via new domain service instead of mutating `HeuristicItineraryGenerator`
- Replan scope driven by request enum (`CurrentBlock`, `CurrentDay`, `RemainingTrip`)
- `DayPlan.IsStale` persisted in DB for trip status queries
- Weather penalty skipped for outdoor must-sees when `ForceIncludeDespiteWeather = true`

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Domain/Services/IItineraryReplanningEngine.cs` | New | Partial replanning domain service |
| `ApplicationServices/Commands/` | New | 4 new handlers: RefreshWeather, RegenerateDay, SmartReplan, ToggleComplete |
| `API/Controllers/TripsController.cs` | Modified | 4 new endpoints |
| `Domain/ValueObjects/MustSee.cs` | Modified | Add `ForceIncludeDespiteWeather` |
| `Infrastructure/Data/Migrations/` | New | 2 migrations: MustSee flag + DayPlan.IsStale |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Must-see placement duplication on partial regen | Med | Lock completed activities; deduplicate by PlaceId before fill |
| Weather API rate limits on refresh | Med | Debounce refresh calls; no auto-refresh |
| Migration conflicts with existing DayPlan data | Low | Backward-compatible `IsStale` default false |
| Complexity of scoped replanning | Med | Extensive handler + domain tests (~30-40 new) |

## Rollback Plan

Revert both EF migrations. Remove new controller endpoints. `HeuristicItineraryGenerator` remains untouched and can regenerate full trips on demand.

## Dependencies

- `OpenMeteoWeatherProvider` (real, no caching)
- Existing `CandidateScorer`, `TransitEnricher`, `TimelineScheduler` phase classes

## Success Criteria

- [ ] All 4 endpoints return correct responses per spec
- [ ] Completed activities never moved or deleted by any replan operation
- [ ] `IsStale` correctly set on days with significant weather changes
- [ ] Outdoor must-sees with `ForceIncludeDespiteWeather=true` score normally in bad weather
- [ ] 507+ existing tests pass; ~30-40 new tests added and green
