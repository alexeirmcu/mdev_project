# Verification Report: return-to-hotel-strategy

**Change:** return-to-hotel-strategy
**Project:** mdev_project (.NET 8, Clean Architecture, EF Core PostgreSQL/InMemory)
**Persistence mode:** openspec
**Date:** 2026-06-21
**Verifier:** sdd-verify

## Artifact Availability

| Artifact | Present | Path |
|---|---|---|
| proposal.md | NO | — |
| spec.md | NO | — |
| design.md | NO | — |
| tasks.md | YES | `openspec/changes/return-to-hotel-strategy/tasks.md` |

**Note:** Only the `tasks.md` artifact exists for this change. No proposal, spec, or design artifacts were authored. Per graceful-artifact handling, verification proceeds on **tasks-only objective completion** plus the audit checklist supplied by the orchestrator. Spec correctness and design coherence checks are **SKIPPED** (missing artifacts) and recorded below.

## Build & Test Evidence

| Command | Result |
|---|---|
| `dotnet build` (solution) | SUCCESS — 0 errors, warnings only pre-existing |
| `dotnet test` (solution) | **Passed! — Failed: 0, Passed: 333, Skipped: 0, Total: 333, Duration: 4s** |

**Test count matches task expectation:** tasks.md (14.2) specifies 333 (308 original + 25 new). Observed = 333. ✅

## Task Completeness

All 14 tasks (39 sub-items) are marked `[x]` complete in `tasks.md`. No unchecked items.

| Task | State | Verified |
|---|---|---|
| 1: Domain Model & Enum | ✅ | `ReturnToHotelStrategy.cs` exists with 3 distinct values; 2 tests pass |
| 2: TripPreferences Update | ✅ | Property, ctor param, equality, DTO all updated |
| 3: EF Core Mapping | ✅ | `HasConversion<string>()` + `HasDefaultValue(Always)` + `OwnsOne` × 3 |
| 4: BlockTimeline InterBlockTransit | ✅ | Property added; `BlockTotalDurationMinutes` excludes it |
| 5: TransitEnricher Strategy Logic | ✅ | `ApplyStrategyAsync` implements Never + ProximityBased |
| 6: TimelineScheduler Block Chaining | ✅ | Chaining logic with `previousBlockEnd` across blocks |
| 7: Response DTO Updates | ✅ | `BlockResponse.InterBlockTransit` added |
| 8: AutoMapper Mapping | ✅ | `InterBlockTransit` mapped on line 68 |
| 9: ApplicationServicesRegistration | ✅ | Full solution builds |
| 10: TransitEnricher Tests (6) | ✅ | All present and passing |
| 11: TimelineScheduler Tests (5) | ✅ | All present and passing |
| 12: Integration Tests (4) | ✅ | All present and passing |
| 13: EF Migration | ✅ | `20260621193200_AddReturnToHotelStrategy.cs` present, symmetric up/down |
| 14: Full Regression | ✅ | 333 / 333 passing |

## Spec Compliance Audit (orchestrator checklist)

| # | Requirement | Status | Evidence |
|---|---|---|---|
| 1 | `ReturnToHotelStrategy` enum exists with `Always`, `Never`, `ProximityBased` | **PASS** | `Enums/ReturnToHotelStrategy.cs` lines 5-7; verified by `ReturnToHotelStrategyTests.Enum_HasThreeDistinctValues` |
| 2 | `TripPreferences.ReturnToHotelStrategy` present, default `Always` | **PASS** | `AggregatesModel/TripPreferences.cs` line 11 (property) + line 18 (ctor default `Always`); `TripPreferencesTests.Default_ReturnToHotelStrategy_IsAlways` |
| 3 | `BlockTimeline.InterBlockTransit` property | **PASS** | `AggregatesModel/BlockTimeline.cs` line 12 `TransitDetails? InterBlockTransit { get; set; }` |
| 4 | `TransitEnricher` implements all three strategies | **PASS** | `Services/TransitEnricher.cs` lines 84-156: Always skipped (84), Never branch (120-131), ProximityBased branch (132-154) |
| 5 | `TimelineScheduler` chains blocks when `InterBlockTransit` is present | **PASS** | `Services/TimelineScheduler.cs` lines 43-50: chains via `previousBlockEnd + Duration + Buffer` |
| 6 | `Always` strategy produces identical behavior to before | **PASS** | Line 84 `!= Always` guards the entire `ApplyStrategyAsync` call; no second pass → same output as pre-change |
| 7 | `Never` skips hotel at Morning↔Afternoon and Afternoon↔Evening | **PASS** | Loop `i < blocks.Length - 1` covers i=0 (M→A) and i=1 (A→E); nulls `currentBlock.TransitToHotel` + `nextBlock.TransitFromHotel`, sets `nextBlock.InterBlockTransit` |
| 8 | `ProximityBased` compares direct vs via-hotel, chooses shorter | **PASS** | Lines 134-153: computes `direct`, `toHotel`, `fromHotel`; `if (directTotal < viaHotelTotal)` choose direct; tie (≤) → hotel kept |
| 9 | Evening ALWAYS returns to hotel regardless of strategy | **PASS** | Evening is the terminal block; `ApplyStrategyAsync` never nulls `Evening.TransitToHotel`. Tests `EnrichAsync_NeverStrategy_EveningAlwaysReturnsToHotel` + `EnrichAsync_ProximityBased_EveningAlwaysReturnsToHotel` + integration `GenerateAsync_NeverStrategy_EveningAlwaysReturnsToHotel` all pass |
| 10 | Response DTOs updated with `InterBlockTransit` | **PASS** | `ApiModels/TripPlanResponse.cs` line 41 `BlockResponse.InterBlockTransit` |
| 11 | AutoMapper configured correctly | **PASS** | `API/Configurations/AutoMapperProfile.cs` line 68 `.ForMember(dest => dest.InterBlockTransit, ...)`; solution builds & mapping tests pass |

## Backward Compatibility

| Check | Status | Evidence |
|---|---|---|
| `BlockTotalDurationMinutes` unchanged | **PASS** | `BlockTimeline.cs` line 13 — formula identical to pre-change: `Activities.Sum(a => a.DurationMinutes + TransitToNext)`; InterBlockTransit excluded. `BlockTimelineTests.BlockTotalDurationMinutes_ExcludesInterBlockTransit` asserts exclusion |
| Existing tests not modified/broken | **PASS** | All 308 pre-existing tests pass; new tests are additive (25 new) |
| `Always` default preserves old behavior | **PASS** | Default ctor param `Always` (line 18); EF `HasDefaultValue(Always)`; `ApplyStrategyAsync` skipped entirely → no-op for default trips |
| No breaking public API changes | **PASS** | Additive only: new enum, new nullable property, new optional ctor param with default, new nullable DTO field. No existing signature removed |

## Design Coherence

**SKIPPED** — no `design.md` artifact exists for this change.

## Spec Correctness (scenario-based)

**SKIPPED** — no `spec.md` artifact exists for this change. Spec compliance is assessed via the orchestrator's audit checklist above, all of which PASS and are backed by runtime test evidence.

## Behavioral Test Coverage Matrix (runtime evidence)

All orchestrator-listed behavioral requirements are covered by **passing** runtime tests:

| Behavior | Covering test(s) (all passing) |
|---|---|
| Always = hotel legs, no InterBlockTransit | `TransitEnricherTests.EnrichAsync_AlwaysStrategy_HotelLegsPresentNoInterBlockTransit`; `HeuristicItineraryGeneratorTests.GenerateAsync_AlwaysStrategy_HotelLegsPresentNoInterBlockTransit` |
| Never = InterBlockTransit present, boundary hotel legs null | `TransitEnricherTests.EnrichAsync_NeverStrategy_InterBlockTransitPresentHotelLegsNullAtBoundaries`; `HeuristicItineraryGeneratorTests.GenerateAsync_NeverStrategy_InterBlockTransitPopulated` |
| Never = Evening always returns to hotel | `TransitEnricherTests.EnrichAsync_NeverStrategy_EveningAlwaysReturnsToHotel`; `HeuristicItineraryGeneratorTests.GenerateAsync_NeverStrategy_EveningAlwaysReturnsToHotel` |
| Never = empty block boundary keeps hotel transit | `TransitEnricherTests.EnrichAsync_NeverStrategy_EmptyBlockBoundary_KeepsHotelTransit` |
| ProximityBased = mechanism runs and chooses direct when shorter | `TransitEnricherTests.EnrichAsync_ProximityBased_ChoosesDirectWhenShorter`; `EnrichAsync_ProximityBased_MechanismRunsAndMakesChoice`; `HeuristicItineraryGeneratorTests.GenerateAsync_ProximityBased_MechanismRuns` |
| ProximityBased = Evening always returns to hotel | `TransitEnricherTests.EnrichAsync_ProximityBased_EveningAlwaysReturnsToHotel` |
| Scheduler chains via InterBlockTransit | `TimelineSchedulerTests.Schedule_BlockWithInterBlockTransit_ChainsFromPreviousBlockEnd` |
| TransitFromHotel takes priority over InterBlockTransit | `TimelineSchedulerTests.Schedule_BlockWithTransitFromHotel_ResetsToStartTime` |
| Mixed boundaries (chain + reset) | `TimelineSchedulerTests.Schedule_InterBlockTransitChainsMixedWithTransitFromHotel` |
| Empty block followed by non-empty resets | `TimelineSchedulerTests.Schedule_EmptyBlockFollowedByNonEmpty_ResetsToStartTime` |
| Multiple InterBlockTransit chains correctly | `TimelineSchedulerTests.Schedule_MultipleInterBlockTransit_ChainsCorrectly` |
| Enum has 3 distinct values, default Always | `ReturnToHotelStrategyTests.Enum_HasThreeDistinctValues` + `DefaultValue_IsAlways` |
| TripPreferences default + equality | `TripPreferencesTests.Default_ReturnToHotelStrategy_IsAlways` + `Equals_DifferentReturnToHotelStrategy_ReturnsFalse` + `Equals_SameReturnToHotelStrategy_ReturnsTrue` |
| InterBlockTransit default null, settable, excluded from total | `BlockTimelineTests.InterBlockTransit_DefaultIsNull` + `InterBlockTransit_CanBeSet` + `BlockTotalDurationMinutes_ExcludesInterBlockTransit` |

## Issues

### CRITICAL
None.

### WARNING
None.

### SUGGESTION
1. **Missing spec/design artifacts.** This change only authored `tasks.md`. For future changes, authoring a `proposal.md` + `spec.md` + `design.md` enables full SDD spec-scenario compliance verification and design coherence checks. The orchestrator's supplied checklist was used as a substitute; consider backfilling artifacts to make this change fully SDD-compliant before archiving.
2. **ProximityBased tie-breaker is directionally implicit.** Code uses `if (directTotal < viaHotelTotal)` (strict) which means equal totals keep hotel transit. This is documented in code comments and is the intended behavior per tasks.md (5.3 — "tie-breaker favors hotel"). Consider surfacing this invariant as a unit test with deliberately equidistant locations to guard against future regression.

## Final Verdict

# ✅ PASS

- **Build:** SUCCESS (0 errors)
- **Tests:** 333 / 333 passing (0 failed, 0 skipped) — matches task commitment exactly
- **Task completeness:** 14 / 14 tasks, 39 / 39 sub-items complete
- **Orchestrator audit checklist:** 11 / 11 requirements PASS with runtime evidence
- **Backward compatibility:** 4 / 4 checks PASS — `Always` default is a pure no-op, additive-only API surface
- **Design coherence:** SKIPPED (no design.md)
- **Spec correctness:** SKIPPED (no spec.md; orchestrator checklist used as surrogate, all PASS)

No CRITICAL or WARNING issues. Two non-blocking SUGGESTIONs raised for future artifact discipline. Implementation is ready for archive.