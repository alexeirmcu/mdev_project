# Design: Normalize Place Attributes

## Technical Approach

Promote `PlaceAttribute` from `ValueObject` (owned via `OwnsMany`) to a standalone `Entity` with identity-based equality. Introduce a many-to-many join table `PlacePlaceAttributes` so multiple places share the same attribute row. The `UpsertRangeAsync` repository method resolves attributes by find-or-create against normalized `(Provider, Key, Value)`. All existing migrations are deleted and regenerated as a single `InitialCreate`.

## Architecture Decisions

| Decision | Choice | Alternates | Rationale |
|----------|--------|------------|-----------|
| PlaceAttribute identity | `Entity` with `long Id` | Keep as `ValueObject` | Shared rows require identity; eliminates duplication |
| Relationship type | Explicit many-to-many via join entity `PlacePlaceAttributes` | EF Core implicit skip navigation | Explicit join gives FK indexes and query control |
| Uniqueness | Case-insensitive unique index on `(Provider, Key, Value)` | Case-sensitive / unique constraint only | Prevents "museum" vs "Museum" duplicates; user decision |
| Orphan handling | Keep orphans (no cascade delete) | CASCADE DELETE | User decision: orphans stay as catalog |
| Immutability | Private setters, no mutation methods | Mutable entity | PlaceAttribute values never change once created |
| API model | `PlaceAttributeModel(Key, Value)` only | Expose Provider | User decision: API consumers only need Key+Value |
| Migration strategy | Delete all, single `InitialCreate` | Incremental migration | User decision — project early-stage, no production data |

## Data Flow

```
UpsertRangeAsync(places)
    │
    ├─ For each place's attributes:
    │   1. Normalize (trim + lower-invariant) Provider, Key, Value
    │   2. Query PlaceAttributes WHERE normalized tuple matches
    │   3. Existing → attach to place via join row
    │   4. New → create PlaceAttribute + join row
    │
    ▼
Place ──< PlacePlaceAttributes >── PlaceAttribute
  │                (Join)                │
  │                                      ├ Id (PK)
  │                                      ├ Provider (CI)
  │                                      ├ Key (CI)
  │                                      └ Value (CI)
  │                                      UNIQUE(Provider, Key, Value) — CI
  │
  └─ PlacePlaceAttributes
        ├ PlaceId (FK → Places.Id)
        └ PlaceAttributeId (FK → PlaceAttributes.Id)
        PK(PlaceId, PlaceAttributeId)
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `Domain/AggregatesModel/PlaceAttribute.cs` | Modify | Change base from `ValueObject` to `Entity`; remove `GetEqualityComponents`; add `long Id` via base |
| `Domain/AggregatesModel/Place.cs` | Modify | `List<PlaceAttribute>` stays; `UpdateFromExternalProvider` adapts to find-or-create pattern |
| `Domain/ApiModels/PlaceAttributeModel.cs` | Modify | Reduce to `record PlaceAttributeModel(string Key, string Value)` |
| `Infrastructure/Configurations/PlaceConfiguration.cs` | Modify | Replace `OwnsMany` with `HasMany(p => p.Attributes).WithMany()` using join entity |
| `Infrastructure/Configurations/PlaceAttributeConfiguration.cs` | Create | New configuration: table `PlaceAttributes`, CI unique index, max lengths |
| `Infrastructure/Configurations/PlacePlaceAttributeConfiguration.cs` | Create | New configuration: join table, composite PK, FK indexes |
| `Infrastructure/PlannerDbContext.cs` | Modify | Add `DbSet<PlaceAttribute>` |
| `Infrastructure/Repositories/PlaceRepository.cs` | Modify | `UpsertRangeAsync` find-or-create logic; `GetDistinctInterestsByCityIdAsync` via join |
| `API/Configurations/AutoMapperProfile.cs` | Modify | Update `PlaceAttribute → PlaceAttributeModel` mapping (ignore Provider) |
| `tests/.../PlaceAttributeTests.cs` | Modify | Update equality tests for identity-based equality (Entity) |
| `tests/.../PlaceRepositoryTests.cs` | Modify | Add tests for find-or-create, deduplication, case-insensitive matching |
| `Infrastructure/Migrations/*` | Delete | All existing migrations removed |
| `Infrastructure/Migrations/InitialCreate.cs` | Create | Single migration from clean slate |

## Interfaces / Contracts

```csharp
// PlaceAttribute — promoted to Entity
public class PlaceAttribute : Entity  // inherits long Id
{
    public string Provider { get; }
    public string Key { get; }
    public string Value { get; }

    public PlaceAttribute(string provider, string key, string value) { /* validation unchanged */ }
    private PlaceAttribute() { } // EF
}

// PlaceAttributeModel — trimmed
public record PlaceAttributeModel(string Key, string Value);

// Join entity (infrastructure-only, no domain class needed — EF Core convention)
// Table: PlacePlaceAttributes(PlaceId, PlaceAttributeId)
```

## Testing Strategy

| Layer | What | Approach |
|-------|------|----------|
| Unit | `PlaceAttribute` construction, validation, Entity equality | MSTest — constructor tests stay; equality tests switch to identity-based |
| Unit | `PlaceAttributeModel` mapping | AutoMapper test: Provider is ignored, Key+Value mapped |
| Integration | `UpsertRangeAsync` deduplication | In-memory or LocalDB: insert same attribute twice → one row in `PlaceAttributes`, two join rows |
| Integration | Case-insensitive uniqueness | Insert ("foursquare","category","museum") then ("Foursquare","Category","Museum") → constraint violation or find-or-create resolves |
| Integration | `GetDistinctInterestsByCityIdAsync` via join | Verify query returns distinct Values across linked attributes |
| Migration | `InitialCreate` applies cleanly | `dotnet ef database update` on fresh DB |

## Migration / Rollout Plan

1. Delete all files under `Infrastructure/Migrations/`
2. Remove `Infrastructure/Migrations/PlannerDbContextModelSnapshot.cs`
3. Run `dotnet ef migrations add InitialCreate` against updated model
4. Verify generated migration creates: `Places`, `PlaceAttributes`, `PlacePlaceAttributes` tables with correct FKs and CI unique index
5. Apply to dev database: `dotnet ef database update`
6. Rollback: revert feature branch — old schema is preserved in git history

## Open Questions

- [ ] None blocking — all decisions captured in proposal