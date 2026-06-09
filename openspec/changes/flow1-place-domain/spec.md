# Specification: flow1-place-domain

## Overview
Implement the Place domain entity and its local repository for Flow 1 (Place Discovery) — Domain + Infrastructure only. This covers the local DB search step from the cascade pipeline.

## Functional Requirements

### FR1: Place Entity
- Inherits from `Entity` (long auto-generated Id).
- `PlaceId` (string, required) with unique index for domain lookup.
- `Name` (string, required).
- `CityId` (string, required, e.g. "madrid-es").
- `TypicalDurationMinutes` (int) — default 60.
- `IsIndoor` (bool) — default false.
- `IsFamilyFriendly` (bool) — default true.
- `Location` — `PlaceLocation` ValueObject (OwnsOne).
- `OpeningHours` — `List<OpeningHoursWindow>` (OwnsMany).

### FR2: OpeningHoursWindow ValueObject
- `DayOfWeek` (DayOfWeek).
- `OpenMinutes` (int, range 0-1439 inclusive).
- `CloseMinutes` (int, range 0-1439 inclusive).
- Validation: OpenMinutes <= CloseMinutes.
- Value equality based on all three properties.

### FR3: PlaceLocation ValueObject
- `Latitude` (double, range -90 to 90 inclusive).
- `Longitude` (double, range -180 to 180 inclusive).
- Value equality based on both coordinates.

### FR4: IPlaceRepository
```csharp
namespace SmartTripPlanner.Domain.Repository;

public interface IPlaceRepository : IRepository<Place>
{
    Task<List<Place>> SearchAsync(string query, string cityId, int maxResults = 20);
    Task<Place?> GetByPlaceIdAsync(string placeId);
}
```

### FR5: PlaceRepository (Infrastructure)
- EF Core implementation in `SmartTripPlanner.Infrastructure/Repositories/PlaceRepository.cs`.
- `PlaceConfiguration` in `SmartTripPlanner.Infrastructure/Configurations/PlaceConfiguration.cs`:
  - HasKey on Id (auto-generated).
  - Unique index on PlaceId.
  - OwnsOne for Location (columns: Location_Latitude, Location_Longitude).
  - OwnsMany for OpeningHours (separate table `PlaceOpeningHours`).
- Register `DbSet<Place>` in `PlannerDbContext`.
- Register `IPlaceRepository` in `InfrastructureServiceRegistration`.

## Non-Functional Requirements
- Strict TDD — tests define contracts before implementation.
- All existing tests must continue passing.
- No modifications to existing entities (Trip, City, DayPlan, etc.).
- Tests follow mirror directory structure under `tests/SmartTripPlanner.Tests/`.

## Acceptance Criteria

### AC1: Place Construction
- Creating a valid Place with minimal required fields succeeds.
- Creating a Place with null/empty PlaceId throws ArgumentNullException.
- Creating a Place with null/empty Name throws ArgumentNullException.
- Creating a Place with valid Location succeeds.
- Creating a Place with invalid Location (lat out of range) throws validation.

### AC2: OpeningHoursWindow Construction
- Creating with valid minutes succeeds.
- Creating with OpenMinutes > CloseMinutes throws ArgumentException.
- Creating with minutes < 0 throws ArgumentOutOfRangeException.
- Creating with minutes > 1439 throws ArgumentOutOfRangeException.
- Two instances with same values are equal.

### AC3: PlaceLocation Construction
- Creating with valid lat/lng succeeds.
- Creating with Latitude > 90 throws ArgumentOutOfRangeException.
- Creating with Longitude > 180 throws ArgumentOutOfRangeException.
- Two instances with same coordinates are equal.

### AC4: Repository Operations
- SearchAsync with matching query and cityId returns matching Places.
- SearchAsync with non-matching query returns empty list.
- SearchAsync filters by CityId correctly (same name, different city = no result).
- GetByPlaceIdAsync returns the correct Place by PlaceId.
- GetByPlaceIdAsync returns null when PlaceId doesn't exist.
- Saved Place preserves all properties including Location and OpeningHours when retrieved.

## Infrastructure Dependencies
- `Microsoft.EntityFrameworkCore.InMemory` package for infrastructure tests.
- `SmartTripPlanner.Tests` needs project reference to `SmartTripPlanner.Infrastructure`.
