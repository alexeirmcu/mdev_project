# Proposal: Refactor Itinerary Generator and Add Interest-Based Filtering

## Intent

`HeuristicItineraryGenerator` (395 lines) is a god class with stubbed distance scoring, mutable `_placesById` state, and no phase separation. This change refactors it into testable collaborators, replaces the distance stub with Haversine scoring, and introduces trip interests to filter candidates—improving relevance and maintainability.

## Scope

### In Scope
- Add `PlaceLocation` to `ActivityNode`.
- Implement Haversine distance scoring in `ICandidateScorer`.
- Add `IReadOnlyList<string> Interests` to `TripPreferences` with validation.
- Extend `IPlaceRepository` with interest-based candidate filtering.
- Extract 5 generator phases into collaborator classes.
- Add `GET /api/cities/{cityCode}/interests`.
- Enforce interests in `GenerateTripValidator`.

### Out of Scope
- Migration of existing trips (backward compatible).
- Real-time routing APIs or OR-Tools.

## Capabilities

### New
- `city-interests-endpoint`: List interest tags per city.
- `trip-interests`: `TripPreferences` adds `IReadOnlyList<string> Interests`; required on new trips.

### Modified
- `itinerary-generation`: ActivityNode stores PlaceLocation; real Haversine scoring; generator refactored into phases.
- `place`: `IPlaceRepository` supports interest-based filtering.

## Approach

1. **Domain**: Extend `ActivityNode`, `TripPreferences`, and `IPlaceRepository`.
2. **Infrastructure**: Haversine scorer; interest-filtered query.
3. **Application/Domain**: Decompose into 5 phase classes; remove mutable state.
4. **API**: Endpoint, validator, mapping.
5. **Tests**: Characterization tests, then phase/scorer/validator tests.

**Key Decisions**
- Backward compat: validate interests only on new trips. No migration.
- Keep `IItineraryGenerator` contract unchanged.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Domain/Entities/ActivityNode.cs` | Modified | Add PlaceLocation |
| `Domain/ValueObjects/TripPreferences.cs` | Modified | Add Interests |
| `Domain/Repository/IPlaceRepository.cs` | Modified | Interest-filtered query |
| `Infrastructure/Services/HeuristicItineraryGenerator.cs` | Modified | Extract phases; remove mutable state |
| `Infrastructure/Repositories/PlaceRepository.cs` | Modified | Interest filtering |
| `API/Controllers/CityController.cs` | Modified | Interests action |
| `API/Validation/GenerateTripValidator.cs` | Modified | Interests rule |
| `tests/` | New/Modified | Phase, scorer, validator tests |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Refactor breaks itinerary behavior | Medium | Characterization tests; keep 172+ tests green |
| Interest filtering over-constrains | Medium | Fallback to unfiltered if too few matches |
| Haversine poor for short urban distances | Low | Document as MVP limitation |

## Rollback Plan

Revert PR. `IItineraryGenerator` contract unchanged; rollback restores legacy generator safely.

## Dependencies

- None.

## Success Criteria

- [ ] Generator split into 5 phase classes; zero mutable state.
- [ ] Haversine scoring returns non-1.0 values.
- [ ] City interests endpoint returns 200.
- [ ] `GenerateTrip` rejects missing interests.
- [ ] Existing trips without interests regenerate.
- [ ] All existing tests pass; new code >80% covered.

## PR Strategy

2 PRs under 400 lines:
1. **Model & API**: Domain, repo interface, validator, endpoint.
2. **Generator Refactor**: Phase extraction, scorer, filtering, tests.
