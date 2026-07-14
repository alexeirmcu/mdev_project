# 🌍 Smart Trip Planner (Europe) v1

An intelligent, block-based travel itinerary generator designed for families doing basecamp-style trips in European cities.

## 🚀 The Pitch
Planning a multi-day family trip is a nightmare of spreadsheets and Google Maps. **Smart Trip Planner** solves this by generating deterministic, executable itineraries organized into three daily blocks (**Morning, Afternoon, and Night**). 

It maximizes sightseeing efficiency, suggests the most sensible transport (Car vs. Public Transport), and adapts instantly to weather changes or delays.

---

## 🏗️ Architecture & Design

### The Hybrid Planning Engine
The core innovation is the separation of **Optimization** and **Enrichment**:

1.  **Deterministic Optimization (Synchronous)**: Uses a **heuristic multi-phase algorithm** to distribute must-sees and candidates across daily blocks in milliseconds, applying proximity, schedule, weather, and transport rules.
2.  **Semantic Enrichment (Asynchronous)**: Uses an **LLM** as a background worker to enrich the place catalog with metadata (family-friendly scores, typical durations, indoor/outdoor flags) that standard Map APIs don't provide.

### Architectural Pattern: Clean Architecture (Onion)
The solution is structured to ensure a strict separation of concerns:

- **`Domain`**: Pure business logic, entities, and repository interfaces. Zero framework dependencies.
- **`ApplicationServices`**: Use-case orchestration using **CQRS with MediatR**. Handles commands, validation (FluentValidation), and mapping (AutoMapper).
- **`Infrastructure`**: Implementation details. EF Core (PostgreSQL), and LLM integration.
- **`API`**: Thin controllers that act as the entry point.

---

## 🛠️ Tech Stack

| Concern | Technology |
|---|---|
| **Platform** | .NET 8 / C# 12 |
| **API** | ASP.NET Core MVC Controllers |
| **Orchestration** | MediatR 12.x |
| **Optimization** | Heuristic multi-phase algorithm (5 phases: GenerateDays → PinnedMustSees → UnpinnedMustSees → FillCandidates → EnrichTransitAndWeather) |
| **Validation** | FluentValidation |
| **Mapping** | AutoMapper 13.x |
| **Persistence** | EF Core 8.x + PostgreSQL |
| **Logging** | Serilog 4.x |
| **API Docs** | Swashbuckle.AspNetCore (Swagger) |

---

## 📖 Domain Glossary

- **Must-See**: A destination the user explicitly wants to visit. These are prioritized by the solver.
- **BlockTimeline**: A segment of the day (Morning/Afternoon/Evening) with a target duration.
- **ActivityNode**: A specific visit within a block, including estimated arrival/departure and duration.
- **Heuristic Itinerary Generator**: Domain service that orchestrates the 5-phase planning algorithm.

---

## 👨‍💻 Development Workflow

To add a new feature or endpoint, follow this flow:

1.  **Contract**: Update `doc/architecture/endpoints.yaml` with the new API definition.
2.  **Domain**: Define any new Entities or Repository interfaces in `SmartTripPlanner.Domain`.
3.  **Application**: Create the MediatR `Command` and `Handler` in `SmartTripPlanner.ApplicationServices`.
4.  **Infrastructure**: Implement the necessary logic or repository methods in `SmartTripPlanner.Infrastructure`.
5.  **API**: Add the endpoint to the `TripsController` in `SmartTripPlanner.API`.
6.  **Verify**: Create a Handler test in `SmartTripPlanner.Tests`.

---

## 🚦 Quick Start (Devlocal)

The project is configured for local development with PostgreSQL via docker-compose.

1. Clone the repo.
2. Open `SmartTripPlanner.sln` in Visual Studio 2022 or JetBrains Rider.
3. Run the `SmartTripPlanner.API` project.
4. Access the Swagger UI at `http://localhost:<port>/swagger` to test the endpoints.

---

## 🚀 Deployment

> **TODO**: CI/CD pipeline documentation will be added here.

The application requires the following secrets to be configured via **User Secrets** (development) or **environment variables** (production). None of these values are committed to the repository.

| Secret Key | Description | Example |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string | `Host=localhost;Database=SmartTripPlanner;Username=postgres;Password=postgres` |
| `FoursquareApi:ApiKey` | Foursquare Places API key | `23IO1KPSLVSTBCG5...` |
| `LlmApi:ApiKey` | LLM provider API key | `AQ.Ab8RN6Jiwv_6...` |
| `Jwt:Secret` | JWT signing secret (min 32 bytes) | `dev-secret-key-that-is-at-least-32-bytes-long-for-hs256` |
| `Jwt:Issuer` | JWT issuer claim | `smart-trip-planner` |
| `Jwt:Audience` | JWT audience claim | `smart-trip-planner-api` |

### Setting secrets in development

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "your-connection-string" --project SmartTripPlanner.API
dotnet user-secrets set "FoursquareApi:ApiKey" "your-foursquare-key" --project SmartTripPlanner.API
dotnet user-secrets set "LlmApi:ApiKey" "your-llm-key" --project SmartTripPlanner.API
dotnet user-secrets set "Jwt:Secret" "your-jwt-secret-min-32-bytes" --project SmartTripPlanner.API
dotnet user-secrets set "Jwt:Issuer" "your-issuer" --project SmartTripPlanner.API
dotnet user-secrets set "Jwt:Audience" "your-audience" --project SmartTripPlanner.API
```

View configured secrets:
```bash
dotnet user-secrets list --project SmartTripPlanner.API
```

---

## 📑 Documentation

- [Solution Architecture](doc/architecture/solution_arch.md) — Deep dive into the layers and models.
- [ADR-001: Hybrid Planning Engine](doc/adr/ADR-v1.md) — The "Why" behind OR-Tools + LLM.
- [MVP Product Spec](doc/spec/spec-v1.md) — Requirements, scoring, and business rules.
- [JWT Authentication Guide](doc/jwt-auth.md) — How to generate and use JWT Bearer tokens for API access.
