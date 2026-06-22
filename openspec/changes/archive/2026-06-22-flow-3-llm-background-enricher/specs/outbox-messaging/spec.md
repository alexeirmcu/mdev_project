# Outbox Messaging Specification

## Purpose

EF-managed Outbox table for asynchronous background work. Stores enrichment messages atomically with the triggering transaction, polled by a BackgroundService with retry and idempotency guarantees.

## Requirements

### Requirement: OutboxMessage Entity

The system MUST define an `OutboxMessage` entity with: `Id` (Guid, PK), `PlaceProviderReferenceId` (string, required), `PayloadJson` (string, nullable), `Status` (enum: Pending, Processing, Completed, Failed), `RetryCount` (int, default 0), `MaxRetries` (int, default 3), `CreatedAt` (DateTime), `NextAttemptAt` (DateTime?), `ProcessedAt` (DateTime?), `UpdatedAt` (DateTime, auto-updated on every state change), `Error` (string?). The entity MUST be registered as `DbSet<OutboxMessage>` in `PlannerDbContext` with an EF configuration class.

#### Scenario: New message has default state

- GIVEN a new OutboxMessage created for a place
- WHEN persisted
- THEN `Status = Pending`, `RetryCount = 0`, `MaxRetries = 3`, `NextAttemptAt` is null, `UpdatedAt` is set to `CreatedAt`

#### Scenario: State change updates UpdatedAt

- GIVEN a Pending message
- WHEN its status transitions to Processing
- THEN `UpdatedAt` is updated to the current time

### Requirement: Outbox Message State Transitions

Messages MUST transition: `Pending → Processing → Completed` (success); `Pending → Processing → Pending` (retry with `RetryCount++` and `NextAttemptAt = now + backoff`); `Pending → Processing → Failed` (when `RetryCount >= MaxRetries`). No other transitions are valid.

#### Scenario: Success path

- GIVEN a Pending message picked for processing
- WHEN processing succeeds
- THEN `Status = Completed`, `ProcessedAt` is set

#### Scenario: Retry path

- GIVEN a Processing message that fails with `RetryCount = 1`
- WHEN retry is scheduled
- THEN `Status = Pending`, `RetryCount = 2`, `NextAttemptAt = now + 60s`

#### Scenario: Final failure path

- GIVEN a Processing message that fails with `RetryCount = 3` (equals MaxRetries)
- WHEN final failure is recorded
- THEN `Status = Failed`, `Error` contains the exception message, `NextAttemptAt` is null

### Requirement: Outbox Write Transaction Boundary

`IOutboxWriter.EnqueueAsync` MUST participate in the same EF Core `DbContext` transaction as the triggering operation. The Outbox insert and the domain save MUST be atomic — either both commit or both roll back.

#### Scenario: Rollback cancels Outbox insert

- GIVEN a trip save and Outbox enqueue in the same transaction
- WHEN the transaction rolls back
- THEN no OutboxMessage row is persisted

### Requirement: Outbox Polling and Retry Policy

The polling query MUST select `WHERE Status = Pending AND (NextAttemptAt IS NULL OR NextAttemptAt <= now) ORDER BY CreatedAt`. Backoff formula: `2^RetryCount * 30s` (yielding 30s, 60s, 120s for retries 1, 2, 3).

#### Scenario: Pending messages polled in CreatedAt order

- GIVEN 3 Pending messages with CreatedAt t1 < t2 < t3
- WHEN the poller queries
- THEN messages are returned in t1, t2, t3 order

#### Scenario: Future NextAttemptAt skipped

- GIVEN a Pending message with `NextAttemptAt = now + 60s`
- WHEN the poller queries
- THEN that message is NOT returned (backoff not yet elapsed)

### Requirement: Outbox Idempotency

The system SHOULD prevent duplicate Pending or Processing messages for the same `PlaceProviderReferenceId`. If a Pending or Processing message already exists for a place, a new enqueue for that place SHALL be skipped (no-op, no exception).

#### Scenario: Duplicate enqueue skipped

- GIVEN a Pending OutboxMessage for place "abc123"
- WHEN `EnqueueAsync("abc123")` is called again
- THEN no new message is created and no exception is thrown
