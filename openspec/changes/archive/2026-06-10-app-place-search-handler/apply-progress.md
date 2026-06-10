## Apply Progress: app-place-search-handler

**Status**: ✅ All 16 tasks complete — ready for verify.

### TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 1.1–1.4 | — (simple records, no tests needed) | Domain | ✅ 67/67 | — | — | — | — |
| 2.1–2.3 | — (csproj/DI/request setup) | Foundation | ✅ 67/67 | — | — | — | — |
| 3.1–3.3 | `SmartTripPlanner.ApplicationServices/Mapping/PlaceMappingProfileTests.cs` | Unit | ✅ 67/67 | ✅ Written | ✅ Passed | ✅ 4 cases (all fields, location, hours, config) | ✅ Shared PlaceFixture |
| 4.1–4.3 | `SmartTripPlanner.ApplicationServices/Handlers/SearchPlacesHandlerTests.cs` | Unit | ✅ 67/67 | ✅ Written | ✅ Passed | ✅ 5 scenarios (3 results, cascade, empty, null query, default max) | ✅ SetupEmptyMapper helper |
| 5.1–5.3 | — (project references) | Wiring | ✅ 76/76 | — | — | — | — |

### Test Results

```
Passed!  - Failed: 0, Passed: 76, Skipped: 0, Total: 76, Duration: 2s
```

- 67 baseline tests (Domain + Infrastructure) unchanged — all pass
- 9 new tests (4 mapping + 5 handler) — all pass

### Files Changed

**Created (11 files):**
| File | Description |
|------|-------------|
| `SmartTripPlanner.Domain/ApiModels/PlaceLocationModel.cs` | Lat/Lng record |
| `SmartTripPlanner.Domain/ApiModels/OpeningHoursWindowModel.cs` | Day/hours record |
| `SmartTripPlanner.Domain/ApiModels/PlaceModel.cs` | Full place model with nested Location |
| `SmartTripPlanner.Domain/ApiModels/PlaceSearchResponse.cs` | Response wrapper |
| `SmartTripPlanner.ApplicationServices/Requests/SearchPlaces.cs` | MediatR request record |
| `SmartTripPlanner.ApplicationServices/Handlers/SearchPlacesHandler.cs` | Handler injecting IPlaceRepository + IMapper |
| `SmartTripPlanner.ApplicationServices/Mapping/PlaceMappingProfile.cs` | AutoMapper Profile |
| `SmartTripPlanner.ApplicationServices/ApplicationServicesRegistration.cs` | IServiceCollection extension |
| `tests/SmartTripPlanner.Tests/Helpers/PlaceFixture.cs` | Shared test fixture |
| `tests/SmartTripPlanner.Tests/SmartTripPlanner.ApplicationServices/Mapping/PlaceMappingProfileTests.cs` | 4 mapping tests |
| `tests/SmartTripPlanner.Tests/SmartTripPlanner.ApplicationServices/Handlers/SearchPlacesHandlerTests.cs` | 5 handler tests |

**Modified (5 files):**
| File | Change |
|------|--------|
| `SmartTripPlanner.ApplicationServices/SmartTripPlanner.ApplicationServices.csproj` | MediatR + AutoMapper NuGet + Domain ref |
| `SmartTripPlanner.API/SmartTripPlanner.API.csproj` | ApplicationServices project ref |
| `SmartTripPlanner.API/Program.cs` | `AddApplicationServices()` call |
| `tests/SmartTripPlanner.Tests/SmartTripPlanner.Tests.csproj` | ApplicationServices ref + AutoMapper NuGet |
| `openspec/changes/app-place-search-handler/tasks.md` | All tasks marked [x] |

### Deviations from Design

None — implementation matches design. Nested `PlaceLocationModel` (design ADR #5) used over flat lat/lng (spec R2 cosmetic mismatch — deferred to archive).
