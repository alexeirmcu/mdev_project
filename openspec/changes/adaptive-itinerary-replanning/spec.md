# Spec: Adaptive Itinerary Replanning

## Overview

Enables mid-trip adaptation: refresh weather forecasts with stale-day detection,
regenerate a single day preserving completed activities and must-sees, smart-replan
from the current point forward driven by a request-scoped enum, toggle activity
completion via a checklist API, and force-include outdoor must-sees despite bad
weather. Builds on the existing `itinerary-generation` capability (MODIFIED) and
introduces four new capabilities.

## Delta Summary

| # | Deliverable | Capability | Spec Kind | Primary Artifacts |
|---|-------------|-----------|-----------|-------------------|
| 1 | Weather Refresh Endpoint | `weather-refresh` | NEW | `RefreshWeather` command/handler, `WeatherRefreshResult` |
| 2 | Day Regeneration Endpoint | `day-regeneration` | NEW | `RegenerateDay` command/handler |
| 3 | Smart Replan Endpoint | `smart-replan` | NEW (replaces 501 stub) | `TripSmartReplan` command/handler, `ReplanScope` enum |
| 4 | Checklist API Endpoint | `checklist-api` | NEW (refines existing stub) | `ToggleActivityCompletion` command/handler |
| 5 | ForceIncludeDespiteWeather Flag | `itinerary-generation` | MODIFIED (delta) | `MustSee`, `MustSeeInput`, `ScoringContext`, `CandidateScorer` |

## Cross-Cutting Domain Changes

These shared changes are referenced by multiple deliverables and formalized in the
[Itinerary-Generation Delta](#itinerary-generation-delta-modified-capability) section.

| Change | Type | Default | Migration | Consumers |
|--------|------|---------|-----------|-----------|
| `DayPlan.IsStale` (`bool`) | New persisted property on `DayPlan` | `false` | EF Migration #1 (backward compatible) | #1 sets, #2/#3 reset |
| `MustSee.ForceIncludeDespiteWeather` (`bool`) | New field on `MustSee` VO + `MustSeeInput` + `MustSeeResponse` | `false` | EF Migration #2 (owned entity column) | #3 respects, #5 scoring |
| `ActivityNode.SetCompleted(bool)` | New method (current `MarkAsCompleted()` only sets `true`) | n/a | none | #4 toggles |
| `ReplanScope` enum (`CurrentBlock`, `CurrentDay`, `RemainingTrip`) | New enum in `Domain/Enums` | n/a | none | #3 |
| `IItineraryReplanningEngine` port + impl | New Domain service port (`Domain/Ports`), impl in `Domain/Services` | n/a | none | #2, #3 |

**New exception types** (all derive from `SmartTripDomainException`, mapped in
`ExceptionHandlingMiddleware` BEFORE the `DomainException` catch-all):

| Exception | HTTP | Used by |
|-----------|------|---------|
| `DayNotFoundException` | 404 | #2, #4 |
| `ActivityNotFoundException` | 404 | #4 |

---

## Deliverable 1: Weather Refresh Endpoint

### Requirements

**FR-W1**: The system SHALL expose `POST /api/trips/{tripId}/weather-refresh` to
re-fetch the weather forecast for an existing trip and flag days whose forecast
changed.

**FR-W2**: The handler SHALL load the trip via `ITripRepository.GetByIdAsync`,
verify `trip.OwnerUserId == IUserContext.UserId` (else `TripForbiddenException`
→ 403), and verify the trip exists (else `TripNotFoundException` → 404) BEFORE any
weather call. A trip with no generated days (`trip.Days` empty) SHALL return an
empty change list without calling the weather provider.

**FR-W3**: The handler SHALL call `IWeatherProvider.GetWeatherAsync(trip.CityId,
trip.StartDate, trip.EndDate, ct)` to obtain a fresh
`Dictionary<DateOnly, WeatherCondition>`. For each `DayPlan`, if the fresh
condition differs from `day.WeatherSummary`, the handler SHALL: (a) update
`day.SetWeather(newCondition)`, (b) set `day.IsStale = true`. Unchanged days
MUST NOT be marked stale and MUST NOT be re-saved with a new weather value.

**FR-W4**: The handler SHALL persist the trip via `UpdateAsync` only when at least
one day changed; otherwise it SHALL skip the write (idempotent no-op).

**NFR-W1**: The endpoint MUST NOT trigger itinerary regeneration. Stale marking is
a metadata update only. Regeneration is a separate, explicit call (#2/#3).

### Scenarios

#### Scenario: Forecast changes for 2 of 3 days
- GIVEN a 3-day trip with stored weather `[Clear, Clear, Good]`
- WHEN the weather provider returns `[Bad, Clear, Good]`
- THEN `DayPlan[0].WeatherSummary` becomes `Bad` and `DayPlan[0].IsStale = true`
- AND `DayPlan[1]` and `DayPlan[2]` remain unchanged with `IsStale = false`
- AND the response lists exactly one `DayWeatherChange` for day 0

#### Scenario: API returns no data
- GIVEN `IWeatherProvider` returns all `Clear` (no data fallback)
- AND every stored day is already `Clear`
- WHEN refresh runs
- THEN no day is marked stale and the change list is empty
- AND `UpdateAsync` is NOT called

#### Scenario: All dates unchanged
- GIVEN the fresh forecast equals the stored forecast for every day
- WHEN refresh runs
- THEN the change list is empty and `IsStale` is not modified on any day

#### Scenario: Trip entirely in the past
- GIVEN `trip.EndDate < DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date)`
- WHEN refresh runs
- THEN the handler returns an empty change list without mutation (forecasts are
  forward-looking; past trips are a no-op)

### API Contract

`POST /api/trips/{tripId}/weather-refresh` — authenticated, empty body.

```csharp
// Command (ApplicationServices/Commands)
public record RefreshWeather(Guid TripId, string UserId) : IRequest<WeatherRefreshResult>;

// Response (Domain/ApiModels)
public record WeatherRefreshResult(
    bool Updated,
    int DaysRefreshed,
    IReadOnlyList<DayWeatherChange> Changes);

public record DayWeatherChange(
    int DayIndex,
    string PreviousWeather,   // WeatherCondition name
    string NewWeather);       // WeatherCondition name
```

| Status | Condition |
|--------|-----------|
| 200 | Refresh completed (change list may be empty) |
| 403 | Non-owner |
| 404 | Trip not found |

Validator (`RefreshWeatherValidator`): `TripId` NotEmpty.

### Domain Changes
- `DayPlan.IsStale` (`bool`, private set, default `false`) + `MarkStale()` /
  `ClearStale()` methods (keeps mutability encapsulated).
- EF Migration #1: add `IsStale` column (`boolean NOT NULL DEFAULT false`) to
  `DayPlans` table.

### Error Cases
| Case | Result |
|------|--------|
| Trip not found | `TripNotFoundException` → 404 |
| Non-owner | `TripForbiddenException` → 403 |
| Weather provider throws | Propagates → 500 (no silent swallow; refresh is explicit) |
| Trip has no days | 200 with `Updated=false, DaysRefreshed=0, Changes=[]` |

### Test Strategy
- **Handler tests** (`RefreshWeatherHandlerTests`, Moq): verify ownership check
  ordering; verify `SetWeather` + `IsStale=true` only on changed days; verify
  `UpdateAsync` called iff changes exist; verify past-trip no-op; verify empty-days
  no provider call.
- **Domain tests** (`DayPlanTests`): `MarkStale`/`ClearStale` toggle `IsStale`;
  `SetWeather` updates `WeatherSummary`.
- Target ~6 new tests.

---

## Deliverable 2: Day Regeneration Endpoint

### Requirements

**FR-D1**: The system SHALL expose
`POST /api/trips/{tripId}/days/{dayIndex}/regenerate` to regenerate exactly one
day in place, preserving completed activities and must-sees.

**FR-D2**: The handler SHALL load the trip, enforce ownership, and validate
`0 <= dayIndex < trip.Days.Count` (else `DayNotFoundException` → 404). A trip with
no days SHALL throw `BusinessRuleException` → 422 ("Itinerary not generated").

**FR-D3**: The handler SHALL fetch fresh candidates
(`IPlaceRepository.GetManyByCityIdAsync(trip.CityId, trip.Preferences.Interests)`)
and fresh weather (`IWeatherProvider.GetWeatherAsync`), then delegate to
`IItineraryReplanningEngine.RegenerateDayAsync(trip, candidates, weather, dayIndex,
ct)`.

**FR-D4**: `RegenerateDayAsync` SHALL, for the target day only:
1. **Lock** every `ActivityNode` with `IsCompleted = true` (keep in current
   block/sequence — never move or delete).
2. **Preserve** all must-sees currently placed on that day (pinned and unpinned);
   they remain in their blocks.
3. **Clear** all other (non-completed, non-must-see) activities from the day's
   blocks, deduplicating by `PlaceId` before refill.
4. **Refill** freed capacity via `ICandidateFiller`, excluding PlaceIds already
   present (completed + must-sees).
5. **Re-enrich** transit (`ITransitEnricher`) and **reschedule** timeline
   (`ITimelineScheduler`) for the target day only.
6. **Reset** `day.IsStale = false`.

**FR-D5**: Other days MUST NOT be touched. The handler SHALL persist via
`UpdateAsync` and return `TripPlanResponse` (full trip) via AutoMapper.

### Scenarios

#### Scenario: Regenerate frees and refills 1 slot, keeps completed
- GIVEN day 1 Morning has [A (completed), B (must-see), C (candidate)]
- WHEN regenerate runs for day 1
- THEN A stays completed in Morning, B stays, C is removed
- AND a new scored candidate D fills the freed slot
- AND `day1.IsStale = false`, day 0 and day 2 are unchanged

#### Scenario: All activities completed
- GIVEN day 2 has every activity marked completed across all blocks
- WHEN regenerate runs for day 2
- THEN no activity is removed; only transit and timeline are recomputed
- AND `IsStale = false`; `CandidateFiller` adds nothing

#### Scenario: dayIndex out of range
- GIVEN a 3-day trip and a request with `dayIndex = 5`
- WHEN the handler validates
- THEN `DayNotFoundException` is thrown → 404
- AND `IItineraryReplanningEngine` is never called

#### Scenario: No candidates available
- GIVEN all candidate places are already placed or filtered out
- WHEN regenerate refills the freed Evening slot
- THEN the slot remains empty (not an error)
- AND `IsStale = false`; the response is 200 with the partially-filled day

### API Contract

`POST /api/trips/{tripId}/days/{dayIndex}/regenerate` — authenticated, empty body.

```csharp
public record RegenerateDay(Guid TripId, int DayIndex, string UserId)
    : IRequest<TripPlanResponse>;
```

| Status | Condition |
|--------|-----------|
| 200 | Day regenerated; full `TripPlanResponse` returned |
| 403 | Non-owner |
| 404 | Trip not found / dayIndex out of range |
| 422 | No days generated (`BusinessRuleException`) |

Validator: `TripId` NotEmpty; `DayIndex >= 0`.

### Domain Changes
- New port `IItineraryReplanningEngine` (see Cross-Cutting) with
  `RegenerateDayAsync`.
- Implementation `ItineraryReplanningEngine` in `Domain/Services` reusing
  `ICandidateFiller`, `ITransitEnricher`, `ITimelineScheduler`.
- `DayPlan.ClearStale()` (from #1).

### Error Cases
| Case | Result |
|------|--------|
| Trip not found | 404 |
| dayIndex out of range | `DayNotFoundException` → 404 |
| Non-owner | 403 |
| No days generated | `BusinessRuleException` → 422 |
| Over-constrained (High must-see cannot fit after refill) | `OverConstrainedRouteException` → 422 |

### Test Strategy
- **Domain tests** (`ItineraryReplanningEngineTests`, pure, no Moq): completed
  activity preservation, must-see preservation, PlaceId dedup before refill,
  stale reset, no cross-day mutation. Use factory `CreateTripWithDays()`.
- **Handler tests** (`RegenerateDayHandlerTests`, Moq): ownership ordering,
  dayIndex validation, delegation to engine, `UpdateAsync` called, response
  mapping, over-constrained propagation.
- Target ~8 new tests.

---

## Deliverable 3: Smart Replan Endpoint

### Requirements

**FR-S1**: The system SHALL expose `POST /api/trips/{tripId}/replan` (replacing the
current 501 stub in `endpoints.yaml`) to replan the itinerary from the traveler's
current point forward, driven by `Scope`.

**FR-S2**: The request SHALL carry `TripSmartReplanRequest`:
`CurrentDateTime` (DateTimeOffset), `CurrentLocation` (LocationModel),
`CurrentBlockWeather` (enum `[Good, Bad]`), `Scope` (enum
`[CurrentBlock, CurrentDay, RemainingTrip]`). All fields required.

**FR-S3**: The handler SHALL load the trip, enforce ownership, and resolve the
**current day** as the `DayPlan` whose `Date == DateOnly.FromDateTime(
CurrentDateTime.LocalDateTime)` and the **current block** from time-of-day
(Morning < 12:00, Afternoon < 18:00, else Evening). If `CurrentDateTime` is after
`trip.EndDate` (end of last day), the handler SHALL throw `BusinessRuleException`
→ 422 ("Current time is after the trip end").

**FR-S4**: `IItineraryReplanningEngine.ReplanAsync` SHALL, within the scope:
1. **Lock** all `IsCompleted` activities across all days (never moved/deleted).
2. **Lock** all past days (`DayIndex < currentDayIndex`) and, on the current day,
   all blocks before the current block.
3. **Replan** from the current block forward per `Scope`:
   - `CurrentBlock` — only the current block of the current day.
   - `CurrentDay` — current block through Evening of the current day.
   - `RemainingTrip` — current block through the last day.
4. **Weather-aware swap**: when `CurrentBlockWeather = Bad`, replace outdoor
   (`IsIndoor = false`) non-completed, non-must-see activities with indoor
   candidates where available. Outdoor must-sees with
   `ForceIncludeDespiteWeather = true` MUST be retained and scored without weather
   penalty (see #5). Outdoor must-sees WITHOUT the flag are retained (must-see
   priority overrides weather) but MAY be relocated within scope to a better block.
5. **Nice-to-have pruning**: if the traveler is behind schedule
   (`CurrentDateTime` past the planned start of the current block's first
   activity), drop `Priority.Low` non-must-see candidates from the current block
   to recover time.
6. **Recompute** transit (`ITransitEnricher`) and timeline (`ITimelineScheduler`)
   for affected blocks; **reset** `IsStale = false` on all affected days.
7. **Persist** via `UpdateAsync`; return `TripPlanResponse`.

**FR-S5**: If no activities remain to replan (all completed or all in the past),
the handler SHALL return the trip unchanged with HTTP 200 (no-op, no error).

### Scenarios

#### Scenario: RemainingTrip replan on Bad weather swaps outdoor candidates
- GIVEN a 3-day trip, current time = day 1 Afternoon, `CurrentBlockWeather = Bad`
- AND day 1 Afternoon has an outdoor candidate X and an indoor candidate Y available
- WHEN replan runs with `Scope = RemainingTrip`
- THEN X is replaced by Y in day 1 Afternoon; days 0 and day 1 Morning are locked
- AND completed activities everywhere are unchanged
- AND affected days have `IsStale = false`

#### Scenario: ForceIncludeDespiteWeather must-see retained in Bad weather
- GIVEN an outdoor must-see Z with `ForceIncludeDespiteWeather = true` on day 2
- AND `CurrentBlockWeather = Bad`, `Scope = RemainingTrip`
- WHEN replan runs
- THEN Z remains in the itinerary (not swapped out) and is scored without the
  outdoor weather penalty
- AND only nice-to-have outdoor candidates are swapped for indoor alternatives

#### Scenario: No remaining activities
- GIVEN every activity from the current block onward is already completed
- WHEN replan runs with `Scope = RemainingTrip`
- THEN the trip is returned unchanged with 200 (no-op, no exception)

#### Scenario: Current time after last day
- GIVEN `CurrentDateTime` is after `trip.EndDate` (end of last day)
- WHEN replan runs
- THEN `BusinessRuleException` → 422 ("Current time is after the trip end")
- AND `IItineraryReplanningEngine` is never called

#### Scenario: All must-sees outdoor + forced + Bad weather
- GIVEN every remaining must-see is outdoor with `ForceIncludeDespiteWeather = true`
- AND `CurrentBlockWeather = Bad`, no indoor candidates available
- WHEN replan runs
- THEN all forced must-sees are retained despite Bad weather
- AND nice-to-haves are pruned/swapped as far as indoor supply allows; the day is
  not over-constrained (forced must-sees are never dropped)

### API Contract

`POST /api/trips/{tripId}/replan` — authenticated.

```csharp
public record TripSmartReplan(Guid TripId, TripSmartReplanRequest Request, string UserId)
    : IRequest<TripPlanResponse>;

public record TripSmartReplanRequest(
    DateTimeOffset CurrentDateTime,
    LocationModel CurrentLocation,
    string CurrentBlockWeather,   // "Good" | "Bad"
    ReplanScope Scope);

public enum ReplanScope { CurrentBlock, CurrentDay, RemainingTrip }
```

| Status | Condition |
|--------|-----------|
| 200 | Replan completed (may be a no-op); full `TripPlanResponse` |
| 403 | Non-owner |
| 404 | Trip not found |
| 422 | No days / current time after trip end / over-constrained |

Validator: `TripId` NotEmpty; `CurrentDateTime` not default; `Scope` defined;
`CurrentBlockWeather` in `[Good, Bad]`. (Extends existing `TripReplanRequest`
schema in `endpoints.yaml` by adding `scope`.)

### Domain Changes
- `ReplanScope` enum (`Domain/Enums`).
- `IItineraryReplanningEngine.ReplanAsync` port method.
- Reuses `ICandidateFiller`, `ITransitEnricher`, `ITimelineScheduler`,
  `ICandidateScorer` (with `ForceIncludeDespiteWeather` context, #5).

### Error Cases
| Case | Result |
|------|--------|
| Trip not found | 404 |
| Non-owner | 403 |
| No days generated | `BusinessRuleException` → 422 |
| Current time after trip end | `BusinessRuleException` → 422 |
| Over-constrained (High must-see cannot fit) | `OverConstrainedRouteException` → 422 |

### Test Strategy
- **Domain tests** (`ItineraryReplanningEngineTests`): scope boundaries
  (CurrentBlock vs CurrentDay vs RemainingTrip), completed/past locking,
  outdoor→indoor swap on Bad, forced must-see retention, nice-to-have pruning when
  behind schedule, stale reset, no-op when nothing remains.
- **Handler tests** (`TripSmartReplanHandlerTests`, Moq): current day/block
  resolution, "after trip end" rejection, ownership ordering, delegation,
  response mapping.
- Target ~12 new tests (highest-risk deliverable).

---

## Deliverable 4: Checklist API Endpoint

### Requirements

**FR-C1**: The system SHALL expose
`PATCH /api/trips/{tripId}/days/{dayIndex}/activities/{placeId}/complete` to
toggle the `IsCompleted` flag of a specific activity. This refines the existing
`/trips/{tripId}/places/{placeId}/complete` stub in `endpoints.yaml` by adding
`dayIndex` to disambiguate when a place could appear in multiple days.

**FR-C2**: The request SHALL carry `ActivityCompletionRequest` with a single
required `IsCompleted` (`bool`). The response SHALL be `ActivityCompletionResponse`
(not 204), returning the toggled state plus trip-wide progress counts.

**FR-C3**: The handler SHALL load the trip, enforce ownership, locate the
`DayPlan` by `dayIndex` (else `DayNotFoundException` → 404), and locate the
`ActivityNode` by `placeId` across that day's three blocks (else
`ActivityNotFoundException` → 404).

**FR-C4**: Completing an activity in a **future** day (`day.Date >
DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date)`) SHALL be rejected with
`BusinessRuleException` → 422 ("Cannot complete an activity in a future day").
Completing activities on the current or past days is allowed. This keeps
`IsCompleted` semantics aligned with smart-replan's locking of completed
activities (#3).

**FR-C5**: The handler SHALL call `ActivityNode.SetCompleted(IsCompleted)` (new
method supporting toggle; existing `MarkAsCompleted()` remains for generation),
persist via `UpdateAsync`, and return `ActivityCompletionResponse` with updated
`CompletedActivitiesCount` / `TotalActivitiesCount` across the whole trip.

### Scenarios

#### Scenario: Mark a current-day activity complete
- GIVEN day 0 Morning has activity P (placeId 42) with `IsCompleted = false`
- AND day 0 is today
- WHEN PATCH with `{ "isCompleted": true }`
- THEN `P.IsCompleted = true` is persisted
- AND the response returns `IsCompleted = true`, `CompletedActivitiesCount`
  incremented, `TotalActivitiesCount` unchanged

#### Scenario: Toggle back to incomplete
- GIVEN activity P is already completed
- WHEN PATCH with `{ "isCompleted": false }`
- THEN `P.IsCompleted = false` and `CompletedActivitiesCount` is decremented

#### Scenario: Activity not found in that day
- GIVEN day 1 has no activity with placeId 99
- WHEN PATCH day 1 / placeId 99
- THEN `ActivityNotFoundException` → 404
- AND `UpdateAsync` is not called

#### Scenario: dayIndex out of range
- GIVEN a 2-day trip and `dayIndex = 4`
- WHEN PATCH runs
- THEN `DayNotFoundException` → 404

#### Scenario: Completing a future-day activity is rejected
- GIVEN day 3's date is tomorrow and activity Q is on day 3
- WHEN PATCH day 3 / Q with `isCompleted = true`
- THEN `BusinessRuleException` → 422 ("Cannot complete an activity in a future
  day")

### API Contract

`PATCH /api/trips/{tripId}/days/{dayIndex}/activities/{placeId}/complete` —
authenticated. `placeId` is the internal `long` PlaceId.

```csharp
public record ToggleActivityCompletion(
    Guid TripId, int DayIndex, long PlaceId,
    ActivityCompletionRequest Request, string UserId)
    : IRequest<ActivityCompletionResponse>;

public record ActivityCompletionRequest(bool IsCompleted);

public record ActivityCompletionResponse(
    int DayIndex,
    long PlaceId,
    bool IsCompleted,
    int CompletedActivitiesCount,
    int TotalActivitiesCount);
```

| Status | Condition |
|--------|-----------|
| 200 | Completion toggled; `ActivityCompletionResponse` returned |
| 403 | Non-owner |
| 404 | Trip / day / activity not found |
| 422 | Future-day completion attempt / validation error |

Validator: `TripId` NotEmpty; `DayIndex >= 0`; `PlaceId > 0`.

### Domain Changes
- `ActivityNode.SetCompleted(bool value)` (new; sets `IsCompleted`).
  `MarkAsCompleted()` retained for generation phases.
- New `DayNotFoundException`, `ActivityNotFoundException`
  (`: SmartTripDomainException`), both added to `ExceptionHandlingMiddleware`
  switch before `DomainException` → 404.
- `endpoints.yaml`: replace `/trips/{tripId}/places/{placeId}/complete` stub with
  the day-scoped path and the `ActivityCompletionResponse` 200 schema.

### Error Cases
| Case | Result |
|------|--------|
| Trip not found | 404 |
| Non-owner | 403 |
| dayIndex out of range | `DayNotFoundException` → 404 |
| placeId not in that day | `ActivityNotFoundException` → 404 |
| Future-day completion | `BusinessRuleException` → 422 |
| `IsCompleted` missing | FluentValidation → 422 |

### Test Strategy
- **Domain tests** (`ActivityNodeTests`): `SetCompleted(true/false)` toggles;
  `MarkAsCompleted()` still sets true only.
- **Handler tests** (`ToggleActivityCompletionHandlerTests`, Moq): locate across
  3 blocks, 404 paths, future-day rejection, count aggregation across whole trip,
  `UpdateAsync` called, response mapping.
- Target ~7 new tests.

---

## Deliverable 5: ForceIncludeDespiteWeather Flag

### Requirements

**FR-F1**: `MustSee` SHALL gain a `bool ForceIncludeDespiteWeather` field (default
`false`) included in `GetEqualityComponents()`. `MustSeeInput` and
`MustSeeResponse` SHALL gain the same field (default `false`) for end-to-end
pass-through. Backward compatible: requests omitting the field default to `false`.

**FR-F2**: An EF Core migration SHALL add a `ForceIncludeDespiteWeather` column
(`boolean NOT NULL DEFAULT false`) to the `MustSee` owned-entity table. Existing
rows default to `false`.

**FR-F3**: `ScoringContext` SHALL gain a `bool ForceIncludeDespiteWeather` field
(default `false`). `CandidateScorer.Score` SHALL, when
`context.IsBadWeather && context.ForceIncludeDespiteWeather && !place.IsIndoor`,
SKIP the `OutdoorWeatherPenalty` AND skip the `IndoorWeatherBonus` (the place is
scored as if weather were not Bad for the weather component only). All other
score components (family bonus, popularity, distance penalty) still apply.

**FR-F4**: The placement and candidate-filling phases SHALL set
`ScoringContext.ForceIncludeDespiteWeather = true` when scoring a place that is a
must-see with `MustSee.ForceIncludeDespiteWeather = true`; otherwise `false`.
Non-must-see candidates are never force-included.

**FR-F5**: Smart replan (#3) SHALL treat outdoor must-sees with this flag as
non-swappable in Bad weather (retained in itinerary), consistent with FR-F3.

### Scenarios

#### Scenario: Forced outdoor must-see scored without penalty in Bad weather
- GIVEN an outdoor must-see Z with `ForceIncludeDespiteWeather = true`
- AND `ScoringContext.IsBadWeather = true`, `ForceIncludeDespiteWeather = true`
- WHEN `CandidateScorer.Score(Z, context)` runs
- THEN the score equals the score with `IsBadWeather = false` (no
  `OutdoorWeatherPenalty`, no `IndoorWeatherBonus`)
- AND distance and popularity components are still applied

#### Scenario: Non-forced outdoor must-see still penalized
- GIVEN an outdoor must-see W with `ForceIncludeDespiteWeather = false`
- AND `IsBadWeather = true`
- WHEN scored
- THEN the `OutdoorWeatherPenalty` is applied (existing behavior unchanged)

#### Scenario: Backward-compatible default
- GIVEN a `MustSeeInput` submitted without `forceIncludeDespiteWeather`
- WHEN mapped to `MustSee`
- THEN `MustSee.ForceIncludeDespiteWeather = false` (existing trips unaffected)

#### Scenario: Forced flag survives equality and round-trip
- GIVEN two `MustSee` instances for the same PlaceId, one forced and one not
- WHEN compared via `Equals`
- THEN they are NOT equal (the flag is an equality component)
- AND both round-trip through EF with their respective flag values

### API Contract
No dedicated endpoint. Surfaced via:
- `MustSeeInput.forceIncludeDespiteWeather` (optional `bool`, default `false`) in
  `POST /trips` and `PATCH /trips/{tripId}` (`mustSeesToAdd`).
- `MustSeeResponse.forceIncludeDespiteWeather` (`bool`) in `TripPlanResponse`.

### Domain Changes
| Artifact | Change |
|----------|--------|
| `MustSee` (VO) | Add `ForceIncludeDespiteWeather` ctor param + equality component |
| `MustSeeInput` (record) | Add `bool ForceIncludeDespiteWeather = false` |
| `MustSeeResponse` (record) | Add `bool ForceIncludeDespiteWeather` |
| `ScoringContext` (record) | Add `bool ForceIncludeDespiteWeather = false` |
| `CandidateScorer` | Branch on forced flag in weather block |
| EF Migration #2 | Add column to `MustSee` owned table |

### Error Cases
No new error cases. Invalid `bool` values are rejected by JSON/model binding
(400). The flag is a pure scoring modifier.

### Test Strategy
- **Domain tests** (`MustSeeTests`): equality with/without flag; default false.
  (`CandidateScorerTests`): forced outdoor skips penalty; non-forced penalized;
  indoor unaffected; forced + non-Bad weather unaffected.
- **Handler/mapping tests**: `MustSeeInput` → `MustSee` → `MustSeeResponse`
  round-trip preserves the flag; omitted field defaults false.
- Target ~6 new tests.

---

## Itinerary-Generation Delta (Modified Capability)

Delta against `openspec/specs/itinerary-generation/spec.md`. New capabilities
(#1–#4) are full specs above; the following are the formal ADDED/MODIFIED
requirements for the `itinerary-generation` domain.

### ADDED Requirements

#### Requirement: IItineraryReplanningEngine partial replanning domain service

A new Domain port `IItineraryReplanningEngine` (impl in `Domain/Services`) SHALL
perform partial replanning on an existing `Trip.Days` collection WITHOUT clearing
all days. It MUST lock `IsCompleted` activities and preserve must-sees, then
re-run `ICandidateFiller`, `ITransitEnricher`, and `ITimelineScheduler` for
affected blocks only. It SHALL expose `RegenerateDayAsync` (single day) and
`ReplanAsync` (scope-driven from current block/day/remaining trip). It MUST NOT
mutate days/blocks outside the requested scope.

- **Scenario: Completed activities never moved or deleted**
  - GIVEN a day with a completed activity A and a candidate C
  - WHEN `RegenerateDayAsync` runs for that day
  - THEN A remains in its block with `IsCompleted = true`; C may be replaced
- **Scenario: Scope isolation**
  - GIVEN `Scope = CurrentBlock` on day 1 Afternoon
  - WHEN `ReplanAsync` runs
  - THEN only day 1 Afternoon is mutated; day 1 Morning and all other days are
    unchanged
- **Scenario: Must-sees preserved within scope**
  - GIVEN a must-see M placed on the target day
  - WHEN the day is regenerated
  - THEN M remains in the day (may shift block for feasibility) and is not
    dropped unless over-constrained

#### Requirement: DayPlan.IsStale persisted metadata

`DayPlan` SHALL expose a persisted `bool IsStale` (default `false`) indicating the
day's weather no longer matches the latest forecast. It SHALL be set `true` by the
weather-refresh handler when the forecast changes and reset `false` by any
regeneration/replan operation that recomputes the day. It MUST NOT affect scoring
or generation logic (metadata only).

- **Scenario: Refresh marks stale on change**
  - GIVEN a day whose fresh forecast differs from stored
  - WHEN weather refresh runs
  - THEN `IsStale = true`
- **Scenario: Regeneration clears stale**
  - GIVEN a day with `IsStale = true`
  - WHEN the day is regenerated or replanned
  - THEN `IsStale = false`

### MODIFIED Requirements

#### Requirement: FR5 Weather filter per day adjusts activity selection

When `TripPreferences.WeatherAwareEnabled = true` and a day's `WeatherSummary =
Bad`, the system MUST deprioritize outdoor (`IsIndoor = false`) activities and
prefer indoor candidates. **EXCEPTION**: an outdoor must-see with
`ForceIncludeDespiteWeather = true` SHALL be scored without the outdoor weather
penalty (see FR-F3) and SHALL be retained rather than swapped during replan.
(Previously: weather penalty applied uniformly to all outdoor places.)

- **Scenario: Forced outdoor must-see exempt from weather penalty**
  - GIVEN a Bad-weather day and an outdoor must-see with
    `ForceIncludeDespiteWeather = true`
  - WHEN scoring runs
  - THEN the must-see is scored without the outdoor weather penalty
- **Scenario: Non-forced outdoor must-see still prioritized but penalized**
  - GIVEN a Bad-weather day and an outdoor must-see with the flag `false`
  - WHEN scoring runs
  - THEN the outdoor weather penalty applies (existing behavior)

### Coverage Assessment

- **Happy paths**: covered for all 5 deliverables.
- **Edge cases**: covered (no data, all unchanged, past trip, all completed, no
  candidates, out-of-range, after-trip-end, future-day completion, all-forced-Bad).
- **Error states**: covered (403/404/422/500 mappings defined per deliverable).
- **Test count estimate**: ~39 new tests (6 + 8 + 12 + 7 + 6), consistent with the
  proposal's "~30-40 new tests" target.

## Next Step

Ready for design (`sdd-design`). The design phase MUST update
`doc/architecture/endpoints.yaml` (per `api-rest-standards` skill) to: (a) replace
the `/replan` 501 stub with the implemented `TripSmartReplanRequest` + `scope`,
(b) replace the `/places/{placeId}/complete` stub with the day-scoped path and
`ActivityCompletionResponse`, and (c) add `forceIncludeDespiteWeather` to
`MustSeeInput`/`MustSeeResponse` and `isStale` to `DayPlan`.
