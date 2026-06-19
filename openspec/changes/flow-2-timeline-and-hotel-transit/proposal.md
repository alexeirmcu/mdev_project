# Proposal: flow-2-timeline-and-hotel-transit

## Intent

Fill two explicit gaps documented in the Flow-2 technical spec (§7.1, §7.4):

1. **Timeline Scheduling**: `ActivityNode.EstimatedArrival` and `EstimatedDeparture` exist as properties but are never populated. The response currently shows sequence order and duration, but no wall-clock times.
2. **Hotel Transit Legs**: `TransitDetails` only connects activity → activity within a block. Clients cannot display "leave hotel at 09:00, arrive at Prado at 09:15" because hotel→first and last→hotel transits are missing.

This change makes the generated itinerary human-readable with exact minute-level timings and complete transit coverage.

## Scope

### In Scope
- Compute `Hotel → FirstActivity` and `LastActivity → Hotel` transit per block and store on `BlockTimeline`.
- Compute `EstimatedArrival` / `EstimatedDeparture` for every `ActivityNode` by walking each block's schedule.
- Expose hotel transit and exact times in `TripPlanResponse` (`BlockResponse`, `ActivityResponse`).
- Update `AutoMapperProfile` mappings.
- Add unit tests for hotel transit assignment and timeline scheduling.

### Out of Scope
- Chaining block start times sequentially (Afternoon starting after Morning ends). MVP treats each block as independently starting at `DayPlan.StartTime`.
- Changing `BlockTotalDurationMinutes` semantics (kept unchanged for capacity validation).
- Real routing API integration (continues using Haversine heuristic).
- OR-Tools VRP solver.

## Capabilities

### New Capabilities
- `timeline-scheduling`: Synchronous computation of `EstimatedArrival` / `EstimatedDeparture` per activity based on block start time, hotel transit, inter-activity transit, and buffers.

### Modified Capabilities
- `itinerary-generation`: Adds hotel transit leg computation to the generation flow (new Phase 6 timeline scheduling; extension of Phase 5 transit enrichment).

## Approach

**Adopt Approach A (Hybrid)** from exploration:

1. **Extend `TransitEnricher`** to calculate `TransitFromHotel` and `TransitToHotel` for each non-empty `BlockTimeline` using the existing `AssignTransitAsync` helper and `Trip.BaseHotel`.
2. **Create `ITimelineScheduler` / `TimelineScheduler`** as a new Phase 6 in `HeuristicItineraryGenerator`. It runs after `TransitEnricher` and performs pure synchronous math: walk each block's activities, advance a `currentTime` cursor by transit duration + buffer + activity duration, and write `EstimatedArrival` / `EstimatedDeparture`.
3. **Update Response DTOs & AutoMapper** to expose the new fields.
4. **Add focused unit tests** for the new scheduler and enriched hotel transit assertions.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `BlockTimeline.cs` | Modified | Add `TransitFromHotel`, `TransitToHotel`, `BlockWallClockDurationMinutes` |
| `TransitEnricher.cs` | Modified | Compute hotel transit legs after inter-activity transit assignment |
| `ITransitEnricher.cs` | Unchanged | Signature sufficient; no breaking change |
| `TimelineScheduler.cs` | New | Phase 6: synchronous time-math scheduler |
| `ITimelineScheduler.cs` | New | Domain port |
| `HeuristicItineraryGenerator.cs` | Modified | Inject `ITimelineScheduler`; call `Schedule(trip)` after Phase 5 |
| `TripPlanResponse.cs` / DTOs | Modified | Add `TransitResponse? TransitFromHotel/ToHotel` on `BlockResponse`; add `EstimatedArrival/Departure` on `ActivityResponse` |
| `AutoMapperProfile.cs` | Modified | Map new transit and time fields |
| `TransitEnricherTests.cs` | Modified | Assert hotel transit is populated |
| `TimelineSchedulerTests.cs` | New | Unit tests for scheduling logic |
| `HeuristicItineraryGeneratorTests.cs` | Modified | Assert timeline fields are set |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| EF Core owned-entity mapping break on `BlockTimeline` | Low | `TransitDetails` is already an owned entity pattern; adding two more owned references is consistent with existing mapping. Verify with migration test. |
| `BlockTotalDurationMinutes` semantic drift | Low | Keep original property unchanged. Add separate `BlockWallClockDurationMinutes` for display. Document in code comments. |
| Test explosion / regression | Low | Estimated ~246 changed lines across 10 files, well under 400-line budget. All 295 existing tests must pass. |
| MVP block-start assumption misleads UX | Low | Code comment documents that all blocks start at `DayPlan.StartTime`; post-MVP can chain sequentially. |

## Rollback Plan

1. Revert the PR — all changes are additive (new properties, new interface/class, new mappings). No existing properties are removed or retyped.
2. If `BlockTimeline` EF Core mapping causes migration issues, mark new `TransitFromHotel` / `TransitToHotel` as `[NotMapped]` and compute them transiently in the response mapping as a temporary fallback.
3. `HeuristicItineraryGenerator` can tolerate `ITimelineScheduler` absence by commenting out the Phase 6 call; the remaining 5 phases are unaffected.

## Dependencies

- None external. Relies on existing `ITransitCalculator`, `Trip.BaseHotel`, and `DayPlan.StartTime`.

## Success Criteria

- [ ] `BlockTimeline.TransitFromHotel` and `TransitToHotel` are populated for every non-empty block when `Trip.BaseHotel` is present.
- [ ] `ActivityNode.EstimatedArrival` and `EstimatedDeparture` are set for every activity in every block.
- [ ] `TripPlanResponse` exposes hotel transit legs and exact arrival/departure times.
- [ ] All 295 existing tests pass; new tests cover hotel transit and timeline scheduling.
- [ ] Review workload stays under 400 changed lines (estimated ~246).
