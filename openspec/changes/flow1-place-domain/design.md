# Design: flow1-place-domain

## Overview
Phase 1: Place domain entity, ValueObjects (OpeningHoursWindow, PlaceLocation), repository interface (IPlaceRepository), and EF Core infrastructure (PlaceConfiguration, PlaceRepository).  
Phase 2: Foursquare API client (`IFoursquareApiClient`), typed HttpClient, Foursquare DTOs, `FoursquareCategoryHeuristics` mapper, and cascade search pipeline in `PlaceRepository`.

Strict TDD: tests first, implementation second.

## Phase 1 (Done) — Class Design

### Place (Entity, IAggregateRoot)
- Inherits Entity (long Id, auto-generated)
- PlaceId (string, required) — unique index for domain lookup
- Name (string, required)
- CityId (string, required, e.g. "madrid-es")
- Location (PlaceLocation, required) — OwnsOne
- TypicalDurationMinutes (int, default 60)
- IsIndoor (bool, default false)
- IsFamilyFriendly (bool, default true)
- OpeningHours (List<OpeningHoursWindow>) — OwnsMany, initialized as empty list
- Constructor validates: PlaceId not null/empty, Name not null/empty, Location not null

### OpeningHoursWindow (ValueObject)
- DayOfWeek (DayOfWeek)
- OpenMinutes (int, 0-1439)
- CloseMinutes (int, 0-1439)
- Validation: OpenMinutes <= CloseMinutes, both in [0, 1439]
- Equality: all 3 properties

### PlaceLocation (ValueObject)
- Latitude (double, -90 to 90)
- Longitude (double, -180 to 180)
- Validation: ranges checked in constructor
- Equality: both coordinates

## Phase 2 — New Components

### FoursquareApiOptions (Infrastructure Configuration)
```csharp
namespace SmartTripPlanner.Infrastructure.Configuration;

public class FoursquareApiOptions
{
    public const string SectionName = "FoursquareApi";
    public string BaseUrl { get; set; } = "https://api.foursquare.com/v3/";
    public string ApiKey { get; set; } = string.Empty;
}
```

### IFoursquareApiClient (Infrastructure — NOT Domain)
```csharp
namespace SmartTripPlanner.Infrastructure.ExternalServices.Foursquare;

public interface IFoursquareApiClient
{
    Task<List<FoursquarePlace>> SearchPlacesAsync(string query, string near, int limit = 20);
    Task<FoursquarePlace?> GetPlaceByIdAsync(string fsqId);
}
```

### FoursquareApiClient (Typed HttpClient)
- Injects `HttpClient` via constructor (managed by `IHttpClientFactory`).
- `SearchPlacesAsync`: GET `/places/search?query={query}&near={near}&limit={limit}`
- `GetPlaceByIdAsync`: GET `/places/{fsqId}?fields=fsq_id,name,geocodes,hours,categories`
- Sets `Authorization: {ApiKey}` header via `DefaultRequestHeaders` (configured in DI).
- Deserializes JSON response into `FoursquarePlace` / `FoursquarePlace[]` DTOs.
- **Error handling**: non-2xx responses throw `HttpRequestException`. Tests verify graceful degradation.

### FoursquarePlace DTO (Infrastructure only — NEVER shared)
```csharp
namespace SmartPlanner.Infrastructure.ExternalServices.Foursquare.Models;

public class FoursquarePlace
{
    public string FsqId { get; set; }
    public string Name { get; set; }
    public FoursquareGeocodes Geocodes { get; set; }
    public FoursquareHours Hours { get; set; }
    public List<FoursquareCategory> Categories { get; set; }
}

public class FoursquareGeocodes
{
    public FoursquareLatLng Main { get; set; }
}

public class FoursquareLatLng
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class FoursquareHours
{
    public List<FoursquareRegularHour> Regular { get; set; }
}

public class FoursquareRegularHour
{
    public int Day { get; set; }  // 1=Mon..7=Sun (Foursquare convention)
    public string Open { get; set; }  // "09:00"
    public string Close { get; set; }  // "21:00"
}

public class FoursquareCategory
{
    public string Id { get; set; }
    public string Name { get; set; }
}
```

### FoursquareCategoryHeuristics (Infrastructure Mapping)
```csharp
namespace SmartTripPlanner.Infrastructure.ExternalServices.Foursquare.Mapping;

public static class FoursquareCategoryHeuristics
{
    public static (int TypicalDurationMinutes, bool IsIndoor, bool IsFamilyFriendly)
        Map(IEnumerable<FoursquareCategory> categories);
}
```
- Category IDs used for matching (more stable than names):
  - `10000` (Museum), `10035` (Art Gallery), `10014` (Theme Park) → 120 min
  - `10024` (Historic Site), `10025` (Monument), `10033` (Plaza), `10040` (Park) → 60 min
  - `13003` (Restaurant), `13002` (Cafe), `13004` (Food Court) → 90 min
  - `10008` (Nightclub), `10009` (Strip Club), `10010` (Adult) → `IsFamilyFriendly = false`
  - `10000` (Museum), `10035` (Art Gallery), any with indoor flag → `IsIndoor = true`
  - Default: 60 min, true, true

## Cascade Search Implementation (PlaceRepository Update)

### Updated PlaceRepository
```csharp
namespace SmartTripPlanner.Infrastructure.Repositories;

public class PlaceRepository : IPlaceRepository
{
    private readonly PlannerDbContext _context;
    private readonly IFoursquareApiClient _foursquareClient;

    public PlaceRepository(PlannerDbContext context, IFoursquareApiClient foursquareClient)
    {
        _context = context;
        _foursquareClient = foursquareClient;
    }

    public async Task<List<Place>> SearchAsync(string query, string cityId, int maxResults = 20)
    {
        // Paso A: Local DB
        var localResults = await _context.Places
            .Include(p => p.OpeningHours)
            .Where(p => p.Name.Contains(query) && p.CityId == cityId)
            .Take(maxResults)
            .ToListAsync();

        if (localResults.Count > 0)
            return localResults;

        // Paso B: Foursquare API fallback
        try
        {
            var fsqResults = await _foursquareClient.SearchPlacesAsync(query, cityId, maxResults);
            // Paso C: Map to domain entities (ephemeral)
            return fsqResults.Select(MapToPlace).ToList();
        }
        catch (HttpRequestException)
        {
            // Graceful degradation
            return new List<Place>();
        }
    }

    private Place MapToPlace(FoursquarePlace fsq)
    {
        var (duration, indoor, family) = FoursquareCategoryHeuristics.Map(fsq.Categories);
        return new Place(
            fsq.FsqId,
            fsq.Name,
            cityId,  // from the SearchAsync parameter context
            new PlaceLocation(fsq.Geocodes.Main.Latitude, fsq.Geocodes.Main.Longitude)
        )
        {
            TypicalDurationMinutes = duration,
            IsIndoor = indoor,
            IsFamilyFriendly = family,
            OpeningHours = fsq.Hours?.Regular?
                .Select(h => new OpeningHoursWindow(
                    (System.DayOfWeek)(h.Day - 1),  // Foursquare 1-7 → DayOfWeek 0-6
                    ParseMinutes(h.Open),
                    ParseMinutes(h.Close)
                )).ToList() ?? new List<OpeningHoursWindow>()
        };
    }
}
```

## DI Registration

In InfrastructureServiceRegistration:
```csharp
// Phase 1
services.AddScoped<IPlaceRepository, PlaceRepository>();

// Phase 2
services.AddOptions<FoursquareApiOptions>()
    .BindConfiguration(FoursquareApiOptions.SectionName)
    .ValidateDataAnnotations();

services.AddHttpClient<IFoursquareApiClient, FoursquareApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<FoursquareApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue(options.ApiKey);
});

// Register cascade services
services.AddScoped<FoursquareCategoryHeuristics>();
```

## File Manifest

### Phase 1 — Already created
1-13. All Phase 1 files exist and are committed.

### Phase 2 — Create
14. SmartTripPlanner.Infrastructure/Configuration/FoursquareApiOptions.cs
15. SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/IFoursquareApiClient.cs
16. SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/FoursquareApiClient.cs
17. SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/Models/FoursquarePlace.cs
18. SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/Models/FoursquareGeocodes.cs
19. SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/Models/FoursquareHours.cs
20. SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/Models/FoursquareCategory.cs
21. SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/Mapping/FoursquareCategoryHeuristics.cs
22. tests/SmartTripPlanner.Tests/SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/FoursquareApiClientTests.cs
23. tests/SmartTripPlanner.Tests/SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/FoursquareCategoryHeuristicsTests.cs
24. tests/SmartTripPlanner.Tests/SmartTripPlanner.Infrastructure/Repositories/PlaceRepositoryCascadeTests.cs

### Phase 2 — Modify
25. SmartTripPlanner.Infrastructure/Repositories/PlaceRepository.cs — inject IFoursquareApiClient, cascade SearchAsync
26. SmartTripPlanner.Infrastructure/InfrastructureServiceRegistration.cs — add options, HttpClient, cascade DI
27. SmartTripPlanner.API/Program.cs — no changes needed (InfrastructureServiceRegistration handles it)
28. SmartTripPlanner.API/appsettings.Development.json — add FoursquareApi section (or User Secrets)

## Test Strategy

### Domain Tests (Phase 1 — already passing)
- Pure unit, no mocking, validate entity/VO constructors and equality
- 44 existing tests must continue passing

### FoursquareApiClient Tests (Phase 2 — new)
- Use `HttpMessageHandler` mock (e.g., `MockHttpMessageHandler` from tests or manual `DelegatingHandler`)
- Test successful responses with valid JSON
- Test empty results (empty array response)
- Test HTTP error (404, 500) → graceful empty list
- Test authorization header present in all requests

### FoursquareCategoryHeuristics Tests (Phase 2 — new)
- Museum ID → 120 min + indoor
- Historic Site ID → 60 min
- Restaurant ID → 90 min
- Nightclub ID → not family-friendly
- Multiple categories → first match wins
- Empty categories → defaults
- Unknown category ID → defaults

### Cascade Repository Tests (Phase 2 — new or extended)
- PlaceRepositoryCascadeTests (separate file):
  - Local results exist → return local, no API call
  - No local results → API called, results mapped and returned
  - No local results + API error → empty returned gracefully
- `IPlaceRepository` interface unchanged — tests validate behavior, not implementation

## Risks
- OwnsMany may cause Include issues in EF Core — mitigated in Phase 1.
- InMemory provider doesn't support Contains correctly — use LINQ-to-Objects.
- Foursquare API contract may change — isolated in Infrastructure DTOs; only mapper needs updating.
- No real Foursquare API key in CI — all tests use `HttpMessageHandler` mocks; no real HTTP calls.
- `PlaceRepository` now has two responsibilities (DB + cascade orchestration) — acceptable for current scope; extract orchestration service if complexity grows.
