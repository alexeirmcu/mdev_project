# Delta for itinerary-generation

## ADDED Requirements

### Requirement: Outbox Trigger After Itinerary Generation

After `IItineraryGenerator.GenerateAsync` populates `Trip.Days`, the `GenerateTripItineraryHandler` MUST extract unique `PlaceId`s from all activities across all days and filter to those where `Place.IsEnriched == false`. The handler MUST call `IOutboxWriter.EnqueueAsync` with the unenriched `PlaceProviderReferenceId`s BEFORE returning the response. The Outbox enqueue MUST participate in the same EF Core transaction as the trip save (`ITripRepository.UpdateAsync`).

#### Scenario: Unenriched places queued after generation

- GIVEN a generated itinerary with 5 unique places, 3 of which have `IsEnriched = false`
- WHEN the handler completes generation
- THEN 3 Outbox messages are enqueued for the unenriched places

#### Scenario: Already enriched places are skipped

- GIVEN a generated itinerary where all places have `IsEnriched = true`
- WHEN the handler completes generation
- THEN zero Outbox messages are enqueued

#### Scenario: Duplicate PlaceIds deduplicated

- GIVEN an itinerary where the same unenriched Place appears in 3 activities across 2 days
- WHEN the handler extracts unique PlaceIds
- THEN only 1 Outbox message is enqueued for that Place

#### Scenario: Outbox enqueue is atomic with trip save

- GIVEN the handler saves the trip and enqueues Outbox messages in one transaction
- WHEN the transaction commits
- THEN both the trip update and Outbox inserts are persisted
- WHEN the transaction rolls back
- THEN neither the trip update nor Outbox inserts are persisted

#### Scenario: Outbox trigger failure does not block itinerary response

- GIVEN `IOutboxWriter.EnqueueAsync` throws
- WHEN the handler catches the exception
- THEN the itinerary response SHALL still be returned (enrichment is best-effort, non-blocking)
- AND the exception is logged
