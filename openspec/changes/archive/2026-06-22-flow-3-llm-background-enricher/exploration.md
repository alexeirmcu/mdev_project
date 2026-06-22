# Exploration: Flow 3 — LLM Background Enricher

## Current State

The Smart Trip Planner generates itineraries via `GenerateTripItineraryHandler`, which:
1. Loads the trip and validates `BaseHotel`
2. Fetches candidate places from `IPlaceRepository.GetManyByCityIdAsync`
3. Calls `IItineraryGenerator.GenerateAsync` to populate `Trip.Days`
4. Saves the trip via `ITripRepository.UpdateAsync`

**Place entities** currently carry `TypicalDurationMinutes`, `IsIndoor`, and `IsFamilyFriendly` (bool) — all populated via `FoursquareCategoryHeuristics` heuristics in `FoursquarePlaceService`. There is **no enrichment loop**; heuristic defaults are permanent.

**Key gaps:**
- No `BackgroundService` exists in the solution
- No Outbox table/entity exists
- No LLM integration (`Microsoft.Extensions.AI` is not referenced)
- `Place` lacks `FamilyFriendlyScore` (int 1–5), `Popularity` (double), and `IsEnriched` (bool)
- `FoursquareApiClient.GetPlaceByIdAsync` requests `fields=fsq_id,name,geocodes,hours,categories` — **no `tips`**
- `GenerateTripItineraryHandler` has no hook to trigger background work after save
- All 333 existing tests pass; no test infrastructure for hosted services or Outbox exists

## Affected Areas

| File | Why Affected |
|------|--------------|
| `SmartTripPlanner.Domain/AggregatesModel/Place.cs` | Add `FamilyFriendlyScore`, `Popularity`, `IsEnriched` |
| `SmartTripPlanner.Infrastructure/Configurations/PlaceConfiguration.cs` | EF config for new columns with defaults |
| `SmartTripPlanner.Infrastructure/PlannerDbContext.cs` | Add `DbSet<OutboxMessage>` |
| `SmartTripPlanner.Infrastructure/Migrations/` | New migration for Outbox table + Place columns |
| `SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/FoursquareApiClient.cs` | Add `tips` to fields parameter; add `Tips` to `FoursquarePlace` DTO |
| `SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/IFoursquareApiClient.cs` | Potentially add `GetPlaceWithTipsAsync` or extend existing method |
| `SmartTripPlanner.ApplicationServices/Handlers/GenerateTripItineraryHandler.cs` | Identify unenriched PlaceIds from itinerary and write Outbox messages |
| `SmartTripPlanner.API/Program.cs` | Register `BackgroundService` and MEAI services |
| `SmartTripPlanner.API/appsettings.json` + secrets | LLM provider configuration (API key, model, base URL) |
| `SmartTripPlanner.Infrastructure/InfrastructureServiceRegistration.cs` | Register `LlmEnrichmentBackgroundService`, `ILlmEnrichmentProcessor`, MEAI client |
| Tests | New tests for Outbox, BackgroundService, LLM prompt builder, retry logic, and handler Outbox trigger |

## Approaches

### 1. MEAI Provider Integration

**Option A — `Microsoft.Extensions.AI.OpenAI`**
- Pros: Best MEAI maturity; `ChatClient` abstraction is stable; local testing with OpenAI-compatible proxies (e.g., Ollama via `http://localhost:11434`) is trivial
- Cons: Requires OpenAI API key or proxy setup; Gemini/Vertex users need an OpenAI-compatible adapter
- Effort: Low

**Option B — `Microsoft.Extensions.AI` + Provider-Specific (Gemini/Vertex)**
- Pros: Native Gemini structured outputs (`response_mime_type: application/json`) align with the spec’s strict JSON requirement
- Cons: Provider packages for MEAI are less mature than OpenAI; harder to test locally without real credentials
- Effort: Medium

**Option C — Raw HttpClient per provider (no MEAI)**
- Pros: Full control over HTTP; no abstraction overhead
- Cons: Violates the “MEAI abstraction” decision; swapping providers requires rewriting service code; harder to mock in tests
- Effort: Medium (higher long-term)

### 2. Outbox Pattern Design

**Option A — Simple EF-Managed Outbox Table**
- `OutboxMessage` entity with `Id`, `PlaceProviderReferenceId`, `PayloadJson`, `Status` (Pending/Processing/Failed/Completed), `RetryCount`, `CreatedAt`, `ProcessedAt`, `Error`
- `BackgroundService` polls `WHERE Status = Pending AND RetryCount < MaxRetries ORDER BY CreatedAt`
- Same `PlannerDbContext` transaction for trip save + Outbox insert (atomic)
- Pros: Zero external dependencies; fits existing EF Core + PostgreSQL stack; easy to test with InMemory provider
- Cons: Home-grown retry/poison-pill logic; no out-of-the-box observability
- Effort: Medium

**Option B — MassTransit or Quartz + Outbox**
- Pros: Production-grade scheduling, retries, and dashboards
- Cons: Heavy dependency for a single background flow; conflicts with the project’s “no message bus for MVP” posture
- Effort: High

### 3. BackgroundService Architecture

**Option A — Monolithic `LlmEnrichmentBackgroundService`**
- Single class handles polling, Foursquare fetch, LLM call, JSON parsing, and Place update
- Pros: Fewer files; fast to implement
- Cons: Hard to unit test; violates SRP; retry logic mixed with business logic
- Effort: Low

**Option B — `BackgroundService` + `ILlmEnrichmentProcessor` + `ILlmClient` ports**
- `LlmEnrichmentBackgroundService`: loops, pulls pending Outbox messages, delegates to processor, handles global cancellation
- `ILlmEnrichmentProcessor` (Infrastructure): orchestrates Foursquare → LLM → Place update per message
- `ILlmClient` (Domain port): abstraction over MEAI `IChatClient`
- Pros: Testable in isolation; aligns with Clean Architecture; `ILlmClient` can be mocked
- Cons: More interfaces to define
- Effort: Medium

### 4. Extending `Place` Without Breaking 333 Tests

**Option A — Add new properties with defaults, update constructors optionally**
- `FamilyFriendlyScore` default `0` (or `-1` for “unset”)
- `Popularity` default `0.0`
- `IsEnriched` default `false`
- Add a new constructor overload or use object initializer pattern
- Pros: Existing constructors and tests remain untouched
- Cons: Slightly more constructor variants
- Effort: Low

**Option B — Add properties with defaults only, no constructor changes**
- Use C# 12 required init-only properties or simple setters with defaults
- Pros: Zero constructor churn
- Cons: Less immutable domain model; deviates from existing Place design
- Effort: Low

## Recommendation

1. **MEAI**: Use `Microsoft.Extensions.AI.Abstractions` + `Microsoft.Extensions.AI.OpenAI`. For Gemini users, the OpenAI-compatible endpoint (`https://generativelanguage.googleapis.com/v1beta/openai/`) works with the same package. This gives maximum provider swap flexibility with minimal code change.

2. **Outbox**: Simple EF-managed `OutboxMessage` table (Option A). It is the right complexity level for the current stack and avoids introducing MassTransit/Quartz for a single background worker.

3. **BackgroundService**: Split architecture (Option B). `LlmEnrichmentBackgroundService` handles the `ExecuteAsync` loop and exception logging. `LlmEnrichmentProcessor` (internal, Infrastructure) handles the per-message business flow. `ILlmClient` (Domain port) wraps MEAI for testability.

4. **Place extension**: Option A — add properties with sensible defaults and a new constructor overload. Keep `IsFamilyFriendly` (bool) alongside the new `FamilyFriendlyScore` (int 1–5); they serve different purposes (binary flag vs. graded score).

5. **Foursquare extension**: Update `GetPlaceByIdAsync` to request `tips` in the fields list. Add `List<FoursquareTip> Tips { get; set; }` to `FoursquarePlace`. No interface change needed if the method already returns the full DTO.

6. **Trigger point**: After `itineraryGenerator.GenerateAsync` in `GenerateTripItineraryHandler`, extract unique `PlaceId`s from `trip.Days` activities. Filter to those where `Place.IsEnriched == false`. Call a new `IOutboxWriter.EnqueueAsync(...)` with the provider reference IDs. The Outbox writer shares the same `PlannerDbContext` (via `IUnitOfWork`), so the Outbox insert and `tripRepository.UpdateAsync` participate in the same transaction.

7. **Retry strategy**: 
   - `RetryCount` max = 3
   - Backoff: `2^RetryCount * 30s` (30s, 60s, 120s)
   - On final failure: mark `Status = Failed`, log error, do NOT block the BackgroundService
   - Use `IChatClient.CompleteAsync` with a structured JSON schema (MEAI `ChatOptions.ResponseFormat`) to force valid JSON without regex hacking.

## Risks

- **Migration collision**: The project already deleted and recreated migrations once (Flow 1). Adding Place columns + Outbox table requires a new migration. Ensure it is generated against the latest snapshot.
- **Test count explosion**: 333 tests exist. Adding BackgroundService tests requires `Microsoft.Extensions.Hosting` test helpers or manual `ExecuteAsync` invocation. Avoid spinning up real hosted services in unit tests.
- **LLM non-determinism**: Even with structured JSON mode, providers may return null fields or out-of-range ints. Validate every parsed field before applying to `Place`.
- **Foursquare tips field availability**: The `tips` field may require a higher Foursquare tier or have different schema than assumed. Verify the API contract before committing to the prompt design.
- **Concurrent enrichment**: If two trips include the same unenriched place, the BackgroundService may process both messages concurrently. Use optimistic concurrency (unique constraint on `Place.ProviderReferenceId`) or idempotent updates in `PlaceRepository`.
- **Secrets management**: The existing Foursquare pattern uses `IOptions<FoursquareApiOptions>` bound to config + user secrets. Follow the same pattern for `LlmApiOptions`.

## Ready for Proposal

**Yes.** 

The orchestrator should tell the user:
1. The exploration confirms the gaps (no BackgroundService, no Outbox, no MEAI, missing Place fields, no tips fetch).
2. The recommended path is: simple EF Outbox + split BackgroundService/Processor + MEAI with OpenAI package (swappable to Gemini via OpenAI-compatible endpoint).
3. A new migration will be needed for the `OutboxMessage` table and Place columns.
4. Existing 333 tests can be preserved; new tests will cover Outbox, processor, and LLM client mocking.
5. The next step is to create the **Proposal** artifact that scopes the work into reviewable chunks.
