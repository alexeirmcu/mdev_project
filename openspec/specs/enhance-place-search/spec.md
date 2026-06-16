# Delta Spec: enhance-place-search

## Domain: place-attributes (NEW CAPABILITY)

### Purpose

Provider-agnostic key-value attribute system for Place entities, enabling search across external provider metadata without coupling domain logic to any specific provider.

## ADDED Requirements

### Requirement: PlaceAttribute ValueObject

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

### Requirement: Place Attributes Collection

`Place` MUST expose `List<PlaceAttribute> Attributes` (initialized empty) and `AddAttribute(PlaceAttribute)` that appends to the collection.

#### Scenario: Add attribute to place

- GIVEN a Place with no attributes
- WHEN `AddAttribute(new PlaceAttribute("foursquare", "category", "Hotel"))` is called
- THEN `Attributes.Count` is 1 and the attribute is present

---

## Domain: place (MODIFIED CAPABILITY)

## MODIFIED Requirements

### Requirement: Place Entity (FR1)

`Place` inherits from `Entity`, is `IAggregateRoot`, and exposes: `ProviderReferenceId` (string, required, unique), `Provider` (enum), `Name` (string, required), `CityId` (long), `City` (navigation), `Location` (`PlaceLocation` OwnsOne), `TypicalDurationMinutes` (int, default 60), `IsIndoor` (bool, default false), `IsFamilyFriendly` (bool, default true), `OpeningHours` (`List<OpeningHoursWindow>` OwnsMany), `Attributes` (`List<PlaceAttribute>` OwnsMany, default empty). Method `AddAttribute(PlaceAttribute)` appends to `Attributes`.
(Previously: Place had no Attributes collection or AddAttribute method)

#### Scenario: Place with attributes persists correctly

- GIVEN a Place with attributes added via AddAttribute
- WHEN the Place is saved and retrieved
- THEN Attributes are loaded with correct Provider, Key, Value

### Requirement: PlaceRepository SearchAsync (FR5)

`SearchAsync(query, cityCode, maxResults)` MUST match places where `Name.Contains(query)` OR any `Attribute.Value.Contains(query)` (case-insensitive) within the specified city. The query MUST include `Attributes` via EF Core `Include`.
(Previously: SearchAsync only matched Name.Contains(query))

#### Scenario: Search matches attribute value

- GIVEN a Place with Name="Gran Palace" and Attribute("foursquare","category","Hotel") in city "MAD"
- WHEN SearchAsync("hotel", "MAD") is called
- THEN the Place is returned

#### Scenario: Existing name search preserved

- GIVEN a Place with Name="Hotel California" in city "MAD"
- WHEN SearchAsync("hotel", "MAD") is called
- THEN the Place is returned

#### Scenario: Search matches chain attribute

- GIVEN a Place with Attribute("foursquare","chain","McDonald's") in city "MAD"
- WHEN SearchAsync("mcdonalds", "MAD") is called
- THEN the Place is returned

### Requirement: FoursquarePlaceService Mapping (FR11)

`MapToPlace` MUST iterate `apiPlace.Categories` and call `AddAttribute(new PlaceAttribute("foursquare", "category", cat.Name))` for each category. Only Pro-tier Foursquare data SHALL be used — no premium fields.
(Previously: MapToPlace discarded category data after heuristics)

#### Scenario: Categories mapped to attributes

- GIVEN an API response with Categories containing Name="Hotel" and Name="Boutique"
- WHEN MapToPlace creates a Place
- THEN Place.Attributes contains corresponding PlaceAttribute entries with Provider="foursquare", Key="category"

## ADDED Requirements

### Requirement: PlaceModel Attributes

`PlaceModel` MUST include `IReadOnlyList<PlaceAttributeModel> Attributes`. `PlaceAttributeModel` is a record with `Provider`, `Key`, `Value` (all string). `AutoMapperProfile` MUST map `PlaceAttribute` to `PlaceAttributeModel`.

#### Scenario: Attributes returned in API response

- GIVEN a Place with two attributes
- WHEN mapped to PlaceModel via AutoMapper
- THEN PlaceModel.Attributes contains matching PlaceAttributeModel entries

### Requirement: EF Core PlaceAttribute Configuration

`PlaceConfiguration` MUST configure `OwnsMany(p => p.Attributes, ...)` with separate table "PlaceAttributes", foreign key "PlaceId", and properties: Provider (max 100, required), Key (max 100, required), Value (max 500, required).

#### Scenario: Attributes persisted and loaded

- GIVEN a Place with attributes saved via EF Core
- WHEN the Place is retrieved with Include
- THEN all Attributes are loaded with correct values

## Constraints

- Only Pro-tier Foursquare data (free, no extra cost)
- Generic design — no Foursquare-specific logic in domain layer
- EF Core `OwnsMany` for attribute collection (separate table)
- No breaking changes to existing API contracts (additive only)
- Case-insensitive search across Name and Attribute.Value
