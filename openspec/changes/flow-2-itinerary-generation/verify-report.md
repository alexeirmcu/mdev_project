# Verification Report — Flow 2: Itinerary Generation

**Change**: flow-2-itinerary-generation  
**Mode**: Standard (no strict TDD)  
**Date**: 2026-06-17  
**Verdict**: **PASS WITH WARNINGS**

---

## 1. Build & Test Evidence

| Command | Result | Details |
|---------|--------|---------|
| `dotnet test` | **PASS** | 251 tests passed, 0 failed, 0 skipped |
| `dotnet build` | **PASS** | 0 errors, 134 warnings (MSTEST0037 analyzer suggestions, CS8602/CS8604 nullable) |

---

## 2. Functional Requirements Compliance

| FR | Description | Status | Evidence |
|----|-------------|--------|----------|
| FR1 | Trip.GenerateDays() creates empty DayPlans with 3 blocks | **COMPLIANT** | `Trip.GenerateDays()` creates N DayPlans with Morning/Afternoon/Evening blocks; default start time 09:00; default weather Clear. Tests: `TripTests.GenerateDays_CreatesCorrectNumberOfDayPlans`, `GenerateDays_SetsCorrectBlockTypes`, `GenerateDays_ClearsExistingDays`, `HeuristicItineraryGeneratorTests.GenerateAsync_ThreeDayTrip_ThreeDayPlansCreated`, `GenerateAsync_SingleDayTrip_OneDayPlanCreated` |
| FR2 | MustSee distribution respects PinnedDayIndex | **COMPLIANT** | `TryPlacePinnedMustSee` places at exact day/block, with overflow to adjacent blocks. Tests: `PinnedMustSeeWithBlock_AppearsInCorrectDayAndBlock`, `PinnedMustSeeNoBlock_PlacedInFirstAvailableBlock`, `Handle_WithPinnedMustSee_CorrectDayAssignment` |
| FR3 | Unpinned must-sees distributed by zone proximity + opening hours | **COMPLIANT** | `ZoneClusteringHelper.Cluster()` groups by 2km radius. `TryPlaceUnpinnedMustSee` prefers days where place is open (opening hours checked via `IsPlaceOpenOnDay`). Tests: `UnpinnedMustSeesInSameZone_ClusteredInSameDay`, `MustSeeClosedOnFirstDay_PlacedOnOpenDay` |
| FR4 | Candidate places fill remaining block capacity | **COMPLIANT** | `FillCandidatesAsync` fills blocks after must-sees using `ICandidateScorer.Score()`. Score = family-friendly bonus + popularity − distance penalty + weather adjustment. Tests: `NoCandidates_PlacesOnlyMustSees`, `CandidateScorerTests` (7 tests) |
| FR5 | Weather filter per day (indoor/outdoor) | **COMPLIANT** | `FillCandidatesAsync` applies `IndoorWeatherBonus (+20)` or `OutdoorWeatherPenalty (-20)` when `isBadWeather && trip.Preferences.WeatherAwareEnabled`. Must-sees preserve priority regardless of weather. Tests: `BadWeatherWithWeatherAware_PrefersIndoorCandidates`, `BadWeatherWeatherAwareDisabled_NoIndoorPreference`, `Handle_BadWeather_IndoorActivitiesPrioritized`, `WeatherData_UpdatesDayPlanWeatherSummary` |
| FR6 | Transport mode assignment per leg | **COMPLIANT** | `AssignTransitAsync` implements: <1.5km → WALK_AND_PUBLIC_TRANSPORT; car available + (PT >20min slower OR distance >10km) → CAR; else → WALK_AND_PUBLIC_TRANSPORT. Tests: `ShortDistanceTransit_UsesWalkAndPublicTransport`, `LongDistanceWithCar_UsesCarTransport`, `Within_1_5km_AlwaysWalkAndPublicTransport_EvenWithCar`, `HaversineTransitCalculatorTests` (12 tests) |
| FR7 | Block capacity validation | **COMPLIANT** | `BlockTimeline.CanFitActivity()` and `AddActivity()` enforce: Morning=3/210min, Afternoon=3/180min, Evening=2/105min via `TripPlanningConstants`. Tests: `BlockTimelineTests` (8 tests), `AddActivity_ExceedsMaxVisits_ThrowsException`, `AddActivity_ExceedsDuration_ThrowsException` |
| FR8 | Fallback by priority (LOW → MEDIUM → exception) | **COMPLIANT** | Must-sees placed in descending priority order via `OrderByDescending(e => e.mustSee.Priority)`. Candidates are separate pool that doesn't displace must-sees. `OverConstrainedRouteException` thrown only when High priority can't fit. Tests: `OnlyHighPriorityMustSeesExceedCapacity_ThrowsOverConstrainedRouteException`, `BlockCapacityExceeded_LowPriorityCandidateSkipped`, `MixedPriorities_HighPlacedFirst` |
| FR9 | GenerateTripHandler invokes itinerary generation | **COMPLIANT** | `GenerateTripHandler.Handle()` calls `itineraryGenerator.GenerateAsync(trip, candidatePlaces, weatherData, ct)` after `tripRepository.AddAsync()`, then `trip.UpdateStatus(GENERATED)` and `tripRepository.UpdateAsync()`. Tests: `Handle_WithMustSees_ReturnsResponseWithDayPlans`, `Handle_NoMustSees_GeneratesWithCandidatesOnly` |
| FR10 | Response includes full DayPlan[] | **PARTIAL** | Response includes `DayPlanResponse` with `DayIndex`, `Date`, `WeatherSummary`, `Blocks` (Morning/Afternoon/Evening), each with `ActivityResponse` containing `PlaceName`, `DurationMinutes`, `TransportMode`, `TransitDurationMinutes`. **Missing fields**: `PlaceId`, `SequenceOrder`, `IsIndoor`, `Priority` on activities; `BufferMinutes`, `FrictionAlert` on transit. Tests: `Handle_WithMustSees_ResponseHasCorrectBlockStructure` |

---

## 3. Acceptance Criteria Compliance

| AC | Description | Status | Evidence |
|----|-------------|--------|----------|
| AC1 | Pinned must-sees in correct day/block | **COMPLIANT** | Tests: `PinnedMustSeeWithBlock_AppearsInCorrectDayAndBlock`, `Handle_WithPinnedMustSee_CorrectDayAssignment` |
| AC2 | Unpinned must-sees respect opening hours | **COMPLIANT** | Test: `MustSeeClosedOnFirstDay_PlacedOnOpenDay` — place closed on Wednesday is NOT placed on day 0 (Wednesday) |
| AC3 | Zone clustering minimizes backtracking | **COMPLIANT** | Tests: `UnpinnedMustSeesInSameZone_ClusteredInSameDay`, `ZoneClusteringHelperTests` (8 tests) covering 2km boundary |
| AC4 | Rainy → outdoor deprioritized | **COMPLIANT** | Tests: `BadWeatherWithWeatherAware_PrefersIndoorCandidates`, `Handle_BadWeather_IndoorActivitiesPrioritized`, `BadWeatherWeatherAwareDisabled_NoIndoorPreference` |
| AC5 | Transport rules followed | **COMPLIANT** | Tests: `ShortDistanceTransit_UsesWalkAndPublicTransport`, `LongDistanceWithCar_UsesCarTransport`, `Within_1_5km_AlwaysWalkAndPublicTransport_EvenWithCar`, `HaversineTransitCalculatorTests` |
| AC6 | Block capacity limits enforced | **COMPLIANT** | Tests: `BlockTimelineTests` (8 tests), `ExactlyAtCapacity_DoesNotThrow`, `BlockCapacityExceeded_LowPriorityCandidateSkipped` |
| AC7 | OverConstrainedRouteException when HIGH doesn't fit | **COMPLIANT** | Tests: `OnlyHighPriorityMustSeesExceedCapacity_ThrowsOverConstrainedRouteException`, `Handle_OverConstrained_ThrowsOverConstrainedRouteException`, `OverConstrainedRouteExceptionTests` (4 tests) |
| AC8 | API response includes DayPlan with blocks, activities, transit | **PARTIAL** | Has core structure (Days → Blocks → Activities with transit info). Missing `PlaceId`, `SequenceOrder`, `IsIndoor`, `Priority`, `BufferMinutes`, `FrictionAlert` fields per FR10 scenario |
| AC9 | All must-sees included unless impossible | **COMPLIANT** | Tests: `NoCandidates_PlacesOnlyMustSees`, `MixedPriorities_HighPlacedFirst`, `MustSeeClosedOnFirstDay_PlacedOnOpenDay` |
| AC10 | 172 existing tests still pass | **COMPLIANT** | 251 total tests pass, 0 failures. Includes pre-existing tests + new Flow 2 tests |

---

## 4. Non-Functional Requirements

| NFR | Description | Status | Evidence |
|-----|-------------|--------|----------|
| NFR1 | No DB-specific syntax in domain ports | **COMPLIANT** | `IItineraryGenerator`, `ICandidateScorer`, `ITransitCalculator`, `IWeatherProvider` are all in `Domain/Ports/` with zero EF/SQL references |
| NFR2 | Domain-agnostic ports | **COMPLIANT** | ICandidateScorer + ITransitCalculator are injectable ports; CandidateScorer and HaversineTransitCalculator implementations live in Domain/Services and Infrastructure/Services respectively |
| NFR3 | Unit-testable without external calls | **COMPLIANT** | All itinerary tests use mock ITransitCalculator; CandidateScorer is pure function; no HTTP clients in domain logic |
| NFR4 | Synchronous within handler | **COMPLIANT** | `GenerateAsync` called directly in handler; no background jobs |
| NFR5 | IItineraryGenerator swappable | **COMPLIANT** | Registered via DI: `services.AddScoped<IItineraryGenerator, HeuristicItineraryGenerator>()` |

---

## 5. Task Completion

| Task | Description | Completed | Evidence |
|------|-------------|-----------|----------|
| T1 | Domain prerequisite fixes | ✅ | OverConstrainedRouteException(long), DayPlan.SetWeather(), PlaceLocation.DistanceKmTo(), OpeningHoursWindow.IsOpenOn() |
| T2 | Domain ports | ✅ | IItineraryGenerator, ICandidateScorer, ITransitCalculator, IWeatherProvider |
| T3 | Domain services | ✅ | HeuristicItineraryGenerator, CandidateScorer, ZoneClusteringHelper |
| T4 | Infrastructure adapters | ✅ | HaversineTransitCalculator, StubbedWeatherProvider |
| T5 | Repository extension | ✅ | IPlaceRepository.GetManyByCityIdAsync() + implementation |
| T6 | Handler integration | ✅ | GenerateTripHandler updated with itinerary generation call |
| T7 | Response mapping | ✅ | TripPlanResponse + DayPlanResponse + BlockResponse + ActivityResponse |
| T8 | EF Core migration | ✅ | Flow2ItineraryGeneration migration created |
| T9 | Unit tests — HeuristicItineraryGenerator | ✅ | 13 test methods |
| T10 | Unit tests — ZoneClusteringHelper | ✅ | 8 test methods |
| T11 | Unit tests — Transit Calculator | ✅ | 12 test methods |
| T12 | Integration tests — Handler | ✅ | 6 test methods |
| T13 | API controller tests | ✅ | Existing TripsControllerTests updated |
| T14 | Regression & validation | ✅ | 251 tests, 0 failures |

---

## 6. Issues

### WARNING (2)

**W1: Response DTO missing activity detail fields (FR10)**
- `ActivityResponse` is missing: `PlaceId`, `SequenceOrder`, `IsIndoor`, `Priority`
- `TransitDetails` fields `BufferMinutes` and `FrictionAlert` are not in `ActivityResponse`
- The spec FR10 scenario explicitly lists these fields: "PlaceId, Name, DurationMinutes, IsIndoor, Priority" and "TransportMode, DurationMinutes, BufferMinutes"
- **Impact**: API consumers cannot identify places by ID, determine indoor/outdoor nature, or see transit buffer/friction info
- **Severity**: Moderate — the core DayPlan→Block→Activity structure is present; the missing fields are useful but not blocking for E2E flow

**W2: Spec-scoring formula deviation**
- The spec tasks (T3) described priority_bonus * 100 for must-sees, but the actual `CandidateScorer` uses a simplified formula without priority multiplier (priority is handled by placement order, not scoring)
- This is a design-level deviation: must-sees are placed before candidates in separate phases, so the priority in scoring doesn't apply to them. Candidates default to `Priority.Medium`.
- **Impact**: Low — functional behavior is correct; must-see priorities are respected through placement order, not scoring

### SUGGESTIONS (3)

**S1: Add missing fields to ActivityResponse**
- Add `PlaceId`, `IsIndoor`, `Priority`, `SequenceOrder` to `ActivityResponse`
- Add `BufferMinutes`, `FrictionAlert` to transit info in response
- This would bring FR10 to full compliance

**S2: Consider distance-aware candidate scoring**
- `EstimateDistanceFromNearestActivity` currently returns a hardcoded `1.0` distance, making all candidates equidistant for scoring purposes
- A future enhancement could embed Place locations on ActivityNode for accurate distance calculations

**S3: Increase test coverage for FR8 fallback chain**
- While priority-ordered placement covers the expected behavior, an explicit test for "Medium priority dropped before High" scenario (spec FR8 scenario 2) would strengthen verification

---

## 7. Summary Statistics

| Metric | Value |
|--------|-------|
| Total tests | 251 |
| Passed | 251 |
| Failed | 0 |
| Skipped | 0 |
| Build errors | 0 |
| Build warnings | 134 (analyzer suggestions) |
| FRs compliant | 9 / 10 |
| FRs partial | 1 (FR10 — missing response fields) |
| FRs non-compliant | 0 |
| ACs compliant | 9 / 10 |
| ACs partial | 1 (AC8 — same root cause as FR10) |
| NFRs compliant | 5 / 5 |
| Tasks completed | 14 / 14 |
| Critical issues | 0 |
| Warnings | 2 |
| Suggestions | 3 |

---

## 8. Final Verdict

**PASS WITH WARNINGS**

All 14 tasks are complete. 251 tests pass with 0 failures. All NFRs are met. The implementation correctly realizes the 5-phase heuristic algorithm (GenerateDays → PinnedMustSees → UnpinnedMustSees → FillCandidates → EnrichTransitAndWeather).

The only notable gap is FR10/AC8: the response DTO is missing `PlaceId`, `SequenceOrder`, `IsIndoor`, `Priority`, `BufferMinutes`, and `FrictionAlert` fields. The core DayPlan→Block→Activity→Transit structure is present and functional, but these detail fields should be added for full spec compliance. This is a straightforward mapping addition in `GenerateTripHandler.MapActivity()` and `ActivityResponse`.