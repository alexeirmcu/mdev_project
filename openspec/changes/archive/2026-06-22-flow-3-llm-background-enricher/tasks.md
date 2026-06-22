# Tasks: Flow 3 — LLM Background Enricher

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 900–1200 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (Domain + Outbox) → PR 2 (LLM + Processor + Service) → PR 3 (Integration + Migration) |
| Delivery strategy | ask-on-risk |
| Chain strategy | stacked-to-main |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Domain ports + Outbox infrastructure | PR 1 | Base branch: main; tests included |
| 2 | LLM adapter + Processor + BackgroundService | PR 2 | Base branch: main; depends on PR 1 types |
| 3 | Handler wiring + DI + Migration + Tests | PR 3 | Base branch: main; depends on PR 1 + PR 2 |

## Phase 1: Domain Foundation

- [x] 1.1 Modify `Domain/AggregatesModel/Place.cs`: add `FamilyFriendlyScore` (int, default 3), `Popularity` (double, default 0.5), `IsEnriched` (bool, default false); add `MarkEnriched(...)` with range validation.
- [x] 1.2 Create `Domain/Ports/ILlmClient.cs`: port with `Task<string> GetEnrichmentJsonAsync(string prompt, CancellationToken ct)`.
- [x] 1.3 Create `Domain/Ports/IOutboxWriter.cs`: port with `Task EnqueueAsync(IEnumerable<string> placeProviderReferenceIds, CancellationToken ct)`.
- [x] 1.4 Test: `Place.MarkEnriched` valid inputs set all fields + `IsEnriched = true`.
- [x] 1.5 Test: `Place.MarkEnriched` throws `SmartTripDomainException` for out-of-range score, popularity, duration.

## Phase 2: Outbox Infrastructure

- [x] 2.1 Create `Infrastructure/Outbox/OutboxMessageStatus.cs`: enum `Pending, Processing, Completed, Failed`.
- [x] 2.2 Create `Infrastructure/Outbox/OutboxMessage.cs`: entity with Guid PK, `Create` factory, state transition methods (`MarkProcessing`, `MarkCompleted`, `ScheduleRetry` with backoff, `MarkFailed`, `Reclaim`).
- [x] 2.3 Create `Infrastructure/Configurations/OutboxMessageConfiguration.cs`: EF config with PK, indexes, status string conversion, defaults.
- [x] 2.4 Create `Infrastructure/Outbox/IOutboxMessageRepository.cs`: internal interface (`GetPendingAsync`, `ReclaimExpiredLeasesAsync`).
- [x] 2.5 Create `Infrastructure/Outbox/OutboxMessageRepository.cs`: implements polling queries on `PlannerDbContext`.
- [x] 2.6 Create `Infrastructure/Outbox/OutboxWriter.cs`: implements `IOutboxWriter` with idempotency check + entity tracking (no SaveChanges).
- [x] 2.7 Modify `Infrastructure/PlannerDbContext.cs`: add `DbSet<OutboxMessage> OutboxMessages`.
- [x] 2.8 Test: `OutboxMessage` state transitions (Create, MarkProcessing, ScheduleRetry backoff calc, MarkFailed, Reclaim).
- [x] 2.9 Test: `OutboxWriter` enqueue + idempotency (InMemory DbContext; seed existing Pending → verify duplicate skipped).
- [x] 2.10 Test: `OutboxMessageRepository` polling order + lease reclamation (seed various Status/NextAttemptAt → verify filtering).

## Phase 3: LLM Infrastructure

- [x] 3.1 Create `Infrastructure/LLM/LlmApiOptions.cs`: config `ApiKey`, `BaseUrl`, `Model` (`SectionName = "LlmApi"`).
- [x] 3.2 Create `Infrastructure/LLM/LlmEnrichmentOptions.cs`: config `UseFoursquarePremiumFields` (false), `MaxRetries` (3), `PollingIntervalSeconds` (30), `LeaseTimeoutSeconds` (300), `BatchSize` (10).
- [x] 3.3 Create `Infrastructure/LLM/PlaceEnrichmentResponse.cs`: DTO with 4 fields + `Validate()` (duration 15-480, score 1-5, popularity 0.0-1.0).
- [x] 3.4 Create `Infrastructure/LLM/PlaceEnrichmentPromptBuilder.cs`: static builder — name, categories, hours, optional tips, JSON schema instruction.
- [x] 3.5 Create `Infrastructure/LLM/ILlmEnrichmentProcessor.cs`: internal interface `Task ProcessAsync(Guid messageId, CancellationToken ct)`.
- [x] 3.6 Create `Infrastructure/LLM/LlmClient.cs`: implements `ILlmClient` via MEAI `IChatClient` with `ChatResponseFormat.Json`.
- [x] 3.7 Create `Infrastructure/LLM/LlmEnrichmentProcessor.cs`: orchestrates Foursquare→Prompt→LLM→Parse→MarkEnriched→Complete/Retry/Failed.
- [x] 3.8 Modify `Infrastructure/ExternalServices/Foursquare/IFoursquareApiClient.cs`: add `bool includeTips = false` to `GetPlaceByIdAsync`.
- [x] 3.9 Modify `Infrastructure/ExternalServices/Foursquare/FoursquareApiClient.cs`: implement `includeTips` — append `,tips` to fields.
- [x] 3.10 Modify `Infrastructure/ExternalServices/Foursquare/Models/FoursquarePlace.cs`: add `List<FoursquareTip> Tips`.
- [x] 3.11 Test: `LlmClient` — mock `IChatClient`, verify JSON returned, exceptions propagated.
- [x] 3.12 Test: `PromptBuilder` — verify prompt contains name/categories/hours; tips included/excluded.
- [x] 3.13 Test: `PlaceEnrichmentResponse.Validate()` — valid/invalid ranges.

## Phase 4: Background Service

- [x] 4.1 Create `Infrastructure/Background/LlmEnrichmentBackgroundService.cs`: hosted service — loop, `IServiceScopeFactory` per iteration, reclaim expired leases, poll pending, delegate to processor, per-message error isolation, graceful shutdown via `stoppingToken`.
- [x] 4.2 Test: `LlmEnrichmentBackgroundService` — real `ServiceCollection`, mocked processor + InMemory repository; verify loop, per-message error isolation, cancellation.
- [x] 4.3 Test: `LlmEnrichmentProcessor` integration — InMemory DbContext (seed OutboxMessage + Place); mock `ILlmClient` (valid JSON, invalid JSON, throw); mock `IFoursquareApiClient`; verify Place enriched, message Completed/Retried/Failed.

## Phase 5: Integration & Wiring

- [x] 5.1 Modify `ApplicationServices/Handlers/GenerateTripItineraryHandler.cs`: inject `IOutboxWriter`; extract unenriched PlaceIds from Days/Activities; call `EnqueueAsync` before `UpdateAsync`; wrap in try/catch (best-effort).
- [x] 5.2 Modify `Infrastructure/InfrastructureServiceRegistration.cs`: register `LlmApiOptions`, `LlmEnrichmentOptions`, `ILlmClient`, `IOutboxWriter`, `IOutboxMessageRepository`, `ILlmEnrichmentProcessor`, `AddHostedService<LlmEnrichmentBackgroundService>`.
- [x] 5.3 Modify `Infrastructure/SmartTripPlanner.Infrastructure.csproj`: add `Microsoft.Extensions.AI.Abstractions` package.
- [x] 5.4 Modify `API/Program.cs`: register `IChatClient` via MEAI OpenAI extension after `AddInfrastructure`.
- [x] 5.5 Modify `API/SmartTripPlanner.API.csproj`: add `Microsoft.Extensions.AI.OpenAI` package.
- [x] 5.6 Modify `API/appsettings.json`: add `LlmApi` and `LlmEnrichment` sections.
- [x] 5.7 Test: `GenerateTripItineraryHandler` — mock `IOutboxWriter`; verify enqueue called with unenriched refIds, dedup, best-effort failure, all-enriched skip.

## Phase 6: Migration

- [x] 6.1 Generate EF migration: `dotnet ef migrations add AddLlmEnrichmentOutbox --project SmartTripPlanner.Infrastructure --startup-project SmartTripPlanner.API`.
- [x] 6.2 Review migration: verify Place columns (`FamilyFriendlyScore`, `Popularity`, `IsEnriched`) + `OutboxMessages` table with composite index `(Status, NextAttemptAt, CreatedAt)`.
- [x] 6.3 Verify all 333 existing tests still pass after migration.
