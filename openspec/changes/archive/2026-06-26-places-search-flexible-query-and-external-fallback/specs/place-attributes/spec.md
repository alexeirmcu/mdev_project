# Delta for place-attributes

## MODIFIED Requirements

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
(Previously: no ProviderId field)

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

The database MUST enforce a case-insensitive unique constraint on the composite `(Provider, Key, Value)` of `PlaceAttribute`. `ProviderId` MUST NOT be part of the unique index — multiple places may share the same attribute with different ProviderId values (though in practice they will be the same).
(Previously: unique index on (Provider, Key, Value) — unchanged, ProviderId intentionally excluded)

## ADDED Requirements

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
