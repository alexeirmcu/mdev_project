---
name: dotnet-clean-arch
description: "Trigger: Clean Architecture, Onion Architecture, layering, project structure. Enforce strict layering and dependency rules for the Smart Trip Planner."
license: Apache-2.0
metadata:
  author: gentleman-programming
  version: "1.0"
---

## Activation Contract
Activate this skill when creating new projects, moving logic between layers, or reviewing architecture compliance.

## Hard Rules
- **Domain Purity**: `SmartTripPlanner.Domain` MUST have zero framework dependencies (except `Microsoft.Extensions.DependencyInjection.Abstractions`). No EF Core, no AutoMapper, no MediatR.
- **Dependency Flow**: Dependencies must only point inwards: `API` $\rightarrow$ `ApplicationServices` $\rightarrow$ `Domain` and `Infrastructure` $\rightarrow$ `Domain`.
- **Infrastructure Isolation**: EF Core types (`DbContext`, `DbSet`) and OR-Tools specifics MUST stay within `SmartTripPlanner.Infrastructure`.
- **Composition Root**: The only place where `API` may reference `Infrastructure` is `Program.cs` for DI registration.
- **No Business Logic in Controllers**: Controllers must only build a command and call `_mediator.Send()`.

## Decision Gates
| Scenario | Action |
|----------|--------|
| New business rule | Implement in `Domain` (Entities/Value Objects) or `ApplicationServices` (Handlers). |
| New external API/DB call | Define interface in `Domain` or `ApplicationServices`, implement in `Infrastructure`. |
| New API Endpoint | Create Controller in `API` $\rightarrow$ Command in `ApplicationServices` $\rightarrow$ Handler in `ApplicationServices`. |

## Execution Steps
1. Identify the target layer.
2. Verify that the required dependency is allowed by the dependency flow.
3. Implement the logic using the appropriate project's naming conventions.
4. Ensure no "leakage" of infrastructure types into the application or domain layers.

## Output Contract
- Code following Clean Architecture boundaries.
- Justification for any necessary exceptions in the Composition Root.

## References
- `doc/architecture/solution_arch.md` — Full architectural breakdown.
