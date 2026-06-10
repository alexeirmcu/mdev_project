# Design: App Place Search Handler

## Technical Approach

Bootstrap the empty `SmartTripPlanner.ApplicationServices` project with a MediatR pipeline: `SearchPlaces` request → handler delegates to `IPlaceRepository.SearchAsync` → results mapped via AutoMapper to `PlaceModel` response records. Registration follows the existing `AddInfrastructure()` extension pattern.

## Architecture Decisions

| Decision | Choice | Alternatives | Rationale |
|----------|--------|-------------|----------|
| **CQRS framework** | MediatR | Raw DI, Brighter | Matches solution architecture plan; minimal ceremony for request→handler pattern |
| **Object mapping** | AutoMapper | Manual mapping, Mapster | Explicit user request; already common in .NET ecosystem |
| **Response model location** | `Domain/ApiModels/` | Dedicated `ApplicationServices/Models/` | Matches existing `TripPlanResponse`, `TripSummaryResponse` convention |
| **Registration pattern** | `IServiceCollection` extension | Direct registration in `Program.cs` | Follows `AddInfrastructure()` precedent; keeps composition root clean |
| **Location mapping** | New `PlaceLocationModel` record | Reuse `LocationModel` | `LocationModel` has `Name` field; `PlaceLocation` only has lat/lng — incompatible |

## Data Flow

```
HTTP (future controller)
  → SearchPlaces (IRequest)
    → SearchPlacesHandler
      → IPlaceRepository.SearchAsync(query, cityId, maxResults)
        → [cascade: local DB → external API]
      ← List<Place>
      → AutoMapper: Place → PlaceModel
    ← PlaceSearchResponse
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `Domain/ApiModels/PlaceLocationModel.cs` | Create | Lat/Lng record for API output |
| `Domain/ApiModels/OpeningHoursWindowModel.cs` | Create | Day/hours record for API output |
| `Domain/ApiModels/PlaceModel.cs` | Create | Full place representation with all fields |
| `Domain/ApiModels/PlaceSearchResponse.cs` | Create | Response wrapping `IReadOnlyList<PlaceModel>` |
| `ApplicationServices/Requests/SearchPlaces.cs` | Create | MediatR request record (`IRequest<PlaceSearchResponse>`) |
| `ApplicationServices/Handlers/SearchPlacesHandler.cs` | Create | Orchestrates repository + mapping |
| `ApplicationServices/Mapping/PlaceMappingProfile.cs` | Create | AutoMapper: `Place` → `PlaceModel` |
| `ApplicationServices/ApplicationServicesRegistration.cs` | Create | DI extension registering MediatR + AutoMapper |
| `ApplicationServices/SmartTripPlanner.ApplicationServices.csproj` | Modify | Add MediatR, AutoMapper NuGet + Domain project reference |
| `API/SmartTripPlanner.API.csproj` | Modify | Add ApplicationServices project reference |
| `API/Program.cs` | Modify | Call `AddApplicationServices()` after `AddInfrastructure()` |
| `tests/.../SmartTripPlanner.Tests.csproj` | Modify | Add ApplicationServices project reference |

## Interfaces / Contracts

```csharp
// Domain/ApiModels/ — new records
public record PlaceLocationModel(double Latitude, double Longitude);
public record OpeningHoursWindowModel(DayOfWeek DayOfWeek, int OpenMinutes, int CloseMinutes);

public record PlaceModel(
    string PlaceId,
    string Name,
    string CityId,
    PlaceLocationModel Location,
    int TypicalDurationMinutes,
    bool IsIndoor,
    bool IsFamilyFriendly,
    IReadOnlyList<OpeningHoursWindowModel> OpeningHours);

public record PlaceSearchResponse(IReadOnlyList<PlaceModel> Results);
```

```csharp
// ApplicationServices/ — request and handler
public record SearchPlaces(string Query, string CityId, int MaxResults)
    : IRequest<PlaceSearchResponse>;

public class SearchPlacesHandler(IPlaceRepository repository, IMapper mapper)
    : IRequestHandler<SearchPlaces, PlaceSearchResponse>
{
    public async Task<PlaceSearchResponse> Handle(
        SearchPlaces request, CancellationToken ct)
    {
        var places = await repository.SearchAsync(
            request.Query, request.CityId, request.MaxResults);
        var models = mapper.Map<List<PlaceModel>>(places);
        return new PlaceSearchResponse(models.AsReadOnly());
    }
}
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | `SearchPlacesHandler` | Mock `IPlaceRepository` + `IMapper`; verify `SearchAsync` called with correct args and mapped result is returned |
| Unit | `PlaceMappingProfile` | Use `IMapper.Map<PlaceModel>` with a populated `Place` fixture; verify all fields including nested `PlaceLocation` and `OpeningHoursWindow` |
| Integration | Full pipeline | Wire up real DI in test; use EF InMemory + repository; verify cascade behavior |

## Migration / Rollout

No migration required. The handler is additive — no existing code changes behavior. Wire the DI registration and deploy.

## Open Questions

- None. All decisions scoped and documented above.
