# Delta for place

## ADDED Requirements

### Requirement: Place Enrichment Fields

The `Place` entity MUST carry enrichment metadata populated by the LLM background enricher. New fields: `FamilyFriendlyScore` (int, range 1-5, default 3), `Popularity` (double, range 0.0-1.0, default 0.5), `IsEnriched` (bool, default false). These fields MUST be persisted as EF Core columns with configured defaults in `PlaceConfiguration`. The existing `IsFamilyFriendly` (bool) SHALL remain alongside `FamilyFriendlyScore` (graded score) — they serve different purposes.

#### Scenario: New Place has default enrichment values

- GIVEN a Place created via existing constructors
- WHEN inspected
- THEN `FamilyFriendlyScore = 3`, `Popularity = 0.5`, `IsEnriched = false`

#### Scenario: Enrichment fields persisted and retrieved

- GIVEN a Place with `FamilyFriendlyScore = 4`, `Popularity = 0.8`, `IsEnriched = true`
- WHEN saved and reloaded via EF Core
- THEN all three fields are correctly persisted with their values

### Requirement: Place.MarkEnriched Method

The `Place` entity MUST expose `MarkEnriched(int typicalDurationMinutes, bool isIndoor, int familyFriendlyScore, double popularity)` that validates all inputs and sets `IsEnriched = true`. `FamilyFriendlyScore` MUST be 1-5; `Popularity` MUST be 0.0-1.0; `TypicalDurationMinutes` MUST be > 0. Out-of-range values SHALL throw `SmartTripDomainException` and MUST NOT mutate any field.

#### Scenario: Valid enrichment marks Place as enriched

- GIVEN a Place with `IsEnriched = false`
- WHEN `MarkEnriched(120, true, 4, 0.8)` is called
- THEN `IsEnriched = true`, `TypicalDurationMinutes = 120`, `IsIndoor = true`, `FamilyFriendlyScore = 4`, `Popularity = 0.8`

#### Scenario: Out-of-range FamilyFriendlyScore throws

- GIVEN a Place
- WHEN `MarkEnriched(60, true, 6, 0.5)` is called
- THEN `SmartTripDomainException` is thrown
- AND `IsEnriched` remains false and no field is mutated

#### Scenario: Out-of-range Popularity throws

- GIVEN a Place
- WHEN `MarkEnriched(60, true, 3, 1.5)` is called
- THEN `SmartTripDomainException` is thrown
- AND `IsEnriched` remains false and no field is mutated
