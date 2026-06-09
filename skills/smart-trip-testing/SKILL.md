---
name: smart-trip-testing
description: "Trigger: MSTest, Moq, Handler tests, unit tests. Define the testing strategy and patterns for the Smart Trip Planner."
license: Apache-2.0
metadata:
  author: gentleman-programming
  version: "1.0"
---

## Activation Contract
Activate this skill when writing new tests, adding test cases for edge cases, or fixing failing tests.

## Hard Rules
- **Naming Convention**: Test methods MUST follow the pattern `Handle_<Scenario>_<Expected>`. Example: `Handle_InvalidDates_ThrowsValidationException`.
- **Pattern**: Strictly follow the **AAA (Arrange, Act, Assert)** pattern.
- **Isolation**: Use `Moq` to isolate the Handler from its dependencies (`ITripRepository`, `ITripOptimizerService`, etc.).
- **Test Scope**: One `[TestClass]` per Handler.

## Decision Gates
| Test Type | Focus |
|-----------|-------|
| Handler Test | Focus on orchestration: did it call the repo? did it map the response? |
| Optimizer Test | Focus on the solver: did it handle the over-constrained case? |
| Validation Test | Focus on `FluentValidation` rules. |

## Execution Steps
1. Create the test class in `tests/SmartTripPlanner.Tests/Handlers/`.
2. Setup Mocks for required interfaces in the `Arrange` section.
3. Execute the handler via `_mediator.Send()` or direct instantiation in the `Act` section.
4. Verify outcomes using `Assert` and `Mock.Verify` in the `Assert` section.

## Output Contract
- Deterministic tests with clear naming and AAA structure.
- High coverage of business-critical scenarios (especially the "over-constrained" path).

## References
- `doc/architecture/solution_arch.md` — Section 3.5 (Tests).
