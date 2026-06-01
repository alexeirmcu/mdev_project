# Documento de Arquitectura de Software (SAD) — Basecamp Trip Planner API

## 1. Resumen Ejecutivo y Metas de Diseño
Este documento define la estructura y las directrices técnicas definitivas para la generación automática del esqueleto de la API REST del proyecto *Basecamp Family Trip Planner (Europe) v2*. 

### Especificaciones Técnicas Base
* **Plataforma Objetivo:** .NET 8.0 SDK / C# 12
* **Patrón Arquitectónico:** Clean Architecture (Arquitectura Cebolla) + CQRS (Command Query Responsibility Segregation).
* **Manejo de Rutas:** Minimal APIs de ASP.NET Core (sin controladores tradicionales).
* **Enfoque de Datos:** Patrón Repositorio abstrayendo Entity Framework Core (proveedor InMemory para el MVP).
* **Objetivo de la IA:** Generar la solución (`.sln`), los tres proyectos físicos (`.csproj`), el árbol jerárquico de carpetas, los enumeradores, las entidades de dominio anémicas, los DTOs (como records inmutables), las reglas de validación y las clases manejadoras de MediatR (Handlers) con respuestas simuladas (*mocks*).

---

## 2. Estructura de la Solución .NET (Clean Architecture)

La solución debe llamarse `BasecampTripPlanner.sln` y estructurarse obligatoriamente en tres proyectos independientes para asegurar el desacoplamiento estricto del núcleo de negocio:

BasecampTripPlanner/
├── src/
│   ├── BasecampTripPlanner.Core/             (Domain & Application)
│   ├── BasecampTripPlanner.Infrastructure/   (Persistence & External Services)
│   └── BasecampTripPlanner.API/              (Presentation - Web REST)
└── tests/
└── BasecampTripPlanner.UnitTests/        (Pruebas Unitarias)

### 2.1. Proyecto: `BasecampTripPlanner.Core` (Clase de Librería)
Contiene las reglas de negocio esenciales (entidades) y la lógica de la aplicación (casos de uso). No tiene dependencias de frameworks web ni de bases de datos.
* **Dependencias NuGet:** `MediatR`, `FluentValidation.DependencyInjectionExtensions`.
* **Estructura de Carpetas e Hilos de Código:**
  * `Domain/Enums/`:
    * `Priority.cs` (`HIGH`, `MEDIUM`, `LOW`).
    * `TransportMode.cs` (`WALK_AND_PUBLIC_TRANSPORT`, `CAR`).
    * `WeatherCondition.cs` (`GOOD`, `BAD`).
    * `BlockType.cs` (`MORNING`, `AFTERNOON`, `EVENING`).
  * `Domain/Entities/`:
    * `Trip.cs` (Contiene `TripId` [Guid], `CityId` [string], `StartDate` [DateTime], `EndDate` [DateTime], `BaseHotel` [Location], y la lista de `DayPlan`).
    * `DayPlan.cs` (Contiene `DayIndex` [int], `Date` [DateTime], `WeatherSummary` [WeatherCondition] y los tres bloques: `Morning`, `Afternoon`, `Evening`).
    * `BlockTimeline.cs` (Contiene `BlockTotalDurationMinutes` [int] y una lista ordenada de `ActivityNode`).
    * `ActivityNode.cs` (Contiene `SequenceOrder` [int], `PlaceId` [string], `Name` [string], `IsCompleted` [bool], `EstimatedArrival` [string/int], `EstimatedDeparture` [string/int], `DurationMinutes` [int], `IsIndoor` [bool] y el objeto de tránsito `TransitToNext`).
    * `TransitDetails.cs` (Contiene `TransportMode` [Enum], `DurationMinutes` [int], `BufferMinutes` [int], `FrictionAlert` [bool]).
    * `Location.cs` (Value Object o registro con `Name`, `Latitude`, `Longitude`).
  * `Application/Interfaces/`:
    * `ITripRepository.cs` (Definir firmas: `GetByIdAsync`, `ListAsync`, `AddAsync`, `UpdateAsync`).
    * `ITripOptimizerService.cs` (Definir firma: `OptimizeRouteAsync(...)` para interactuar con OR-Tools).
    * `ICatalogEnrichmentService.cs` (Definir firma para el procesamiento asíncrono con el LLM).
  * `Application/DTOs/`: Records inmutables para el mapeo estricto con los contratos JSON de OpenAPI (`TripSummaryDto`, `TripPlanDto`, `LocationDto`, etc.).
  * `Application/Features/Trips/Commands/`:
    * `GenerateTrip/` (`GenerateTripCommand.cs`, `GenerateTripCommandHandler.cs`, `GenerateTripValidator.cs`).
    * `CompletePlace/` (`CompletePlaceCommand.cs`, `CompletePlaceCommandHandler.cs`, `CompletePlaceValidator.cs`).
    * `ReplanTrip/` (`ReplanTripCommand.cs`, `ReplanTripCommandHandler.cs`, `ReplanTripValidator.cs`).
  * `Application/Features/Trips/Queries/`:
    * `GetTripDetails/` (`GetTripDetailsQuery.cs`, `GetTripDetailsQueryHandler.cs`).
    * `ListTrips/` (`ListTripsQuery.cs`, `ListTripsQueryHandler.cs`).

### 2.2. Proyecto: `BasecampTripPlanner.Infrastructure` (Clase de Librería)
Implementa los contratos de infraestructura definidos en el Core.
* **Dependencias NuGet:** `Google.OrTools`, `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.InMemory`.
* **Estructura de Carpetas:**
  * `Persistence/`: Contexto de Entity Framework (`TripDbContext.cs`) e implementación concreta del repositorio `TripRepository.cs`.
  * `Optimization/`: Clase `GoogleOrToolsOptimizer.cs` que implementa `ITripOptimizerService.cs`. **Directriz para la IA:** No codificar el solver matemático lineal real de OR-Tools; únicamente declarar la clase, inyectar las dependencias necesarias y retornar un itinerario estructurado de imitación (*mock*) que cumpla con la firma del contrato.
  * `Services/`: Clase `LlmCatalogEnricher.cs` que implementa `ICatalogEnrichmentService.cs` simulando las llamadas asíncronas de enriquecimiento de lugares turísticos.

### 2.3. Proyecto: `BasecampTripPlanner.API` (Web API de ASP.NET Core)
Punto de entrada de la aplicación y capa de transporte HTTP.
* **Dependencias NuGet:** `Swashbuckle.AspNetCore`, `Microsoft.AspNetCore.OpenApi`.
* **Estructura de Carpetas:**
  * `Endpoints/`: Clase estática `TripEndpoints.cs` que expone los endpoints mediante Minimal APIs. Debe inyectar `IMediator` y mapear directamente los verbos a las queries/comandos correspondientes.
  * `Middleware/`: `ExceptionHandlingMiddleware.cs` encargado de interceptar excepciones globales de validación o fallos del negocio y serializar respuestas estructuradas en formato JSON (HTTP 400 y 422).

---

## 3. Contratos de Enrutamiento y Mapeo HTTP (Minimal APIs)

La capa de presentación en `TripEndpoints.cs` debe mapear estrictamente los siguientes contratos con sus respectivos comandos y queries de MediatR:

1.  **Listar Viajes:** `GET /api/trips`
    * *Query Parameters:* `cityId` (string, opcional), `startDate` (string/date, opcional), `endDate` (string/date, opcional).
    * *Mapeo:* Envía `ListTripsQuery`. Retorna `200 OK` con un array de `TripSummaryResponse`.
2.  **Generar Viaje:** `POST /api/trips`
    * *Request Body:* JSON equivalente a `TripGenerationRequest`.
    * *Mapeo:* Envía `GenerateTripCommand`. Retorna `200 OK` con el `TripPlanResponse` detallado completo tras pasar por el mock del optimizador. Retorna `422 Unprocessable Entity` si el modelo de negocio es inválido o inviable.
3.  **Detalle del Viaje:** `GET /api/trips/{tripId}`
    * *Path Parameter:* `tripId` (Guid).
    * *Mapeo:* Envía `GetTripDetailsQuery`. Retorna `200 OK` con el `TripPlanResponse` completo del viaje guardado, o `404 Not Found`.
4.  **Marcar Destino Completado:** `PATCH /api/trips/{tripId}/places/{placeId}/complete`
    * *Path Parameters:* `tripId` (Guid), `placeId` (string).
    * *Request Body:* `{"isCompleted": true}`.
    * *Mapeo:* Envía `CompletePlaceCommand`. Cambia de forma atómica el estado de la actividad en la persistencia. Retorna `200 OK`.
5.  **Replanificar sobre la Marcha:** `POST /api/trips/{tripId}/replan`
    * *Path Parameter:* `tripId` (Guid).
    * *Request Body:* JSON equivalente a `TripReplanRequest` (`currentDateTime`, `currentLocation`, `currentBlockWeather`).
    * *Mapeo:* Envía `ReplanTripCommand`. El Handler recupera el viaje de la base de datos, extrae los ítems pendientes e invoca el mock del motor de optimización bajo las nuevas condiciones (ej. clima `BAD` forzando descarte de atracciones al aire libre). Retorna `200 OK` con el itinerario modificado.

---

## 4. Convenciones de Código y Reglas de Validación para la IA

* **Inmutabilidad en Contratos:** Todos los DTOs de entrada/salida y las peticiones de MediatR deben ser declarados usando estructuras `public record` posicionales en lugar de clases tradicionales.
* **Manejo de Tiempos Internos:** Las horas de llegada y salida estimadas enviadas en las respuestas REST pueden formatearse como cadenas (`"09:30"`). Sin embargo, internamente el dominio debe estar listo para manejar aritmética (número entero que representa los minutos transcurridos desde las 00:00, p. ej. 9:30 AM = $570$).
* **Reglas de FluentValidation a generar de forma obligatoria:**
    * Validar que `StartDate` sea igual o mayor a la fecha actual y que `EndDate` sea igual o mayor a `StartDate`.
    * Validar que el formato de `defaultStartHour` cumpla con la expresión regular de formato de 24 horas: `^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$`.
    * Validar que la lista de `mustSees` contenga al menos un elemento al generar un viaje.


