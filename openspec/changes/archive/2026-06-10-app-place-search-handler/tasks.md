# Tasks: App Place Search Handler

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~310 |
| 400-line budget risk | Medium |
| Chained PRs recommended | No |
| Suggested split | Single PR |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Medium

## Phase 1: Domain ApiModels — Records

- [x] 1.1 Create `Domain/ApiModels/PlaceLocationModel` record with `Latitude`/`Longitude` doubles
- [x] 1.2 Create `Domain/ApiModels/OpeningHoursWindowModel` record with `DayOfWeek`/`OpenMinutes`/`CloseMinutes`
- [x] 1.3 Create `Domain/ApiModels/PlaceModel` record with all fields (PlaceId, Name, CityId, Location, TypicalDurationMinutes, IsIndoor, IsFamilyFriendly, OpeningHours)
- [x] 1.4 Create `Domain/ApiModels/PlaceSearchResponse` wrapping `IReadOnlyList<PlaceModel>`

## Phase 2: ApplicationServices Foundation

- [x] 2.1 Add MediatR + AutoMapper.Extensions.Microsoft.DependencyInjection NuGet and Domain project reference to `ApplicationServices.csproj`
- [x] 2.2 Create `Requests/SearchPlaces` record: `string? Query`, `string CityId`, `int MaxResults = 20` — implement `IRequest<PlaceSearchResponse>`
- [x] 2.3 Create `ApplicationServicesRegistration` — `AddApplicationServices()` extension registering MediatR (scan assembly) + AutoMapper profiles

## Phase 3: AutoMapper Profile — TDD

- [x] 3.1 RED: Write `PlaceMappingProfileTests` with a populated `Place` fixture; verify all fields including flattened Location and OpeningHoursWindow map correctly
- [x] 3.2 GREEN: Create `PlaceMappingProfile` (AutoMapper Profile) mapping `Place` → `PlaceModel`, flattening `Place.Location` to `Latitude`/`Longitude` and `OpeningHours` list
- [x] 3.3 REFACTOR: Extract shared Place fixture helper, verify test passes

## Phase 4: Handler — TDD

- [x] 4.1 RED: Write `SearchPlacesHandlerTests` — mock `IPlaceRepository` + `IMapper`; scenarios: returns 3 mapped models, cascade results transparently, empty results, null Query passthrough, MaxResults defaults to 20
- [x] 4.2 GREEN: Create `SearchPlacesHandler` — inject `IPlaceRepository` + `IMapper`, call `SearchAsync(query, cityId, maxResults)`, map via `IMapper.Map<List<PlaceModel>>`, return `PlaceSearchResponse`
- [x] 4.3 REFACTOR: Clean up test setup (shared mock factory), verify green

## Phase 5: API Wiring

- [x] 5.1 Add ApplicationServices project reference to `SmartTripPlanner.API.csproj`
- [x] 5.2 Add `builder.Services.AddApplicationServices()` in `Program.cs` after `AddInfrastructure()`
- [x] 5.3 Add ApplicationServices project reference to `tests/SmartTripPlanner.Tests.csproj`
