# Smart Trip Planner

MVP for generating realistic, block-based travel itineraries for families doing basecamp-style trips in European cities.

## Value Proposition

Generate an executable day-by-day itinerary (Morning / Afternoon / Night blocks) that maximizes what a family can see, uses sensible transport, and adapts quickly when plans change or weather is bad.

**Core constraint:** The LLM never writes the final itinerary. Itinerary generation is deterministic — owned by the planner/backend layer.

## Tech Stack

| Concern | Technology |
|---|---|
| Platform | .NET 8 / C# 12 |
| Web framework | ASP.NET Core MVC Controllers |
| Architecture | Clean Architecture + CQRS |
| Mediator | MediatR 12.x |
| Mapping | AutoMapper 13.x |
| Validation | FluentValidation |
| Logging | Serilog 4.x |
| Route optimization | Google OR-Tools (VRPTW) |
| AI enrichment | External LLM API (background/offline only) |
| ORM | Entity Framework Core 8.x (InMemory for MVP) |
| API docs | Swashbuckle.AspNetCore 6.8.x |
| Database (target) | PostgreSQL |
## Key Design Decision

Itinerary scheduling uses **Google OR-Tools** (VRPTW) synchronously for sub-50ms deterministic results. The LLM is used only asynchronously in the background to enrich place metadata (duration estimates, family-friendly scores, indoor/outdoor flags). See [ADR-001](doc/adr/ADR-v1.md) for full rationale.

## Documentation

| Document | Description |
|---|---|
| [Solution Architecture](doc/architecture/solution_arch.md) | Clean Architecture breakdown, domain model, CQRS features, API endpoints |
| [ADR-001: Hybrid Planning Engine](doc/adr/ADR-v1.md) | Decision record for OR-Tools + LLM approach |
| [MVP Product Spec](doc/spec/spec-v1.md) | Product requirements, scoring model, transport and weather rules |
