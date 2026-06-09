# Design: flow1-place-domain

## Overview
Design and implement the Place domain entity, its ValueObjects (OpeningHoursWindow, PlaceLocation), the repository interface (IPlaceRepository), and the EF Core infrastructure (PlaceConfiguration, PlaceRepository). Strict TDD: domain tests first, infrastructure tests next, then implementation.

## Class Design

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

## Interfaces

### IPlaceRepository : IRepository<Place>
- SearchAsync(string query, string cityId, int maxResults = 20) -> List<Place>
- GetByPlaceIdAsync(string placeId) -> Place?

## EF Core Mapping (PlaceConfiguration)
- Table: Places
- Key: Id (ValueGeneratedOnAdd)
- Unique index on PlaceId
- Required: PlaceId, Name, CityId (max lengths: PlaceId 100, Name 200, CityId 50)
- OwnsOne: Location (columns: Location_Latitude, Location_Longitude)
- OwnsMany: OpeningHoursWindow (table: PlaceOpeningHours, shadow FK PlaceId, cascade delete)
- Default values: TypicalDurationMinutes=60, IsIndoor=false, IsFamilyFriendly=true

## DbContext Changes
- Add: `public DbSet<Place> Places { get; set; }`
- ApplyConfigurationsFromAssembly picks up PlaceConfiguration automatically
- Include Places.OpeningHours in queries (or EF Core auto-includes owned types)

## Repository Implementation (PlaceRepository)
- Constructor injects PlannerDbContext
- SearchAsync: `_context.Places.Where(p => p.Name.Contains(query) && p.CityId == cityId).Take(maxResults).ToListAsync()`
- GetByPlaceIdAsync: `_context.Places.FirstOrDefaultAsync(p => p.PlaceId == placeId)`

## DI Registration
In InfrastructureServiceRegistration:
```csharp
services.AddScoped<IPlaceRepository, PlaceRepository>();
```

## File Manifest

### Create
1. SmartTripPlanner.Domain/AggregatesModel/Place.cs
2. SmartTripPlanner.Domain/AggregatesModel/OpeningHoursWindow.cs
3. SmartTripPlanner.Domain/AggregatesModel/PlaceLocation.cs
4. SmartTripPlanner.Domain/Repository/IPlaceRepository.cs
5. SmartTripPlanner.Infrastructure/Repositories/PlaceRepository.cs
6. SmartTripPlanner.Infrastructure/Configurations/PlaceConfiguration.cs
7. tests/SmartTripPlanner.Tests/SmartTripPlanner.Domain/AggregatesModel/PlaceTests.cs
8. tests/SmartTripPlanner.Tests/SmartTripPlanner.Domain/AggregatesModel/OpeningHoursWindowTests.cs
9. tests/SmartTripPlanner.Tests/SmartTripPlanner.Domain/AggregatesModel/PlaceLocationTests.cs
10. tests/SmartTripPlanner.Tests/SmartTripPlanner.Infrastructure/Repositories/PlaceRepositoryTests.cs

### Modify
11. SmartTripPlanner.Infrastructure/PlannerDbContext.cs — add DbSet<Place>
12. SmartTripPlanner.Infrastructure/InfrastructureServiceRegistration.cs — register IPlaceRepository
13. tests/SmartTripPlanner.Tests/SmartTripPlanner.Tests.csproj — add Infrastructure project reference + EF Core InMemory

## Test Strategy
- Domain tests: pure unit, no mocking, validate each entity/VO's constructor and equality
- Infrastructure tests: EF Core InMemory, seed Place data, test SearchAsync and GetByPlaceIdAsync
- All existing tests must continue to pass
- Naming: Method_Scenario_Expected
- Manual try/catch for expected exceptions

## Risks
- OwnsMany may cause Include issues in EF Core — mitigate by testing with Include and without
- InMemory provider doesn't support Contains correctly — use LINQ-to-Objects after ToList or use EF.Functions.Like with caution
