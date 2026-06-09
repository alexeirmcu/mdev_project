---
name: cqrs-mediatr-patterns
description: "Trigger: MediatR, Command, Handler, Query, CQRS. Enforce standard naming and implementation patterns for MediatR requests and handlers."
license: Apache-2.0
metadata:
  author: gentleman-programming
  version: "1.0"
---

## Activation Contract
Activate this skill when implementing new use cases, refactoring handlers, or adding pipeline behaviors.

## Hard Rules
- **Command Naming**: Commands MUST NOT use the suffix `Command` or `Query`. (e.g., Use `GenerateTrip`, NOT `GenerateTripCommand`).
- **Handler Naming**: Handlers MUST follow the pattern `<Command>Handler`. (e.g., `GenerateTripHandler`).
- **Validation**: Every Command requiring business validation MUST have a corresponding `FluentValidation` validator.
- **Response Types**: Use specific `ApiModel` records for responses instead of returning Domain entities.
- **Pipeline Behaviors**: Ensure `LoggingBehavior` and `ValidationBehavior` are registered in the correct order in `Program.cs`.

## Decision Gates
| Need | Action |
|------|--------|
| Read operation | Implement as a Command (per project convention) and call it a "Query" in documentation. |
| Cross-cutting concern | Implement as an `IPipelineBehavior<TRequest, TResponse>`. |
| Complex validation | Implement in a separate `IValidator<T>` class in `ApplicationServices/Commands/`. |

## Execution Steps
1. Create the request record (e.g., `GenerateTrip`) in `ApplicationServices/Commands/`.
2. Create the handler class (e.g., `GenerateTripHandler`) implementing `IRequestHandler<TRequest, TResponse>`.
3. Implement the validator using `AbstractValidator<T>`.
4. Map the result to an `ApiModel` using `IMapper`.

## Output Contract
- MediatR request and handler following the established naming convention.
- Validated request pipeline.

## References
- `doc/architecture/solution_arch.md` — Section 3.2 (ApplicationServices).
