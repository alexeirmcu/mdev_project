# Tasks: flow1-place-domain (Phase 1 + Phase 2)

## Phase 1 Status
T1-T10: COMPLETE (44 tests passing, committed as `c120d43`)

## Workload Forecast (Phase 2)
- Estimated changed lines: ~350
- Chained PRs recommended: No
- 400-line budget risk: Low (Phase 2 only)
- Decision needed before apply: No

## Dependency Order
Phase 2 tasks MUST be executed in this order. Phase 1 is already complete.

### T11: Create FoursquareApiOptions configuration class + tests
- **Implementation**: SmartTripPlanner.Infrastructure/Configuration/FoursquareApiOptions.cs
  - Class with BaseUrl (default "https://api.foursquare.com/v3/") and ApiKey properties
  - SectionName const = "FoursquareApi"
  - DataAnnotations validation (Required for ApiKey, Url for BaseUrl)
- **Test**: tests/SmartTripPlanner.Tests/SmartTripPlanner.Infrastructure/Configuration/FoursquareApiOptionsTests.cs
  - Constructor_WithDefaults_SetsPropertiesCorrectly
  - Validation_WithMissingApiKey_ReturnsError (if using DataAnnotations)

### T12: Create Foursquare DTOs (Models)
- **Directory**: SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/Models/
- Create files:
  - FoursquarePlace.cs — FsqId, Name, Geocodes, Hours, Categories
  - FoursquareGeocodes.cs — Main (FoursquareLatLng)
  - FoursquareLatLng.cs — Latitude, Longitude
  - FoursquareHours.cs — Regular (List<FoursquareRegularHour>)
  - FoursquareRegularHour.cs — Day (int 1-7), Open (string "HH:mm"), Close (string "HH:mm")
  - FoursquareCategory.cs — Id (string), Name (string)
- All records/classes with `{ get; set; }` properties for JSON deserialization
- No tests needed (pure data classes)

### T13: Create FoursquareCategoryHeuristics + tests
- **Test first**: tests/SmartTripPlanner.Tests/SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/FoursquareCategoryHeuristicsTests.cs
  - Map_MuseumCategory_Returns120MinAndIndoor
  - Map_HistoricSite_Returns60Min
  - Map_Restaurant_Returns90Min
  - Map_NightclubCategory_ReturnsNotFamilyFriendly
  - Map_MultipleCategories_FirstMatchWins
  - Map_EmptyCategories_ReturnsDefaults
  - Map_UnknownCategory_ReturnsDefaults
- **Implementation**: SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/Mapping/FoursquareCategoryHeuristics.cs
  - Static class with Map(IEnumerable<FoursquareCategory> categories) method
  - Category ID matching with dictionary of known heuristics
  - Fallback to defaults when no categories match

### T14: Create IFoursquareApiClient interface + FoursquareApiClient implementation + tests
- **Test first**: tests/SmartTripPlanner.Tests/SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/FoursquareApiClientTests.cs
  - SearchPlacesAsync_SuccessfulResponse_ReturnsMappedPlaces
  - SearchPlacesAsync_EmptyResponse_ReturnsEmptyList
  - SearchPlacesAsync_HttpError_ReturnsEmptyList (graceful degradation)
  - GetPlaceByIdAsync_ValidId_ReturnsPlace
  - GetPlaceByIdAsync_NonExistentId_ReturnsNull
  - GetPlaceByIdAsync_HttpError_ReturnsNull
- **Implementation**: SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/IFoursquareApiClient.cs
  - Interface with SearchPlacesAsync and GetPlaceByIdAsync methods
- **Implementation**: SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/FoursquareApiClient.cs
  - Constructor injects HttpClient
  - Builds request URIs, sets headers, deserializes JSON
  - Returns FoursquarePlace DTOs (Infrastructure-only types)

### T15: Update PlaceRepository with cascade logic + tests
- **Test first**: tests/SmartTripPlanner.Tests/SmartTripPlanner.Infrastructure/Repositories/PlaceRepositoryCascadeTests.cs
  - SearchAsync_LocalResultsExist_ReturnsLocalResults_NoApiCall (verify by injecting a mock that would throw if called)
  - SearchAsync_NoLocalResults_CallsApi_ReturnsMappedResults
  - SearchAsync_NoLocalResults_ApiError_ReturnsEmptyList (graceful)
  - SearchAsync_LocalResults_StillReturnsFromDb (existing tests still pass)
- **Implementation**: SmartTripPlanner.Infrastructure/Repositories/PlaceRepository.cs
  - Add IFoursquareApiClient dependency to constructor
  - Update SearchAsync to implement cascade:
    1. Local DB query (existing logic)
    2. If count > 0 → return local
    3. Call API → map via FoursquareCategoryHeuristics → return
    4. On HttpRequestException → return empty
  - Ensure existing PlaceRepositoryTests still pass (they use InMemory without mocking foursquare)

### T16: Update InfrastructureServiceRegistration
- **File**: SmartTripPlanner.Infrastructure/InfrastructureServiceRegistration.cs
- Add:
  ```csharp
  services.AddOptions<FoursquareApiOptions>()
      .BindConfiguration(FoursquareApiOptions.SectionName)
      .ValidateDataAnnotations();
  
  services.AddHttpClient<IFoursquareApiClient, FoursquareApiClient>((sp, client) =>
  {
      var options = sp.GetRequiredService<IOptions<FoursquareApiOptions>>().Value;
      client.BaseAddress = new Uri(options.BaseUrl);
      client.DefaultRequestHeaders.Authorization =
          new System.Net.Http.Headers.AuthenticationHeaderValue(options.ApiKey);
  });
  ```
- Verify build succeeds

### T17: Add FoursquareApi section to appsettings and User Secrets setup
- **File**: SmartTripPlanner.API/appsettings.Development.json
  ```json
  {
    "FoursquareApi": {
      "BaseUrl": "https://api.foursquare.com/v3/"
    }
  }
  ```
- Document User Secrets command:
  ```bash
  dotnet user-secrets set "FoursquareApi:ApiKey" "fsq-..."
  ```
- Verify: dotnet build succeeds

### T18: Run all tests and verify
- dotnet test tests/SmartTripPlanner.Tests/SmartTripPlanner.Tests.csproj
- All Phase 1 tests (44) + Phase 2 tests pass
- 0 build errors

## Review Notes
- T11–T14 are independent (can be parallelized if desired)
- T15 depends on T13 (heuristics) + T14 (API client) + T11 (options) for the full cascade
- T16 depends on T11 (options) + T14 (API client)
- T17 is independent
- Use work-unit commits (each task group = one commit)
- **No Domain changes** — all Phase 2 work stays in Infrastructure layer
