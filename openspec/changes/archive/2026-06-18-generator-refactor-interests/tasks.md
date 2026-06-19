# Tasks: Generator Refactor + Interests

## PR #1: Model + API (~10 tasks, ~400 LOC)

### T1: ActivityNode.Location
- Add `PlaceLocation Location { get; }` to ActivityNode
- Update constructor and all call sites
- Update EF Core `TripConfiguration` (3 owned activity tables)
- Create migration `AddActivityNodeLocation`
- **Files**: ActivityNode.cs, TripConfiguration.cs, HeuristicItineraryGenerator.cs, tests

### T2: TripPreferences.Interests
- Add `IReadOnlyList<string> Interests` to TripPreferences
- Update constructor and `GetEqualityComponents`
- Update `TripPreferencesInput` record
- Update EF Core config (PostgreSQL `text[]`)
- Create migration `AddTripPreferencesInterests`
- **Files**: TripPreferences.cs, TripPreferencesInput.cs, TripConfiguration.cs, AutoMapperProfile.cs

### T3: PlaceRepository interest filtering
- Update `IPlaceRepository.GetManyByCityIdAsync` signature with `IEnumerable<string>? interests`
- Implement inclusive filtering: `Attributes.Any(a => interests.Contains(a.Value))`
- Must translate to SQL (no in-memory filtering)
- **Files**: IPlaceRepository.cs, PlaceRepository.cs

### T4: GetCityInterests endpoint
- Create `GetCityInterests` query + handler
- Create `GetCityInterestsValidator`
- Add `CitiesController` with `GET /api/cities/{cityCode}/interests`
- Returns distinct `PlaceAttribute.Value` for the city
- **Files**: GetCityInterests.cs, GetCityInterestsHandler.cs, GetCityInterestsValidator.cs, CitiesController.cs

### T5: GenerateTripValidator update
- Add rule: `Preferences.Interests` must not be null/empty
- Update validator tests
- **Files**: GenerateTripValidator.cs, GenerateTripValidatorTests.cs

### T6: Handler updates for PR1
- Update `GenerateTripHandler` to map `Interests`
- Update `GenerateTripItineraryHandler` to pass interests to repo
- Update `UpdateTripHandler` to support updating interests
- **Files**: GenerateTripHandler.cs, GenerateTripItineraryHandler.cs, UpdateTripHandler.cs

## PR #2: Generator Refactor (~9 tasks, ~500 LOC) [COMPLETE]

### T7: PinnedMustSeePlacer
- Extract `TryPlacePinnedMustSee` into `PinnedMustSeePlacer` class
- Interface: `IPinnedMustSeePlacer`
- Method: `Place(Trip trip, MustSee mustSee, Place place) -> bool`
- **Files**: IPinnedMustSeePlacer.cs, PinnedMustSeePlacer.cs

### T8: UnpinnedMustSeePlacer
- Extract `TryPlaceUnpinnedMustSee` + zone clustering into `UnpinnedMustSeePlacer`
- Interface: `IUnpinnedMustSeePlacer`
- Method: `Place(Trip trip, MustSee mustSee, Place place) -> bool`
- **Files**: IUnpinnedMustSeePlacer.cs, UnpinnedMustSeePlacer.cs

### T9: CandidateFiller with real distance
- Extract `FillCandidatesAsync` into `CandidateFiller`
- Interface: `ICandidateFiller`
- Implement real Haversine distance using `ActivityNode.Location`
- Remove hardcoded `1.0` from `EstimateDistanceFromNearestActivity`
- **Files**: ICandidateFiller.cs, CandidateFiller.cs

### T10: TransitEnricher
- Extract `EnrichTransitAndWeatherAsync` into `TransitEnricher`
- Interface: `ITransitEnricher`
- Remove dependency on mutable `_placesById`; pass `IReadOnlyDictionary<long, Place>` explicitly
- **Files**: ITransitEnricher.cs, TransitEnricher.cs

### T11: Refactor HeuristicItineraryGenerator
- Replace god method with collaborator injection
- Remove mutable `_placesById` state
- Constructor takes: `IPinnedMustSeePlacer`, `IUnpinnedMustSeePlacer`, `ICandidateFiller`, `ITransitEnricher`, `ICandidateScorer`, `ITransitCalculator`
- **Files**: HeuristicItineraryGenerator.cs

### T12: DI registration
- Register new collaborators in `ApplicationServicesRegistration.cs`
- **Files**: ApplicationServicesRegistration.cs

### T13: Update all tests
- Update `HeuristicItineraryGeneratorTests` for new collaborators
- Add unit tests for each phase class
- Update handler tests for new signatures
- **Files**: HeuristicItineraryGeneratorTests.cs, new test files

## Verification
- [x] All 268 existing tests pass
- [x] New tests for each phase collaborator
- [x] Integration tests for full generation pipeline
- [ ] Endpoint tests for city interests (PR #1 scope)
- [ ] EF migrations apply cleanly (PR #1 scope)
- [x] Distance scoring produces different (better) results than stub
