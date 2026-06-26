# Delta for place-search-handler

## MODIFIED Requirements

### R1: SearchPlaces Request & Response

`SearchPlaces : IRequest<PlaceSearchResponse>` MUST expose:

| Field | Type | Required | Default |
|-------|------|----------|---------|
| `Query` | `string?` | No | null |
| `CityId` | `string` | Yes | — |
| `MaxResults` | `int` | No | 20 |
| `Category` | `string?` | No | null |
| `FetchFromExternalIfInsufficient` | `bool?` | No | true |

`PlaceSearchResponse` MUST wrap `IReadOnlyList<PlaceModel> Results`.
(Previously: no Category or FetchFromExternalIfInsufficient fields)

#### Scenario: Valid request passes through to repository
- GIVEN `SearchPlaces` with Query="Museum", CityId="madrid-es", Category=null, FetchFromExternalIfInsufficient=true
- WHEN the handler processes the request
- THEN it passes (Query, CityId, MaxResults) to `IPlaceRepository.SearchAsync`

#### Scenario: Category-only search
- GIVEN `SearchPlaces` with Query=null, Category="Museum", CityId="madrid-es"
- WHEN the handler processes the request
- THEN local `SearchAsync` is called with query=null, category="Museum"

### R3: SearchPlacesHandler

The handler MUST inject `IPlaceRepository`, `IMapper`, and a dedup orchestrator/helper for external fallback. The handler SHALL:
1. Call `SearchAsync(query, cityCode, maxResults, category)` for local results.
2. If `localCount < maxResults` AND `FetchFromExternalIfInsufficient != false`:
   a. Resolve category name to `fsq_category_ids` (via repository or service).
   b. Call external service with resolved IDs.
   c. Merge external results by `ProviderReferenceId` — external basic fields win, enrichment preserved.
   d. Persist merged/inserted places via `UpsertRangeAsync`.
3. Return combined results mapped to `PlaceModel`.
(Previously: handler delegated entirely to repository cascade, no external fallback control)

#### Scenario: Local results sufficient, no external call
- GIVEN `SearchAsync` returns 10 local results and `maxResults=10`
- WHEN the handler processes the request
- THEN external service is NOT called
- AND `PlaceSearchResponse.Results` contains 10 items

#### Scenario: Insufficient local results triggers external fallback
- GIVEN `SearchAsync` returns 3 local results, `maxResults=10`, `FetchFromExternalIfInsufficient=true`
- WHEN the handler processes the request
- THEN it resolves category → fsq_category_ids
- AND calls external service
- AND merges results

#### Scenario: External fallback disabled by flag
- GIVEN `SearchAsync` returns 3 local results, `FetchFromExternalIfInsufficient=false`
- WHEN the handler processes the request
- THEN external service is NOT called
- AND only the 3 local results are returned

#### Scenario: Cold start skips external
- GIVEN category "Museum" has no matching `PlaceAttribute` in local DB (no ProviderId)
- WHEN the handler resolves category → fsq_category_ids
- THEN resolution returns empty
- AND external call is skipped

#### Scenario: Empty results for non-matching query
- GIVEN `SearchAsync` returns an empty list
- WHEN the handler processes the request
- THEN `PlaceSearchResponse.Results` is an empty (non-null) list
