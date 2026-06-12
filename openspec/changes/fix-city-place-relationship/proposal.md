# Proposal: Fix City and Place Relationship

## Intent
Resolve design inconsistencies between the `City` and `Place` entities. Replace redundant configuration-based city validation with database-driven validation using `ICityRepository`.

## Scope

### In Scope
- **Domain**: Add `IsAllowed` flag to `City` entity (default `true`).
- **Domain**: Add `City` navigation property to `Place` entity.
- **Application**: Remove `AllowedCities` from `PlaceSearchOptions`. Keep `MaxResults`.
- **Application**: Update `SearchPlacesRequestValidator` to use `ICityRepository` checking `IsAllowed`.
- **Infrastructure**: Create `CityRepository` implementing `ICityRepository`.
- **Infrastructure**: Update `PlaceConfiguration` to configure the FK relationship from `Place` to `City`.
- **Infrastructure**: Update `CityConfiguration` to configure `IsAllowed` column.
- **Infrastructure**: Register `ICityRepository` / `CityRepository` in DI.
- **Config**: Remove `AllowedCities` from appsettings files.
- **Tests**: Mock `ICityRepository` in `SearchPlacesRequestValidatorTests`.

### Out of Scope
- Seed data for cities.
- UI or frontend modifications.
- API contract changes (`cityId` as string/CityCode remains).

## Capabilities

### New Capabilities
None

### Modified Capabilities
- `api-places-search`: Replace configuration-based city validation with database lookup via `ICityRepository.GetByIdAsync` in `SearchPlacesRequestValidator`.
- `place`: Establish a navigation property and EF Core foreign key relationship from `Place` to `City` via `CityId` (CityCode).
- `city`: Add `IsAllowed` flag to control which cities are valid for place search.

## Approach
- **Clean Architecture Flow**: Domain -> ApplicationServices -> Infrastructure.
- **EF Core Relationship**: Configured as: `Place` has one `City` (via `CityId` FK), `City` has many `Places`.
- `CityId` in `Place` remains a string (mapped to `City.CityCode`, which is unique).
- Use `ICityRepository.GetByIdAsync(cityId)` in validator via `MustAsync`, checking that city exists AND `IsAllowed == true`.
- `City.IsAllowed` defaults to `true` — existing cities are allowed by default.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `SmartTripPlanner.Domain/AggregatesModel/City.cs` | Modified | Add `IsAllowed` property. |
| `SmartTripPlanner.Domain/AggregatesModel/Place.cs` | Modified | Add `City` navigation property. |
| `SmartTripPlanner.ApplicationServices/Configurations/PlaceSearchOptions.cs` | Modified | Remove `AllowedCities` list. |
| `SmartTripPlanner.ApplicationServices/Validators/SearchPlacesRequestValidator.cs` | Modified | Inject `ICityRepository`, use `MustAsync` for city validation. |
| `SmartTripPlanner.ApplicationServices/ApplicationServicesRegistration.cs` | Modified | No direct change, but verify application layer registrations. |
| `SmartTripPlanner.Infrastructure/Repositories/CityRepository.cs` | New | Implement `ICityRepository` fetching by `CityCode`. |
| `SmartTripPlanner.Infrastructure/Configurations/PlaceConfiguration.cs` | Modified | Add relationship mapping for `Place` to `City`. |
| `SmartTripPlanner.Infrastructure/Configurations/CityConfiguration.cs` | Modified | Add `IsAllowed` column config. |
| `SmartTripPlanner.Infrastructure/InfrastructureServiceRegistration.cs` | Modified | Register `CityRepository` as `ICityRepository`. |
| `SmartTripPlanner.API/appsettings.json` | Modified | Remove `"AllowedCities"` property. |
| `tests/.../SearchPlacesRequestValidatorTests.cs` | Modified | Mock `ICityRepository` and test validation rules. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Missing DI Registration | Low | DI validation test or Startup test. |
| DB Referential Integrity Violations | Low | Ensure existing Places map to existing Cities. |

## Rollback Plan
- Revert Git commits.
- Restore `AllowedCities` configuration in `PlaceSearchOptions` and appsettings.
- Remove `CityRepository` class and DI registration.
- Revert `PlaceConfiguration` relationship.

## Success Criteria
- [ ] All unit and integration tests pass successfully.
- [ ] Validator verifies city existence AND `IsAllowed == true` via `ICityRepository.GetByIdAsync`.
- [ ] EF Core relationship `Place` -> `City` is configured and validated.
- [ ] `City.IsAllowed` flag is persisted and defaults to `true`.
