# Design: Refactor DayPlan Database Schema

## Technical Approach

Pure persistence refactor: replace 3 `OwnsOne` BlockTimeline properties with a `HasMany` collection, consolidate 3 activity tables into one, and squash 23 migration files into a single `InitialCreate`. DayPlan table drops from ~50 to ~8 columns. No domain behavior changes — all handlers/services that already use `GetBlock(BlockType)` are unaffected; the few that still access `.Morning`/`.Afternoon`/`.Evening` directly get migrated.

## Architecture Decisions

### Decision: DayPlan domain model — collection backing field

**Choice**: `IReadOnlyList<BlockTimeline> Blocks` backed by `List<BlockTimeline> _blocks` field, with constructor that accepts 3 `BlockTimeline` instances.
**Alternatives considered**: Keep 3 properties + add collection; use factory static method.
**Rationale**: The 3-property model is what needs replacing. Constructor validation ensures exactly 3 blocks (Morning, Afternoon, Evening). `GetBlock()` goes public and indexes into `_blocks` by ordinal. `AddActivity`/`ForceAddActivity`/`RemoveActivity` already delegate to `GetBlock()` — body unchanged.

### Decision: BlockTimeline as independent table (not OwnsOne)

**Choice**: `HasMany(d => d.Blocks).WithOne().HasForeignKey("DayPlanId")` with unique index on `(DayPlanId, BlockType)`.
**Alternatives considered**: Keep `OwnsOne` with separate columns; use JSON column.
**Rationale**: `OwnsOne` causes the ~50-column DayPlan bloat. A separate table with FK is the standard relational approach for a 1:N that is exactly 3 per parent. Unique index enforces domain invariant (one block of each type per day) at DB level.

### Decision: Single Activities table with FK to BlockTimeline

**Choice**: `OwnsMany(bt => bt.Activities, a => { a.ToTable("Activities"); ... })` with FK `BlockTimelineId`.
**Alternatives considered**: Keep 3 separate tables; use TPH inheritance.
**Rationale**: 3 identical tables are schema duplication. A single table with FK + BlockType reachable through BlockTimeline is clean. `OwnsMany` keeps ActivityNode as owned entity (value-object-like) within the aggregate. `TransitDetails` and `PlaceLocation` stay as `OwnsOne` inline columns.

## Data Flow

```
Trip ──OwnsMany──> DayPlan ──HasMany──> BlockTimeline ──OwnsMany──> ActivityNode
  │                    │                     │ OwnsOne                   │ OwnsOne
  │                    │                     ├─ TransitFromHotel        ├─ TransitToNext
  │                    │                     ├─ TransitToHotel          └─ PlaceLocation
  │                    │                     └─ InterBlockTransit
  │                    ├── DayIndex
  │                    ├── Date
  │                    └── WeatherSummary
  ├── (Trip columns unchanged)
  └── OwnsMany: OriginalMustSees
```

Tables after refactor:
```
DayPlans:           Id, TripId, DayIndex, Date, WeatherSummary, StartTime, IsStale, WeatherLastUpdatedAt (8 cols)
BlockTimelines:     Id, DayPlanId, BlockType, TransitFromHotel_*, TransitToHotel_*, InterBlockTransit_* (~15 cols)
Activities:         Id, BlockTimelineId, SequenceOrder, PlaceId, Name, IsCompleted, DurationMinutes, IsIndoor,
                    TransitToNext_*, Location_Lat, Location_Lng, OvertimeAlert, Priority (~17 cols)
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `Domain/AggregatesModel/DayPlan.cs` | Modify | Replace 3 props → `IReadOnlyList<BlockTimeline> Blocks`, make `GetBlock()` public, add constructor |
| `Domain/AggregatesModel/Trip.cs` | Modify | `GenerateDaysFrom()` — change object init to use new DayPlan constructor |
| `Infrastructure/Configurations/TripConfiguration.cs` | Modify | Rewrite DayPlan + BlockTimeline + Activities mapping (lines 79-147) |
| `Infrastructure/Repositories/TripRepository.cs` | Modify | Add `.ThenInclude(d => d.Blocks).ThenInclude(b => b.Activities)` |
| `ApplicationServices/Handlers/ToggleActivityCompletionHandler.cs` | Modify | Replace `d.Morning`/`d.Afternoon`/`d.Evening` → `Blocks` + `GetBlock()` |
| `ApplicationServices/Handlers/GenerateTripItineraryHandler.cs` | Modify | Replace `new[] { d.Morning, d.Afternoon, d.Evening }` → `d.Blocks` |
| `Domain/Services/ItineraryGeneratorHelpers.cs` | Modify | `GetTotalFreeSlots` — use `dayPlan.GetBlock(BlockType.X)` |
| `API/Configurations/AutoMapperProfile.cs` | Modify | `DayPlan→DayPlanResponse`: `src.Blocks` directly. `Trip→TripSummaryResponse`: use `d.Blocks` |
| `Infrastructure/Migrations/*.cs` (23 files) | Delete | Remove all before creating InitialCreate |
| `Infrastructure/Migrations/InitialCreate.cs` | Create | Fresh migration reflecting final schema |
| `tests/.../DayPlanTests.cs` | Modify | Use new DayPlan constructor |
| `tests/.../TimelineSchedulerTests.cs` | Modify | Use `GetBlock()` or constructor for block assignment |
| `tests/.../TripTests.cs` | Modify | Assert via `GetBlock()` |
| `tests/.../UnpinnedMustSeePlacerTests.cs` | Modify | Replace direct `.Morning` → `GetBlock()` or `Blocks` |
| `tests/.../TransitEnricherTests.cs` | Modify | Replace direct `.Morning` → `GetBlock()` |
| `tests/.../TripsControllerTests.cs` | Review | Already uses `Blocks` property — verify after mapping change |

## Interfaces / Contracts

```csharp
// DayPlan.cs - new shape
public class DayPlan : Entity
{
    private readonly List<BlockTimeline> _blocks = new();
    
    public int DayIndex { get; init; }
    public DateOnly Date { get; init; }
    public WeatherCondition WeatherSummary { get; private set; }
    public IReadOnlyList<BlockTimeline> Blocks => _blocks.AsReadOnly();
    public TimeOnly StartTime { get; private set; } = new(9, 0);
    public bool IsStale { get; private set; }
    public DateTimeOffset? WeatherLastUpdatedAt { get; private set; }

    internal DayPlan() { } // EF Core

    public DayPlan(int dayIndex, DateOnly date,
        BlockTimeline morning, BlockTimeline afternoon, BlockTimeline evening)
    {
        DayIndex = dayIndex;
        Date = date;
        _blocks = new List<BlockTimeline> { morning, afternoon, evening };
    }

    public BlockTimeline GetBlock(BlockType blockType) => Blocks[(int)blockType];
    
    // AddActivity, ForceAddActivity, RemoveActivity remain unchanged —
    // they already call GetBlock(blockType).Method(...)
}
```

```csharp
// TripConfiguration.cs - new mapping
builder.OwnsMany(t => t.Days, day =>
{
    day.WithOwner().HasForeignKey("TripId");
    day.Property<long>("Id");
    day.HasKey("Id");
    day.Property(d => d.IsStale).HasColumnName("IsStale").HasDefaultValue(false);
    day.Property(d => d.WeatherLastUpdatedAt).HasColumnName("WeatherLastUpdatedAt");

    day.HasMany(d => d.Blocks).WithOne().HasForeignKey("DayPlanId");
});

// BlockTimeline configuration (separate from DayPlan)
// Applied via assembly scan or explicit IEntityTypeConfiguration<BlockTimeline>
builder.Entity<BlockTimeline>(bt =>
{
    bt.HasKey(b => b.Id);
    bt.Property(b => b.Id).ValueGeneratedOnAdd();

    bt.Property(b => b.BlockType).HasConversion<string>().IsRequired();
    bt.HasIndex("DayPlanId", "BlockType").IsUnique();

    bt.OwnsOne(b => b.TransitFromHotel);
    bt.OwnsOne(b => b.TransitToHotel);
    bt.OwnsOne(b => b.InterBlockTransit);

    bt.OwnsMany(b => b.Activities, a =>
    {
        a.ToTable("Activities");
        a.WithOwner().HasForeignKey("BlockTimelineId");
        a.Property<long>("Id");
        a.HasKey("Id");
        a.Property(ac => ac.OvertimeAlert).HasColumnName("OvertimeAlert").HasDefaultValue(false);
        a.OwnsOne(ac => ac.TransitToNext);
        a.OwnsOne(ac => ac.Location, loc =>
        {
            loc.Property(l => l.Latitude).HasColumnName("Latitude");
            loc.Property(l => l.Longitude).HasColumnName("Longitude");
        });
    });
});
```

```csharp
// TripRepository.cs - new include chain
return await _dbContext.Trips
    .Include(t => t.City)
    .Include(t => t.Days)
    .ThenInclude(d => d.Blocks)
    .ThenInclude(b => b.Activities)
    .FirstOrDefaultAsync(t => t.TripId == tripId, ct);
```

## Handler Migration Examples

| Pattern | Before | After |
|---------|--------|-------|
| `FindActivityAcrossBlocks` | `day.Morning.Activities.FirstOrDefault(...)` | `day.Blocks.SelectMany(b => b.Activities).FirstOrDefault(...)` |
| `completedCount` | `new[] { d.Morning.Activities, d.Afternoon.Activities, d.Evening.Activities }` | `d.Blocks.SelectMany(b => b.Activities)` |
| `enrichment check` | `new[] { d.Morning, d.Afternoon, d.Evening }` | `d.Blocks` |
| `GetTotalFreeSlots` | `MaxVisitsM - dayPlan.Morning.Activities.Count + ...` | `dayPlan.GetBlock(BlockType.Morning)` per block |

## Testing Strategy

| Layer | What | Approach |
|-------|------|----------|
| Unit | DayPlan construction, `GetBlock()`, `Blocks` enumeration | Direct assert |
| Unit | All existing handler/service behavior | No new tests — existing 172+ must pass unchanged |
| Persistence | BlockTimelines + Activities table mapping | Integration test with in-memory or test DB |
| Migration | Squash produces correct schema | `dotnet ef migrations list`, verify SQL |

## Migration / Rollout

1. Delete all 23 migration files
2. `dotnet ef migrations add InitialCreate`
3. Verify generated SQL matches target schema (~8 cols DayPlan, ~15 cols BlockTimelines, ~17 cols Activities)
4. Remove `20260619063606_InitialCreate` and `20260626083127_AddPlaceAttributeProviderId` — verify the number of generated migration files (should be 3: InitialCreate.cs, InitialCreate.Designer.cs, PlannerDbContextModelSnapshot.cs)
5. Full `dotnet test` — all must pass

> **Rollback**: `git checkout -- SmartTripPlanner.Infrastructure/Migrations/` + revert each production file.

## Resolved Questions

- [x] `DayPlan` constructor: **YES** — must validate that each BlockTimeline's BlockType matches the expected ordinal (Morning=0, Afternoon=1, Evening=2). Throw if mismatch.
