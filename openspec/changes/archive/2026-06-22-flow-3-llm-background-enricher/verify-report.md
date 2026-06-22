# Verify Report: Flow 3 — LLM Background Enricher

**Change:** `flow-3-llm-background-enricher`
**Date:** 2026-06-22
**Mode:** OpenSpec file persistence
**Verdict:** PASS WITH WARNINGS

---

## 1. Test Execution

| Metric | Value |
|--------|-------|
| Command | `dotnet test` |
| Total tests | 405 |
| Passed | 405 |
| Failed | 0 |
| Skipped | 0 |
| Duration | ~5s |
| Exit code | 0 |

### Build / Type-check

| Metric | Value |
|--------|-------|
| Command | `dotnet build SmartTripPlanner.slnx` |
| Warnings | 0 |
| Errors | 0 |
| Result | Build succeeded |

### Task Completion

| Metric | Value |
|--------|-------|
| Total tasks | 37 |
| Completed (checked) | 37 |
| Incomplete | 0 |

All 37 tasks across 6 phases are marked complete.

---

## 2. Spec Compliance Matrix

### Place Spec (`specs/place/spec.md`)

| Requirement / Scenario | Status | Evidence |
|---|---|---|
| `FamilyFriendlyScore` (int, default 3) exists | PASS | `Place.cs:19` — `= 3`; test `DefaultFamilyFriendlyScore_Is3` |
| `Popularity` (double, default 0.5) exists | PASS | `Place.cs:20` — `= 0.5`; test `DefaultPopularity_Is05` |
| `IsEnriched` (bool, default false) exists | PASS | `Place.cs:21` — `= false`; test `DefaultIsEnriched_IsFalse` |
| `IsFamilyFriendly` remains alongside new score | PASS | `Place.cs:17` — `IsFamilyFriendly` preserved |
| EF config for new columns with defaults | PASS | `PlaceConfiguration.cs:60-62` — `HasDefaultValue(3/0.5/false)` |
| `MarkEnriched` validates range 1-5 score | PASS | `Place.cs:66-67`; tests `MarkEnriched_With*Score*` |
| `MarkEnriched` validates range 0.0-1.0 popularity | PASS | `Place.cs:68-69`; tests `MarkEnriched_WithPopularity*` |
| `MarkEnriched` validates duration > 0 | PASS | `Place.cs:70-71`; tests `MarkEnriched_WithDuration*` |
| Out-of-range throws `SmartTripDomainException` | PASS | `Place.cs:67,69,71`; all 6 range tests pass |
| Out-of-range does not mutate fields | PASS | test `MarkEnriched_AfterException_DoesNotMutateFields` |
| Valid enrichment sets `IsEnriched = true` | PASS | `Place.cs:77`; test `MarkEnriched_WithValidInputs_*` |

### Itinerary Generation Spec (`specs/itinerary-generation/spec.md`)

| Requirement / Scenario | Status | Evidence |
|---|---|---|
| Extracts unique PlaceIds from all days/activities | PASS | `GenerateTripItineraryHandler.cs:40-44` — `.Distinct()` |
| Filters to `IsEnriched == false` | PASS | `Handler.cs:46` — `.Where(p => !p.IsEnriched)` |
| Calls `IOutboxWriter.EnqueueAsync` before trip save | PASS | `Handler.cs:53` before `UpdateAsync` at line 61 |
| Enqueue participates in same EF transaction | PASS | `OutboxWriter` tracks entities, no `SaveChanges`; handler's `UpdateAsync` provides atomic save |
| Best-effort try/catch, doesn't block response | PASS | `Handler.cs:38,56-59` — try/catch with `LogWarning` |
| Unenriched places queued after generation | PASS | test `Handle_WithUnenrichedPlaces_EnqueuesOutboxMessages` — verifies 2 refIds enqueued |
| Already enriched places are skipped | PASS | test `Handle_AllPlacesEnriched_DoesNotEnqueueOutbox` — verifies `Times.Never` |
| Duplicate PlaceIds deduplicated | PASS | `Handler.cs:44` — `.Distinct()` on PlaceIds; line 48 — `.Distinct()` on refIds |
| Outbox trigger failure does not block response | PASS | test `Handle_OutboxWriterThrows_StillCallsUpdateAsync` — verifies `UpdateAsync` still called |

### Outbox Messaging Spec (`specs/outbox-messaging/spec.md`)

| Requirement / Scenario | Status | Evidence |
|---|---|---|
| `OutboxMessage` entity with all 11 required fields | PASS | `OutboxMessage.cs:5-15` — Id, PlaceProviderReferenceId, PayloadJson, Status, RetryCount, MaxRetries, CreatedAt, NextAttemptAt, ProcessedAt, UpdatedAt, Error |
| `DbSet<OutboxMessage>` registered | PASS | `PlannerDbContext.cs:15` |
| EF configuration class exists | PASS | `OutboxMessageConfiguration.cs` |
| New message has default state (Pending, 0, 3, null) | PASS | `OutboxMessage.cs:22-35`; test `Create_WithValidRefId_SetsDefaultState` |
| State change updates `UpdatedAt` | PASS | All transition methods set `UpdatedAt = DateTime.UtcNow`; test `MarkProcessing_SetsStatusAndUpdatesTimestamp` |
| Pending → Processing → Completed (success) | PASS | `MarkProcessing`/`MarkCompleted`; test `FullLifecycle_SuccessPath` |
| Retry path: RetryCount++ + NextAttemptAt backoff | PASS | `ScheduleRetry`; test `ScheduleRetry_CalculatesBackoffAndIncrementsCount` |
| Final failure: Status=Failed, Error set, NextAttemptAt=null | PASS | `MarkFailed`; test `MarkFailed_SetsStatusErrorAndClearsNextAttempt` |
| Backoff formula: `2^RetryCount * 30s` (30s, 60s, 120s) | PASS | `OutboxMessage.cs:52`; test `ScheduleRetry_MultipleRetries_ExponentialBackoff` |
| Polling query: Status=Pending, NextAttemptAt null/<=now, ORDER BY CreatedAt | PASS | `OutboxMessageRepository.cs:17-22`; test `GetPendingAsync_WithPendingMessages_ReturnsInOrder` |
| Future NextAttemptAt skipped | PASS | test `GetPendingAsync_WithFutureNextAttemptAt_SkipsMessage` |
| Idempotency: duplicate Pending/Processing skipped | PASS | `OutboxWriter.cs:17-33`; tests `EnqueueAsync_WithDuplicateRefId_SkipsExisting*` |
| Lease reclamation resets Processing after timeout | PASS | `OutboxMessageRepository.cs:25-37`; test `ReclaimExpiredLeasesAsync_ReclaimsStuckProcessing` |
| Recent Processing not reclaimed | PASS | test `ReclaimExpiredLeasesAsync_DoesNotReclaimRecentProcessing` |
| Status enum stored as string | PASS | `OutboxMessageConfiguration.cs:26` — `HasConversion<string>()` |
| Composite index (Status, NextAttemptAt, CreatedAt) | PASS | `OutboxMessageConfiguration.cs:46-47` |

### LLM Place Enrichment Spec (`specs/llm-place-enrichment/spec.md`)

| Requirement / Scenario | Status | Evidence |
|---|---|---|
| `ILlmClient` port in Domain (no MEAI refs) | PASS | `Domain/Ports/ILlmClient.cs`; Domain.csproj has only `DI.Abstractions` |
| `LlmClient` wraps MEAI `IChatClient` with JSON format | PASS | `LlmClient.cs:28` — `ResponseFormat = ChatResponseFormat.Json` |
| ILlmClient returns JSON string | PASS | test `GetEnrichmentJsonAsync_WithValidResponse_ReturnsJsonString` |
| ILlmClient propagates failures | PASS | test `GetEnrichmentJsonAsync_WhenChatClientThrows_PropagatesException` |
| Prompt includes place name | PASS | `PromptBuilder.cs:11`; test `Build_WithPlace_ContainsName` |
| Prompt includes categories | PASS | `PromptBuilder.cs:13-19`; test `Build_WithPlace_ContainsCategories` |
| Prompt includes opening hours | PASS | `PromptBuilder.cs:21-30`; test `Build_WithPlace_ContainsOpeningHours` |
| Tips included when premium enabled | PASS | `PromptBuilder.cs:32-35`; test `Build_WithTipsText_IncludesTips` |
| Tips excluded when premium disabled | PASS | Processor `cs:69-77` gates fetch; test `Build_WithoutTipsText_*` + `ProcessAsync_WithPremiumFieldsDisabled_*` |
| JSON response: duration 15-480 validated | PASS | `PlaceEnrichmentResponse.cs:12-13`; tests `Validate_WithDuration*` |
| JSON response: score 1-5 validated | PASS | `Response.cs:14-15`; tests `Validate_WithScore*` |
| JSON response: popularity 0.0-1.0 validated | PASS | `Response.cs:16-17`; tests `Validate_WithPopularity*` |
| Valid JSON parsed and applied via MarkEnriched | PASS | test `ProcessAsync_WithValidLlmResponse_EnrichesPlaceAndCompletes` |
| Out-of-range value triggers retry | PASS | test `ProcessAsync_WithOutOfRangeValues_SchedulesRetry` |
| Malformed JSON triggers retry | PASS | test `ProcessAsync_WithInvalidJson_SchedulesRetry` |
| `LlmApiOptions` (ApiKey, BaseUrl, Model) | PASS | `LlmApiOptions.cs`; bound in `InfrastructureServiceRegistration.cs:46-47` |
| `LlmEnrichmentOptions` with defaults | PASS | `LlmEnrichmentOptions.cs` — UseFoursquarePremiumFields=false, MaxRetries=3, PollingIntervalSeconds=30 |
| Options bound via IOptions<T> pattern | PASS | `InfrastructureServiceRegistration.cs:46-50` |
| Premium fields flag gates Foursquare tips fetch | PASS | `Processor.cs:69`; test `ProcessAsync_WithPremiumFieldsEnabled_FetchesFoursquareTips` |

### Background Enrichment Processing Spec (`specs/background-enrichment-processing/spec.md`)

| Requirement / Scenario | Status | Evidence |
|---|---|---|
| `LlmEnrichmentBackgroundService` as HostedService | PASS | `InfrastructureServiceRegistration.cs:56` — `AddHostedService<>` |
| Polls pending messages at configured interval | WARN | Hardcodes 30s instead of `PollingIntervalSeconds` (see Issues #1) |
| Delegates to processor per message | PASS | `BackgroundService.cs:46` — `processor.ProcessAsync(message.Id, ...)` |
| Loop continues after individual failures | PASS | `BackgroundService.cs:44-52` — per-message try/catch; test `ExecuteAsync_WithProcessorException_ContinuesToNextMessage` |
| Respects CancellationToken on shutdown | PASS | `BackgroundService.cs:27,41-42,64-70`; test `ExecuteAsync_OnCancellation_StopsGracefully` |
| Processor: Processing → Foursquare → Prompt → LLM → Validate → MarkEnriched → Completed | PASS | `Processor.cs:49-95`; test `ProcessAsync_WithValidLlmResponse_*` |
| On failure: retry with backoff | PASS | `Processor.cs:100-115`; test `ProcessAsync_WithLlmExceptionAndRetryBelowMax_SchedulesRetry` |
| Marked Failed after max retries | PASS | `Processor.cs:105-107`; test `ProcessAsync_WithMaxRetriesExceeded_MarksFailed` |
| Message leasing: Processing excluded from poll | PASS | `OutboxMessageRepository.cs:18` — `Status == Pending`; test `GetPendingAsync_WithProcessingMessages_ExcludesThem` |
| Lease reclamation after timeout | PASS | `OutboxMessageRepository.cs:25-37`; tests `Reclaim*` |
| Graceful shutdown preserves in-flight messages | PASS | Message stays Processing → reclaimed by lease on next start (design: 324) |
| Poison pill isolated (Failed excluded + logged) | PASS | `Processor.cs:105-107` + `MarkFailed`; test `ProcessAsync_WithMaxRetriesExceeded_MarksFailed` |

---

## 3. Design Compliance

| Design Item | Status | Evidence |
|---|---|---|
| Clean Architecture: Domain has no MEAI references | PASS | `Domain.csproj` — only `Microsoft.Extensions.DependencyInjection.Abstractions` |
| `ILlmClient` / `IOutboxWriter` contracts match design | PASS | Signatures match design exactly |
| `OutboxMessage` entity fields match design | PASS | All 11 fields present with correct types |
| State transition methods match design | PASS | Create, MarkProcessing, MarkCompleted, ScheduleRetry, MarkFailed, Reclaim all present |
| EF config: indexes, defaults, string conversion for enum | PASS | `OutboxMessageConfiguration.cs` — composite index, `HasConversion<string>()`, defaults |
| EF config: Place columns with defaults | PASS | `PlaceConfiguration.cs:60-62` |
| DI registration order matches design | PASS | Options → ILlmClient → IOutboxWriter → IOutboxMessageRepository → ILlmEnrichmentProcessor → HostedService |
| IChatClient registered in Program.cs after AddInfrastructure | PASS | `Program.cs:24,27-33` |
| Configuration options follow FoursquareApiOptions pattern | PASS | `SectionName` const + `BindConfiguration` |
| Migration: Place columns + OutboxMessages table + composite index | PASS | `20260622104111_AddLlmEnrichmentOutbox.cs` — 3 AddColumn + CreateTable + CreateIndex |
| `OutboxWriter` tracks entities, no SaveChanges | PASS | `OutboxWriter.cs` — `.Add(message)` only; test `EnqueueAsync_DoesNotCallSaveChanges` |
| BackgroundService uses `IServiceScopeFactory` per iteration | PASS | `BackgroundService.cs:31` — `CreateScope()` each iteration |
| Lease reclamation before each poll cycle | PASS | `BackgroundService.cs:36` — `ReclaimExpiredLeasesAsync` before `GetPendingAsync` |
| Processor retry logic formula | DEVIATION | Design says `RetryCount + 1 >= MaxRetries`; implementation uses `RetryCount >= MaxRetries`. See Issues #2 |
| `LlmApi`/`LlmEnrichment` sections in appsettings.json | PASS | `appsettings.json:20-31` — both sections present |

---

## 4. Code Quality

| Item | Status | Evidence |
|---|---|---|
| No compilation warnings | PASS | `0 Warning(s), 0 Error(s)` |
| Existing code not unnecessarily modified | PASS | Existing Place constructor/fields preserved; Foursquare `includeTips` is additive default param |
| Naming conventions consistent with project | PASS | Matches existing patterns (PascalCase, private set, internal sealed classes) |
| Async/await used correctly | PASS | All async methods return Task/Task<T>; no `.Result`/`.Wait()` observed |
| Cancellation tokens propagated | PASS | Handler passes `ct`; BackgroundService passes `stoppingToken` to all async calls; Processor accepts and passes `ct` |

---

## 5. Issues Found

### WARNING #1: BackgroundService hardcodes polling interval

**Where:** `Infrastructure/Background/LlmEnrichmentBackgroundService.cs:64`
```csharp
await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
```

**Problem:** The design and spec both require polling at the configured `PollingIntervalSeconds` interval. The `options` variable (resolved at line 32) is scoped to the inner `try` block and is out of scope at line 64. The delay hardcodes 30 seconds, ignoring `LlmEnrichmentOptions.PollingIntervalSeconds` for non-default configurations.

**Impact:** Functionally correct with the default value (30s). If an operator configures `PollingIntervalSeconds` to a different value (e.g., 5s for faster dev iteration, or 60s for production), the BackgroundService will NOT respect it — it always waits 30s between iterations.

**Severity:** WARNING — does not break any spec scenario (no scenario tests a non-default interval), but violates the configurable-interval design intent and spec requirement text ("polls pending Outbox messages at the configured PollingIntervalSeconds interval").

**Recommended fix:** Capture the polling interval into a local variable before the inner `try` block, or restructure to make `options` accessible at the delay point:
```csharp
int pollingIntervalSeconds = 30;
// inside try:
pollingIntervalSeconds = options.PollingIntervalSeconds;
// at delay:
await Task.Delay(TimeSpan.FromSeconds(pollingIntervalSeconds), stoppingToken);
```

### SUGGESTION #2: Processor retry threshold differs from design pseudocode

**Where:** `Infrastructure/LLM/LlmEnrichmentProcessor.cs:105`
```csharp
if (message.RetryCount >= message.MaxRetries)
```

**Problem:** The design document's error-handling pseudocode (design.md:331) specifies `message.RetryCount + 1 >= message.MaxRetries`, which yields 3 total processing attempts. The implementation uses `message.RetryCount >= message.MaxRetries`, yielding 4 total attempts (1 initial + 3 retries).

**Spec compliance:** The **spec requirement text** explicitly says "mark Failed when RetryCount >= MaxRetries" and the spec scenario says "GIVEN a message with RetryCount = 3 (equals MaxRetries)". The implementation matches the spec exactly. The design document's pseudocode is the outlier.

**Severity:** SUGGESTION — the implementation is spec-compliant. This is a documentation inconsistency in the design doc, not a code deficiency. Recommend updating the design pseudocode to match the spec and implementation (`RetryCount >= MaxRetries`).

---

## 6. Final Verdict

**PASS WITH WARNINGS**

### Rationale

All 37 tasks are complete. All 405 tests pass (333 existing + 72 new). Build is clean (0 warnings, 0 errors). All 5 spec files have their requirements and scenarios implemented with covering runtime test evidence. Clean Architecture is respected. The migration includes all required schema changes. The DI registration and configuration follow existing project patterns.

One WARNING exists: the BackgroundService hardcodes the polling delay to 30s instead of using the configured `PollingIntervalSeconds`. This is a design/spec deviation that is functionally invisible with default configuration but breaks the configurability intent. It does not cause any test failure or spec scenario violation.

One SUGGESTION notes that the processor retry threshold (`RetryCount >= MaxRetries`) matches the spec requirement text and scenario but differs from the design document's pseudocode. The implementation is spec-compliant; the design doc is the inconsistency.

Neither issue is CRITICAL. The change is ready for archive pending the orchestrator's decision on the WARNING.