# Design: Abstract Place External Service

## Technical Approach

Port & Adapter (Hexagonal Architecture): introduce `IPlaceExternalService` in Domain as the port, `FoursquarePlaceService` in Infrastructure as the adapter. `PlaceRepository` swaps its `IFoursquareApiClient` dependency for the port. Foursquare types become `internal`. Mapping logic moves from repository to adapter.

## Architecture Decisions

### Decision: Interface returns `Place`, not DTO
| Option | Tradeoff | Decision |
|--------|----------|----------|
| Return `Place` entity | Port returns domain type directly — no mapping leak. FoursquareService owns all mapping. | **Selected** |
| Return intermediate DTO | Extra mapping layer, violates simplicity goal per spec | Rejected |

Rationale: Per spec `FR5`, mapping belongs in the adapter. The port returns domain entities so consumers (PlaceRepository) see only domain types.

### Decision: PlaceRepository keeps optional constructor parameter
| Option | Tradeoff | Decision |
|--------|----------|----------|
| Optional `IPlaceExternalService?` | Keeps existing test pattern (DB-only tests pass null). Follows current code convention. | **Selected** |
| Required parameter + DI-only | Breaks existing `PlaceRepositoryTests` that construct repo without the service. | Rejected |

Rationale: Current tests construct `PlaceRepository` without an API client. Keeping optional preserves backward compat for DB-only tests until they're refactored.

### Decision: FoursquarePlaceService owns mapping + graceful degradation
| Option | Tradeoff | Decision |
|--------|----------|----------|
| Service swallows exceptions internally | Adapter returns empty list on failure — matches current PlaceRepository behavior | **Selected** |
| Exceptions propagate | Caller must handle; changes cascade semantics | Rejected |

Rationale: `PlaceRepository` currently catches `HttpRequestException` and returns empty list. Moving this into the adapter keeps the same contract.

## Data Flow

```
Controller
  → Application Service
    → PlaceRepository.SearchAsync(query, cityId)
      ├─ Local DB match? → return List<Place>
      └─ No match
         → IPlaceExternalService.SearchPlacesAsync(query, cityId)
              └─ FoursquarePlaceService
                   ├─ IFoursquareApiClient.SearchPlacesAsync() → FoursquarePlace[]
                   ├─ FoursquareCategoryHeuristics.Map() → heuristics
                   └─ Map to Place entity → return List<Place>
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `SmartTripPlanner.Domain/Repository/IPlaceExternalService.cs` | Create | Port: `Task<List<Place>> SearchPlacesAsync(string query, string cityId, int maxResults = 20)` |
| `SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/FoursquarePlaceService.cs` | Create | Adapter: implements `IPlaceExternalService`, wraps `IFoursquareApiClient`, maps → `Place` |
| `SmartTripPlanner.Infrastructure/Repositories/PlaceRepository.cs` | Modify | Replace `IFoursquareApiClient?` → `IPlaceExternalService?`; remove `MapToPlace`; drop Foursquare usings |
| `SmartTripPlanner.Infrastructure/InfrastructureServiceRegistration.cs` | Modify | Add `services.AddScoped<IPlaceExternalService, FoursquarePlaceService>()` |
| `SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/Models/*.cs` | Modify | All model classes → `internal` |
| `SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/Mapping/FoursquareCategoryHeuristics.cs` | Modify | Class → `internal` |
| `tests/.../PlaceRepositoryCascadeTests.cs` | Modify | Mock `IPlaceExternalService` instead of `IFoursquareApiClient` |
| `tests/.../PlaceRepositoryTests.cs` | Modify | No change needed (DB-only tests construct repo without service) |

## Interfaces / Contracts

```csharp
// SmartTripPlanner.Domain/Repository/IPlaceExternalService.cs
namespace SmartTripPlanner.Domain.Repository;

public interface IPlaceExternalService
{
    Task<List<Place>> SearchPlacesAsync(string query, string cityId, int maxResults = 20);
}
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | PlaceRepository cascade with `IPlaceExternalService` mock | `PlaceRepositoryCascadeTests` refactored: mock port, verify call/response |
| Unit | `FoursquarePlaceService` mapping + error handling | New test file: mock `IFoursquareApiClient`, verify Place output, empty list on error |
| Unit | Existing DB-only PlaceRepository tests | Unchanged — constructor accepts optional `IPlaceExternalService?=null` |
| Unit | `FoursquareCategoryHeuristics` | Unchanged — already tested in `FoursquareCategoryHeuristicsTests` |

## Migration / Rollout

No migration required. Add `services.AddScoped<IPlaceExternalService, FoursquarePlaceService>()` alongside existing `IFoursquareApiClient` registration in `InfrastructureServiceRegistration.cs`. The old `IFoursquareApiClient` + `FoursquareApiClient` DI wire stays — `FoursquarePlaceService` consumes it. Rollback: revert commits, delete new files, restore `PlaceRepository`.

## Open Questions

- [ ] Should `PlaceRepository` constructor become required (non-optional) for production DI, accepting only the port? Current optional pattern supports test flexibility.
