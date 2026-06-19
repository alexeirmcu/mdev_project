# Design: Refactor Itinerary Generator and Add Interest-Based Filtering

## Technical Approach

Decompose `HeuristicItineraryGenerator` into 5 phase collaborator classes behind the unchanged `IItineraryGenerator` contract. Add `ActivityNode.Location` for real Haversine distance, replace the `return 1.0` stub. Extend `TripPreferences` with `Interests` (PostgreSQL `text[]`), add interest-filtered candidate queries in `IPlaceRepository`, and expose a `/api/cities/{cityCode}/interests` endpoint. Implementation ships as 2 PRs: (1) Model + API, (2) Generator refactor.

## Architecture Decisions

| Decision | Choice | Alternatives | Rationale |
|----------|--------|--------------|-----------|
| Phase extraction pattern | 5 classes injected via DI, orchestrated by `HeuristicItineraryGenerator` | Strategy pattern with `IPhase` interface; single class with partial methods | DI-injected classes are independently testable (NFR2) while keeping the orchestrator simple. No artificial `IPhase` abstraction — each phase has a unique signature. |
| `ActivityNode.Location` mapping | `OwnsOne` on `ActivityNode` inside 3 owned `Activities` collections (Morning/Afternoon/Evening) | JSON column; shared Location table | Matches existing EF Core pattern (`OwnsOne` for `TransitToNext`). 3 tables follows project convention where each block's activities table gets its owned entities. |
| `TripPreferences.Interests` persistence | PostgreSQL `text[]` via Npgsql array mapping | JSON column; separate interests table | `text[]` is native to PostgreSQL, supports server-side `ANY()` queries for interest filtering, and aligns with NFR1 (SQL-translated WHERE clause). |
| Interest filtering query | `WHERE pa.Value ILIKE ANY(@interests)` with EF Core `Any` + `Contains` | Raw SQL; materialize then filter | EF Core translates `Attributes.Any(a => interests.Contains(a.Value))` to `EXISTS` subquery — server-side, no in-memory filter. ILIKE handled via `EF.Functions.ILike` or lower-case normalization in service. |
| Distance scoring | Reuse existing `PlaceLocation.DistanceKmTo()` | New static Haversine utility | `PlaceLocation` already has Haversine. Pass `ActivityNode.Location` into scoring instead of dictionary lookup, removing `_placesById` mutable state. |
| City interests endpoint | CQRS query via MediatR (`GetCityInterests`) + controller action | Direct repository call in controller | Matches project pattern (MediatR handlers for all write/side-effect reads). Query returns `InterestsResponse` directly from repo — no mapping. |
| Validator scope | `GenerateTripValidator` adds interest rule for new trips only | Separate validator; rule in handler | Existing pattern uses FluentValidation. Rule fires on `GenerateTrip` (POST) only — regeneration skips this validator. |

## Data Flow

```
POST /api/trips (GenerateTrip)
  → GenerateTripValidator (interests rule)
  → GenerateTripHandler
      → TripRepository.AddAsync (persist with Interests)
      → PlaceRepository.GetCandidatesByCityAndInterestsAsync (when interests present)
      → fallback: PlaceRepository.GetManyByCityIdAsync (when interests empty/no matches)
      → IItineraryGenerator.GenerateAsync (unchanged contract)
          → Phase 1: HeuristicItineraryGenerator.GenerateAsync (orchestrator)
          → Phase 2: PinnedPlacementPhase.PlaceAsync
          → Phase 3: UnpinnedPlacementPhase.PlaceAsync (uses ZoneClusteringHelper)
          → Phase 4: CandidateFillingPhase.FillAsync (uses ICandidateScorer + ActivityNode.Location)
          → Phase 5a: TransitEnrichmentPhase.EnrichAsync
          → Phase 5b: WeatherEnrichmentPhase.EnrichAsync

GET /api/cities/{cityCode}/interests
  → GetCityInterestsHandler
      → PlaceRepository.GetDistinctAttributeValuesByCityCodeAsync
      → InterestsResponse
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `Domain/AggregatesModel/ActivityNode.cs` | Modify | Add `PlaceLocation? Location` property; update constructor |
| `Domain/AggregatesModel/TripPreferences.cs` | Modify | Add `IReadOnlyList<string> Interests` property; update constructor + equality |
| `Domain/ApiModels/TripPreferencesInput.cs` | Modify | Add `IReadOnlyList<string>? Interests` parameter |
| `Domain/ApiModels/InterestsResponse.cs` | Create | `record InterestsResponse(string[] Interests)` |
| `Domain/Repository/IPlaceRepository.cs` | Modify | Add `GetCandidatesByCityAndInterestsAsync`, `GetDistinctAttributeValuesByCityCodeAsync` |
| `Domain/Ports/IPinnedPlacementPhase.cs` | Create | Phase port with `PlaceAsync` |
| `Domain/Ports/IUnpinnedPlacementPhase.cs` | Create | Phase port with `PlaceAsync` |
| `Domain/Ports/ICandidateFillingPhase.cs` | Create | Phase port with `FillAsync` |
| `Domain/Ports/ITransitEnrichmentPhase.cs` | Create | Phase port with `EnrichAsync` |
| `Domain/Ports/IWeatherEnrichmentPhase.cs` | Create | Phase port with `EnrichAsync` |
| `Domain/Ports/IGetCityInterests.cs` | Create | MediatR query interface |
| `Domain/Services/PinnedPlacementPhase.cs` | Create | Extract from HeuristicItineraryGenerator lines 60-70, 113-150 |
| `Domain/Services/UnpinnedPlacementPhase.cs` | Create | Extract from lines 73-98, 152-183 |
| `Domain/Services/CandidateFillingPhase.cs` | Create | Extract from lines 100-248; uses `ActivityNode.Location` for Haversine |
| `Domain/Services/TransitEnrichmentPhase.cs` | Create | Extract from lines 250-289; uses `ActivityNode.Location` instead of `_placesById` |
| `Domain/Services/WeatherEnrichmentPhase.cs` | Create | Extract from lines 258-259 |
| `Domain/Services/HeuristicItineraryGenerator.cs` | Modify | Reduce to orchestrator; inject 5 phase collaborators; remove `_placesById` |
| `Infrastructure/Repositories/PlaceRepository.cs` | Modify | Add `GetCandidatesByCityAndInterestsAsync`, `GetDistinctAttributeValuesByCityCodeAsync` |
| `Infrastructure/Configurations/TripConfiguration.cs` | Modify | Add `OwnsOne` for `ActivityNode.Location` in 3 activity tables; add `Interests` `text[]` column on Preferences |
| `Infrastructure/Configurations/ActivityNodeConfiguration.cs` | Create | Extract ActivityNode owned-entity config (optional if kept inline in TripConfiguration) |
| `Infrastructure/Queries/GetCityInterestsHandler.cs` | Create | MediatR handler calling `IPlaceRepository.GetDistinctAttributeValuesByCityCodeAsync` |
| `API/Controllers/CitiesController.cs` | Create | `GET /api/cities/{cityCode}/interests` endpoint |
| `API/Configurations/AutoMapperProfile.cs` | Modify | Map `Interests` between `TripPreferencesInput` ↔ `TripPreferences` |
| `ApplicationServices/Validators/GenerateTripValidator.cs` | Modify | Add interests validation rule |
| `Infrastructure/InfrastructureServiceRegistration.cs` | Modify | Register phase classes and GetCityInterestsHandler |
| `Infrastructure/Migrations/<timestamp>_AddActivityNodeLocationAndInterests.cs` | Create | EF Core migration: add `Location_Latitude`, `Location_Longitude` to 3 activity tables; add `Interests` text[] to Preferences |
| `tests/.../PinnedPlacementPhaseTests.cs` | Create | Unit tests for pinned placement phase |
| `tests/.../UnpinnedPlacementPhaseTests.cs` | Create | Unit tests for unpinned placement phase |
| `tests/.../CandidateFillingPhaseTests.cs` | Create | Unit tests for candidate filling with Haversine distance |
| `tests/.../TransitEnrichmentPhaseTests.cs` | Create | Unit tests for transit enrichment |
| `tests/.../WeatherEnrichmentPhaseTests.cs` | Create | Unit tests for weather enrichment |
| `tests/.../HeuristicItineraryGeneratorTests.cs` | Modify | Update to test orchestrator wiring; add characterization tests for distance change |
| `tests/.../TripPreferencesTests.cs` | Modify | Add Interests property tests |
| `tests/.../ActivityNodeTests.cs` | Modify | Add Location property tests |
| `tests/.../GenerateTripValidatorTests.cs` | Modify | Add interests validation scenarios |
| `tests/.../PlaceRepositoryTests.cs` | Modify | Add interest-filtered query tests; add distinct-attribute-values tests |
| `tests/.../GetCityInterestsHandlerTests.cs` | Create | Query handler tests |
| `tests/.../CitiesControllerTests.cs` | Create | Endpoint integration tests |

## Interfaces / Contracts

```csharp
// Domain/Ports/IPinnedPlacementPhase.cs
public interface IPinnedPlacementPhase
{
    List<long> PlaceAsync(Trip trip, IReadOnlyList<(MustSee mustSee, Place place)> pinnedMustSees);
}

// Domain/Ports/IUnpinnedPlacementPhase.cs
public interface IUnpinnedPlacementPhase
{
    List<long> PlaceAsync(Trip trip, IReadOnlyList<(MustSee mustSee, Place place)> unpinnedMustSees);
}

// Domain/Ports/ICandidateFillingPhase.cs
public interface ICandidateFillingPhase
{
    Task FillAsync(Trip trip, IReadOnlyList<Place> candidatePool,
        Dictionary<DateOnly, WeatherCondition> weatherData, CancellationToken ct);
}

// Domain/Ports/ITransitEnrichmentPhase.cs
public interface ITransitEnrichmentPhase
{
    Task EnrichAsync(Trip trip, CancellationToken ct);
}

// Domain/Ports/IWeatherEnrichmentPhase.cs
public interface IWeatherEnrichmentPhase
{
    Task EnrichAsync(Trip trip, Dictionary<DateOnly, WeatherCondition> weatherData);
}

// Domain/Repository/IPlaceRepository.cs (additions)
Task<List<Place>> GetCandidatesByCityAndInterestsAsync(long cityId, IReadOnlyList<string> interests, CancellationToken ct = default);
Task<List<string>> GetDistinctAttributeValuesByCityCodeAsync(string cityCode, CancellationToken ct = default);

// Domain/Ports/IGetCityInterests.cs
public record GetCityInterests(string CityCode) : IRequest<InterestsResponse>;
public record InterestsResponse(string[] Interests);

// Domain/AggregatesModel/ActivityNode.cs (addition)
public PlaceLocation? Location { get; init; }

// Domain/AggregatesModel/TripPreferences.cs (addition)
public IReadOnlyList<string> Interests { get; }
// Constructor adds: IReadOnlyList<string>? interests = null
// Default: empty list (backward compatible)

// Domain/ApiModels/TripPreferencesInput.cs (modified)
public record TripPreferencesInput(
    bool CarAvailable = false,
    int MaxWalkingMinutes = 30,
    bool WeatherAwareEnabled = true,
    IReadOnlyList<string>? Interests = null);
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | `PinnedPlacementPhase.PlaceAsync` | Mock `ICandidateScorer`; verify must-sees land on correct day/block |
| Unit | `UnpinnedPlacementPhase.PlaceAsync` | Verify zone-clustered ordering and overflow behavior |
| Unit | `CandidateFillingPhase.FillAsync` | Mock `ICandidateScorer`; verify Haversine distance replaces stub 1.0; verify interest filtering fallback |
| Unit | `TransitEnrichmentPhase.EnrichAsync` | Verify `ActivityNode.Location` used for transit; no `_placesById` dependency |
| Unit | `WeatherEnrichmentPhase.EnrichAsync` | Verify weather assigned per day |
| Unit | `TripPreferences.Interests` | Empty list default, case-insensitive comparison |
| Unit | `ActivityNode.Location` | Carry `PlaceLocation` from source `Place` |
| Unit | `GenerateTripValidator` | Reject null/empty interests; accept valid interests |
| Integration | Full generation pipeline | End-to-end through `HeuristicItineraryGenerator` with all 5 phases wired |
| Integration | `PlaceRepository.GetCandidatesByCityAndInterestsAsync` | Verify SQL translation (no in-memory `.Where()`) |
| Integration | `PlaceRepository.GetDistinctAttributeValuesByCityCodeAsync` | Verify `SELECT DISTINCT` in SQL |
| Characterization | Distance scoring behavior change | Snapshot existing assertions; update tolerances from stub 1.0 to real Haversine values |
| API | `GET /api/cities/{cityCode}/interests` | 200 with distinct values; 404 for unknown city |

## Migration / Rollout

**Migration 1**: Add `ActivityNode_Location` columns — `Latitude` and `Longitude` as `double precision` nullable columns on `MorningActivities`, `AfternoonActivities`, and `EveningActivities` tables. Existing rows get NULL (backward compatible — `ActivityNode.Location` is nullable).

**Migration 2**: Add `Interests` column — `text[]` nullable on the `Preferences` owned table (inside Trips). Existing rows get NULL default. EF Core maps `null` → empty `IReadOnlyList<string>` via value converter in `TripConfiguration`.

**Rollback**: Both PRs are independently revertible. `IItineraryGenerator` contract is unchanged. Removing the `Interests` column restores previous behavior for existing trips.

## Open Questions

- [ ] Should `CandidateFillingPhase` accept `IReadOnlyList<string> interests` directly, or receive `TripPreferences`? (Leaning: `TripPreferences` — single param, more context)
- [ ] Should the city interests endpoint validate city existence via `ICityRepository` before querying attributes? (Spec says 404 for unknown city — yes)
- [ ] Exact Npgsql `text[]` value converter for `IReadOnlyList<string>` ↔ `string[]` — need to verify Npgsql EF Core provider handles this natively or requires explicit converter