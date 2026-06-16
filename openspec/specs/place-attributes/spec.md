# Specification: place-attributes

## Purpose

Provider-agnostic key-value attribute system for Place entities, enabling search across external provider metadata without coupling domain logic to any specific provider.

## Functional Requirements

### FR1: PlaceAttribute ValueObject

The system SHALL provide `PlaceAttribute` inheriting from `ValueObject`:

| Property | Type | Constraint |
|----------|------|------------|
| Provider | string | Required, non-empty, max 100 |
| Key | string | Required, non-empty, max 100 |
| Value | string | Required, non-empty, max 500 |

Equality MUST be based on (Provider, Key, Value).

#### Scenario: Valid construction and equality

- GIVEN Provider="foursquare", Key="category", Value="Hotel"
- WHEN a PlaceAttribute is created
- THEN all properties are set and two instances with same values are equal

#### Scenario: Null/empty validation

- GIVEN a null or empty Provider, Key, or Value
- WHEN PlaceAttribute is constructed
- THEN `SmartTripDomainException` is thrown

### FR2: Place Attributes Collection

`Place` MUST expose `List<PlaceAttribute> Attributes` (initialized empty) and `AddAttribute(PlaceAttribute)` that appends to the collection.

#### Scenario: Add attribute to place

- GIVEN a Place with no attributes
- WHEN `AddAttribute(new PlaceAttribute("foursquare", "category", "Hotel"))` is called
- THEN `Attributes.Count` is 1 and the attribute is present

## Non-Functional Requirements

- Only Pro-tier Foursquare data (free, no extra cost)
- Generic design — no Foursquare-specific logic in domain layer
- EF Core `OwnsMany` for attribute collection (separate table)
- Case-insensitive search across Name and Attribute.Value
