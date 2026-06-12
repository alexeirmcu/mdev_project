# Tasks: Fix City and Place Relationship

## Review Workload Forecast
- Changed lines estimate: ~250
- 400-line budget risk: Low
- Decision needed before apply: No

## Task List

| # | Task | Files | Effort | Dependencies |
|---|------|-------|--------|-------------|
| 1 | Add `IsAllowed` flag to City entity | `City.cs`, `CityConfiguration.cs` | S | [x] |
| 2 | Create CityRepository | `CityRepository.cs`, `InfrastructureServiceRegistration.cs` | S | [x] |
| 3 | Add City navigation property to Place | `Place.cs`, `PlaceConfiguration.cs` | S | [x] |
| 4 | Remove AllowedCities from config & options | `PlaceSearchOptions.cs`, `appsettings.json`, `appsettings.Development.json` | S | [x] |
| 5 | Update validator to use ICityRepository | `SearchPlacesRequestValidator.cs` | M | [x] |
| 6 | Update tests | `SearchPlacesRequestValidatorTests.cs` | M | [x] |
| 7 | New migration for IsAllowed + FK | EF migration | S | [x] |

### Task 1: Add `IsAllowed` flag to City entity

#### Description
Add an `IsAllowed` boolean property to the `City` domain entity that controls whether the city is valid for place search. Default to `true` so existing data works without migration changes.

#### Files to modify
- `SmartTripPlanner.Domain/AggregatesModel/City.cs` — Add `IsAllowed` property, update constructor
- `SmartTripPlanner.Infrastructure/Configurations/CityConfiguration.cs` — Add `IsAllowed` column config with `.HasDefaultValue(true)`

#### Acceptance Criteria
- [ ] `City` has `IsAllowed` property defaulting to `true`
- [ ] EF Core config sets `.HasDefaultValue(true)` for `IsAllowed`
- [ ] Build succeeds

### Task 2: Create CityRepository

#### Description
Implement `ICityRepository` with `GetByIdAsync(string cityId)` that queries `Cities` by `CityCode` AND `IsAllowed == true`. Register in DI.

#### Files to modify
- `SmartTripPlanner.Domain/Repository/ICityRepository.cs` — Update XML doc to clarify it filters by IsAllowed
- `SmartTripPlanner.Infrastructure/Repositories/CityRepository.cs` — New file implementing ICityRepository
- `SmartTripPlanner.Infrastructure/InfrastructureServiceRegistration.cs` — Register `ICityRepository` / `CityRepository` as scoped

#### Acceptance Criteria
- [ ] `GetByIdAsync("madrid-es")` returns City only if `CityCode == "madrid-es"` AND `IsAllowed == true`
- [ ] `GetByIdAsync("non-existent")` returns null
- [ ] `GetByIdAsync("disabled-city")` returns null if `IsAllowed == false`
- [ ] `CityRepository` is registered in DI as scoped

### Task 3: Add City navigation property to Place

#### Description
Add a `City` navigation property to `Place` and configure the EF Core relationship: Place has one City via `CityId` FK → `City.CityCode`, City has many Places.

#### Files to modify
- `SmartTripPlanner.Domain/AggregatesModel/Place.cs` — Add `public City? City { get; private set; }` navigation property
- `SmartTripPlanner.Infrastructure/Configurations/PlaceConfiguration.cs` — Add `HasOne(p => p.City).WithMany(c => c.Places).HasForeignKey(p => p.CityId).HasPrincipalKey(c => c.CityCode)`

#### Notes
- Add `Places` collection navigation to City entity: `public ICollection<Place> Places { get; private set; } = new List<Place>();`
- The FK uses `City.CityCode` (unique) as the principal key, not the entity PK `Id`

#### Acceptance Criteria
- [ ] `Place` has `City?` navigation property
- [ ] `City` has `ICollection<Place> Places` navigation property
- [ ] EF FK relationship is configured using `CityCode` as principal key
- [ ] Build succeeds

### Task 4: Remove AllowedCities from config & options

#### Description
Remove `AllowedCities` from `PlaceSearchOptions` and both `appsettings.json` files. Keep `MaxResults`.

#### Files to modify
- `SmartTripPlanner.ApplicationServices/Configurations/PlaceSearchOptions.cs` — Remove `AllowedCities` property
- `SmartTripPlanner.API/appsettings.json` — Remove `"AllowedCities"` from `PlaceSearch` section
- `SmartTripPlanner.API/appsettings.Development.json` — Remove `"AllowedCities"` from `PlaceSearch` section

#### Acceptance Criteria
- [ ] `PlaceSearchOptions` only has `MaxResults`
- [ ] No `AllowedCities` references in config files
- [ ] Build succeeds

### Task 5: Update validator to use ICityRepository

#### Description
Change `SearchPlacesRequestValidator` from injecting `IOptions<PlaceSearchOptions>` for city validation to injecting `ICityRepository` and using `MustAsync` to check city existence + `IsAllowed`. Keep `IOptions<PlaceSearchOptions>` for `MaxResults` validation only.

#### Files to modify
- `SmartTripPlanner.ApplicationServices/Validators/SearchPlacesRequestValidator.cs`

#### Implementation details
- Inject both `ICityRepository` and `IOptions<PlaceSearchOptions>` (latter for MaxResults only)
- Replace `.Must(cityId => opts.AllowedCities.Contains(cityId))` with `.MustAsync(async (cityId, ct) => { var city = await _cityRepo.GetByIdAsync(cityId); return city is not null; })`
- Keep error message generic: `"City '{cityId}' is not supported."` — do NOT expose whether city exists or is not allowed
- Add `using SmartTripPlanner.Domain.Repository;`

#### Acceptance Criteria
- [ ] City validation uses `ICityRepository.GetByIdAsync()`
- [ ] `MaxResults` validation still uses `PlaceSearchOptions.MaxResults`
- [ ] Empty city → `REQUIRED_FIELD`
- [ ] Non-existent city → `INVALID_CITY`
- [ ] City with `IsAllowed = false` → `INVALID_CITY`
- [ ] Valid city (exists + allowed) → passes
- [ ] Error message does not distinguish between "not found" and "not allowed"

### Task 6: Update tests

#### Description
Update `SearchPlacesRequestValidatorTests` to mock `ICityRepository` instead of using `IOptions<PlaceSearchOptions>` for city validation.

#### Files to modify
- `tests/.../SearchPlacesRequestValidatorTests.cs`

#### Implementation
- Remove `PlaceSearchOptions` with `AllowedCities`
- Add `Mock<ICityRepository>` setup
- Constructor injects both `ICityRepository` and `IOptions<PlaceSearchOptions>` (for MaxResults)
- Keep existing test cases updated for new approach:
  - Valid request → city returns City object
  - Empty CityId → `REQUIRED_FIELD`
  - City not found → `INVALID_CITY`
  - City found but IsAllowed=false → `INVALID_CITY`
  - Query/MaxResults tests unchanged
- Use `FluentValidation.TestHelper` as before

#### Acceptance Criteria
- [ ] All 8 existing test cases pass with new mock setup
- [ ] New test: "CityExistsButNotAllowed_Fails_WithInvalidCity"
- [ ] New test: "CityDoesNotExist_Fails_WithInvalidCity"
- [ ] Build succeeds, all tests green

### Task 7: New migration for IsAllowed + FK

#### Description
Create a new EF Core migration that adds the `IsAllowed` column to `Cities` table (default `true`) and configures the FK relationship between `Places` and `Cities`.

#### Command
```powershell
dotnet ef migrations add AddCityIsAllowedAndPlaceCityFk --project SmartTripPlanner.Infrastructure --startup-project SmartTripPlanner.API
```

#### Acceptance Criteria
- [ ] Migration adds `IsAllowed` column to `Cities` table with default value `true`
- [ ] Migration adds FK `Place.CityId` → `City.CityCode`
- [ ] `dotnet build` succeeds
- [ ] `dotnet ef database update` succeeds (against local DB)
