# Specification: place-attributes

## Purpose

Provider-agnostic key-value attribute system for Place entities, enabling search across external provider metadata without coupling domain logic to any specific provider.

## Functional Requirements

### FR1: PlaceAttribute Entity

The system SHALL provide `PlaceAttribute` inheriting from `Entity` (not `ValueObject`):

| Property | Type | Constraint |
|----------|------|------------|
| Id | long | Auto-generated, identity-based equality |
| Provider | string | Required, non-empty, max 100, init-only |
| Key | string | Required, non-empty, max 100, init-only |
| Value | string | Required, non-empty, max 500, init-only |
| ProviderId | string? | Nullable, max 100, init-only |

Equality MUST be identity-based (inherited from `Entity.Id`). `Provider`, `Key`, `Value`, and `ProviderId` MUST remain immutable — no public setters after construction. `ProviderId` stores the external provider's ID for this attribute (e.g., Foursquare category ID "10000").

#### Scenario: Valid construction with identity-based equality

- GIVEN Provider="foursquare", Key="category", Value="Hotel"
- WHEN a PlaceAttribute is created
- THEN all properties are set and `Id` is default (0, transient)
- AND two transient instances with same (Provider, Key, Value) are NOT equal by `Entity.Equals` (different objects)

#### Scenario: Null/empty validation

- GIVEN a null or empty Provider, Key, or Value
- WHEN PlaceAttribute is constructed
- THEN `SmartTripDomainException` is thrown with the same validation rules as before

#### Scenario: Valid construction without ProviderId
- GIVEN Provider="foursquare", Key="category", Value="Hotel"
- WHEN a PlaceAttribute is created
- THEN ProviderId is null

#### Scenario: Valid construction with ProviderId
- GIVEN Provider="foursquare", Key="category", Value="Museum", ProviderId="10000"
- WHEN a PlaceAttribute is created
- THEN ProviderId="10000"

#### Scenario: Immutability after construction

- GIVEN a PlaceAttribute with Provider="foursquare", Key="category", Value="Hotel", ProviderId="10000"
- WHEN a caller attempts to change any property
- THEN the properties are not settable (init-only or no public setter)

### FR2: Case-insensitive unique constraint on PlaceAttribute

The database MUST enforce a case-insensitive unique constraint on the composite `(Provider, Key, Value)` of `PlaceAttribute`. `ProviderId` MUST NOT be part of the unique index — multiple places may share the same attribute with different ProviderId values (though in practice they will be the same). Duplicate attribute definitions with differing case (e.g., "Hotel" vs "hotel") MUST be rejected at the database level.

#### Scenario: Duplicate with different case is rejected

- GIVEN a PlaceAttribute with Value="Hotel" persisted
- WHEN another PlaceAttribute with same Provider, Key, and Value="hotel" is inserted
- THEN the database rejects the insert with a unique constraint violation

#### Scenario: Same value same case is rejected

- GIVEN a PlaceAttribute with (Provider="foursquare", Key="category", Value="Hotel") persisted
- WHEN another PlaceAttribute with identical (Provider, Key, Value) is inserted
- THEN the database rejects the insert with a unique constraint violation

### FR3: PlaceAttribute is a shared entity

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

### FR4: ProviderId Persistence

When a PlaceAttribute is created or resolved during `UpsertRangeAsync` and the incoming data includes a `ProviderId`, it MUST be persisted on the matching PlaceAttribute row.

#### Scenario: ProviderId populated during upsert
- GIVEN a PlaceAttribute (Provider="foursquare", Key="category", Value="Museum") exists without ProviderId
- WHEN `UpsertRangeAsync` processes an incoming Place with the same attribute and ProviderId="10000"
- THEN the existing PlaceAttribute row is updated with ProviderId="10000"

#### Scenario: ProviderId retained on subsequent upserts
- GIVEN a PlaceAttribute with ProviderId="10000"
- WHEN `UpsertRangeAsync` processes an incoming Place with the same attribute but no ProviderId
- THEN the existing ProviderId is retained (not overwritten with null)

## Non-Functional Requirements

- Only Pro-tier Foursquare data (free, no extra cost)
- Generic design — no Foursquare-specific logic in domain layer
- EF Core `HasMany...WithMany` with explicit join entity `PlacePlaceAttributes` (shared entity, not owned)
- Case-insensitive search across Name and Attribute.Value
