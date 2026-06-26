# Specification: external-fallback-dedup

## Purpose

Deduplication and merge logic for external place search results against local data. Ensures that when the system falls back to Foursquare for additional results, externally-fetched places are merged with existing local records without overwriting LLM-enriched metadata.

## Requirements

### R1: Deduplication by ProviderReferenceId

The system MUST match incoming external `Place` entities by `ProviderReferenceId` against persisted local data. If a matching local Place exists, the system SHALL update basic fields and preserve enrichment fields.

| Field | Source | Behavior |
|-------|--------|----------|
| `Name` | External | Overwrites local |
| `Location` (lat/lng) | External | Overwrites local |
| `OpeningHours` | External | Overwrites local |
| `Categories` (attributes) | External | Overwrites local |
| `FamilyFriendlyScore` | Local | Preserved (LLM enrichment) |
| `Popularity` | Local | Preserved (LLM enrichment) |
| `IsEnriched` | Local | Preserved |
| `IsIndoor` | Local | Preserved |
| `TypicalDurationMinutes` | Local | Preserved |

#### Scenario: Existing place with enrichment fields preserved
- GIVEN a local Place with ProviderReferenceId="fsq_123", Name="Old Name", FamilyFriendlyScore=4, IsEnriched=true
- WHEN an incoming external Place with ProviderReferenceId="fsq_123", Name="New Name", FamilyFriendlyScore=2 is merged
- THEN the persisted Name becomes "New Name"
- AND FamilyFriendlyScore remains 4
- AND IsEnriched remains true

#### Scenario: New external place inserted
- GIVEN no local Place with ProviderReferenceId="fsq_999"
- WHEN an incoming external Place with ProviderReferenceId="fsq_999" is processed
- THEN a new Place entity is created and persisted with default enrichment values (FamilyFriendlyScore=3, Popularity=0.5, IsEnriched=false)

### R2: ProviderId Population

When merging or inserting external places, the system MUST populate `PlaceAttribute.ProviderId` with the Foursquare category ID for each category attribute.

#### Scenario: Category attribute ProviderId set on insert
- GIVEN an external Place with categories containing (Name="Museum", Id="10000")
- WHEN the Place is persisted via `UpsertRangeAsync`
- THEN the matching PlaceAttribute has ProviderId="10000"

### R3: No Duplicate External Calls

The dedup logic MUST be idempotent — calling the same external results twice MUST NOT create duplicate Place rows.

#### Scenario: Idempotent merge
- GIVEN a Place with ProviderReferenceId="fsq_123" exists locally after first merge
- WHEN the same external Place is merged again
- THEN no duplicate row is created
- AND the existing row is updated with any new external data

### R4: Integration with UpsertRangeAsync

The dedup logic MUST be integrated into `PlaceRepository.UpsertRangeAsync`. The repository SHALL check `ProviderReferenceId` before inserting: if match found, update basic fields; if not found, insert new.

#### Scenario: Upsert delegates to dedup
- GIVEN `UpsertRangeAsync` receives a list with mixed known and unknown ProviderReferenceIds
- WHEN executed
- THEN known IDs trigger updates (basic fields only)
- AND unknown IDs trigger inserts
