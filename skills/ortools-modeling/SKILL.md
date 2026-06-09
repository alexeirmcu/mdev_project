---
name: ortools-modeling
description: "Trigger: OR-Tools, VRPTW, routing, scheduling, optimization. Guidance for modeling travel itineraries using Google OR-Tools."
license: Apache-2.0
metadata:
  author: gentleman-programming
  version: "1.0"
---

## Activation Contract
Activate this skill when implementing or modifying the `GoogleOrToolsOptimizer` or refining the route optimization logic.

## Hard Rules
- **Time Representation**: ALL times MUST be represented as `int` (minutes from 00:00). Example: 09:30 $\rightarrow$ 570.
- **Deterministic Results**: Ensure the solver is configured for determinism.
- **Over-constrained Handling**: If the solver finds no solution, the system MUST NOT crash. It must implement a relaxation strategy (e.g., removing low-priority must-sees).
- **Buffer Management**: Always include `BufferMinutes` between activities to account for family movement friction.

## Decision Gates
| Scenario | Action |
|----------|--------|
| Must-see with fixed day | Add a hard constraint to the solver for that specific day index. |
| Indoor vs Outdoor | Use a weighted cost function to prefer indoor places when `WeatherCondition == BAD`. |
| Transport Mode | Calculate costs based on `TransportMode` (e.g., penalize CAR in dense city centers). |

## Execution Steps
1. Convert all `DateTime` and `TimeSpan` inputs to `int` minutes.
2. Initialize the `RoutingIndexManager` and `RoutingModel`.
3. Define the distance/time callbacks.
4. Add constraints (Time Windows, Priorities).
5. Solve and map the `Assignment` back to `ActivityNode` sequences.

## Output Contract
- Optimized itinerary that respects hard constraints and optimizes soft priorities.
- Graceful degradation when the route is over-constrained.

## References
- `doc/adr/ADR-v1.md` — Hybrid Planning Engine decision.
- `doc/spec/spec-v1.md` — Heuristics and scoring model.
