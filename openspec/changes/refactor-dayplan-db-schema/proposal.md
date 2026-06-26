# Proposal: Refactor DayPlan Database Schema

## Intent

Eliminate schema bloat from flattened OwnsOne mappings and 3 identical activity tables. DayPlan drops from ~50 to ~8 columns; 6 itinerary tables consolidate to 3. Pure persistence refactor — no domain behavior changes.

## Scope

### In Scope
- Collapse 3 BlockTimeline properties into `IReadOnlyList<BlockTimeline> Blocks`
- Map BlockTimeline as independent table (FK to DayPlan) instead of OwnsOne
- Consolidate 3 identical activity tables into single `Activities` table (FK to BlockTimeline)
- Squash 13 migrations into fresh `InitialCreate`
- Update all handlers/services from `dayPlan.Morning/Afternoon/Evening` → `dayPlan.GetBlock(BlockType.X)`

### Out of Scope
- BlockTimeline domain model changes (no structural change, only persistence mapping)
- ActivityNode or PlaceLocation changes (stays embedded)
- Cross-aggregate references (Trip remains sole aggregate root)
- OR-Tools or optimization engine changes

## Capabilities

### New Capabilities
- None — pure persistence refactor, no new spec-level behavior

### Modified Capabilities
- `itinerary-generation`: DayPlan exposes `IReadOnlyList<BlockTimeline> Blocks` + public `GetBlock(BlockType)` instead of 3 individual properties. Spec scenarios referencing `DayPlan[1].Afternoon` → `DayPlan[1].GetBlock(BlockType.Afternoon)`. All FRs functionally unchanged.

## Approach

1. Update `DayPlan.cs` — replace 3 properties with collection, make `GetBlock()` public
2. Rewrite EF config in `TripConfiguration.cs` — `HasMany(...).WithOne()` for BlockTimelines, single table for Activities
3. Update `TripRepository.cs` — add `ThenInclude(b => b.BlockTimelines)` and `ThenInclude(bt => bt.Activities)`
4. Audit all `.Morning`, `.Afternoon`, `.Evening` references in solution → replace with `GetBlock(BlockType.X)`
5. Update AutoMapper profiles if any direct property mapping exists
6. Delete all 13 migrations, create `InitialCreate`
7. Run full test suite — all 172+ must pass with zero behavioral change

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Domain/Models/DayPlan.cs` | Modified | 3 props → collection, GetBlock public |
| `Infrastructure/TripConfiguration.cs` | Modified | Full mapping rewrite (lines 79-147) |
| `Infrastructure/Persistence/TripRepository.cs` | Modified | Add ThenInclude for BlockTimelines + Activities |
| `Application/Handlers/ToggleActivityCompletionHandler.cs` | Modified | Switch to GetBlock |
| `Application/Handlers/GenerateTripItineraryHandler.cs` | Modified | Switch to GetBlock |
| `Application/Services/UnpinnedMustSeePlacer.cs` | Modified | Switch to GetBlock |
| `Domain/Helpers/ItineraryGeneratorHelpers.cs` | Modified | Switch to GetBlock |
| `Application/Mapping/AutoMapperProfile.cs` | Modified | Review DayPlan mapping |
| Tests (4+ files) | Modified | Update to new DayPlan API |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Missed direct property access in handlers | Med | Full grep for `.Morning`, `.Afternoon`, `.Evening` across solution before any change |
| Migration squash breaks dev workflow | Low | Dev-only data; confirm with team before squashing |
| Test coverage gap in accessor refactor | Low | All 172+ tests must pass; CI gate blocks merge |

## Rollback Plan

1. Restore `DayPlan.cs` properties (Morning/Afternoon/Evening) and revert `GetBlock()` visibility
2. Restore `TripConfiguration.cs` OwnsOne block from git
3. Restore migrations from git
4. Revert repository `ThenInclude` additions
5. Revert handler/service changes

No data migration needed — dev-only data.

## Dependencies

- None

## Success Criteria

- [ ] All 172+ existing tests pass with zero behavioral changes
- [ ] BlockTimelines table created with FK to DayPlan (verified via migration SQL)
- [ ] Single Activities table replaces 3 activity tables (verified via migration SQL)
- [ ] DayPlan table has ~8 columns (down from ~50, verified via migration SQL)
- [ ] Zero `.Morning`, `.Afternoon`, `.Evening` direct property references in solution (grep returns 0 beyond test assertions for old API)
