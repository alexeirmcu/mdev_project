# Delta for place-attributes

## MODIFIED Requirements

### Requirement: PlaceAttribute Entity

The system SHALL provide `PlaceAttribute` inheriting from `Entity` (not `ValueObject`):

| Property | Type | Constraint |
|----------|------|------------|
| Id | long | Auto-generated, identity-based equality |
| Provider | string | Required, non-empty, max 100, init-only |
| Key | string | Required, non-empty, max 100, init-only |
| Value | string | Required, non-empty, max 500, init-only |

Equality MUST be identity-based (inherited from `Entity.Id`). Value equality by `(Provider, Key, Value)` is no longer used for object equality. `Provider`, `Key`, and `Value` MUST remain immutable — no public setters after construction.

(Previously: PlaceAttribute was a ValueObject with equality based on (Provider, Key, Value) tuple.)

#### Scenario: Valid construction with identity-based equality

- GIVEN Provider="foursquare", Key="category", Value="Hotel"
- WHEN a PlaceAttribute is created
- THEN all properties are set and `Id` is default (0, transient)
- AND two transient instances with same (Provider, Key, Value) are NOT equal by `Entity.Equals` (different objects)

#### Scenario: Null/empty validation preserved

- GIVEN a null or empty Provider, Key, or Value
- WHEN PlaceAttribute is constructed
- THEN `SmartTripDomainException` is thrown with the same validation rules as before

#### Scenario: Immutability after construction

- GIVEN a PlaceAttribute with Provider="foursquare", Key="category", Value="Hotel"
- WHEN a caller attempts to change Provider, Key, or Value
- THEN the properties are not settable (init-only or no public setter)

## ADDED Requirements

### Requirement: Case-insensitive unique constraint on PlaceAttribute

The database MUST enforce a case-insensitive unique constraint on the composite `(Provider, Key, Value)` of `PlaceAttribute`. Duplicate attribute definitions with differing case (e.g., "Hotel" vs "hotel") MUST be rejected at the database level.

#### Scenario: Duplicate with different case is rejected

- GIVEN a PlaceAttribute with Value="Hotel" persisted
- WHEN another PlaceAttribute with same Provider, Key, and Value="hotel" is inserted
- THEN the database rejects the insert with a unique constraint violation

#### Scenario: Same value same case is rejected

- GIVEN a PlaceAttribute with (Provider="foursquare", Key="category", Value="Hotel") persisted
- WHEN another PlaceAttribute with identical (Provider, Key, Value) is inserted
- THEN the database rejects the insert with a unique constraint violation

### Requirement: PlaceAttribute is a shared entity

`PlaceAttribute` rows MUST NOT be owned by a single `Place`. The same `PlaceAttribute` row MAY be linked to multiple `Place` entities through a join table. When all `Place` references are removed, the `PlaceAttribute` row MUST be retained (keep orphans).

#### Scenario: Shared attribute across multiple places

- GIVEN Place A and Place B both have category "Museum"
- WHEN both places reference the same attribute (Provider, Key, Value)
- THEN only ONE PlaceAttribute row exists for "Museum" in the database
- AND the join table links this row to both Place A and Place B

#### Scenario: Orphaned attribute is retained

- GIVEN Place A is the only place linked to PlaceAttribute "Museum"
- WHEN Place A is deleted or its link to "Museum" is removed
- THEN the PlaceAttribute row for "Museum" is NOT deleted from the database

## REMOVED Requirements

### Requirement: Place Attributes Collection

(Reason: This requirement describes Place.AddAttribute behavior, which belongs to the `place` capability. It is superseded by the many-to-many relationship delta in the `place` spec.)
(Migration: See `place` delta spec for the updated AddAttribute contract.)

## Non-Functional Requirements

- EF Core `OwnsMany` for attribute collection is REMOVED — replaced by `HasMany...WithMany` with explicit join entity.
- Case-insensitive search across Name and Attribute.Value is preserved.
- Only Pro-tier Foursquare data (free, no extra cost) — no change.
- Generic design — no Foursquare-specific logic in domain layer — no change.