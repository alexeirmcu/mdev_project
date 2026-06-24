# Tasks: Adaptive Itinerary Replanning

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~1400-1600 (20+ files, 39 tests, 3 migrations, 4 endpoints) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (Foundation) → PR 2 (Engine + Tests) → PR 3 (API + Handlers) |
| Delivery strategy | ask-on-risk |
| Chain strategy | stacked-to-main |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Domain model + migrations + collaborator scoped overloads | PR 1 | Base branch: main; includes domain tests |
| 2 | Replanning engine + engine unit tests | PR 2 | Base: PR 1 branch; pure domain, no mocks |
| 3 | API endpoints + handlers + validators + integration tests | PR 3 | Base: PR 2 branch; Moq handler tests |

## Phase 1: Foundation (Domain Model + Migrations + Exceptions) ✅ COMPLETED

- [x] 1.1 Add `IsStale` (private set, default false) + `MarkStale()` / `ClearStale()` to `DayPlan.cs`
- [x] 1.2 Add `WeatherLastUpdatedAt` (DateTimeOffset?) + `UpdateWeatherTimestamp()` to `DayPlan.cs`
- [x] 1.3 Add `ForceIncludeDespiteWeather` ctor param + equality component to `MustSee.cs`
- [x] 1.4 Add `SetCompleted(bool value)` to `ActivityNode.cs`
- [x] 1.5 Create `ReplanScope` enum in `Domain/Enums/ReplanScope.cs` (CurrentBlock, CurrentDay, RemainingTrip)
- [x] 1.6 Create `DayNotFoundException` and `ActivityNotFoundException` in `Domain/Exceptions/`
- [x] 1.7 Add `ForceIncludeDespiteWeather` (default false) to `ScoringContext` record in `ICandidateScorer.cs`
- [x] 1.8 Update `CandidateScorer.Score` to skip weather penalty/bonus when forced + bad weather + outdoor
- [x] 1.9 Add `FillScopedAsync` to `ICandidateFiller.cs` port + implement in `CandidateFiller.cs`
- [x] 1.10 Add `EnrichScopedAsync` to `ITransitEnricher.cs` port + implement in `TransitEnricher.cs`
- [x] 1.11 Add `ScheduleScoped` to `ITimelineScheduler.cs` port + implement in `TimelineScheduler.cs`
- [x] 1.12 Update `TripConfiguration.cs`: add `IsStale`, `WeatherLastUpdatedAt`, `ForceIncludeDespiteWeather` in OwnsMany blocks
- [x] 1.13 Create EF migration `AddDayPlanIsStale` (boolean NOT NULL DEFAULT false)
- [x] 1.14 Create EF migration `AddDayPlanWeatherLastUpdatedAt` (timestamp with time zone NULL)
- [x] 1.15 Create EF migration `AddMustSeeForceIncludeDespiteWeather` (boolean NOT NULL DEFAULT false)

## Phase 2: Replanning Engine (Core Domain Service)

- [x] 2.1 Create `IItineraryReplanningEngine` port + `ReplanContext` record in `Domain/Ports/IItineraryReplanningEngine.cs`
- [x] 2.2 Implement `ItineraryReplanningEngine.RegenerateDayAsync` in `Domain/Services/` (lock/preserve/clear/refill/enrich/schedule/stale-reset)
- [x] 2.3 Implement `ItineraryReplanningEngine.ReplanAsync` (scope resolution, weather swap, nice-to-have pruning, stale-reset)
- [x] 2.4 Register `ItineraryReplanningEngine` in Domain DI (`ApplicationServicesRegistration.cs`)

## Phase 3: API Models + Commands + Validators

- [x] 3.1 Create ApiModel records: `WeatherRefreshResult`, `DayWeatherChange` in `Domain/ApiModels/`
- [x] 3.2 Create ApiModel records: `TripSmartReplanRequest`, `ActivityCompletionRequest`, `ActivityCompletionResponse` in `Domain/ApiModels/`
- [x] 3.3 Update `MustSeeInput` and `MustSeeResponse` to include `ForceIncludeDespiteWeather`
- [ ] 3.4 Create command records: `RefreshWeather`, `RegenerateDay`, `TripSmartReplan`, `ToggleActivityCompletion` in `ApplicationServices/Commands/`
- [ ] 3.5 Create validators: `RefreshWeatherValidator`, `RegenerateDayValidator`, `TripSmartReplanValidator`, `ToggleActivityCompletionValidator` in `ApplicationServices/Validators/`

## Phase 4: Handlers + API Layer

- [ ] 4.1 Implement `RefreshWeatherHandler` (ownership, weather fetch, stale marking, conditional persist)
- [ ] 4.2 Implement `RegenerateDayHandler` (ownership, dayIndex validation, delegate to engine, persist, map)
- [ ] 4.3 Implement `TripSmartReplanHandler` (ownership, resolve current day/block, after-trip-end check, delegate to engine)
- [ ] 4.4 Implement `ToggleActivityCompletionHandler` (ownership, locate day/activity across 3 blocks, future-day rejection, toggle, count aggregation)
- [ ] 4.5 Add 4 endpoints to `TripsController.cs` (POST weather-refresh, POST days/regenerate, POST replan, PATCH complete)
- [x] 4.6 Update `ExceptionHandlingMiddleware.GetStatusCode`: add `DayNotFoundException` and `ActivityNotFoundException` → 404 before DomainException catch-all
- [ ] 4.7 Update `AutoMapperProfile.cs`: map `ForceIncludeDespiteWeather` and `IsStale`
- [ ] 4.8 Update `doc/architecture/endpoints.yaml`: replace replan stub, replace complete stub, add new schema fields

## Phase 5: Testing

- [x] 5.1 Extend `DayPlanTests`: `MarkStale`/`ClearStale` toggle, `SetWeather` updates
- [x] 5.2 Extend `ActivityNodeTests`: `SetCompleted(true/false)` toggle, `MarkAsCompleted` still true-only
- [x] 5.3 Extend `MustSeeTests`: equality with/without flag, default false
- [x] 5.4 Extend `CandidateScorerTests`: forced outdoor skips penalty+bonus, non-forced penalized, indoor unaffected
- [x] 5.5 Create `ItineraryReplanningEngineTests`: completed/must-see preservation, scope isolation (3 scopes), outdoor→indoor swap, forced retention, nice-to-have pruning, stale reset, no-op
- [ ] 5.6 Create `RefreshWeatherHandlerTests`: ownership, stale on change, UpdateAsync iff changes, past-trip no-op, empty-days no provider
- [ ] 5.7 Create `RegenerateDayHandlerTests`: ownership, dayIndex validation, delegation, UpdateAsync, mapping, over-constrained
- [ ] 5.8 Create `TripSmartReplanHandlerTests`: current day/block resolution, after-trip-end rejection, ownership, delegation
- [ ] 5.9 Create `ToggleActivityCompletionHandlerTests`: locate across 3 blocks, 404 paths, future-day rejection, count aggregation
- [ ] 5.10 Extend `MustSeeMappingTests`: round-trip preserves flag, omitted defaults false
