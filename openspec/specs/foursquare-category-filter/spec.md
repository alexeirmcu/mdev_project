# Specification: foursquare-category-filter

## Purpose

Infrastructure-level capability that resolves human-readable category names (e.g., "Museum") to Foursquare-specific category IDs (`fsq_category_ids`), enabling the search handler to push category filters to the Foursquare Places API. Handles cold-start scenarios where no local category data exists.

## Requirements

### R1: Category Resolution

The system MUST resolve a category name string to a list of Foursquare category IDs (`fsq_category_ids`) by querying local `PlaceAttribute` data where `Provider="foursquare"`, `Key="category"`, and `Value` matches (case-insensitive). The `ProviderId` of matching attributes SHALL be used as the `fsq_category_ids` value.

#### Scenario: Category resolves to one ProviderId
- GIVEN a PlaceAttribute with (Provider="foursquare", Key="category", Value="Museum", ProviderId="10000")
- WHEN the system resolves "Museum"
- THEN it returns ["10000"]

#### Scenario: Category resolves to multiple ProviderIds (subcategories)
- GIVEN PlaceAttributes with (Value="Museum", ProviderId="10000") and (Value="Art Gallery", ProviderId="10001")
- WHEN the system resolves "Museum"
- THEN it returns ["10000"]

#### Scenario: Unmatched category (cold start)
- GIVEN no PlaceAttribute with Value="Aquarium" exists
- WHEN the system resolves "Aquarium"
- THEN it returns an empty list

### R2: API Parameter Injection

When resolved `fsq_category_ids` is non-empty, the `IFoursquareApiClient` call MUST include the `fsq_category_ids` query parameter with comma-separated IDs. The existing `IFoursquareApiClient.SearchPlacesAsync` SHALL accept an optional `string? fsqCategoryIds` parameter.

#### Scenario: Category filter sent to Foursquare API
- GIVEN resolved ids=["10000", "10001"]
- WHEN `IFoursquareApiClient.SearchPlacesAsync` is called with `fsqCategoryIds="10000,10001"`
- THEN the request URL includes `&fsq_category_ids=10000%2C10001`

#### Scenario: No category filter when ids empty
- GIVEN resolved ids=[] (cold start)
- WHEN `IFoursquareApiClient.SearchPlacesAsync` is called
- THEN the `fsq_category_ids` parameter is NOT included in the request

### R3: Cold Start Handling

If category resolution returns an empty list (no local data), the system MUST skip the external Foursquare call entirely and return whatever local results are available. This is NOT an error condition.

#### Scenario: Cold start returns local results only
- GIVEN category "Aquarium" has no matching PlaceAttribute
- AND 2 local places match the search otherwise
- WHEN the search handler processes the request
- THEN no external API call is made
- AND the response contains only the 2 local results

### R4: Integration Point

Category resolution SHALL be invoked by the search handler (`SearchPlacesHandler`) before calling `IPlaceExternalService`. The resolution logic MAY live in a dedicated `CategoryResolver` service within Infrastructure, or be a method on the repository (`GetProviderIdForCategoryAsync`).

#### Scenario: Handler delegates resolution before external call
- GIVEN a search request with Category="Museum"
- WHEN local results are insufficient
- THEN the handler calls the category resolver before the external service
