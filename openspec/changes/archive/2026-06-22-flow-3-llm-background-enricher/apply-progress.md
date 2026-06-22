# Apply Progress: Flow 3 — LLM Background Enricher

**Status**: ✅ Complete — All 37 tasks implemented in single PR (size:exception)
**Mode**: Standard (no strict TDD)
**Tests**: 405 total (333 existing + 72 new) — all passing

## Completed Tasks (37/37)

### Phase 1: Domain Foundation (5/5)
- [x] 1.1 Place.cs: Added FamilyFriendlyScore, Popularity, IsEnriched, MarkEnriched() with validation
- [x] 1.2 Created ILlmClient.cs port
- [x] 1.3 Created IOutboxWriter.cs port
- [x] 1.4 Test: MarkEnriched valid inputs
- [x] 1.5 Test: MarkEnriched throws on invalid inputs

### Phase 2: Outbox Infrastructure (10/10)
- [x] 2.1 OutboxMessageStatus enum
- [x] 2.2 OutboxMessage entity
- [x] 2.3 OutboxMessageConfiguration EF config
- [x] 2.4 IOutboxMessageRepository interface
- [x] 2.5 OutboxMessageRepository implementation
- [x] 2.6 OutboxWriter with idempotency
- [x] 2.7 PlannerDbContext DbSet<OutboxMessage>
- [x] 2.8 OutboxMessage state transition tests
- [x] 2.9 OutboxWriter enqueue/idempotency tests
- [x] 2.10 OutboxMessageRepository polling tests

### Phase 3: LLM Infrastructure (13/13)
- [x] 3.1 LlmApiOptions config
- [x] 3.2 LlmEnrichmentOptions config
- [x] 3.3 PlaceEnrichmentResponse DTO + Validate()
- [x] 3.4 PlaceEnrichmentPromptBuilder (static builder)
- [x] 3.5 ILlmEnrichmentProcessor interface
- [x] 3.6 LlmClient (MEAI IChatClient adapter)
- [x] 3.7 LlmEnrichmentProcessor (full orchestration)
- [x] 3.8 IFoursquareApiClient includeTips parameter
- [x] 3.9 FoursquareApiClient tips field implementation
- [x] 3.10 FoursquarePlace Tips property + FoursquareTip model
- [x] 3.11 LlmClient tests
- [x] 3.12 PromptBuilder tests
- [x] 3.13 PlaceEnrichmentResponse validation tests

### Phase 4: Background Service (3/3)
- [x] 4.1 LlmEnrichmentBackgroundService
- [x] 4.2 BackgroundService loop tests
- [x] 4.3 Processor integration tests

### Phase 5: Integration & Wiring (7/7)
- [x] 5.1 GenerateTripItineraryHandler: IOutboxWriter injection + outbox trigger
- [x] 5.2 InfrastructureServiceRegistration: all new DI registrations
- [x] 5.3 Infrastructure csproj: Microsoft.Extensions.AI.Abstractions
- [x] 5.4 Program.cs: IChatClient registration
- [x] 5.5 API csproj: Microsoft.Extensions.AI.OpenAI
- [x] 5.6 appsettings.json: LlmApi + LlmEnrichment sections
- [x] 5.7 Handler outbox trigger tests

### Phase 6: Migration (3/3)
- [x] 6.1 Migration generated: AddLlmEnrichmentOutbox
- [x] 6.2 Migration verified: correct columns, index, defaults
- [x] 6.3 All tests passing: 405/405

## Files Changed

### Created (17 files)
| File | Description |
|------|------------|
| `SmartTripPlanner.Domain/Ports/ILlmClient.cs` | Domain port for LLM enrichment |
| `SmartTripPlanner.Domain/Ports/IOutboxWriter.cs` | Domain port for outbox queue |
| `SmartTripPlanner.Infrastructure/Outbox/OutboxMessageStatus.cs` | Outbox status enum |
| `SmartTripPlanner.Infrastructure/Outbox/OutboxMessage.cs` | Outbox message entity with state machine |
| `SmartTripPlanner.Infrastructure/Outbox/IOutboxMessageRepository.cs` | Internal repository interface |
| `SmartTripPlanner.Infrastructure/Outbox/OutboxMessageRepository.cs` | Repository implementation |
| `SmartTripPlanner.Infrastructure/Outbox/OutboxWriter.cs` | OutboxWriter with idempotency |
| `SmartTripPlanner.Infrastructure/Configurations/OutboxMessageConfiguration.cs` | EF configuration |
| `SmartTripPlanner.Infrastructure/LLM/LlmApiOptions.cs` | LLM API configuration options |
| `SmartTripPlanner.Infrastructure/LLM/LlmEnrichmentOptions.cs` | Enrichment processing options |
| `SmartTripPlanner.Infrastructure/LLM/PlaceEnrichmentResponse.cs` | LLM response DTO |
| `SmartTripPlanner.Infrastructure/LLM/PlaceEnrichmentPromptBuilder.cs` | Static prompt builder |
| `SmartTripPlanner.Infrastructure/LLM/ILlmEnrichmentProcessor.cs` | Processor interface |
| `SmartTripPlanner.Infrastructure/LLM/LlmClient.cs` | MEAI IChatClient adapter |
| `SmartTripPlanner.Infrastructure/LLM/LlmEnrichmentProcessor.cs` | Full processing orchestrator |
| `SmartTripPlanner.Infrastructure/Background/LlmEnrichmentBackgroundService.cs` | Hosted background service |
| `SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/Models/FoursquareTip.cs` | Foursquare tip DTO |

### Modified (12 files)
| File | Description |
|------|------------|
| `SmartTripPlanner.Domain/AggregatesModel/Place.cs` | Added enrichment fields + MarkEnriched() |
| `SmartTripPlanner.Infrastructure/PlannerDbContext.cs` | Added DbSet<OutboxMessage> |
| `SmartTripPlanner.Infrastructure/InfrastructureServiceRegistration.cs` | Registered all new services |
| `SmartTripPlanner.Infrastructure/Configurations/PlaceConfiguration.cs` | Added new column EF config |
| `SmartTripPlanner.Infrastructure/SmartTripPlanner.Infrastructure.csproj` | Added MEAI + Hosting packages |
| `SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/IFoursquareApiClient.cs` | Added includeTips param |
| `SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/FoursquareApiClient.cs` | Implemented tips field fetch |
| `SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/Models/FoursquarePlace.cs` | Added Tips property |
| `SmartTripPlanner.ApplicationServices/Handlers/GenerateTripItineraryHandler.cs` | Outbox trigger injection |
| `SmartTripPlanner.API/Program.cs` | IChatClient registration |
| `SmartTripPlanner.API/SmartTripPlanner.API.csproj` | Added MEAI.OpenAI package |
| `SmartTripPlanner.API/appsettings.json` | Added LlmApi + LlmEnrichment sections |

### Migration (2 files)
| File | Description |
|------|------------|
| `SmartTripPlanner.Infrastructure/Migrations/{timestamp}_AddLlmEnrichmentOutbox.cs` | EF migration |
| `SmartTripPlanner.Infrastructure/Migrations/{timestamp}_AddLlmEnrichmentOutbox.Designer.cs` | Migration designer |

### Test Files Created (8 files)
| File | Description |
|------|------------|
| `tests/.../SmartTripPlanner.Infrastructure/Outbox/OutboxMessageTests.cs` | State transition tests |
| `tests/.../SmartTripPlanner.Infrastructure/Outbox/OutboxWriterTests.cs` | Enqueue/idempotency tests |
| `tests/.../SmartTripPlanner.Infrastructure/Outbox/OutboxMessageRepositoryTests.cs` | Polling/reclamation tests |
| `tests/.../SmartTripPlanner.Infrastructure/LLM/LlmClientTests.cs` | LLM client adapter tests |
| `tests/.../SmartTripPlanner.Infrastructure/LLM/PlaceEnrichmentPromptBuilderTests.cs` | Prompt builder tests |
| `tests/.../SmartTripPlanner.Infrastructure/LLM/PlaceEnrichmentResponseTests.cs` | Response validation tests |
| `tests/.../SmartTripPlanner.Infrastructure/LLM/LlmEnrichmentProcessorTests.cs` | Processor integration tests |
| `tests/.../SmartTripPlanner.Infrastructure/Background/LlmEnrichmentBackgroundServiceTests.cs` | Background service tests |

### Test Files Modified (2 files)
| File | Description |
|------|------------|
| `tests/.../SmartTripPlanner.Domain/AggregatesModel/PlaceTests.cs` | Added MarkEnriched tests |
| `tests/.../SmartTripPlanner.ApplicationServices/Handlers/GenerateTripItineraryHandlerTests.cs` | Added IOutboxWriter + outbox trigger tests |

## Deviations from Design
- **Processor retry check**: Used `message.RetryCount >= message.MaxRetries` instead of `message.RetryCount + 1 >= message.MaxRetries` to correctly allow MaxRetries retries before marking Failed (matching spec scenarios)
- **LlmClient response handling**: Added `string.IsNullOrEmpty` check in addition to null check for robustness
- **JsonOptions**: Added `PropertyNameCaseInsensitive = true` for deserialization resilience against casing variations

## Issues Found
- **MEAI version alignment**: Microsoft.Extensions.AI.OpenAI 10.3.0 requires Microsoft.Extensions.AI.Abstractions 10.3.0. Had to align versions.
- **BackgroundService namespace**: `Microsoft.Extensions.Hosting.Abstractions` package required for BackgroundService in Infrastructure project.
- **Handler test mapper mock issue**: Moq's `Entity.Equals` override caused mock matching failures for transient entities. Switched to `It.IsAny<Trip>()` for mapper mock in the failing test.

## Notes
- Package versions: Microsoft.Extensions.AI.Abstractions 10.3.0, Microsoft.Extensions.AI.OpenAI 10.3.0, Microsoft.Extensions.Hosting.Abstractions 8.0.1
- OpenAI SDK 2.8.0's `ChatClient` implements `IChatClient` via explicit cast
- LlmApiOptions.ApiKey stored empty in appsettings.json (filled via User Secrets per existing FoursquareApi pattern)
