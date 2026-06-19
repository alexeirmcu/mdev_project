# Delta for trip-interests

## MODIFIED Requirements

### Requirement: TripPreferences supports IReadOnlyList<string> Interests

`TripPreferences` MUST include an `Interests` property of type `IReadOnlyList<string>`. Interests are matched inclusively against `PlaceAttribute.Value` — a place matches if ANY of its linked attribute values matches ANY of the trip's interests. Interests MUST be case-insensitive. Empty or null interests list means "no filtering" (backward compatible).

(Previously: Interests were matched against PlaceAttribute.Value via direct owned collection navigation. Now matched through shared entity join table.)

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
- WHEN compared against linked PlaceAttribute values "museum" and "food"
- THEN the comparison matches regardless of case

## Non-Functional Requirements

- Interest filtering MUST remain SQL-translatable through the join table (Place→PlacePlaceAttributes→PlaceAttribute).
- No in-memory filtering after materialization for `GetCandidatesByCityAndInterestsAsync`.