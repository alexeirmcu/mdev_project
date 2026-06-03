# Documento de Arquitectura de Software (SAD) — Smart Trip Planner API

## 1. Resumen Ejecutivo y Metas de Diseño

Este documento define la estructura y las directrices técnicas definitivas para el proyecto *Smart Trip Planner (Europe) v1*.

### Especificaciones Técnicas Base

| Aspecto | Detalle |
|---------|---------|
| Plataforma | .NET 8.0 SDK / C# 12 |
| Patrón Arquitectónico | Clean Architecture (Onion) + CQRS |
| HTTP API | ASP.NET Core MVC Controllers (`[ApiController]`) |
| Mediator | MediatR 12.x con `LoggingBehavior<,>` y `ValidationBehavior<,>` |
| Mapping | AutoMapper 13.x |
| Logging | Serilog 4.x |
| Validación | FluentValidation |
| Persistencia | Entity Framework Core 8.x (InMemory para MVP) |
| API Docs | Swashbuckle.AspNetCore 6.8.x |

---

## 2. Estructura de la Solución .NET

La solución se llama `SmartTripPlanner.sln`. Los proyectos cuelgan directamente de la raíz, sin wrapper `src/`:

```
/
├── SmartTripPlanner.sln
├── SmartTripPlanner.API/                  # Entry point — Controllers, Middleware, Config
├── SmartTripPlanner.ApplicationServices/  # Commands, Handlers, Services, Behaviors
├── SmartTripPlanner.Domain/               # AggregatesModel, ApiModels, Enums, Repository
├── SmartTripPlanner.Infrastructure/       # EF Core, Repositories, OR-Tools, LLM Enricher
└── tests/
    └── SmartTripPlanner.Tests/
```

### Referencias entre proyectos

| Proyecto | Referencia |
|----------|-----------|
| `API` | ApplicationServices, Domain, Infrastructure ¹ |
| `ApplicationServices` | Domain |
| `Infrastructure` | Domain |
| `Tests` | API, ApplicationServices, Domain, Infrastructure |

> ¹ La referencia `API → Infrastructure` es exclusiva del **composition root** (`Program.cs`). Los controllers y middlewares no deben consumir tipos de Infrastructure directamente; lo impide el Tripwire §7.

---

## 3. Proyectos

### 3.1 `SmartTripPlanner.Domain` (Class Library)

Zero dependencias de framework. Contiene el modelo de dominio puro y los modelos de API. La única excepción permitida es `Microsoft.Extensions.DependencyInjection.Abstractions` para exponer `IServiceCollectionExtension.cs` como punto de registro propio.

**NuGet:** `Microsoft.Extensions.DependencyInjection.Abstractions`

**Estructura:**

```
Domain/
├── AggregatesModel/
│   ├── Trip.cs                # TripId (Guid), CityId, StartDate, EndDate, BaseHotel, Days, OriginalMustSees
│   ├── DayPlan.cs             # DayIndex, Date, WeatherSummary, Morning/Afternoon/Evening
│   ├── BlockTimeline.cs       # BlockTotalDurationMinutes, Activities[]
│   ├── ActivityNode.cs        # SequenceOrder, PlaceId, Name, IsCompleted, EstimatedArrival/EstimatedDeparture, DurationMinutes, IsIndoor, TransitToNext
│   ├── TransitDetails.cs      # TransportMode, DurationMinutes, BufferMinutes, FrictionAlert
│   ├── Location.cs            # Value Object: Name, Latitude, Longitude
│   └── City.cs                # CityId (string slug), CityName (string) — entidad del catálogo
├── ApiModels/
│   ├── TripGenerationRequest.cs   # public record — entrada POST /api/trips
│   ├── TripReplanRequest.cs       # public record — entrada POST /api/trips/{id}/replan
│   ├── CompletePlaceRequest.cs    # public record — entrada PATCH .../complete
│   ├── MustSeeInput.cs            # public record — anidado en TripGenerationRequest
│   ├── LocationModel.cs           # public record — anidado (lat/lon/name)
│   ├── TripSummaryResponse.cs     # public record — salida GET /api/trips (ver campos en §3.1.1)
│   ├── TripPlanResponse.cs        # public record — salida POST y GET /api/trips/{id}
│   └── ErrorResponse.cs           # public record — code, message, conflictingPlaceIds[]
├── Enums/
│   ├── Priority.cs            # HIGH, MEDIUM, LOW
│   ├── TransportMode.cs       # WALK_AND_PUBLIC_TRANSPORT, CAR
│   ├── WeatherCondition.cs    # GOOD, BAD
│   └── BlockType.cs           # MORNING, AFTERNOON, EVENING
├── Repository/
│   ├── ITripRepository.cs     # GetByIdAsync, ListAsync, AddAsync, UpdateAsync
│   └── ICityRepository.cs     # GetByIdAsync(string cityId) : Task<City?>
├── Constants/
│   └── TripPlanningConstants.cs   # Duraciones por bloque, máx. visitas, buffers
├── Exceptions/
│   ├── TripNotFoundException.cs
│   ├── CityNotFoundException.cs
│   └── OverConstrainedRouteException.cs
└── IServiceCollectionExtension.cs  # AddDomain(IServiceCollection) — registra solo abstracciones del dominio
```

#### 3.1.1 Campos de `TripSummaryResponse`

| Campo | Tipo | Descripción | Origen |
|-------|------|-------------|--------|
| `TripId` | `Guid` | Identificador del viaje | `Trip.TripId` |
| `CityId` | `string` | Slug de la ciudad (ej. `madrid-es`) | `Trip.CityId` |
| `CityName` | `string` | Nombre legible de la ciudad | `ICityRepository.GetByIdAsync(Trip.CityId)` |
| `StartDate` | `DateOnly` | Fecha de inicio del viaje | `Trip.StartDate` |
| `EndDate` | `DateOnly` | Fecha de fin del viaje | `Trip.EndDate` |
| `TotalMustSees` | `int` | Must-sees originales en la creación | `Trip.OriginalMustSees.Count` |
| `CompletedActivitiesCount` | `int` | Actividades marcadas como completadas | Agregado sobre `ActivityNode.IsCompleted == true` |
| `TotalActivitiesCount` | `int` | Total de actividades en el itinerario | Agregado sobre todos los `ActivityNode` |

> `ListTripsHandler` inyecta `ITripRepository` e `ICityRepository`. Resuelve `CityName` con una llamada a `ICityRepository` por cada viaje de la lista.

#### 3.1.2 Mapeo `ActivityNode` — Dominio vs. API

| Propiedad dominio | Campo JSON (endpoints.yaml) | Notas |
|-------------------|-----------------------------|-------|
| `EstimatedArrival` (`int` min) | `estimatedArrival` (`string "HH:mm"`) | Formateado en AutoMapperProfile |
| `EstimatedDeparture` (`int` min) | `estimatedDeparture` (`string "HH:mm"`) | Formateado en AutoMapperProfile |
| `DurationMinutes` (`int`) | `durationMinutes` (`integer`) | Derivado: `EstimatedDeparture - EstimatedArrival` |

**Nota de tiempos internos:** las horas se representan como `int` (minutos desde las 00:00); p. ej. 09:30 → 570. Las respuestas REST formatean como string `"09:30"`.

---

### 3.2 `SmartTripPlanner.ApplicationServices` (Class Library)

Orquestación de casos de uso. Sin tipos de EF ni dependencias de infraestructura.

**NuGet:** `MediatR 12.x`, `FluentValidation.DependencyInjectionExtensions`, `AutoMapper 13.x`

**Estructura:**

```
ApplicationServices/
├── Commands/
│   ├── GenerateTrip.cs        # public record GenerateTrip(TripGenerationRequest Payload) : IRequest<TripPlanResponse>
│   ├── CompletePlace.cs       # public record CompletePlace(Guid TripId, string PlaceId, bool IsCompleted) : IRequest<Unit>
│   ├── ReplanTrip.cs          # public record ReplanTrip(Guid TripId, TripReplanRequest Payload) : IRequest<TripPlanResponse>
│   ├── GetTripDetails.cs      # public record GetTripDetails(Guid TripId) : IRequest<TripPlanResponse>
│   └── ListTrips.cs           # public record ListTrips(string? CityId, DateOnly? StartDate, DateOnly? EndDate) : IRequest<IEnumerable<TripSummaryResponse>>
├── Handlers/
│   ├── GenerateTripHandler.cs      # llama ITripOptimizerService, luego ICatalogEnrichmentService fire-and-forget
│   ├── CompletePlaceHandler.cs
│   ├── ReplanTripHandler.cs
│   ├── GetTripDetailsHandler.cs
│   └── ListTripsHandler.cs         # inyecta ITripRepository + ICityRepository
├── Services/
│   ├── ITripOptimizerService.cs    # Task<IEnumerable<DayPlan>> OptimizeRouteAsync(Trip trip, TripReplanRequest? replanContext, CancellationToken ct)
│   └── ICatalogEnrichmentService.cs  # Task EnrichAsync(string cityId, CancellationToken ct)
├── Behaviors/
│   ├── LoggingBehavior.cs          # IPipelineBehavior<,> — traza entrada/salida de cada request
│   └── ValidationBehavior.cs       # IPipelineBehavior<,> — ejecuta FluentValidation validators; lanza ValidationException si falla
└── IServiceCollectionExtension.cs
```

> **Nota de estructura CQRS:** `GetTripDetails` y `ListTrips` son semánticamente queries (lecturas). Por convención de esta solución se co-localizan en `Commands/` para evitar proliferación de carpetas en un bounded context pequeño. Si el proyecto crece, se recomienda separar en `Commands/` y `Queries/`.

> **Patrón fire-and-forget en `GenerateTripHandler`:** tras guardar el trip, el handler lanza el enriquecimiento del catálogo sin bloquear la respuesta:
> ```csharp
> await _tripRepository.AddAsync(trip, ct);
> _ = Task.Run(() => _enricher.EnrichAsync(trip.CityId, CancellationToken.None), CancellationToken.None);
> return _mapper.Map<TripPlanResponse>(trip);
> ```
> El `CancellationToken.None` es intencional: el enriquecimiento no debe cancelarse si el request HTTP finaliza.

**Convenciones de naming (metodología org):**
- Commands sin sufijo `Command` / `Query`: `GenerateTrip`, `CompletePlace`, `ReplanTrip`, `GetTripDetails`, `ListTrips`
- Handlers: `<Command>Handler` — `GenerateTripHandler`, `GetTripDetailsHandler`
- Validators en el mismo archivo que el Command o en clase separada `<Command>Validator`

**Reglas de layering:**
- Handlers inyectan solo interfaces: `ITripRepository`, `ICityRepository`, `ITripOptimizerService`, `ICatalogEnrichmentService`, `ILogger<T>`, `IMapper`
- Sin referencias a `DbContext`, `IQueryable<T>` ni tipos de EF

---

### 3.3 `SmartTripPlanner.Infrastructure` (Class Library)

Implementaciones concretas. Los tipos de EF quedan confinados aquí.

**NuGet:** `Google.OrTools`, `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.InMemory`, `Serilog 4.x`

**Estructura:**

```
Infrastructure/
├── EfModel/
│   └── TripDbContext.cs
├── Repositories/
│   ├── TripRepository.cs           # implementa ITripRepository
│   └── CityRepository.cs           # implementa ICityRepository — MVP: datos hardcoded ciudad piloto (Madrid)
├── Optimization/
│   └── GoogleOrToolsOptimizer.cs   # implementa ITripOptimizerService
│                                   # MVP: mock estructurado con datos de Madrid, sin solver real
├── Services/
│   └── LlmCatalogEnricher.cs       # implementa ICatalogEnrichmentService (async mock)
└── IServiceCollectionExtension.cs
```

---

### 3.4 `SmartTripPlanner.API` (ASP.NET Core Web API)

Entry point. Controllers thin — sin lógica de negocio.

**NuGet:** `Swashbuckle.AspNetCore 6.8.x`, `AutoMapper 13.x`, `Serilog.AspNetCore 4.x`

> NuGet opcionales (fuera de scope MVP, necesarios al activar Key Vault): `Azure.Extensions.AspNetCore.Configuration.Secrets`, `Azure.Identity`

**Estructura:**

```
API/
├── Program.cs                          # WebApplication minimal hosting
├── AppConfig.cs                        # Root settings POCO
├── ConfigurationHelper.cs              # appsettings + env + KeyVault loader
├── appsettings.json
├── appsettings.Development.json
├── appsettings.Devlocal.json           # InMemory, sin dependencias reales
├── Controllers/
│   └── TripsController.cs              # [ApiController] [Route("api/trips")] — inyecta IMediator
├── Configurations/
│   └── AutoMapperProfile.cs
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs  # Excepciones → JSON estructurado (400 / 404 / 422)
└── IServiceCollectionExtension.cs
```

**Regla de controllers (metodología org):**

```
[HttpPost] → build command → await _mediator.Send(command, ct) → return Ok(response)
```

Sin business logic, sin acceso directo a repositorios.

---

### 3.5 `SmartTripPlanner.Tests`

**NuGet:** `MSTest.TestFramework`, `MSTest.TestAdapter`, `Moq`

**Estructura:**

```
Tests/
├── BaseTestClass.cs
├── ServiceCollectionsFake.cs
└── Handlers/
    ├── GenerateTripHandlerTests.cs
    ├── CompletePlaceHandlerTests.cs
    ├── ReplanTripHandlerTests.cs
    ├── GetTripDetailsHandlerTests.cs
    └── ListTripsHandlerTests.cs
```

**Convenciones:**
- Un `[TestClass]` por handler
- Patrón AAA
- Naming de métodos: `Handle_<Scenario>_<Expected>`

---

## 4. Arranque y Registro de Dependencias

`Program.cs` usa el modelo minimal hosting (`WebApplication`). Orden de registro (Domain → Infrastructure → ApplicationServices):

```csharp
builder.Services.AddDomain();                                   // sin IConfiguration — Domain no tiene settings
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationServices(builder.Configuration);
```

MediatR se registra en `AddApplicationServices` escaneando los tres assemblies relevantes y añadiendo behaviors en orden de ejecución (`LoggingBehavior` → `ValidationBehavior`):

```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<IServiceCollectionExtension>();
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

// FluentValidation — escanea validators del assembly de ApplicationServices
services.AddValidatorsFromAssemblyContaining<IServiceCollectionExtension>();
```

**Lifetimes:**
- `Scoped` — Handlers, Repositories, DbContext
- `Singleton` — IOptions, IMapper, IHttpClientFactory clients
- `Transient` — Validators (FluentValidation, registrado por `AddValidatorsFromAssembly`)

> **Nota (C-4):** `ExceptionHandlingMiddleware` se registra mediante `app.UseMiddleware<ExceptionHandlingMiddleware>()` en `Program.cs`, no mediante `IServiceCollection`. El middleware pipeline de ASP.NET Core gestiona su ciclo de vida de forma independiente al contenedor DI; no debe aparecer en la tabla de lifetimes.

---

## 5. Contratos HTTP

> **Nota de versión:** El fichero `endpoints.yaml` de referencia está marcado como `v2.0.0` y usa el prefijo de servidor `/v2`. Esta SAD describe la arquitectura interna y las rutas lógicas sin prefijo de versión de URL; la versión del contrato se gestiona a nivel de gateway/proxy.

| # | Verbo | Ruta | MediatR Command | OK | Error |
|---|-------|------|-----------------|----|-------|
| 1 | GET | `/api/trips` | `ListTrips` | 200 `TripSummaryResponse[]` | 400 |
| 2 | POST | `/api/trips` | `GenerateTrip` | 200 `TripPlanResponse` | 400, 422 |
| 3 | GET | `/api/trips/{tripId}` | `GetTripDetails` | 200 `TripPlanResponse` | 404 |
| 4 | PATCH | `/api/trips/{tripId}/places/{placeId}/complete` | `CompletePlace` | 204 (sin body) | 404 |
| 5 | POST | `/api/trips/{tripId}/replan` | `ReplanTrip` | 200 `TripPlanResponse` | 404 |

> **Nota `TripPlanResponse`:** el contrato actual expone `tripId`, `cityId` y `days[]`. Los campos `startDate`, `endDate` y `baseHotel` del agregado `Trip` no se incluyen en la respuesta del MVP. Se recomienda añadirlos en v3 del contrato para evitar que el cliente deba reconstruirlos desde los datos del día.

---

## 6. Reglas de Validación (FluentValidation)

Validators co-localizados con su Command en `ApplicationServices/Commands/`.

| Command | Validator | Observaciones |
|---------|-----------|---------------|
| `GenerateTrip` | `GenerateTripValidator` | Reglas completas — ver abajo |
| `ReplanTrip` | `ReplanTripValidator` | `CurrentDateTime` ≤ now + 24h; `CurrentLocation` required |
| `CompletePlace` | — | Sin validator; validación implícita por model binding (tipos primitivos) |
| `GetTripDetails` | — | Sin validator; `TripId` validado por route constraint `{tripId:guid}` |
| `ListTrips` | — | Sin validator; parámetros opcionales sin restricciones de negocio |

> El `ValidationBehavior<,>` no lanza excepción si no existe validator para el tipo — FluentValidation devuelve `ValidationResult` vacío y el behavior lo trata como válido.

**Obligatorias en `GenerateTripValidator`:**
- `StartDate` ≥ fecha actual; `EndDate` ≥ `StartDate`
- `defaultStartHour` cumple regex `^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$`
- `mustSees` contiene al menos un elemento

**Obligatorias en `ReplanTripValidator`:**
- `CurrentDateTime` no puede ser superior a `DateTime.UtcNow + 24h` (evita replans con fechas futuras)
- `CurrentLocation.Latitude` en `[-90, 90]`; `CurrentLocation.Longitude` en `[-180, 180]`

---

## 7. Tripwires

- Sin business logic en Controllers — solo `_mediator.Send(command, ct)`
- Sin tipos EF (`DbContext`, `IQueryable<T>`, `DbSet<T>`) fuera de Infrastructure
- Sin `Thread.Sleep`, `.Result`, `.Wait()`, `async void`
- Sin `new HttpClient()` — siempre `IHttpClientFactory`
- Sin `IConfiguration["..."]` en business code — solo `IOptions<T>`
- Sin `Console.WriteLine` fuera de `Program.cs`
- Sin string interpolation en log messages — usar named placeholders
- No hacer `await` sobre `ICatalogEnrichmentService` en el request path — siempre fire-and-forget con `CancellationToken.None`
