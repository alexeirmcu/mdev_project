# Background Enrichment Processing Specification

## Purpose

Defines the BackgroundService polling loop, processor orchestration, message leasing, error handling, and graceful shutdown for the LLM enrichment pipeline.

## Requirements

### Requirement: LlmEnrichmentBackgroundService Loop

The system MUST provide `LlmEnrichmentBackgroundService` as a hosted service (`IHostedService`) that polls pending Outbox messages at the configured `PollingIntervalSeconds` interval and delegates each to `ILlmEnrichmentProcessor`. The loop MUST continue after individual message failures. The loop MUST respect the `CancellationToken` (`stoppingToken`) on shutdown.

#### Scenario: Loop polls and processes pending messages

- GIVEN 2 Pending Outbox messages
- WHEN the loop iterates
- THEN both messages are delegated to the processor

#### Scenario: Loop continues after processing failure

- GIVEN a message that causes the processor to throw
- WHEN the loop processes it
- THEN the exception is logged and the loop continues to the next message

#### Scenario: Loop stops on cancellation

- GIVEN the service is running
- WHEN `stoppingToken` is cancelled
- THEN the loop exits cleanly without processing remaining messages

### Requirement: ILlmEnrichmentProcessor Flow

`ILlmEnrichmentProcessor` MUST orchestrate per-message: mark message `Processing` → fetch optional Foursquare tips (gated) → build prompt → call `ILlmClient` → parse/validate JSON → call `Place.MarkEnriched` → mark message `Completed`. On failure: increment `RetryCount`, set `NextAttemptAt` per backoff formula, or mark `Failed` when `RetryCount >= MaxRetries`.

#### Scenario: Successful processing

- GIVEN a Pending message for an unenriched place
- WHEN the processor handles it
- THEN the Place is enriched (`IsEnriched = true`) and the message is `Completed`

#### Scenario: LLM failure triggers retry

- GIVEN a Processing message where `ILlmClient` throws
- WHEN the processor handles the failure
- THEN `RetryCount` is incremented, `NextAttemptAt` is set per backoff, `Status = Pending`

#### Scenario: Max retries exceeded marks Failed

- GIVEN a message with `RetryCount = 3` that fails again
- WHEN the processor handles the failure
- THEN `Status = Failed`, `Error` is recorded, and the message is not retried

### Requirement: Message Leasing

A message MUST be marked `Processing` before work begins, preventing concurrent processors from picking the same message. The poller MUST exclude messages with `Status = Processing` from results.

#### Scenario: Concurrent processing prevented

- GIVEN a message already in `Processing` state
- WHEN the poller queries for Pending messages
- THEN that message is NOT returned

### Requirement: Message Lease Reclamation

A message in `Processing` state MUST be automatically reclaimed if it has been in that state for longer than the configured `LeaseTimeoutSeconds` (default 300 seconds / 5 minutes). Reclamation MUST reset the message to `Pending` with `NextAttemptAt = null` so it can be picked up by the next polling cycle. This prevents messages from being lost when a processor crashes or the service restarts mid-work.

#### Scenario: Stuck Processing message reclaimed after lease timeout

- GIVEN a message in `Processing` state with `UpdatedAt = now - 6 minutes` and `LeaseTimeoutSeconds = 300`
- WHEN the reclamation sweep runs before polling
- THEN the message is reset to `Pending` and `NextAttemptAt = null`

#### Scenario: Recent Processing message not reclaimed

- GIVEN a message in `Processing` state with `UpdatedAt = now - 2 minutes` and `LeaseTimeoutSeconds = 300`
- WHEN the reclamation sweep runs
- THEN the message remains in `Processing` state

### Requirement: Error Handling and Poison Pill

Final failures (max retries exceeded) MUST be marked `Failed` with the error message stored in the `Error` field. The BackgroundService MUST NOT crash on any single message failure. Failed messages SHALL be excluded from future polling and logged for manual intervention.

#### Scenario: Poison pill isolated

- GIVEN a message that always fails
- WHEN it reaches max retries
- THEN it is marked `Failed` and excluded from future polling

### Requirement: Graceful Shutdown

The service MUST respect `stoppingToken`. In-progress work SHOULD complete or the message SHOULD return to `Pending` state so it is not lost between service restarts.

#### Scenario: Shutdown preserves in-flight message

- GIVEN a message being processed when shutdown is requested
- WHEN the service stops
- THEN the message is returned to `Pending` (or completed) and not lost
