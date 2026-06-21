# Archive Report: return-to-hotel-strategy

**Archived:** 2026-06-21
**Project:** mdev_project (.NET 8, Clean Architecture, EF Core PostgreSQL/InMemory)
**Persistence mode:** openspec
**Archiver:** sdd-archive

---

## What Was Implemented

Added a configurable `ReturnToHotelStrategy` to `TripPreferences` with three options:
- **Always** (default) — existing behavior: return to hotel between each activity block. No behavioral change for existing trips.
- **Never** — skip hotel return between adjacent blocks that both have activities; compute a direct `InterBlockTransit` leg instead. Evening block always returns to hotel regardless.
- **ProximityBased** — compute both the direct route and the via-hotel route for each block boundary; pick the shorter option. Tie-breaker favors hotel (≤ stays with hotel).

### Key Components Changed

| Component | File | Change |
|---|---|---|
| Enum | `SmartTripPlanner.Domain/Enums/ReturnToHotelStrategy.cs` | New enum with `Always`, `Never`, `ProximityBased` |
| Domain model | `SmartTripPlanner.Domain/AggregatesModel/TripPreferences.cs` | Added `ReturnToHotelStrategy` property, default `Always` |
| Input DTO | `SmartTripPlanner.Domain/ApiModels/TripPreferencesInput.cs` | Added `ReturnToHotelStrategy` field |
| Block model | `SmartTripPlanner.Domain/AggregatesModel/BlockTimeline.cs` | Added `InterBlockTransit` property (nullable `TransitDetails`) |
| Transit logic | `SmartTripPlanner.Domain/Services/TransitEnricher.cs` | Added `ApplyStrategyAsync` — implements Never and ProximityBased |
| Scheduler | `SmartTripPlanner.Domain/Services/TimelineScheduler.cs` | Block chaining via `InterBlockTransit` or reset |
| Response DTO | `SmartTripPlanner.Domain/ApiModels/TripPlanResponse.cs` | Added `BlockResponse.InterBlockTransit` |
| AutoMapper | `SmartTripPlanner.API/Configurations/AutoMapperProfile.cs` | Mapped `InterBlockTransit` |
| EF Core | `SmartTripPlanner.Infrastructure/Configurations/TripConfiguration.cs` | Enum→string conversion, `InterBlockTransit` owned entity |
| Migration | `SmartTripPlanner.Infrastructure/Migrations/20260621193200_AddReturnToHotelStrategy.cs` | Migration with symmetric up/down |

---

## Key Decisions

1. **Default is `Always`** — full backward compatibility. Existing trips serialize with `Always`, which means `ApplyStrategyAsync` is a no-op. No migration data fixup needed.
2. **Evening ALWAYS returns to hotel** — regardless of strategy. The terminal block must end at the hotel per domain rules.
3. **ProximityBased tie-breaker favors hotel** — strict `<` comparison means equal-distance boundaries stay with the hotel route. This is a deliberate conservative choice.
4. **Delta specs not authored** — this is noted in the verify report as a suggestion for future artifact discipline. The orchestrator supplied an audit checklist as a surrogate, all 11 requirements pass with runtime evidence.
5. **No design.md authored** — design coherence check was SKIPPED. Implementation followed the existing pattern established by prior transit work.

---

## Test Results

| Metric | Value |
|---|---|
| Total tests | 333 passed (0 failed, 0 skipped) |
| Pre-existing tests | 308 (all pass — no regressions) |
| New tests | 25 |
| Test categories | Enum tests (2), TransitEnricher (6), TimelineScheduler (5), BlockTimeline (3), TripPreferences (3), Integration (4) + 2 mapping tests |

### Test Breakdown

| Test Suite | Tests | Status |
|---|---|---|
| `ReturnToHotelStrategyTests` | 2 | ✅ |
| `TripPreferencesTests` | 3 | ✅ |
| `BlockTimelineTests` | 4 | ✅ |
| `TransitEnricherTests` | 6 | ✅ |
| `TimelineSchedulerTests` | 5 | ✅ |
| `HeuristicItineraryGeneratorTests` | 4 | ✅ |
| Pre-existing (mapping, config, others) | 308 | ✅ |

---

## Known Limitations / Deferred Work

| Issue | Priority | Details |
|---|---|---|
| Missing spec/design artifacts | Low (suggestion) | Change only authored `tasks.md`. Consider backfilling `proposal.md` + `spec.md` + `design.md` for full SDD compliance. |
| ProximityBased tie-breaker invariant | Low (suggestion) | The strict `<` comparison (`directTotal < viaHotelTotal`) means equal distances keep hotel transit. This is documented in code comments but lacks a dedicated unit test with deliberately equidistant locations. |
| 400-line review budget risk noted | Low | Tasks.md flagged Medium budget risk at ~350-400 lines but user explicitly chose single PR. |

No CRITICAL or WARNING issues exist.

---

## Commit Reference

```
Commit: d328bf2
Message: feat(itinerary): add ReturnToHotelStrategy with inter-block transit optimization
Files:   20 files changed, +2342 / −8 lines
```

---

## Archive Verification Checklist

| Check | Result |
|---|---|
| Main specs updated with delta specs | ⏭️ N/A — no delta specs authored |
| Change folder moved to archive | ✅ `openspec/changes/archive/2026-06-21-return-to-hotel-strategy/` |
| Archive contains all artifacts | ✅ `tasks.md`, `verify-report.md` |
| Archived `tasks.md` has no unchecked tasks | ✅ 14/14 tasks, 39/39 sub-items `[x]` complete |
| Active changes directory clean | ✅ `openspec/changes/return-to-hotel-strategy/` removed |
| No CRITICAL issues in verify-report | ✅ None found |
| Archive audit trail preserved | ✅ This report written |
