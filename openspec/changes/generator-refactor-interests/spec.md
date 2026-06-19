# Delta Spec: Refactor Itinerary Generator and Add Interest-Based Filtering

## Affected Domains

| Domain | Type | Description |
|--------|------|-------------|
| itinerary-generation | Modified | ActivityNode gains PlaceLocation; Haversine scoring replaces stub; generator decomposed into phases |
| place | Modified | IPlaceRepository gains interest-filtered and distinct-attribute queries |
| city-interests-endpoint | New | GET endpoint returning distinct interest values per city |
| trip-interests | New | TripPreferences.Interests property, validation, and PostgreSQL persistence |

## Detailed Specs

See domain-specific delta specs:
- [itinerary-generation/spec.md](specs/itinerary-generation/spec.md)
- [place/spec.md](specs/place/spec.md)
- [city-interests-endpoint/spec.md](specs/city-interests-endpoint/spec.md)
- [trip-interests/spec.md](specs/trip-interests/spec.md)

## Non-Functional Requirements

| ID | Requirement |
|----|-------------|
| NFR1 | Candidate filtering by interests MUST happen in SQL via EF Core translation, not in-memory after materialization. |
| NFR2 | Generator phase classes MUST be independently unit-testable without the full `HeuristicItineraryGenerator`. Each phase accepts only its direct dependencies (scorer, calculator, etc.). |
| NFR3 | Existing trips without interests MUST continue to regenerate itineraries without errors. The system falls back to unfiltered candidates when interests are absent. |
| NFR4 | `IItineraryGenerator` contract remains unchanged — no breaking changes to the port interface. |
| NFR5 | `InterestsResponse` endpoint responds within 200ms for cities with up to 10,000 places. |

## Integration Points

| Integration Point | Direction | Description |
|-------------------|-----------|-------------|
| `ActivityNode.Location` — EF Core owned entity | Domain → Infrastructure | 3 owned tables migration: `ActivityNode_Location` (Latitude, Longitude) |
| `TripPreferences.Interests` — EF Core column | Domain → Infrastructure | PostgreSQL `text[]` migration with NULL default for backward compat |
| `IPlaceRepository.GetCandidatesByCityAndInterestsAsync` | Domain → Infrastructure | New method on existing interface; SQL-translated WHERE clause |
| `IPlaceRepository.GetDistinctAttributeValuesByCityCodeAsync` | Domain → Infrastructure | New method; SELECT DISTINCT on PlaceAttribute.Value |
| `AutoMapperProfile` — TripPreferences mapping | Domain → API | Map `IReadOnlyList<string> Interests` to/from `TripPreferencesInput.Interests` |
| `GET /api/cities/{cityCode}/interests` | API → Application | New controller action returning `InterestsResponse` |
| `GenerateTripValidator` | ApplicationServices | New rule: interests must contain at least one non-empty string |
| `HeuristicItineraryGenerator` decomposition | Domain Services → 5 Phase classes | Mutable `_placesById` removed; ActivityNode.Location used for distance |

## Acceptance Criteria

| ID | Criterion |
|----|-----------|
| AC1 | `ActivityNode.Location` stores `PlaceLocation`; EF Core migration creates `ActivityNode_Location` owned table |
| AC2 | Haversine distance is computed between consecutive `ActivityNode.Location` values; stub `return 1.0` is replaced |
| AC3 | `TripPreferences.Interests` persists as PostgreSQL `text[]`; existing rows have NULL/empty default |
| AC4 | `GenerateTripValidator` rejects trip creation with empty/null interests; returns `REQUIRED_FIELD` error |
| AC5 | Existing trips without interests regenerate without validation errors (backward compatible) |
| AC6 | `GET /api/cities/{cityCode}/interests` returns 200 with distinct attribute values; 404 for unknown city |
| AC7 | `IPlaceRepository.GetCandidatesByCityAndInterestsAsync` filters in SQL; no post-materialization `.Where()` |
| AC8 | Generator decomposed into 5 phase classes; each independently unit-testable; mutable `_placesById` removed |
| AC9 | All 172+ existing tests pass; new code >80% covered |
| AC10 | `ICandidateScorer` receives real Haversine `DistanceFromBlockCenterKm`; empty block yields 0 |

## Risks

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| 3-table migration complexity for ActivityNode.Location | Medium | ActivityNode owns Location, OpeningHours, and potentially TransitDetails — verify all 3 owned entity configurations. Test migration against PostgreSQL test db. |
| Distance scoring change breaks existing test assertions | Medium | Characterization tests before refactor; update assertion tolerances for Haversine vs stub (1.0). |
| Breaking change in GenerateTripValidator | Medium | Validator rule only fires on new trips (POST); existing trip regeneration skips it. Migration adds NULL column. |
| Interest filtering over-constrains candidate pool | Medium | Fallback to unfiltered `GetManyByCityIdAsync` when interest matches yield zero results. |
| Haversine approximations poor for short urban distances | Low | Document as MVP limitation; acceptable for intra-city itinerary scoring. |