# Design: Adaptive Itinerary Replanning

## Technical Approach

Add mid-trip adaptation as a **new Domain service** (`IItineraryReplanningEngine`) that performs *scoped* partial replanning on an existing `Trip.Days` collection without clearing it. The engine reuses the existing generation collaborators (`ICandidateFiller`, `ICandidateScorer`, `ITransitEnricher`, `ITimelineScheduler`) but through **new scoped overloads**, because the existing methods iterate the whole trip and would violate the spec's scope-isolation rule. Four new MediatR commands/handlers expose the capability via `TripsController`; `DayPlan.IsStale` + `MustSee.ForceIncludeDespiteWeather` are persisted via two backward-compatible EF migrations. The spec is the authoritative contract; where the task brief and spec diverge, the spec wins (see Open Questions).

## Architecture Decisions

| Decision | Choice | Rejected alternative | Rationale |
|----------|--------|----------------------|-----------|
| Scoped collaborators | Add scoped overloads to `ICandidateFiller`/`ITransitEnricher`/`ITimelineScheduler` | Call existing trip-scoped methods | Existing methods iterate all days/blocks → would mutate locked/past/completed blocks, violating FR-S4 "MUST NOT mutate outside scope". Scoped overloads keep one impl per port and stay unit-testable. |
| Weather-refresh location | Handler-only logic (`RefreshWeatherHandler`) | Engine method `RefreshWeatherAsync` | FR-W3 explicitly states "the handler SHALL" compare/set weather/stale. Logic is a flat day loop — no orchestration worth a domain service. Keeps engine focused on replanning. |
| Engine impl name | `ItineraryReplanningEngine` (spec) | `AdaptiveItineraryReplanningEngine` (task brief) | Spec is the implementation contract; trivial naming alignment. |
| Replan current-day/block resolution | Handler resolves current day+block from `CurrentDateTime`, passes resolved `ReplanContext` to engine | Engine resolves "now" | Keeps engine pure (no `DateTimeOffset.UtcNow` / clock dependency) and deterministic for tests. |
| Timeline scope for chaining | Scoped scheduler walks the **affected day's blocks in order**, seeded with `previousBlockEnd` from the last locked/completed activity; it skips `IsCompleted` activities (preserves their times) | Schedule single block in isolation | A block's arrival depends on the prior block's end (`InterBlockTransit` chaining). Isolated scheduling would break chaining; skipping completed preserves "completed never moved" including times. |
| Command `UserId` carrier | Commands carry `UserId` (per spec signatures) | Handler-injected `IUserContext` only | Spec defines `RefreshWeather(Guid TripId, string UserId)` etc.; matches existing `GenerateTrip` convention. (`GenerateTripItinerary` is the inconsistent outlier.) |
| `TripReplanRequest` stub | Replace with `TripSmartReplanRequest` (`DateTimeOffset` + `ReplanScope` + `CurrentBlockWeather`) | Extend existing `TripReplanRequest` | Stub is 501 (unimplemented) → safe to replace. Spec types align to `DateTimeOffset` and the new enum. |
| `WeatherLastUpdatedAt` field | NOT added (spec omits it) | Add as task brief suggests | Spec defines only `DayPlan.IsStale`. Adding an undocumented field violates "don't guess". Flagged in Open Questions. |

## Data Flow

Weather refresh (handler-only):

```
Client ──POST weather-refresh──▶ RefreshWeatherHandler
  └─▶ ITripRepository.GetByIdAsync → ownership check
  └─▶ IWeatherProvider.GetWeatherAsync
  └─▶ for each DayPlan: if changed → SetWeather + MarkStale
  └─▶ UpdateAsync (only if any changed) ──▶ WeatherRefreshResult
```

Day regeneration / smart replan (engine):

```
Handler ──▶ resolve scope (dayIndex | currentDay+block) ──▶ IItineraryReplanningEngine
  Engine:
   1. LOCK   IsCompleted activities (all days) + past days + pre-current blocks
   2. PRESERVE must-sees in scope (pinned + unpinned)
   3. CLEAR  non-completed, non-must-see activities in scope (dedup by PlaceId)
   4. REFILL via ICandidateFiller.FillScopedAsync (exclude present PlaceIds)
   5. SWAP   outdoor→indoor on Bad weather (except forced must-sees) [#3 only]
   6. PRUNE  Priority.Low nice-to-haves when behind schedule [#3 only]
   7. ENRICH ITransitEnricher.EnrichScopedAsync + SCHEDULE ITimelineScheduler.ScheduleScopedAsync
   8. ClearStale on affected days
Handler ──▶ ITripRepository.UpdateAsync ──▶ AutoMapper ──▶ TripPlanResponse
```

Clean Architecture flow: `API (Controllers) → ApplicationServices (Commands/Handlers) → Domain (Engine + aggregates)`. `Infrastructure` implements ports (`IWeatherProvider`, `ITransitCalculator`, repos) and EF config. Domain has zero framework deps.

## Domain Model Changes

| Artifact | Change |
|----------|--------|
| `MustSee` (VO) | Add `bool ForceIncludeDespiteWeather` ctor param (default `false`); add to `GetEqualityComponents()`. |
| `DayPlan` | Add `bool IsStale { get; private set; }` (default `false`); add `MarkStale()` / `ClearStale()`. Add `DateTimeOffset? WeatherLastUpdatedAt { get; private set; }` with `UpdateWeatherTimestamp()`. |
| `ActivityNode` | Add `SetCompleted(bool value)`; keep `MarkAsCompleted()` (generation). |
| `ReplanScope` enum | New in `Domain/Enums`: `CurrentBlock, CurrentDay, RemainingTrip`. |
| `DayNotFoundException` | New in `Domain/Exceptions`, `: SmartTripDomainException`. |
| `ActivityNotFoundException` | New in `Domain/Exceptions`, `: SmartTripDomainException`. |
| `ScoringContext` | Add `bool ForceIncludeDespiteWeather = false` to record. |
| `CandidateScorer.Score` | When `IsBadWeather && ForceIncludeDespiteWeather && !IsIndoor` → skip `OutdoorWeatherPenalty` AND `IndoorWeatherBonus`; other components unchanged. |
| `MustSeeInput` / `MustSeeResponse` | Add `ForceIncludeDespiteWeather` (input default `false`). |
| `BlockTimeline` | (Optional) helper to remove non-locked activities; engine can use existing `RemoveActivity` iteratively. |

## Domain Service: IItineraryReplanningEngine

Port in `Domain/Ports`; impl `ItineraryReplanningEngine` in `Domain/Services`.

```csharp
public interface IItineraryReplanningEngine
{
    Task RegenerateDayAsync(Trip trip, int dayIndex,
        IReadOnlyList<Place> candidates, Dictionary<DateOnly, WeatherCondition> weather, CancellationToken ct);

    Task ReplanAsync(Trip trip, ReplanContext context,
        IReadOnlyList<Place> candidates, Dictionary<DateOnly, WeatherCondition> weather, CancellationToken ct);
}

public record ReplanContext(int CurrentDayIndex, BlockType CurrentBlock,
    ReplanScope Scope, bool IsBadWeather, DateTimeOffset CurrentDateTime);
```

**Scoped collaborator overloads** (new, alongside existing trip-scoped methods):

```csharp
// ICandidateFiller
Task FillScopedAsync(Trip trip, IReadOnlyList<(int DayIndex, BlockType Block)> scope,
    List<Place> pool, HashSet<long> excludePlaceIds, Dictionary<DateOnly, WeatherCondition> weather, CancellationToken ct);
// ITransitEnricher
Task EnrichScopedAsync(Trip trip, IReadOnlyList<(int DayIndex, BlockType Block)> scope,
    IReadOnlyDictionary<long, Place> placesById, Dictionary<DateOnly, WeatherCondition> weather, CancellationToken ct);
// ITimelineScheduler
void ScheduleScoped(Trip trip, IReadOnlyList<int> dayIndices, int? seedPreviousBlockEnd = null);
```

**Preservation rules (both methods):** `IsCompleted` activities never moved/deleted; must-sees (PlaceId ∈ `trip.OriginalMustSees`) retained in scope. `RegenerateDayAsync` targets one day; `ReplanAsync` derives the `(DayIndex, BlockType)` set from `Scope`:

- `CurrentBlock` → `[(currentDay, currentBlock)]`
- `CurrentDay` → `[(currentDay, currentBlock..Evening)]`
- `RemainingTrip` → `[(currentDay, currentBlock..Evening), (currentDay+1..lastDay, all blocks)]`

Bad-weather swap (#3 only): replace outdoor non-completed non-forced candidates with indoor alternatives; forced outdoor must-sees retained and scored without penalty (FR-F3). Over-constrained (High must-see cannot fit) → `OverConstrainedRouteException`. No-op (nothing remaining) → return trip unchanged.

## Application Layer

| Command (record) | Handler | Validator | Response |
|------------------|---------|-----------|----------|
| `RefreshWeather(Guid TripId, string UserId)` | `RefreshWeatherHandler` | `RefreshWeatherValidator` (`TripId` NotEmpty) | `WeatherRefreshResult` |
| `RegenerateDay(Guid TripId, int DayIndex, string UserId)` | `RegenerateDayHandler` | `RegenerateDayValidator` (`TripId` NotEmpty; `DayIndex >= 0`) | `TripPlanResponse` |
| `TripSmartReplan(Guid TripId, TripSmartReplanRequest Request, string UserId)` | `TripSmartReplanHandler` | `TripSmartReplanValidator` (`TripId` NotEmpty; `CurrentDateTime` not default; `Scope` defined; `CurrentBlockWeather` ∈ {Good,Bad}) | `TripPlanResponse` |
| `ToggleActivityCompletion(Guid TripId, int DayIndex, long PlaceId, ActivityCompletionRequest Request, string UserId)` | `ToggleActivityCompletionHandler` | `ToggleActivityCompletionValidator` (`TripId` NotEmpty; `DayIndex >= 0`; `PlaceId > 0`) | `ActivityCompletionResponse` |

New ApiModels (records in `Domain/ApiModels`): `WeatherRefreshResult`, `DayWeatherChange`, `TripSmartReplanRequest`, `ActivityCompletionRequest`, `ActivityCompletionResponse`. DTOs mirror the spec's API contract exactly.

**Handler common pattern** (matches `GenerateTripItineraryHandler`): primary-ctor injection → `GetByIdAsync` → null→`TripNotFoundException` → `OwnerUserId != UserId`→`TripForbiddenException` → business rules → `UpdateAsync` → `mapper.Map<TripPlanResponse>(trip, opts => opts.Items["City"] = trip.City)`.

Per-deliverable handler rules:
- **RefreshWeather:** empty days → empty list, no provider call; past trip (`EndDate < today`) → empty list no-op; `UpdateAsync` only if ≥1 day changed.
- **RegenerateDay:** `0 <= dayIndex < Days.Count` else `DayNotFoundException`; no days → `BusinessRuleException` ("Itinerary not generated"); delegate to engine.
- **TripSmartReplan:** resolve current day (`Date == CurrentDateTime.LocalDateTime`); current block from time (Morning <12:00, Afternoon <18:00, else Evening); `CurrentDateTime > trip.EndDate end` → `BusinessRuleException` ("Current time is after the trip end"); delegate to engine.
- **ToggleActivityCompletion:** locate `DayPlan` by index (else `DayNotFoundException`); locate `ActivityNode` by `placeId` across 3 blocks (else `ActivityNotFoundException`); future-day (`day.Date > today`) + `IsCompleted=true` → `BusinessRuleException` ("Cannot complete an activity in a future day"); call `SetCompleted`; aggregate `CompletedActivitiesCount`/`TotalActivitiesCount` across whole trip.

AutoMapper profile must map new `MustSee.ForceIncludeDespiteWeather` → `MustSeeResponse` and `DayPlan.IsStale` → `DayPlan` schema.

## API Layer

`TripsController` additions (thin: build command with `_userContext.UserId`, `_mediator.Send`):

| Method | Route | Status codes |
|--------|-------|--------------|
| `POST` | `api/trips/{tripId}/weather-refresh` | 200, 403, 404 |
| `POST` | `api/trips/{tripId}/days/{dayIndex}/regenerate` | 200, 403, 404, 422 |
| `POST` | `api/trips/{tripId}/replan` | 200, 403, 404, 422 |
| `PATCH` | `api/trips/{tripId}/days/{dayIndex}/activities/{placeId}/complete` | 200, 403, 404, 422 |

`doc/architecture/endpoints.yaml` reconciliation (source of truth, per `api-rest-standards`):
1. Replace `/trips/{tripId}/replan` 501 stub → implemented `TripSmartReplanRequest` (add `scope` enum `[CurrentBlock, CurrentDay, RemainingTrip]`) with 200/403/404/422.
2. Replace `/trips/{tripId}/places/{placeId}/complete` (204) → `/trips/{tripId}/days/{dayIndex}/activities/{placeId}/complete` (200 `ActivityCompletionResponse`).
3. Add `forceIncludeDespiteWeather` (bool, default false) to `MustSeeInput` + `MustSeeResponse`.
4. Add `isStale` (bool) to `DayPlan` schema.

## Infrastructure Layer

**EF migrations** (owned-entity column adds, backward compatible):

| Migration | Table | Column | Type | Default |
|-----------|-------|--------|------|---------|
| `AddDayPlanIsStale` | `DayPlan` | `IsStale` | `boolean NOT NULL` | `false` |
| `AddDayPlanWeatherLastUpdatedAt` | `DayPlan` | `WeatherLastUpdatedAt` | `timestamp with time zone NULL` | `null` |
| `AddMustSeeForceIncludeDespiteWeather` | `TripMustSees` | `ForceIncludeDespiteWeather` | `boolean NOT NULL` | `false` |

- Update `TripConfiguration`:
  - `day.Property(d => d.IsStale).HasDefaultValue(false);`
  - `day.Property(d => d.WeatherLastUpdatedAt).IsRequired(false);`
  - `mustSee.Property(m => m.ForceIncludeDespiteWeather).HasDefaultValue(false);`
  (all inside respective `OwnsMany` blocks).
- Migrations follow existing `AddColumn<bool>(..., defaultValue: false)` + symmetric `Down` pattern (cf. `AddMustSeeOvertimeSupport`).

**`ExceptionHandlingMiddleware.GetStatusCode`** — add BEFORE the `DomainException => 422` catch-all:

```csharp
TripForbiddenException => 403,
TripNotFoundException => 404,
DayNotFoundException => 404,        // NEW
ActivityNotFoundException => 404,  // NEW
DomainException => 422,            // catch-all (BusinessRuleException, OverConstrainedRouteException)
_ => 500
```

**DI registrations:** `ItineraryReplanningEngine` + scoped collaborator impls are Domain services → register in `Infrastructure` DI (where `CandidateFiller`/`TransitEnricher`/`TimelineScheduler` are already wired). No new Infrastructure types beyond migrations.

## Test Design

| Layer | Class | Focus | Approach |
|-------|-------|-------|----------|
| Domain | `ItineraryReplanningEngineTests` | completed/must-see preservation, PlaceId dedup, scope isolation (3 scopes), outdoor→indoor swap on Bad, forced must-see retention, nice-to-have pruning, stale reset, no-op, no cross-day mutation | Pure, no Moq; `private static CreateTripWithDays()` factory; fake collaborators via simple stub impls |
| Domain | `DayPlanTests` (extend) | `MarkStale`/`ClearStale` toggle; `SetWeather` | Pure |
| Domain | `ActivityNodeTests` (extend) | `SetCompleted(true/false)`; `MarkAsCompleted` still true-only | Pure |
| Domain | `MustSeeTests` (extend) | equality with/without flag; default false | Pure |
| Domain | `CandidateScorerTests` (extend) | forced outdoor skips penalty+bonus; non-forced penalized; indoor unaffected; forced + non-Bad unaffected | Pure |
| Handler | `RefreshWeatherHandlerTests` | ownership ordering, stale only on changed, `UpdateAsync` iff changes, past-trip no-op, empty-days no provider call | Moq `ITripRepository`/`IWeatherProvider`/`IMapper` |
| Handler | `RegenerateDayHandlerTests` | ownership, dayIndex validation, delegation, `UpdateAsync`, mapping, over-constrained propagation | Moq |
| Handler | `TripSmartReplanHandlerTests` | current day/block resolution, "after trip end" rejection, ownership, delegation, mapping | Moq |
| Handler | `ToggleActivityCompletionHandlerTests` | locate across 3 blocks, 404 paths, future-day rejection, count aggregation, `UpdateAsync`, mapping | Moq |
| Mapping | `MustSeeMappingTests` (extend) | `MustSeeInput`→`MustSee`→`MustSeeResponse` round-trip preserves flag; omitted defaults false | AutoMapper |

Test data builders needed: `CreateTripWithDays(int dayCount)`, `CreateDayPlanWithActivities(...)`, `CreateActivityNode(placeId, isCompleted)`. ~39 new tests (6+8+12+7+6) per spec coverage assessment.

## Data Migration Strategy

Both migrations are additive column adds with `DEFAULT false` → fully backward compatible, no data backfill needed:

- **Existing `MustSee` rows** → `ForceIncludeDespiteWeather = false` automatically (column default). Existing trips behave exactly as before (forced flag never set → existing scoring/placement unchanged).
- **Existing `DayPlan` rows** → `IsStale = false` automatically. Existing days are treated as fresh; the first explicit weather-refresh call re-evaluates them.
- **No feature flag / phased rollout** required — new endpoints are additive; old `/replan` 501 and old `/complete` 204 stubs are replaced (no live clients depend on 501/204).
- Rollback: each migration's `Down` drops the column.

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `Domain/Ports/IItineraryReplanningEngine.cs` | Create | Replanning engine port + `ReplanContext` record |
| `Domain/Services/ItineraryReplanningEngine.cs` | Create | Engine impl: lock/preserve/clear/refill/swap/prune/enrich/schedule per scope |
| `Domain/Ports/ICandidateFiller.cs` | Modify | Add `FillScopedAsync` overload |
| `Domain/Ports/ITransitEnricher.cs` | Modify | Add `EnrichScopedAsync` overload |
| `Domain/Ports/ITimelineScheduler.cs` | Modify | Add `ScheduleScoped` overload |
| `Domain/Services/CandidateFiller.cs` | Modify | Implement `FillScopedAsync` (respect `excludePlaceIds`) |
| `Domain/Services/TransitEnricher.cs` | Modify | Implement `EnrichScopedAsync` |
| `Domain/Services/TimelineScheduler.cs` | Modify | Implement `ScheduleScoped` (skip completed, seed `previousBlockEnd`) |
| `Domain/Services/CandidateScorer.cs` | Modify | Forced-flag weather branch |
| `Domain/Ports/ICandidateScorer.cs` | Modify | `ScoringContext` + `ForceIncludeDespiteWeather` |
| `Domain/AggregatesModel/MustSee.cs` | Modify | Add field + equality component |
| `Domain/AggregatesModel/DayPlan.cs` | Modify | `IsStale` + `MarkStale`/`ClearStale` |
| `Domain/AggregatesModel/ActivityNode.cs` | Modify | `SetCompleted(bool)` |
| `Domain/Enums/ReplanScope.cs` | Create | New enum |
| `Domain/Exceptions/DayNotFoundException.cs` | Create | 404 exception |
| `Domain/Exceptions/ActivityNotFoundException.cs` | Create | 404 exception |
| `Domain/ApiModels/MustSeeInput.cs`, `MustSeeResponse.cs` | Modify | Add flag |
| `Domain/ApiModels/WeatherRefreshResult.cs`, `DayWeatherChange.cs` | Create | #1 response DTOs |
| `Domain/ApiModels/TripSmartReplanRequest.cs` | Create | #3 request DTO (replaces stub) |
| `Domain/ApiModels/ActivityCompletionRequest.cs`, `ActivityCompletionResponse.cs` | Create | #4 DTOs |
| `ApplicationServices/Commands/{RefreshWeather,RegenerateDay,TripSmartReplan,ToggleActivityCompletion}.cs` | Create | 4 command records |
| `ApplicationServices/Handlers/{...}Handler.cs` | Create | 4 handlers |
| `ApplicationServices/Validators/{...}Validator.cs` | Create | 4 FluentValidation validators |
| `API/Controllers/TripsController.cs` | Modify | 4 endpoints |
| `API/Middleware/ExceptionHandlingMiddleware.cs` | Modify | 2 new 404 cases before catch-all |
| `Infrastructure/Configurations/TripConfiguration.cs` | Modify | 2 owned-entity columns |
| `Infrastructure/Migrations/*_AddDayPlanIsStale.cs` | Create | Migration #1 |
| `Infrastructure/Migrations/*_AddMustSeeForceIncludeDespiteWeather.cs` | Create | Migration #2 |
| `doc/architecture/endpoints.yaml` | Modify | Reconcile stubs + new schema fields |
| AutoMapper profile (mapping config) | Modify | Map new flag + `IsStale` |
| Tests: 4 handler suites, `ItineraryReplanningEngineTests`, extend 4 domain suites | Create/Modify | ~39 tests |

## Open Questions — RESOLVED

| # | Question | Decision | Rationale |
|---|----------|----------|-----------|
| 1 | `WeatherLastUpdatedAt` on `DayPlan`? | **ADDED** | Provides temporal context for front ("last updated 2h ago"). Adds 3rd migration `AddDayPlanWeatherLastUpdatedAt`. |
| 2 | Engine impl naming | `ItineraryReplanningEngine` | Spec is authoritative contract. |
| 3 | Scoped overloads vs refactor | **Scoped overloads accepted** | Keeps existing trip-scoped methods untouched; isolated risk for partial regeneration. |
| 4 | Completed-activity time preservation | **Accepted** | `IsCompleted` activities are never rescheduled; their wall-clock times are preserved. |
