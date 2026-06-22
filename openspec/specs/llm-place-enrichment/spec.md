# LLM Place Enrichment Specification

## Purpose

Defines the LLM prompt orchestration, JSON response schema, validation rules, Place update logic, and configuration for background enrichment via Microsoft.Extensions.AI.

## Requirements

### Requirement: ILlmClient Domain Port

The Domain layer MUST define `ILlmClient` as a port wrapping MEAI `IChatClient` for testability. Method: `Task<string> GetEnrichmentJsonAsync(string prompt, CancellationToken ct)`. The Infrastructure adapter `LlmClient` MUST use `IChatClient` with structured JSON response format (`ChatOptions.ResponseFormat`).

#### Scenario: ILlmClient returns JSON string

- GIVEN a valid prompt and a working IChatClient
- WHEN `GetEnrichmentJsonAsync` is called
- THEN a JSON string is returned

#### Scenario: ILlmClient propagates failures

- GIVEN an IChatClient that throws
- WHEN `GetEnrichmentJsonAsync` is called
- THEN the exception propagates to the caller (no silent swallow)

### Requirement: LLM Prompt Construction

The prompt MUST include the place name, categories (from `Place.Attributes`), and opening hours. The prompt MUST optionally include Foursquare tips gated by `UseFoursquarePremiumFields`. The prompt MUST request a JSON response with fields: `TypicalDurationMinutes`, `IsIndoor`, `FamilyFriendlyScore`, `Popularity`.

#### Scenario: Prompt includes place metadata

- GIVEN a Place "Prado Museum" with category attributes ["museum", "art"] and opening hours
- WHEN the prompt is built
- THEN the prompt contains the name, categories, and hours

#### Scenario: Tips included when premium fields enabled

- GIVEN `UseFoursquarePremiumFields = true` and Foursquare tips available for the place
- WHEN the prompt is built
- THEN the prompt includes the tips text

#### Scenario: Tips excluded when premium fields disabled

- GIVEN `UseFoursquarePremiumFields = false`
- WHEN the prompt is built
- THEN no Foursquare tips are fetched or included in the prompt

### Requirement: LLM JSON Response Schema and Validation

The response MUST be valid JSON with: `TypicalDurationMinutes` (int, range 15-480), `IsIndoor` (bool), `FamilyFriendlyScore` (int, range 1-5), `Popularity` (double, range 0.0-1.0). Every field MUST be validated before applying to `Place`. Out-of-range, missing, or null fields SHALL cause the enrichment to fail (triggering retry). Malformed JSON SHALL throw and trigger retry.

#### Scenario: Valid JSON parsed and applied

- GIVEN an LLM response `{"TypicalDurationMinutes":120,"IsIndoor":true,"FamilyFriendlyScore":4,"Popularity":0.8}`
- WHEN parsed and validated
- THEN all values pass validation and are applied to Place via `MarkEnriched`

#### Scenario: Out-of-range value triggers failure

- GIVEN an LLM response with `FamilyFriendlyScore: 7`
- WHEN parsed and validated
- THEN validation fails, no Place update occurs, and the message is retried

#### Scenario: Malformed JSON triggers failure

- GIVEN an LLM response that is not valid JSON
- WHEN parsing is attempted
- THEN parsing throws and the Outbox message is scheduled for retry

### Requirement: Configuration Options

The system MUST define `LlmApiOptions` (`ApiKey`, `BaseUrl`, `Model`) and `LlmEnrichmentOptions` (`UseFoursquarePremiumFields` default false, `MaxRetries` default 3, `PollingIntervalSeconds`). Options MUST be bound via `IOptions<T>` pattern in Infrastructure registration. The API key MUST be stored in User Secrets for development, following the existing `FoursquareApiOptions` pattern.

#### Scenario: Default configuration values

- GIVEN `LlmEnrichmentOptions` without explicit configuration
- WHEN bound from config
- THEN `UseFoursquarePremiumFields = false`, `MaxRetries = 3`

#### Scenario: Premium fields flag gates Foursquare tips fetch

- GIVEN `UseFoursquarePremiumFields = false`
- WHEN enrichment processing runs
- THEN Foursquare tips fetch is skipped entirely (no API call made)
