# Tasks: flow1-place-domain

## Workload Forecast
- Estimated changed lines: ~350
- Chained PRs recommended: No
- 400-line budget risk: Low
- Decision needed before apply: No

## Dependency Order
Tasks MUST be executed in this order.

### T1: Add Infrastructure project reference + InMemory package to test project
- File: `tests/SmartTripPlanner.Tests/SmartTripPlanner.Tests.csproj`
- Add ProjectReference to SmartTripPlanner.Infrastructure
- Add PackageReference to Microsoft.EntityFrameworkCore.InMemory (version 8.0.10)
- Verify: dotnet build succeeds

### T2: Create PlaceLocation ValueObject + tests
- **Test class**: tests/SmartTripPlanner.Tests/SmartTripPlanner.Domain/AggregatesModel/PlaceLocationTests.cs
  - Constructor_WithValidLatLng_SetsProperties
  - Constructor_WithLatitudeTooHigh_ThrowsArgumentOutOfRangeException
  - Constructor_WithLatitudeTooLow_ThrowsArgumentOutOfRangeException
  - Constructor_WithLongitudeTooHigh_ThrowsArgumentOutOfRangeException
  - Equals_SameCoordinates_ReturnsTrue
  - Equals_DifferentCoordinates_ReturnsFalse
- **Implementation**: SmartTripPlanner.Domain/AggregatesModel/PlaceLocation.cs
  - Extends ValueObject, Latitude/Longitude validation, GetEqualityComponents

### T3: Create OpeningHoursWindow ValueObject + tests
- **Test class**: tests/SmartTripPlanner.Tests/SmartTripPlanner.Domain/AggregatesModel/OpeningHoursWindowTests.cs
  - Constructor_WithValidMinutes_SetsProperties
  - Constructor_WithOpenAfterClose_ThrowsArgumentException
  - Constructor_WithOpenMinutesNegative_ThrowsArgumentOutOfRangeException
  - Constructor_WithCloseMinutesOver1439_ThrowsArgumentOutOfRangeException
  - Equals_SameValues_ReturnsTrue
  - Equals_DifferentValues_ReturnsFalse
- **Implementation**: SmartTripPlanner.Domain/AggregatesModel/OpeningHoursWindow.cs
  - Extends ValueObject, DayOfWeek/OpenMinutes/CloseMinutes, validation, equality

### T4: Create Place entity + tests
- **Test class**: tests/SmartTripPlanner.Tests/SmartTripPlanner.Domain/AggregatesModel/PlaceTests.cs
  - Constructor_WithValidFields_SetsProperties
  - Constructor_WithNullPlaceId_ThrowsArgumentNullException
  - Constructor_WithEmptyPlaceId_ThrowsArgumentException
  - Constructor_WithNullName_ThrowsArgumentNullException
  - Constructor_WithNullLocation_ThrowsArgumentNullException
  - DefaultTypicalDurationMinutes_Is60
  - DefaultIsIndoor_IsFalse
  - DefaultIsFamilyFriendly_IsTrue
  - OpeningHours_InitiallyEmpty
- **Implementation**: SmartTripPlanner.Domain/AggregatesModel/Place.cs
  - Entity + IAggregateRoot, properties, constructor validation, OpeningHours init

### T5: Create IPlaceRepository interface
- **File**: SmartTripPlanner.Domain/Repository/IPlaceRepository.cs
- Implements IRepository<Place>
- SearchAsync(string query, string cityId, int maxResults = 20)
- GetByPlaceIdAsync(string placeId)

### T6: Create Place EF Core Configuration
- **File**: SmartTripPlanner.Infrastructure/Configurations/PlaceConfiguration.cs
- HasKey(Id), ValueGeneratedOnAdd, unique index on PlaceId
- Required PlaceId(100), Name(200), CityId(50)
- OwnsOne(location) columns: Location_Latitude, Location_Longitude
- OwnsMany(openingHours) table: PlaceOpeningHours, cascade delete
- Default values for TypicalDurationMinutes, IsIndoor, IsFamilyFriendly

### T7: Update PlannerDbContext
- **File**: SmartTripPlanner.Infrastructure/PlannerDbContext.cs
- Add: public DbSet<Place> Places { get; set; }

### T8: Create PlaceRepository + tests
- **Test class**: tests/SmartTripPlanner.Tests/SmartTripPlanner.Infrastructure/Repositories/PlaceRepositoryTests.cs
  - SearchAsync_WithMatchingQuery_ReturnsResults
  - SearchAsync_WithNonMatchingQuery_ReturnsEmpty
  - SearchAsync_FiltersByCityId
  - SearchAsync_RespectsMaxResults
  - GetByPlaceIdAsync_WithExistingId_ReturnsPlace
  - GetByPlaceIdAsync_WithNonExistingId_ReturnsNull
  - SavePlace_PreservesAllProperties
- **Implementation**: SmartTripPlanner.Infrastructure/Repositories/PlaceRepository.cs
  - Constructor injects PlannerDbContext
  - SearchAsync: context.Places.Include(p => p.OpeningHours).Where(...).Take(...).ToListAsync()
  - GetByPlaceIdAsync: context.Places.Include(p => p.OpeningHours).FirstOrDefaultAsync(...)

### T9: Register IPlaceRepository in DI
- **File**: SmartTripPlanner.Infrastructure/InfrastructureServiceRegistration.cs
- Add: services.AddScoped<IPlaceRepository, PlaceRepository>();

### T10: Run all tests and verify
- dotnet test tests/SmartTripPlanner.Tests/SmartTripPlanner.Tests.csproj
- All tests pass (domain + existing + infrastructure)

## Review Notes
- T1 is prerequisite for T8
- T8 needs T6 + T7 to compile
- Use work-unit commits (each task = one commit or logical batch)
