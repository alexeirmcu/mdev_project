# Proposal: Abstract Place External Service

## Intent

Eliminar el acoplamiento directo de `PlaceRepository` a `IFoursquareApiClient` mediante un Puerto/Adapter (`IPlaceExternalService`), permitiendo cambiar de proveedor de lugares (Foursquare, Google Places, etc.) sin modificar el core de la aplicación.

## Scope

### In Scope
- Crear `IPlaceExternalService` en Domain (`SmartTripPlanner.Domain/Repository/`)
- Crear `FoursquarePlaceService` como adapter en Infrastructure
- Simplificar `PlaceRepository` para que dependa de `IPlaceExternalService` en vez de `IFoursquareApiClient`
- Actualizar `InfrastructureServiceRegistration.cs` — DI wire del nuevo adapter
- Tests del adapter con mocks
- Todo compila y tests existentes pasan

### Out of Scope
- No se crea un segundo proveedor (Google Places, TomTom) — solo se desacopla Foursquare
- No se mueve la cascade (DB → API) fuera de `PlaceRepository`
- No cambios en la API layer, Application Services, ni UI

## Capabilities

### New Capabilities
None — pure refactor, no new user-facing behavior.

### Modified Capabilities
- `place`: `PlaceRepository` cambia su dependencia interna de `IFoursquareApiClient` a `IPlaceExternalService`. Los modelos Foursquare pasan a ser `internal` dentro del adapter.

## Approach

Port & Adapter (Hexagonal Architecture):
1. `IPlaceExternalService` en Domain: `Task<List<Place>> SearchPlacesAsync(string query, string cityId, int maxResults = 20)` — devuelve entidad `Place` directamente (sin DTO intermedio).
2. `FoursquarePlaceService` en Infrastructure: implementa `IPlaceExternalService`, encapsula `IFoursquareApiClient`, mapeo a `Place`, y `FoursquareCategoryHeuristics` como lógica interna.
3. `PlaceRepository` inyecta `IPlaceExternalService` en vez de `IFoursquareApiClient`. La cascade DB → API se mantiene intacta.
4. DI: `IFoursquareApiClient` + `FoursquareApiClient` se registran via `AddHttpClient` como ahora, pero el nuevo adapter se registra como `IPlaceExternalService`. `PlaceRepository` ya no recibe `IFoursquareApiClient`.
5. Tests: mock de `IPlaceExternalService` para aislar `PlaceRepository`. Tests del adapter con mock de `IFoursquareApiClient`.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `SmartTripPlanner.Domain/Repository/IPlaceExternalService.cs` | **New** | Puerto para búsqueda externa de lugares |
| `SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/FoursquarePlaceService.cs` | **New** | Adapter que implementa el puerto |
| `SmartTripPlanner.Infrastructure/Repositories/PlaceRepository.cs` | **Modified** | Reemplazar `IFoursquareApiClient` por `IPlaceExternalService` |
| `SmartTripPlanner.Infrastructure/InfrastructureServiceRegistration.cs` | **Modified** | Registrar `IPlaceExternalService` → `FoursquarePlaceService` |
| `SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/Models/FoursquarePlace.cs` | **Modified** | Cambiar a `internal` |
| `SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/Mapping/FoursquareCategoryHeuristics.cs` | **Modified** | Cambiar a `internal` |
| Tests existentes (PlaceRepository + Foursquare) | **Modified** | Adaptar a nueva abstracción |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Romper cascade DB → API | Low | Tests existentes cubren el flujo completo; solo se cambia la dependencia |
| Fugas de modelos Foursquare a otras capas | Low | Se marcan como `internal` |
| Tests existentes requieren refactor significativo | Medium | Se actualizan para usar mock de `IPlaceExternalService` |

## Rollback Plan

Revertir commits del cambio. Los archivos nuevos (`IPlaceExternalService.cs`, `FoursquarePlaceService.cs`) se eliminan. PlaceRepository vuelve a su estado anterior.

## Dependencies

Ninguna. Todo el cambio es intra-solución.

## Success Criteria

- [ ] `PlaceRepository.SearchAsync` sin resultados locales llama a `IPlaceExternalService` y retorna Places mapeados
- [ ] `PlaceRepository` no referencia ningún tipo de Foursquare directamente
- [ ] Todos los tests existentes pasan sin modificar su lógica de negocio
- [ ] Nuevos tests del adapter pasan con mock de `IFoursquareApiClient`
