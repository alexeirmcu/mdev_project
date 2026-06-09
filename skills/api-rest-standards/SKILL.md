---
name: api-rest-standards
description: "Trigger: API, Endpoint, REST, Controller, HTTP response. Enforce consistency between the REST API and the OpenAPI specification."
license: Apache-2.0
metadata:
  author: gentleman-programming
  version: "1.0"
---

## Activation Contract
Activate this skill when adding new endpoints, modifying request/response models, or implementing error handling.

## Hard Rules
- **Source of Truth**: `doc/architecture/endpoints.yaml` is the definitive contract. Any change to the API MUST start with an update to this file.
- **Response Consistency**: Use `TripPlanResponse` and `TripSummaryResponse` exactly as defined in the spec.
- **Error Codes**:
  - `400 Bad Request`: Validation failures (FluentValidation).
  - `404 Not Found`: Entity not found (`TripNotFoundException`, etc.).
  - `422 Unprocessable Entity`: Mathematically irresolvable routes (`OverConstrainedRouteException`).
- **Controller Thinness**: No business logic in controllers. Only `_mediator.Send()`.

## Decision Gates
| Action | Response Code |
|--------|---------------|
| Resource Created | `200 OK` (returning the generated trip) |
| Resource Updated (Complete) | `204 No Content` |
| Resource Not Found | `404 Not Found` |
| Business Rule Violation | `422 Unprocessable Entity` |

## Execution Steps
1. Update `endpoints.yaml` with the new path/method/schema.
2. Create/Update `ApiModel` records in `SmartTripPlanner.Domain/ApiModels/`.
3. Implement the corresponding MediatR Command and Handler.
4. Add the endpoint to `TripsController` and verify it matches the YAML.

## Output Contract
- API endpoints that strictly adhere to the OpenAPI specification.
- Structured error responses via `ExceptionHandlingMiddleware`.

## References
- `doc/architecture/endpoints.yaml` — OpenAPI Spec.
- `doc/architecture/solution_arch.md` — Section 5 (HTTP Contracts).
