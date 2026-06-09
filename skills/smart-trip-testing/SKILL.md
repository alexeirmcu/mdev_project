---
name: smart-trip-testing
description: "Trigger: MSTest, Moq, Handler tests, domain tests, unit tests. Define testing conventions for Smart Trip Planner: domain tests mirror source structure, handler tests use Moq."
license: Apache-2.0
metadata:
  author: gentleman-programming
  version: "1.1"
---

## Activation Contract
Activate this skill when writing new tests, adding test cases for edge cases, or fixing failing tests.

## Hard Rules

### General (all tests)
- **AAA Pattern**: Strictly follow **Arrange, Act, Assert** — separate each section with a blank line.
- **Implicit Usings**: Enabled. `Microsoft.VisualStudio.TestTools.UnitTesting` is available via the csproj `Using`.
- **Parallelism**: Tests run at method level (`ExecutionScope.MethodLevel` in `MSTestSettings.cs`).
- **Test Class**: `[TestClass] public sealed class {Name}Tests` — always `sealed`.
- **File-scoped namespaces**: Use `namespace SmartTripPlanner.Tests.{SourceNamespace};`.

### Domain Tests (pure unit tests, no mocking)
- **Directory mirroring**: Test file path MUST mirror the source file path under `tests/SmartTripPlanner.Tests/`.
  - Source: `SmartTripPlanner.Domain/AggregatesModel/Trip.cs`
  - Test: `tests/SmartTripPlanner.Tests/SmartTripPlanner.Domain/AggregatesModel/TripTests.cs`
- **One file per class**: `{ClassName}Tests.cs` — one `[TestClass]` per file.
- **Naming**: `Method_Scenario_Expected`. Examples:
  - `Constructor_WithNullPlaceId_ThrowsArgumentNullException`
  - `AddSelectedAttraction_AddsToList`
  - `Equals_SamePlaceId_ReturnsTrue`
- **No mocking**: Domain tests are pure — no Moq, no dependencies.
- **Expected exceptions**: MSTest 4.0.1 does not expose `Assert.ThrowsException<T>`. Use manual try/catch:

```csharp
[TestMethod]
public void Method_WithInvalidInput_Throws()
{
    try
    {
        _ = new Target(null!);
        Assert.Fail("Expected XxxException was not thrown");
    }
    catch (ArgumentNullException ex)
    {
        Assert.AreEqual("paramName", ex.ParamName);
    }
}
```
- **Helper factory methods**: Use `private static` factory methods for Arrange when the same setup repeats across tests (e.g., `CreateTrip()`, `CreateDayPlan()`).
- **using directives**: Import from the source namespace (`using SmartTripPlanner.Domain.AggregatesModel;`), not from the test namespace.

### Handler Tests (orchestration with Moq)
- **Naming**: `Handle_<Scenario>_<Expected>`. Example: `Handle_InvalidDates_ThrowsValidationException`.
- **Isolation**: Use `Moq` to isolate the Handler from its dependencies (`ITripRepository`, `ITripOptimizerService`, etc.).
- **Test Scope**: One `[TestClass]` per Handler.

## Decision Gates
| Test Type | Focus |
|-----------|-------|
| Domain Test | Pure logic: constructors, equality, validation, state changes. No mocking. |
| Handler Test | Orchestration: did it call the repo? did it map the response? |
| Optimizer Test | Solver: did it handle the over-constrained case? |
| Validation Test | FluentValidation rules. |

## Execution Steps

### Domain Tests
1. Identify the source file path under the domain project (e.g., `SmartTripPlanner.Domain/AggregatesModel/Trip.cs`).
2. Mirror the path under `tests/SmartTripPlanner.Tests/` (e.g., `tests/SmartTripPlanner.Tests/SmartTripPlanner.Domain/AggregatesModel/TripTests.cs`).
3. Create a `public sealed class {ClassName}Tests` with `[TestClass]`.
4. Use file-scoped namespace matching the path: `namespace SmartTripPlanner.Tests.Domain.AggregatesModel;`.
5. Add a `private static` factory method if multiple tests need the same Arrange setup.
6. Write test methods following AAA with `Method_Scenario_Expected` naming.
7. Use manual try/catch for expected exceptions.

### Handler Tests
1. Create the test class in `tests/SmartTripPlanner.Tests/Handlers/`.
2. Setup Mocks for required interfaces in the `Arrange` section.
3. Execute the handler via `_mediator.Send()` or direct instantiation in the `Act` section.
4. Verify outcomes using `Assert` and `Mock.Verify` in the `Assert` section.

## Output Contract
- Deterministic tests with clear naming and AAA structure.
- Domain tests organized in mirror directories, one file per class.
- High coverage of business-critical scenarios (especially the "over-constrained" path for handlers).

## References
- `doc/architecture/solution_arch.md` — Section 3.5 (Tests).
