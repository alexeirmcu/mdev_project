# Trip Interests Specification

## Purpose

Add interest-based trip filtering to `TripPreferences` and enforce it during trip creation validation, enabling users to specify what kinds of activities they care about.

## Requirements

### Requirement: TripPreferences supports IReadOnlyList<string> Interests

`TripPreferences` MUST include an `Interests` property of type `IReadOnlyList<string>`. Interests are matched inclusively against `PlaceAttribute.Value` — a place matches if ANY of its linked attribute values (through the `PlacePlaceAttributes` join table) matches ANY of the trip's interests. Interests MUST be case-insensitive. Empty or null interests list means "no filtering" (backward compatible).

#### Scenario: Trip created with interest list

- GIVEN `TripPreferences` constructed with interests ["museum", "food"]
- WHEN the preferences object is created
- THEN `Interests` returns ["museum", "food"] as `IReadOnlyList<string>`

#### Scenario: Trip created with null or empty interests — backward compatible

- GIVEN a `TripPreferences` constructed without specifying interests
- WHEN the default constructor is used
- THEN `Interests` is an empty list (not null)
- AND itinerary generation uses unfiltered candidates as fallback

#### Scenario: Interests are case-insensitive

- GIVEN `TripPreferences` with interests ["Museum", "FOOD"]
- WHEN compared against linked place attribute values "museum" and "food"
- THEN the comparison matches regardless of case

### Requirement: GenerateTripValidator requires at least one interest

`GenerateTripValidator` MUST enforce that `Payload.Preferences.Interests` contains at least one non-empty string. This rule applies ONLY on new trip creation (POST), not on trip regeneration or update. Existing trips without interests continue to work without modification.

#### Scenario: New trip with valid interests passes validation

- GIVEN a `GenerateTrip` command with `Preferences.Interests = ["museum"]`
- WHEN `GenerateTripValidator` validates
- THEN validation passes with no errors

#### Scenario: New trip with empty interests list fails validation

- GIVEN a `GenerateTrip` command with `Preferences.Interests = []`
- WHEN `GenerateTripValidator` validates
- THEN validation fails with error: "At least one interest is required."
- AND the error code is `REQUIRED_FIELD`

#### Scenario: New trip with null interests fails validation

- GIVEN a `GenerateTrip` command with `Preferences.Interests = null`
- WHEN `GenerateTripValidator` validates
- THEN validation fails with error: "At least one interest is required."

#### Scenario: Existing trip without interests continues working

- GIVEN a previously created trip with no interests stored
- WHEN the trip is regenerated or its itinerary is recalculated
- THEN the system falls back to unfiltered candidates without validation errors

### Requirement: TripPreferences persistence uses PostgreSQL text[]

EF Core MUST persist `TripPreferences.Interests` as a PostgreSQL `text[]` array column. The column SHALL allow NULL values for backward compatibility with existing rows. A migration MUST be provided that adds the column with NULL default.

#### Scenario: Interests persisted as array

- GIVEN a `TripPreferences` with interests ["museum", "food"]
- WHEN saved to the database
- THEN the column stores the value as a PostgreSQL `text[]` array
- AND retrieving the entity returns the same interests list

#### Scenario: Existing rows have NULL interests column

- GIVEN an existing `TripPreferences` row without the Interests column
- WHEN the migration is applied
- THEN the column is added with NULL default
- AND existing rows return an empty list when accessed (not null)