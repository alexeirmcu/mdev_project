# Proposal: Normalize Place Attributes

## Intent
Eliminate duplicate `PlaceAttribute` rows by promoting the ValueObject to a shared `Entity` with a many-to-many relationship, so identical attributes ("Museum") are stored once and linked.

## Scope

### In Scope
- Promote `PlaceAttribute` to `Entity` with `Id`
- Many-to-many EF Core mapping via join table `PlacePlaceAttributes`
- Case-insensitive unique index on `(Provider, Key, Value)`
- `PlaceRepository.UpsertRangeAsync` find-or-create attribute resolution
- Reduce `PlaceAttributeModel` to `(Key, Value)` in API
- Delete and recreate migrations as single `InitialCreate`
- Update affected tests

### Out of Scope
- Orphan cleanup (orphaned definitions kept as catalog)
- Foursquare mapping logic changes
- UI/client changes beyond API model

## Capabilities

### New Capabilities
- None

### Modified Capabilities
- `place-attributes`: Persistence changes from owned ValueObject to shared Entity; equality now identity-based; immutability preserved
- `place`: `Attributes` relationship changes from `OwnsMany` to `HasMany...WithMany`; `PlaceConfiguration` and repository queries updated
- `trip-interests`: `GetCandidatesByCityAndInterestsAsync` filters via join table
- `city-interests-endpoint`: `GetDistinctAttributeValuesByCityCodeAsync` queries distinct values through join table

## Approach
- Add `long Id` to `PlaceAttribute`, remove `ValueObject` base
- Configure `HasMany(p => p.Attributes).WithMany()` using explicit join entity `PlacePlaceAttributes`
- Apply case-insensitive unique index on `(Provider, Key, Value)`
- In `UpsertRangeAsync`, resolve existing attributes by normalized `(Provider, Key, Value)` before attaching to places
- Keep `PlaceAttribute` immutable (no setters on Provider/Key/Value)
- Trim `PlaceAttributeModel` to expose only `Key` and `Value`
- Drop all migrations and generate a single fresh `InitialCreate`

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Domain/PlaceAggregate/PlaceAttribute.cs` | Modified | Becomes `Entity` with `Id` |
| `Infrastructure/Configurations/PlaceConfiguration.cs` | Modified | `HasMany...WithMany` + join table |
| `Infrastructure/Repositories/PlaceRepository.cs` | Modified | Attribute resolution in `UpsertRangeAsync` |
| `API/Models/PlaceAttributeModel.cs` | Modified | Exposes only `Key`, `Value` |
| `tests/` | Modified | `PlaceAttributeTests`, `PlaceRepositoryTests` adjusted |
| `Migrations/` | Removed/Added | All deleted; single `InitialCreate` added |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Data loss during migration | Low | Recreate from scratch with known seed data; validate row counts |
| Case-sensitivity edge cases | Med | Unique index uses CI collation; add integration test |
| Query performance with join table | Low | Index on join table FKs; monitor query plans |
| Test breakage | High | Update unit tests for entity equality/tracking before merge |

## Rollback Plan
Revert the feature branch. Since existing migrations are deleted, rollback means keeping the pre-change database schema and re-applying the old migration history.

## Dependencies
None

## Success Criteria
- [ ] `PlaceAttribute` stored uniquely; no duplicate `(Provider, Key, Value)` rows
- [ ] API returns only `Key` and `Value` for attributes
- [ ] Search, interest filtering, and distinct-value queries return correct results
- [ ] All existing and new tests pass
- [ ] Single `InitialCreate` migration applies cleanly
