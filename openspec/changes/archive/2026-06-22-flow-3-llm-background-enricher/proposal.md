# Proposal: Flow 3 — LLM Background Enricher

## Intent

Replace heuristic Place metadata (`TypicalDurationMinutes=60`, `IsIndoor=false`, `IsFamilyFriendly=true`) with real semantic data extracted from an LLM. After an itinerary is generated, unenriched places are queued and processed asynchronously by a BackgroundService using `Microsoft.Extensions.AI`.

## Scope

### In Scope
- Extend `Place` with `FamilyFriendlyScore`, `Popularity`, `IsEnriched`
- Add `OutboxMessage` entity and EF-managed Outbox table
- Split BackgroundService architecture (`LlmEnrichmentBackgroundService`, `ILlmEnrichmentProcessor`, `ILlmClient`)
- Integrate `Microsoft.Extensions.AI.Abstractions` + OpenAI-compatible provider
- Trigger Outbox writes from `GenerateTripItineraryHandler` after itinerary generation
- Foursquare `tips` field fetch (optional, gated by `UseFoursquarePremiumFields`)
- Retry policy: max 3 attempts with exponential backoff

### Out of Scope
- Weather stub (`StubbedWeatherProvider`) implementation
- Automatic replanning engine
- MassTransit/Quartz (home-grown Outbox only)
- Raw HTTP LLM clients (MEAI abstraction is required)

## Capabilities

### New Capabilities
- `llm-place-enrichment`: LLM prompt orchestration, JSON parsing, and Place update logic
- `outbox-messaging`: EF-managed `OutboxMessage` persistence, polling, and retry bookkeeping
- `background-enrichment-processing`: `BackgroundService` loop, message leasing, and poison-pill handling

### Modified Capabilities
- `place`: Add `FamilyFriendlyScore` (int, default 3), `Popularity` (double, default 0.5), `IsEnriched` (bool, default false)
- `itinerary-generation`: After `IItineraryGenerator.GenerateAsync`, enqueue unenriched `PlaceId`s into Outbox before returning response

## Approach

Use MEAI `IChatClient` with structured JSON response format. A domain port `ILlmClient` wraps MEAI for testability. `GenerateTripItineraryHandler` uses an `IOutboxWriter` port (implemented in Infrastructure) to enqueue messages in the same EF transaction as the trip save. The `LlmEnrichmentBackgroundService` polls pending messages and delegates to `ILlmEnrichmentProcessor`, which fetches optional Foursquare tips, builds a prompt, parses the LLM JSON response, validates ranges, and updates the `Place`. Retries use `2^count * 30s` backoff; final failures are marked `Failed` and logged.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Domain/AggregatesModel/Place.cs` | Modified | New properties with defaults |
| `Infrastructure/Configurations/PlaceConfiguration.cs` | Modified | EF config for new columns |
| `Infrastructure/PlannerDbContext.cs` | Modified | Add `DbSet<OutboxMessage>` |
| `Infrastructure/Migrations/` | New | Migration for Outbox + Place columns |
| `ApplicationServices/Handlers/GenerateTripItineraryHandler.cs` | Modified | Enqueue unenriched places after generation |
| `Infrastructure/InfrastructureServiceRegistration.cs` | Modified | Register BackgroundService, processor, MEAI client |
| `API/Program.cs` | Modified | Add MEAI services and hosted service |
| `API/appsettings.json` | Modified | `LlmApiOptions` section |
| `Infrastructure/Background/` | New | `LlmEnrichmentBackgroundService`, `LlmEnrichmentProcessor` |
| `Domain/Ports/ILlmClient.cs` | New | Domain port wrapping MEAI |
| `Infrastructure/LLM/` | New | `LlmClient` adapter, prompt builder, options |
| `Domain/Ports/IOutboxWriter.cs` | New | Outbox write port |
| `Infrastructure/Outbox/` | New | `OutboxMessage` entity, `OutboxWriter`, configuration |
| `Tests/` | New | Outbox, processor, BackgroundService, LLM client mocking |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| LLM returns out-of-range or null fields | Medium | Validate every parsed field before updating Place |
| Concurrent enrichment of same Place | Medium | Idempotent update in PlaceRepository; unique index on `ProviderReferenceId` |
| Migration collision with existing snapshot | Low | Generate migration against latest snapshot; review diff |
| Foursquare tips require paid tier | Low | Gate with `UseFoursquarePremiumFields` (default false) |
| Test count explosion / hosted service testing | Low | Invoke `ExecuteAsync` directly in tests; do not spin up real host |

## Rollback Plan

1. Revert the migration (`dotnet ef migrations remove` or apply down script)
2. Remove `DbSet<OutboxMessage>` and BackgroundService registration from `Program.cs`
3. Revert `GenerateTripItineraryHandler` to remove Outbox trigger
4. Keep new `Place` columns nullable/with defaults so existing data remains valid

## Dependencies

- `Microsoft.Extensions.AI.Abstractions`
- `Microsoft.Extensions.AI.OpenAI` (or provider-specific MEAI package)
- Existing Foursquare API client (optional tips fetch)

## Success Criteria

- [ ] 333 existing tests continue passing
- [ ] New tests cover Outbox persistence, BackgroundService loop, Processor orchestration, LLM client mocking, and Place enrichment validation
- [ ] Unenriched places appearing in generated itineraries are queued within the same transaction as the trip save
- [ ] BackgroundService processes pending messages and updates `Place.IsEnriched = true` with valid metadata
- [ ] Failed messages are retried up to 3 times and then marked `Failed` without crashing the service
- [ ] `UseFoursquarePremiumFields = false` skips Foursquare tips fetch entirely
