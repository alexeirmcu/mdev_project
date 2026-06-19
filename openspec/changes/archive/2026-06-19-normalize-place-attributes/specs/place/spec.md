# Delta for place

## MODIFIED Requirements

### Requirement: Place Entity

`Place` inherits from `Entity` (long auto-generated Id).

- `PlaceId` (string, required) with unique index for domain lookup.
- `Name` (string, required).
- `CityId` (string, required, e.g. "madrid-es").
- `TypicalDurationMinutes` (int) — default 60.
- `IsIndoor` (bool) — default false.
- `IsFamilyFriendly` (bool) — default true.
- `Location` — `PlaceLocation` ValueObject (OwnsOne).
- `OpeningHours` — `List<OpeningHoursWindow>` (OwnsMany, default empty).
- `Attributes` — `List<PlaceAttribute>` (HasMany-through-join-table, default empty). Relationship is many-to-many via explicit join entity `PlacePlaceAttributes`. `PlaceAttribute` is a shared entity, not owned.
- `AddAttribute(PlaceAttribute)` — method that appends to the `Attributes` collection (null check enforced).

(Previously: Attributes was `List<PlaceAttribute>` with OwnsMany; AddAttribute appended an owned value.)

#### Scenario: Add attribute to place — shared entity

- GIVEN a Place with no attributes and an existing PlaceAttribute entity
- WHEN `AddAttribute(existingAttribute)` is called
- THEN `Attributes.Count` is 1 and the attribute is linked (not cloned)

#### Scenario: Add null attribute throws

- GIVEN a Place with no attributes
- WHEN `AddAttribute(null)` is called
- THEN `SmartTripDomainException` is thrown

### Requirement: IPlaceRepository

```csharp
namespace SmartTripPlanner.Domain.Repository;

public interface IPlaceRepository : IRepository<Place>
{
    Task<List<Place>> SearchAsync(string query, string cityCode, int maxResults = 20);
    Task<Place?> GetByProviderReferenceIdAsync(string providerReferenceId);
    Task<IEnumerable<Place>> GetManyByIdsAsync(IEnumerable<long> placeIds, CancellationToken ct);
    Task<List<Place>> GetManyByCityIdAsync(long cityId, CancellationToken ct = default);
    Task<List<Place>> GetCandidatesByCityAndInterestsAsync(long cityId, IReadOnlyList<string> interests, CancellationToken ct = default);
    Task<List<string>> GetDistinctAttributeValuesByCityCodeAsync(string cityCode, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<Place> places);
    Task UpsertRangeAsync(IEnumerable<Place> places);
}
```

(Previously: identical interface — no signature changes, but behavioral contracts differ.)

#### Scenario: Interest-filtered candidate retrieval via join table

- GIVEN a city with places linked to shared PlaceAttribute entities via join table
- WHEN `GetCandidatesByCityAndInterestsAsync(cityId, ["museum", "food"])` is called
- THEN only places linked to at least one PlaceAttribute whose Value matches any interest are returned
- AND matching is inclusive (place matches if ANY linked attribute matches ANY interest)

#### Scenario: Distinct attribute values per city code via join table

- GIVEN a city with places linked to shared PlaceAttribute entities
- WHEN `GetDistinctAttributeValuesByCityCodeAsync("madrid-es")` is called
- THEN distinct PlaceAttribute.Value strings are returned (no duplicates)

### Requirement: PlaceRepository (Infrastructure)

EF Core implementation in `PlaceRepository.cs`.

- `PlaceConfiguration` in `PlaceConfiguration.cs` configures `HasMany(p => p.Attributes).WithMany()` using explicit join entity `PlacePlaceAttributes`.
- Case-insensitive unique index on `PlaceAttribute(Provider, Key, Value)`.
- **Attribute search**: `SearchAsync(query, cityCode, maxResults)` MUST match places where `Name.Contains(query)` OR any linked `Attribute.Value.Contains(query)` (case-insensitive) within the specified city. The query MUST include Attributes via EF Core `Include` through join table.
- **Interest-filtered query**: `GetCandidatesByCityAndInterestsAsync` MUST filter candidates in SQL through the join table linking Places to PlaceAttributes.
- **Distinct attribute values**: `GetDistinctAttributeValuesByCityCodeAsync` MUST query distinct PlaceAttribute.Value strings through the join table in SQL.
- **UpsertRangeAsync attribute resolution**: `UpsertRangeAsync` MUST resolve attributes using find-or-create: for each attribute on an incoming Place, check if a PlaceAttribute with matching (Provider, Key, Value) already exists (case-insensitive). If found, link the existing entity to the Place. If not found, create and persist a new PlaceAttribute entity, then link it.

(Previously: OwnsMany for Attributes; UpsertRangeAsync delegated attributes via UpdateFromExternalProvider; queries navigated directly through owned collection.)

#### Scenario: UpsertRangeAsync finds existing attribute and links

- GIVEN PlaceAttribute (Provider="foursquare", Key="category", Value="Museum") already exists in DB
- WHEN `UpsertRangeAsync` processes a new Place with the same attribute
- THEN the existing PlaceAttribute row is linked via join table (no duplicate row created)

#### Scenario: UpsertRangeAsync creates new attribute when not found

- GIVEN no PlaceAttribute with (Provider="foursquare", Key="category", Value="Aquarium") exists
- WHEN `UpsertRangeAsync` processes a Place with this attribute
- THEN a new PlaceAttribute row is created and linked to the Place via join table

#### Scenario: Search matches attribute value through join table

- GIVEN a Place with Name="Gran Palace" linked to PlaceAttribute(Value="Hotel") via join table in city "MAD"
- WHEN `SearchAsync("hotel", "MAD")` is called
- THEN the Place is returned

#### Scenario: Interest filtering via join table is performed in SQL

- GIVEN `GetCandidatesByCityAndInterestsAsync`
- WHEN the method is called with interests ["museum"]
- THEN the generated SQL joins Place→PlacePlaceAttributes→PlaceAttribute and filters on PlaceAttribute.Value
- AND no in-memory `.Where()` is applied after materialization

#### Scenario: Distinct attribute values via join table in SQL

- GIVEN `GetDistinctAttributeValuesByCityCodeAsync`
- WHEN called with cityCode "madrid-es"
- THEN the generated SQL joins City→Place→PlacePlaceAttributes→PlaceAttribute and applies SELECT DISTINCT
- AND no in-memory `.Distinct()` is applied after full table materialization

### Requirement: EF Core PlaceAttribute Configuration

`PlaceConfiguration` MUST configure the many-to-many relationship between `Place` and `PlaceAttribute` using an explicit join entity `PlacePlaceAttributes` with foreign keys `PlaceId` and `PlaceAttributeId`. `PlaceAttribute` MUST be configured as a separate entity with its own `DbSet<PlaceAttribute>` in `PlannerDbContext`. A case-insensitive unique index MUST be applied on `(Provider, Key, Value)`.

(Previously: OwnsMany configuration with separate table "PlaceAttributes" and foreign key "PlaceId"; composite index on (PlaceId, Value).)

#### Scenario: Attributes persisted through join table and loaded

- GIVEN a Place linked to shared PlaceAttribute entities via join table
- WHEN the Place is retrieved with Include on Attributes
- THEN all linked Attributes are loaded with correct values

### Requirement: PlaceModel Attributes

`PlaceModel` MUST include `IReadOnlyList<PlaceAttributeModel> Attributes`. `PlaceAttributeModel` is a record with `Key` and `Value` (string). `Provider` and `Id` MUST NOT be exposed in `PlaceAttributeModel`. `AutoMapperProfile` MUST map `PlaceAttribute` to `PlaceAttributeModel` projecting only `Key` and `Value`.

(Previously: PlaceAttributeModel was a record with Provider, Key, Value.)

#### Scenario: Attributes returned in API response without Provider or Id

- GIVEN a Place with a linked PlaceAttribute (Provider="foursquare", Key="category", Value="Hotel", Id=42)
- WHEN mapped to PlaceModel via AutoMapper
- THEN PlaceModel.Attributes contains a PlaceAttributeModel with Key="category", Value="Hotel"
- AND the PlaceAttributeModel does NOT contain Provider or Id fields

## ADDED Requirements

### Requirement: Delete and recreate migrations

All existing EF Core migration files MUST be deleted. A single `InitialCreate` migration MUST be generated that captures the full schema including the `PlacePlaceAttributes` join table, `PlaceAttribute` as an entity with `Id`, and the case-insensitive unique index on `(Provider, Key, Value)`. The `__EFMigrationsHistory` table MUST start fresh with no prior migration history.

#### Scenario: Fresh database applies InitialCreate

- GIVEN a blank database
- WHEN the InitialCreate migration is applied
- THEN all tables (Places, PlaceAttributes, PlacePlaceAttributes, Cities, etc.) are created with correct schema
- AND the migration history contains only the InitialCreate entry

#### Scenario: No legacy migrations remain

- GIVEN the source code after migration cleanup
- WHEN the Migrations folder is inspected
- THEN only the InitialCreate migration file exists (plus snapshot)