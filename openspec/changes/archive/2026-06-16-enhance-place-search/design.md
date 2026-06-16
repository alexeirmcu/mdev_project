# Design: Enhance Place Search

## Technical Approach

Add `PlaceAttribute` ValueObject following the existing `OpeningHoursWindow` pattern, persist it via EF Core `OwnsMany` in a separate `PlaceAttributes` table, extend `PlaceRepository.SearchAsync` to match attribute values alongside name, and map Foursquare categories/chains into attributes. This is purely additive — no existing contracts change.

## Architecture Decisions

### Decision: PlaceAttribute as ValueObject (not Entity)

| Option | Tradeoff | Decision |
|--------|----------|----------|
| ValueObject (no Id) | Follows OpeningHoursWindow pattern; equality by value is correct semantics | **Chosen** |
| Entity (with Id) | Would let PlaceAttribute exist independently; wrong semantics — attributes have no identity beyond their values | Rejected |

**Rationale**: `OpeningHoursWindow` is already a `ValueObject` persisted via `OwnsMany` with a shadow `Id` property in configuration. PlaceAttribute follows the same pattern. Equality is (Provider, Key, Value) — no independent identity needed.

### Decision: Attribute key naming convention

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Short keys: `"category"`, `"chain"` | Simple; provider already in separate field | **Chosen** |
| Namespaced keys: `"foursquare.category"` | Redundant since Provider field exists; adds coupling | Rejected |

**Rationale**: The `Provider` field disambiguates. `PlaceAttribute("foursquare", "category", "Hotel")` is clear without namespacing the key. Future providers use their own Provider value.

### Decision: Search implementation

| Option | Tradeoff | Decision |
|--------|----------|----------|
| `Any(a => a.Value.Contains(query))` | LINQ-idiomatic; EF Core translates to SQL with JOIN | **Chosen** |
| Computed `SearchText` column | Denormalized; faster reads but added write complexity & migration risk | Deferred |

**Rationale**: The `Any`/`Contains` approach generates correct SQL. For SQLite, `Contains` maps to `LIKE '%query%'` which is case-insensitive by default (SQLite `LIKE` is case-insensitive for ASCII). A composite index on `(PlaceId, Value)` mitigates the table scan. A denormalized `SearchText` column can be evaluated later if performance requires it.

### Decision: Chain attribute mapping

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Map chains as `PlaceAttribute("foursquare", "chain", name)` | Per proposal scope; enables "McDonald's" search | **Chosen** |
| Skip chains | Simpler but misses key spec scenario | Rejected |

**Rationale**: The spec explicitly includes a chain search scenario. Foursquare's Pro-tier `FoursquarePlace` already exposes `ChainLabel` — mapping it is trivial.

## Data Flow

```
FoursquarePlace.Categories ─→ MapToPlace ─→ Place.AddAttribute(...)
        .ChainLabel?  ─────────────────────→ Place.AddAttribute(...)

SearchPlacesHandler ─→ PlaceRepository.SearchAsync(query, cityCode)
                              │
                              ├── Include(p => p.Attributes)
                              ├── Include(p => p.City)
                              └── Where(Name.Contains(query) || Attributes.Any(a => a.Value.Contains(query)))

Place ─→ AutoMapper ─→ PlaceModel (with Attributes list)
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `Domain/AggregatesModel/PlaceAttribute.cs` | Create | ValueObject with Provider, Key, Value; validation; equality |
| `Domain/AggregatesModel/Place.cs` | Modify | Add `Attributes` collection and `AddAttribute` method |
| `Domain/ApiModels/PlaceAttributeModel.cs` | Create | Record with Provider, Key, Value |
| `Domain/ApiModels/PlaceModel.cs` | Modify | Add `IReadOnlyList<PlaceAttributeModel> Attributes` |
| `Infrastructure/Configurations/PlaceConfiguration.cs` | Modify | Add `OwnsMany(p => p.Attributes)` block with table, index |
| `Infrastructure/Repositories/PlaceRepository.cs` | Modify | Add `.Include(p => p.Attributes)`, extend `.Where()` |
| `Infrastructure/ExternalServices/Foursquare/FoursquarePlaceService.cs` | Modify | Map categories and chains to attributes |
| `API/Configurations/AutoMapperProfile.cs` | Modify | Add `PlaceAttribute → PlaceAttributeModel` mapping |
| `tests/.../Domain/PlaceAttributeTests.cs` | Create | Construction, equality, validation tests |
| `tests/.../Domain/PlaceTests.cs` | Modify | Add `AddAttribute` and initial-empty tests |
| `tests/.../Infrastructure/PlaceRepositoryTests.cs` | Modify | Add attribute search scenarios |
| `tests/.../Mapping/PlaceMappingProfileTests.cs` | Modify | Add attribute mapping assertion |
| `tests/.../Helpers/PlaceFixture.cs` | Modify | Add attributes to `CreatePopulatedPlace` |
| `tests/.../ExternalServices/Foursquare/FoursquarePlaceServiceTests.cs` | Modify | Verify attributes in MapToPlace |

## Interfaces / Contracts

```csharp
// Domain/AggregatesModel/PlaceAttribute.cs
public class PlaceAttribute : ValueObject
{
    public string Provider { get; }
    public string Key { get; }
    public string Value { get; }

    private PlaceAttribute() { }  // EF
    public PlaceAttribute(string provider, string key, string value) { /* validate */ }

    protected override IEnumerable<object> GetEqualityComponents()
        => new object[] { Provider, Key, Value };
}

// Place.cs additions
public List<PlaceAttribute> Attributes { get; private set; } = new();
public void AddAttribute(PlaceAttribute attribute) { Attributes.Add(attribute ?? throw ...); }

// PlaceModel.cs — update record
public record PlaceModel(
    string ProviderReferenceId, string Name, long CityId,
    PlaceLocationModel Location, int TypicalDurationMinutes,
    bool IsIndoor, bool IsFamilyFriendly,
    IReadOnlyList<OpeningHoursWindowModel> OpeningHours,
    IReadOnlyList<PlaceAttributeModel> Attributes);

public record PlaceAttributeModel(string Provider, string Key, string Value);
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | PlaceAttribute construction, equality, null/empty validation | MSTest, direct instantiation |
| Unit | Place.AddAttribute, initial empty collection | MSTest on Place entity |
| Unit | FoursquarePlaceService.MapToPlace populates attributes | Mock IFoursquareApiClient |
| Integration | PlaceRepository attribute search (match by category value) | InMemoryDatabase |
| Integration | PlaceRepository name search still works (regression) | InMemoryDatabase, existing tests |
| Integration | Attribute persistence round-trip via EF Core | InMemoryDatabase |

## Migration / Rollout

Additive EF Core migration creates `PlaceAttributes` table. Existing `Place` rows have empty collections — no data migration needed. Rollback: drop table, remove `Attributes` property, revert `SearchAsync`.

## Open Questions

- [ ] Should chains be mapped only when `FoursquarePlace.ChainLabel` is non-empty? (Yes — skip null/empty chains)
- [ ] Does `FoursquarePlace` already expose a `ChainLabel` or similar field? (Needs verification during implementation)