# Delta for city-interests-endpoint

## MODIFIED Requirements

### Requirement: GET /api/cities/{cityCode}/interests returns distinct attribute values

The system SHALL expose `GET /api/cities/{cityCode}/interests` that returns a JSON array of distinct `PlaceAttribute.Value` strings for all places belonging to the specified city. The endpoint MUST delegate to `IPlaceRepository.GetDistinctAttributeValuesByCityCodeAsync` which queries distinct values through the `PlacePlaceAttributes` join table linking places to shared `PlaceAttribute` entities. Results MUST be returned as `InterestsResponse` with an `Interests` property of type `string[]`.

(Previously: Distinct values queried directly from owned PlaceAttribute collection via OwnsMany. Now queried through shared entity join table.)

#### Scenario: City with known attributes returns distinct values

- GIVEN a city "madrid-es" with places linked to shared PlaceAttribute entities having values ["museum", "museum", "history", "food", "food"]
- WHEN `GET /api/cities/madrid-es/interests` is called
- THEN the response is `200 OK` with body `{ "interests": ["museum", "history", "food"] }`

#### Scenario: City with no places returns empty array

- GIVEN a city code "unknown-xx" with zero places
- WHEN `GET /api/cities/unknown-xx/interests` is called
- THEN the response is `200 OK` with body `{ "interests": [] }`

#### Scenario: Invalid city code returns 404

- GIVEN a city code that does not exist in the database
- WHEN `GET /api/cities/nonexistent/interests` is called
- THEN the response is `404 Not Found`

### Requirement: InterestsResponse DTO

The API layer SHALL define `InterestsResponse` as a record with a single `Interests` property of type `string[]`. AutoMapper does not apply; the response is constructed directly from the repository result. No change from previous spec — included for completeness.

#### Scenario: Response mapped from repository result

- GIVEN `GetDistinctAttributeValuesByCityCodeAsync` returns ["art", "history"]
- WHEN the controller maps to `InterestsResponse`
- THEN `Interests` equals ["art", "history"] with no transformation