# Design: Trip Generation Flow 0 — Trip Aggregate Root & Must-Sees

## Technical Approach

Implement the foundational Trip creation flow using the existing Clean Architecture layers (Domain → ApplicationServices → API → Infrastructure). The central change is refactoring `Trip` from a naive entity with `SelectedPlaces` (ICollection<Place>) to a proper DDD Aggregate Root with Value Objects (`MustSee`, `Travelers`, `TripPreferences`), a state machine (`TripStatus`), and a public-facing `TripCode`. The flow follows the established MediatR CQRS + FluentValidation pipeline pattern (mirroring `SearchPlacesHandler`/`SearchPlacesRequestValidator`).

## Architecture Decisions

### Decision: Trip Identity — Guid TripId alongside long Id

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Make `Entity<TId>` generic | Clean but requires refactoring City, Place, etc. | Rejected — too broad for MVP |
| Trip inherits Entity (long Id), add `Guid TripId` as public alternate key | Dual-key, adds `TripId` property; EF maps `Id` as PK, `TripId` with unique index | **Chosen** — minimal disruption to existing aggregates |
| Replace long Id with Guid Id on Trip | Breaks EF convention, needs config override | Rejected — complicates relationship mapping with City/Place (long FKs) |

**Rationale**: The spec requires `TripId : Guid (PK)` as the public identifier. Keeping `long Id` from `Entity` as the EF internal PK avoids touching the `Entity` base class and all other aggregates. The API exposes only `TripId` (Guid); `Id` (long) is never exposed.

### Decision: MustSee as Value Object, not Entity

| Option | Tradeoff | Decision |
|--------|----------|----------|
| MustSee as Entity with own Id | Allows independent lifecycle; adds complexity | Rejected |
| MustSee as Value Object (Owned type) | Stored inline with Trip; no independent identity; EF `OwnsMany` | **Chosen** — MustSees have no lifecycle outside Trip |

**Rationale**: MustSees are intrinsically part of the Trip aggregate. They are created and deleted with the Trip. EF Core `OwnsMany` maps them to a separate table with FK to Trip but no independent PK lifecycle.

### Decision: Validation Strategy — Two Layers

| Layer | Responsibility | Mechanism |
|-------|----------------|-----------|
| API/Input | Shape validation (required fields, format, ranges) | FluentValidation (`GenerateTripValidator`) via `ValidationBehavior` pipeline |
| Domain/Application | Business rules (city exists, PlaceIds exist, PinnedDay range, duplicate MustSees) | Handler throws `DomainException` subtypes (`CityNotFoundException`, `BusinessRuleException`) |

**Rationale**: Existing pattern already splits validation this way (see `SearchPlacesRequestValidator` checking city-code validity async). Input validation catches early; domain validation enforces invariants that need repositories.

### Decision: BusinessRuleException New Domain Exception

The spec mentions 422 responses for business rule violations. Current codebase has `SmartTripDomainException` but no `BusinessRuleException`. Add a `BusinessRuleException : SmartTripDomainException` that the `ExceptionHandlingMiddleware` catches and maps to 422 (same as other `DomainException` types). No middleware changes needed — `ExceptionHandlingMiddleware.GetStatusCode` already maps `DomainException → 422`.

### Decision: Refactor Trip.CityId from string to long

Current `Trip.CityId` is `string`. The spec requires `long CityId` (FK → City.Id). This is a breaking change but Trip has no existing production data (MVP), so the migration is straightforward.

### Decision: TripCode Generation — Application Service

Spec provides `TripCodeGenerator` as a static class using `ITripRepository.ExistsByTripCodeAsync()`. This creates a dependency on infrastructure inside domain. **Resolution**: Implement as an Application service injectable via DI, receiving `ITripRepository`. The handler calls it after city validation.

## Data Flow

```
Client ──POST /api/trips──> TripsController
                                    │
                                    ▼
                        GenerateTrip (MediatR Command)
                                    │
                                    ▼
                    ┌────────────────────────────────┐
                    │  ValidationBehavior<TRequest>   │
                    │  → GenerateTripValidator        │
                    │  (FluentValidation: shape/rules)│
                    └────────────────────────────────┘
                                    │
                                    ▼
                    ┌────────────────────────────────┐
                    │  GenerateTripHandler             │
                    │  1. Resolve CityCode → CityId   │
                    │     (ICityRepository)           │
                    │  2. Validate PlaceIds exist     │
                    │     (IPlaceRepository)           │
                    │  3. Validate PinnedDay/Block     │
                    │  4. Generate TripCode            │
                    │  5. Materialize Trip aggregate   │
                    │  6. Persist via ITripRepository  │
                    │  7. Map to TripPlanResponse      │
                    └────────────────────────────────┘
                                    │
                                    ▼
                        201 Created + Location header
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `Domain/AggregatesModel/Trip.cs` | **Modify** | Replace `SelectedPlaces` with `OriginalMustSees`, add `TripCode`, `Travelers`, `TripPreferences`, `TripStatus`, `TripId` (Guid), `AddMustSee`, `RemoveMustSee`, `GenerateDays`, `UpdateStatus`. Change `CityId` from string to long. |
| `Domain/AggregatesModel/MustSee.cs` | **Create** | Value Object: PlaceId (long), Priority, PinnedDayIndex, PinnedBlock |
| `Domain/AggregatesModel/Travelers.cs` | **Create** | Value Object: Adults, Children, Infants, Total with validation |
| `Domain/AggregatesModel/TripPreferences.cs` | **Create** | Value Object: CarAvailable, MaxWalkingMinutes, WeatherAwareEnabled |
| `Domain/Enums/TripStatus.cs` | **Create** | Enum: CREATED, GENERATED, COMPLETED |
| `Domain/Exceptions/BusinessRuleException.cs` | **Create** | Inherits SmartTripDomainException; wraps business rule violations |
| `Domain/Repository/ITripRepository.cs` | **Modify** | Add GetByIdAsync(Guid, ct), GetByTripCodeAsync, ExistsByTripCodeAsync; change ListAsync CityId param to long? |
| `Domain/Repository/ICityRepository.cs` | **Modify** | Add CancellationToken to GetByCodeAsync |
| `Domain/Repository/IPlaceRepository.cs` | **Modify** | Add GetManyByIdsAsync(IEnumerable<long>, CancellationToken) |
| `Domain/ApiModels/TripGenerationRequest.cs` | **Modify** | CityId → CityCode; add Travelers, Preferences, MustSees uses long PlaceId |
| `Domain/ApiModels/MustSeeInput.cs` | **Modify** | PlaceId from string → long |
| `Domain/ApiModels/TripPlanResponse.cs` | **Modify** | Expand with TripId, TripCode, CityId, CityCode, CityName, BaseHotel, Travelers, Preferences, MustSees, Status, DefaultStartHour |
| `Domain/ApiModels/TravelersInput.cs` | **Create** | Input DTO |
| `Domain/ApiModels/TripPreferencesInput.cs` | **Create** | Input DTO |
| `Domain/ApiModels/MustSeeResponse.cs` | **Create** | Response DTO |
| `Domain/ApiModels/TripUpdateRequest.cs` | **Create** | PATCH request DTO with nullable fields |
| `ApplicationServices/Commands/GenerateTrip.cs` | **Create** | MediatR IRequest record |
| `ApplicationServices/Handlers/GenerateTripHandler.cs` | **Create** | IRequestHandler: validates city, placeIds, materializes Trip, persists |
| `ApplicationServices/Validators/GenerateTripValidator.cs` | **Create** | FluentValidation rules |
| `ApplicationServices/Commands/UpdateTrip.cs` | **Create** | MediatR IRequest record |
| `ApplicationServices/Handlers/UpdateTripHandler.cs` | **Create** | PATCH handler — enforces status-based edit rules |
| `ApplicationServices/Validators/UpdateTripValidator.cs` | **Create** | FluentValidation rules for PATCH |
| `API/Controllers/TripsController.cs` | **Create** | POST /api/trips and PATCH /api/trips/{tripId} |
| `Infrastructure/Configurations/TripConfiguration.cs` | **Modify** | Remove SelectedPlaces; add OwnsMany(OriginalMustSees), OwnsOne(Travelers), OwnsOne(Preferences), TripCode unique index, TripId unique index |
| `Infrastructure/Repositories/TripRepository.cs` | **Create** | Implement ITripRepository |
| `API/Configurations/AutoMapperProfile.cs` | **Modify** | Add Trip/TripPlanResponse, MustSee/MustSeeResponse, Location mappings |
| `Infrastructure/InfrastructureServiceRegistration.cs` | **Modify** | Register ITripRepository → TripRepository |
| `tests/.../TripTests.cs` | **Modify** | Replace SelectedPlaces tests with MustSee tests |
| `tests/.../GenerateTripHandlerTests.cs` | **Create** | Handler unit tests |
| `tests/.../GenerateTripValidatorTests.cs` | **Create** | Validator unit tests |

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit — Domain | Trip aggregate: AddMustSee duplicate detection, RemoveMustSee, GenerateDays guard, Status transitions | MSTest, direct invocation |
| Unit — Domain | MustSee value object equality | MSTest, equality checks |
| Unit — Domain | Travelers validation (Adults ≥ 1, Total ≤ 10) | MSTest, constructor guards |
| Unit — Handler | GenerateTripHandler: happy path, city not found, city not allowed, missing PlaceIds, PinnedDay range, max duration | MSTest + Moq (ICityRepository, IPlaceRepository, ITripRepository, IMapper) |
| Unit — Handler | UpdateTripHandler: edit fields, block changes when GENERATED, add/remove MustSees | MSTest + Moq |
| Unit — Validator | GenerateTripValidator: required fields, date ranges, MustSee duplicates | MSTest + FluentValidation TestHelper |

## Migration / Rollout

No production data migration required — this is MVP with no existing Trip data. The EF migration will:

1. Drop `SelectedPlaces` relationship and `TripPlaces` join table
2. Add `OriginalMustSees` owned collection table
3. Add `Travelers` owned columns
4. Add `Preferences` owned columns
5. Add `TripCode` column with unique index
6. Add `TripId` (Guid) column with unique index
7. Add `Status` column (default: CREATED)
8. Change `CityId` from string to long

## Open Questions

- [ ] Should `TripCodeGenerator` be a static utility or injectable service? Spec shows static with repository dependency — recommend injectable service for testability.
- [ ] PATCH MustSees removal when `status == GENERATED` requires checking if MustSee is in DayPlans. DayPlan tracking isn't implemented yet. Recommend deferring that constraint to Flow 2.