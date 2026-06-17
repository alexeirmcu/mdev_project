# Proposal: Flow 2 — Itinerary Generation (Heuristic MVP)

## Intent

Implement the PRD v1 heuristic itinerary generator for multi-day family trips. The existing `doc/spec/spec-trip-generation-flow-2.md` describes a Google OR-Tools VRP solver that is over-engineered for MVP and incomplete (ends at AC1). This change replaces that path with the PRD-mandated 5-step heuristic planner and defers OR-Tools to post-MVP optimization.

## Scope

### In Scope
- Domain service `IItineraryGenerator` with heuristic implementation
- Block-based daily planning (Morning / Afternoon / Evening)
- Must-see placement with priority and optional pinned day
- Zone clustering to reduce backtracking
- Candidate scoring and slot filling
- Transport mode selection (car vs public transit + walk)
- Weather-based indoor/outdoor filtering
- Block capacity validation and overflow trimming
- `DayPlan` and `BlockTimeline` population via existing domain model

### Out of Scope
- Google OR-Tools VRP solver (deferred to post-MVP)
- Real-time routing API integration (uses estimated transit)
- Exact per-visit start times
- Multi-city / hotel-switch trips
- Automatic replanning engine (UI trigger only, no solver)

## Capabilities

### New Capabilities
- `itinerary-generation`: Heuristic multi-day itinerary builder with block scheduling, zone clustering, transport scoring, and weather filtering.

### Modified Capabilities
- None

## Approach

Introduce `IItineraryGenerator` as a Domain Service port. The heuristic implementation lives in `SmartTripPlanner.Domain/Services/HeuristicItineraryGenerator.cs` and follows the PRD v1 algorithm:

1. Place must-sees first (respect opening hours and pinned day).
2. Group placed activities by zone/barrio to minimize backtracking.
3. Fill remaining slots with scored candidates (family-friendly bonus, popularity, transit penalty).
4. Insert family buffers between visits.
5. Validate block capacity (~3 visits per block); trim lowest-value items on overflow.

Transport selection uses pragmatic rules: default to public transit + walking, switch to car when significantly faster or walking is excessive. Weather filtering adjusts candidate scoring per block when rain is expected.

The existing domain model (`Trip.GenerateDays`, `BlockTimeline`, `ActivityNode`, `TransitDetails`, `MustSee`, `Place`) already supports ~80% of this flow. The generator will hydrate `DayPlan` collections directly.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Domain/Services/IItineraryGenerator.cs` | New | Port interface for itinerary generation |
| `Domain/Services/HeuristicItineraryGenerator.cs` | New | Heuristic implementation |
| `Domain/ValueObjects/TransitDetails.cs` | Modified | May need scoring helpers |
| `Application/Handlers/GenerateTripHandler.cs` | Modified | Orchestrates generator and persists result |
| `tests/.../ItineraryGeneratorTests.cs` | New | Unit tests for heuristic logic |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Transit times are inaccurate without real routing API | Med | Use buffered estimates; document as known MVP limitation |
| Zone clustering produces poor groupings | Med | Start with simple distance threshold; iterate with user feedback |
| Candidate place source is not yet built | High | Generate from Place repository + Foursquare fallback; add hardcoded Madrid seed data if needed |
| Opening hours edge cases cause empty blocks | Low | Validate must-see feasibility before generation; surface clear error messages |

## Rollback Plan

Remove `HeuristicItineraryGenerator` and revert `GenerateTripHandler` to its pre-integration state. The existing `Trip.GenerateDays()` creates empty `DayPlan` lists, which is the safe fallback.

## Dependencies

- Place search / repository (already implemented)
- Weather service port (can be stubbed with simple rain flag for MVP)

## Success Criteria

- [ ] Itinerary generates N days with 3 blocks each containing visits and transit segments
- [ ] Must-sees with `HIGH` priority are included unless physically impossible
- [ ] Transport mode is selected per segment with pragmatic rules
- [ ] Rainy days prefer indoor activities
- [ ] Block capacity does not exceed ~3 visits; overflow items are trimmed
- [ ] All existing 172+ tests continue passing
- [ ] OR-Tools spec is explicitly documented as deferred, not deleted
