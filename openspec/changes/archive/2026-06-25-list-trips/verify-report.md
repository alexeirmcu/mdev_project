# Verify Report: List Trips

**Date**: 2026-06-25
**Status**: PASS (with 1 warning, 2 suggestions)
**Test count**: 17/17 passed (0 failed, 0 skipped)

---

## 1. Requirements Coverage

### R1 — GET /api/trips returns owner-scoped trip summaries

| Criterion | Result | Evidence |
|-----------|--------|----------|
| `GET /api/trips` returns `200 OK` with `TripSummaryResponse[]` | ✅ PASS | `TripsController.cs` L92-104: `[HttpGet]` action returns `Ok(result)` |
| `[Authorize]` applied | ✅ PASS | Class-level `[Authorize]` on `TripsController` (L10) |
| Handler calls `ListAsync(ownerUserId, cityId, startDate, endDate, ct)` | ✅ PASS | `ListTripsHandler.cs` L35-40 — passes `userContext.UserId` and all optional filters |
| All trip statuses included | ✅ PASS | No status filtering in handler — all results from `ListAsync` returned |
| CREATED trips with 0 counts | ✅ PASS | AutoMapper flattens days: empty `Days` → both counts = 0. Test: `Handle_CreatedTrip_ReturnsZeroCounts` |
| User with trips receives 200 + summaries | ✅ PASS | `Handle_NoFilters_ReturnsAllOwnerTrips` |
| User with no trips receives `[]` | ✅ PASS | `Handle_EmptyList_ReturnsEmptyResult` |
| Other owner's trips invisible | ✅ PASS | `Handle_OtherOwnerTrips_NotIncluded` — verifies repo called with correct `UserId` |

### R2 — Optional cityCode filter resolves to cityId

| Criterion | Result | Evidence |
|-----------|--------|----------|
| `cityCode` accepted as string query param | ✅ PASS | `[FromQuery] string? cityCode` in controller (L96) |
| Handler resolves via `ICityRepository.GetByCodeAsync` | ✅ PASS | `ListTripsHandler.cs` L25 |
| Found → pass `cityId` to `ListAsync` | ✅ PASS | `ListTripsHandler.cs` L32 passes `city.Id` |
| Not found → return `[]` without calling `ListAsync` (short-circuit) | ✅ PASS | `ListTripsHandler.cs` L27-30 returns empty list. Test: `Handle_CityCodeNotFound_ReturnsEmptyList` verifies `Times.Never` on `ListAsync` |
| Valid cityCode returns matching trips | ✅ PASS | `Handle_WithValidCityCode_FiltersByCity` |
| cityCode omitted → `cityId = null` | ✅ PASS | `Handle_NoFilters_ReturnsAllOwnerTrips` |

### R3 — Optional startDate / endDate filter by date range

| Criterion | Result | Evidence |
|-----------|--------|----------|
| `startDate` accepted as `DateOnly?` query param | ✅ PASS | Controller L97: `[FromQuery] DateOnly? startDate` |
| `endDate` accepted as `DateOnly?` query param | ✅ PASS | Controller L98: `[FromQuery] DateOnly? endDate` |
| Both optional independently | ✅ PASS | Validator tests: `StartDateOnly_Passes`, `EndDateOnly_Passes` |
| Dates passed to `ListAsync` | ✅ PASS | `Handle_WithDateFilters_PassesDatesToListAsync` |

### R4 — Combined filters applied together

| Criterion | Result | Evidence |
|-----------|--------|----------|
| All params passed conjunctively | ✅ PASS | Handler passes all resolved params to single `ListAsync` call |
| Combined scenario covered | ✅ PASS | Controller test `ListTrips_ReturnsOkWithResponse` uses all 3 params; individual handler params validated separately |

### R5 — Invalid query parameters return 422

| Criterion | Result | Evidence |
|-----------|--------|----------|
| `[ProducesResponseType(422)]` declared on action | ✅ PASS | Controller L94: `[ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]` |
| Validator checks StartDate ≤ EndDate | ✅ PASS | `ListTripsQueryValidator.cs` L10-13 |
| Invalid date format → 422 | ⚠️ WARNING | See finding below |

**⚠️ WARNING — R5 implementation gap**: The spec requires invalid date formats (e.g. `startDate=not-a-date`) to return `422 Unprocessable Entity`. However, ASP.NET model binding fails BEFORE FluentValidation runs for type conversion errors on `DateOnly?`, returning **400 Bad Request** by default. The implementation does not include custom middleware or model binding error handling to convert type conversion failures to 422. This creates a mismatch between the declared API contract (422) and actual behavior (400).

### R6 — TripSummaryResponse fixes CityId to long and adds CityCode

| Criterion | Result | Evidence |
|-----------|--------|----------|
| `CityId` changed from `string` to `long` | ✅ PASS | `TripSummaryResponse.cs` L5: `long CityId` |
| `CityCode` added as `string` | ✅ PASS | `TripSummaryResponse.cs` L6: `string CityCode` |
| AutoMapper maps `Trip.CityId` | ✅ PASS | AutoMapper, automatic by naming convention (`long CityId` ↔ `Trip.CityId`) |
| AutoMapper maps `Trip.City.CityCode` | ✅ PASS | `AutoMapperProfile.cs` L30: `ForCtorParam("CityCode", ... => src.City.CityCode)` |
| Correct JSON types in response | ✅ PASS | `endpoints.yaml` L425-429: `cityId` → `int64`, `cityCode` → `string` |

### API Contract (endpoints.yaml)

| Criterion | Result | Evidence |
|-----------|--------|----------|
| Path `/trips` matches controller route | ✅ PASS | Controller: `[Route("api/trips")]` + `[HttpGet]` |
| Query params: cityCode, startDate, endDate | ✅ PASS | Matches controller binding |
| 200 → `TripSummaryResponse[]` | ✅ PASS | `[ProducesResponseType(typeof(List<TripSummaryResponse>), 200)]` |
| 422 declared | ✅ PASS | `[ProducesResponseType(422)]` declared |
| `TripSummaryResponse` schema matches record | ✅ PASS | All 9 fields match (tripId, cityId, cityCode, cityName, startDate, endDate, totalMustSees, completedActivitiesCount, totalActivitiesCount) |

---

## 2. Test Coverage

### Handler Tests (9 tests) — ✅ ALL PASS

| Test | Covers Spec Scenario | Result |
|------|---------------------|--------|
| `Handle_NoFilters_ReturnsAllOwnerTrips` | R1 — User with trips / R2 — cityCode omitted | ✅ PASS |
| `Handle_WithValidCityCode_FiltersByCity` | R2 — Filter by valid cityCode | ✅ PASS |
| `Handle_CityCodeNotFound_ReturnsEmptyList` | R2 — cityCode not found (short-circuit verified) | ✅ PASS |
| `Handle_EmptyList_ReturnsEmptyResult` | R1 — User with no trips | ✅ PASS |
| `Handle_WithDateFilters_PassesDatesToListAsync` | R3 — Date filters passed to repo | ✅ PASS |
| `Handle_CreatedTrip_ReturnsZeroCounts` | R1 — CREATED trips with counts = 0 | ✅ PASS |
| `Handle_GeneratedTrip_ComputesActivityCounts` | R1 — Activity counts computed correctly | ✅ PASS |
| `Handle_GeneratedTrip_TotalMustSees` | R1 — TotalMustSees computed correctly | ✅ PASS |
| `Handle_OtherOwnerTrips_NotIncluded` | R1 — Other owner's trips invisible | ✅ PASS |

### Validator Tests (6 tests) — ✅ ALL PASS

| Test | Covers Spec Scenario | Result |
|------|---------------------|--------|
| `NoFilters_Passes` | R3 — all optional | ✅ PASS |
| `StartDateOnly_Passes` | R3 — startDate without endDate | ✅ PASS |
| `EndDateOnly_Passes` | R3 — endDate without startDate | ✅ PASS |
| `StartDateEqualsEndDate_Passes` | R3 — edge: equal dates | ✅ PASS |
| `StartDateBeforeEndDate_Passes` | R3 — valid range | ✅ PASS |
| `StartDateAfterEndDate_Fails` | R5 — invalid range | ✅ PASS |

### Controller Tests (2 ListTrips-specific) — ✅ ALL PASS

| Test | Covers Spec Scenario | Result |
|------|---------------------|--------|
| `ListTrips_ReturnsOkWithResponse` | R1 — 200 with body, all params passed | ✅ PASS |
| `ListTrips_WithoutFilters_PassesNullsToQuery` | R2 — no filters → nulls sent to handler | ✅ PASS |

---

## 3. Edge Case Verification

| Edge Case | Status | Notes |
|-----------|--------|-------|
| Empty list | ✅ Covered | Handler test: `Handle_EmptyList_ReturnsEmptyResult` |
| CREATED trips with 0 counts | ✅ Covered | Handler test: `Handle_CreatedTrip_ReturnsZeroCounts` + AutoMapper handles empty Days |
| cityCode not found → short-circuit | ✅ Covered | Handler test: `Handle_CityCodeNotFound_ReturnsEmptyList` — verifies `Times.Never` on repo |
| Invalid date range (start > end) | ✅ Covered | Validator test: `StartDateAfterEndDate_Fails` |
| Other owner excluded | ✅ Covered | Handler test: `Handle_OtherOwnerTrips_NotIncluded` |
| Invalid date format | ⚠️ GAP | See R5 warning |

---

## 4. Detailed Findings

### WARNING

**W1 (R5) — Invalid date format returns 400 instead of 422**

The spec (R5) mandates `422 Unprocessable Entity` with `ValidationResult[]` when parameters fail format validation. Currently, if a caller passes `startDate=not-a-date`, ASP.NET model binding fails during `DateOnly?` conversion and returns **400 Bad Request**, never reaching the FluentValidation pipeline. No custom middleware or error handling exists to convert this to 422.

**Impact**: The OpenAPI contract advertises 422, but consumers receive 400 for invalid date formats.

**Fix options**:
1. Add a custom `ModelBindingException` middleware or action filter that catches format errors and returns 422 with proper `ValidationResult[]`.
2. Accept the 400 behavior and update the API contract to match.

### SUGGESTIONS

**S1 — Design deviation: computed fields in AutoMapper vs handler**

The design document (`design.md`) describes computed activity counts as "handler sets after AutoMapper map" — post-processing in the handler. The implementation places them in `AutoMapperProfile.cs` via `ForCtorParam`. Both approaches work; the chosen approach is cleaner (single mapping layer). Not a defect, but the design should be updated for accuracy.

**S2 — No dedicated combined-filters handler test**

The controller test `ListTrips_ReturnsOkWithResponse` exercises all 3 query params together, and handler tests exercise each param individually. There's no handler-level test that verifies all three params combined. The combination works implicitly (all params passed to `ListAsync`), but a dedicated test would strengthen coverage for the R4 scenario explicitly.

---

## 5. Summary

| Area | Verdict |
|------|---------|
| Requirements coverage | ✅ 6/6 requirements covered |
| Scenarios covered | ✅ 13/14 scenarios covered (1 gap: invalid date format → 422) |
| Test pass rate | ✅ 17/17 tests pass |
| API contract match | ⚠️ Partial — 422 declared but not implemented for format errors |
| Code quality | ✅ Clean, follows CQRS pattern, controller thinness, proper DI |

**Overall: PASS — 1 warning, 2 suggestions**

**Next**: `sdd-archive` — run after resolving the R5 422-vs-400 gap or accepting the drift.
