# Especificación Técnica — Flujo 1: Descubrimiento e Ingesta de Lugares (Must-Sees)

## 1. Resumen del Flujo

Este flujo regula el proceso en el cual un usuario busca y descubre lugares de interés (*Must-sees*) en la ciudad piloto de **Madrid**. El sistema combina una base de datos local curada con la API externa de Foursquare mediante un **Pipeline de Búsqueda en Cascada** para garantizar rendimiento, control de costes y experiencia de usuario transparente.

**Responsabilidad del Flujo:** Proporcionar resultados de búsqueda de lugares enriquecidos con metadatos de negocio (duración típica, indoor/outdoor, aptitud familiar) que el usuario pueda seleccionar posteriormente como *Must-Sees* para un viaje.

**Alcance:**
- Búsqueda de lugares por texto y ciudad.
- Persistencia de resultados externos en BD local para futuras búsquedas.
- Inyección de datos semánticos mediante mapeo de emergencia por categoría.

**No Alcance:**
- Creación del viaje (Flow 0).
- Optimización de itinerario (Flow 2).
- Enriquecimiento asíncrono con LLM (Flow 3).

---

## 2. Modelo de Dominio: Entidad `Place`

La entidad `Place` es un **Aggregate Root** en `SmartTripPlanner.Domain`. Todos los datos expuestos al exterior (API, handlers) deben provenir de esta entidad, nunca directamente de Foursquare.

### 2.1 Estructura de `Place`

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `Id` | `long` | **Clave primaria interna** (auto-incremental, generada por PostgreSQL). No se expone directamente a la API. |
| `ProviderReferenceId` | `string` | Identificador externo del proveedor. Para Foursquare, almacena el `fsq_id` original. |
| `Provider` | `Provider` | Enum del proveedor de origen (`Foursquare`). |
| `Name` | `string` | Nombre comercial del punto de interés (ej. *"Museo del Prado"*). |
| `CityId` | `long` | **Clave foránea interna** a `City.Id`. |
| `City` | `City?` | Navegación a la ciudad. |
| `Location` | `PlaceLocation` | Value Object con `Latitude` (`double`, rango `[-90, 90]`) y `Longitude` (`double`, rango `[-180, 180]`). |
| `TypicalDurationMinutes` | `int` | Duración estimada de visita para una familia. Default: `60`. |
| `IsIndoor` | `bool` | `true` si es techado/cerrado. Default: `false`. |
| `IsFamilyFriendly` | `bool` | `true` si es apto para niños. Default: `true`. |
| `IsAutoUpdateEnabled` | `bool` | `true` si permite actualización desde el proveedor externo. Default: `true`. |
| `OpeningHours` | `List<OpeningHoursWindow>` | Ventanas de apertura por día de semana (`DayOfWeek`, `OpenMinutes`, `CloseMinutes`). |
| `Attributes` | `List<PlaceAttribute>` | Metadatos clave-valor del proveedor (ej. categorías, cadenas). |

### 2.2 Identidad y Referencias

- **Internamente**, el sistema usa `Place.Id` (`long`) para todas las relaciones del dominio (`MustSee.PlaceId`, `ActivityNode.PlaceId`).
- **Externamente**, la API usa `ProviderReferenceId` (`string`) para identificar lugares de forma agnóstica al proveedor.
- **Razón:** Desacoplar el dominio de los IDs volátiles de terceros. Si Foursquare cambia un `fsq_id`, la relación interna (`long`) sigue intacta.

---

## 3. API REST: Endpoint de Búsqueda

### 3.1 Contrato

```http
POST /api/trips/places/search
Content-Type: application/json
```

#### Request Body: `PlaceSearchRequest`

```json
{
  "query": "Museo del Prado",
  "cityCode": "madrid-es",
  "maxResults": 10
}
```

| Campo | Tipo | Requerido | Descripción | Reglas |
|-------|------|-----------|-------------|--------|
| `Query` | `string?` | Sí | Texto de búsqueda. | `NotEmpty`, `MinLength(3)` |
| `CityCode` | `string` | Sí | Código de la ciudad. | `NotEmpty`, debe existir en BD y `IsAllowed == true` |
| `MaxResults` | `int?` | No | Límite de resultados. | Si se envía, `1 <= MaxResults <= 10` (configurable por `PlaceSearchOptions.MaxResults`) |

#### Response Body: `SearchPlacesResponse`

```json
{
  "results": [
    {
      "providerReferenceId": "fsq-prado-123",
      "name": "Museo del Prado",
      "cityId": 1,
      "location": {
        "latitude": 40.4168,
        "longitude": -3.7038
      },
      "typicalDurationMinutes": 120,
      "isIndoor": true,
      "isFamilyFriendly": false,
      "isAutoUpdateEnabled": true,
      "openingHours": [
        {
          "dayOfWeek": "Monday",
          "openMinutes": 600,
          "closeMinutes": 1200
        }
      ],
      "attributes": [
        {
          "provider": "foursquare",
          "key": "category",
          "value": "Museum"
        }
      ]
    }
  ]
}
```

#### Códigos de Error (HTTP 422)

Todos los errores de validación y lógica de negocio se unifican en **HTTP 422** con una lista de `ValidationResult`:

| ErrorCode | Escenario | HTTP |
|-----------|-----------|------|
| `REQUIRED_FIELD` | Query o CityCode vacíos. | 422 |
| `MIN_LENGTH_VIOLATION` | Query con menos de 3 caracteres. | 422 |
| `INVALID_CITY` | CityCode no existe o no está permitida. | 422 |
| `MAX_RESULTS_EXCEEDED` | MaxResults supera el límite configurado. | 422 |
| `EXTERNAL_SERVICE_FAILURE` | Foursquare no responde o error de red. | 422 |

### 3.2 Implementación Actual

- **Controller:** `PlacesController` (`SmartTripPlanner.API`)
- **Handler:** `SearchPlacesHandler` (`SmartTripPlanner.ApplicationServices`)
- **Validator:** `SearchPlacesRequestValidator` (FluentValidation)
- **Mapper:** `AutoMapperProfile` (`Place` → `PlaceModel`)

---

## 4. Pipeline de Búsqueda en Cascada

### 4.1 Diagrama del Flujo

```
[Usuario envía POST /api/trips/places/search]
│
▼
┌─────────────────────────────────────────┐
│ Validación (FluentValidation)           │
│ • Query no vacío, >= 3 chars            │
│ • CityCode existe y está habilitada     │
│ • MaxResults dentro del límite          │
└─────────────────────────────────────────┘
│
▼
┌─────────────────────────────────────────┐
│ Paso A: SearchAsync en BD Local         │
│ (EF Core + Like en Name y Attributes)   │
└─────────────────────────────────────────┘
│
├─ ¿Resultados > 0? ── SÍ ──> [Mapper] ──> [HTTP 200]
│
└─ NO
│
▼
┌─────────────────────────────────────────┐
│ Paso B: IPlaceExternalService           │
│ (FoursquarePlaceService → API v3)       │
└─────────────────────────────────────────┘
│
▼
┌─────────────────────────────────────────┐
│ Paso C: UpsertRangeAsync + SaveChanges  │
│ (Persistir en BD local para futuro)     │
└─────────────────────────────────────────┘
│
▼
┌─────────────────────────────────────────┐
│ [Mapper] ──> [HTTP 200 con resultados]  │
└─────────────────────────────────────────┘
```

### 4.2 Paso A: Búsqueda Local

- **Responsable:** `PlaceRepository.SearchAsync(query, cityCode, maxResults)`
- **Query EF Core:**
  ```csharp
  _context.Places
      .Include(p => p.OpeningHours)
      .Include(p => p.Attributes)
      .Include(p => p.City)
      .Where(p => p.City.CityCode == cityCode
          && (EF.Functions.Like(p.Name, $"%{query}%")
              || p.Attributes.Any(a => EF.Functions.Like(a.Value, $"%{query}%"))))
      .Take(maxResults)
      .ToListAsync();
  ```
- **Criterio de corte:** Si `places.Count > 0`, se retorna inmediatamente sin llamar a Foursquare.
- **Razón:** Rendimiento y reducción de costes de API externa.

### 4.3 Paso B: Búsqueda Externa (Foursquare)

- **Responsable:** `FoursquarePlaceService` (implementa `IPlaceExternalService`)
- **Trigger:** Solo si Paso A devuelve vacío.
- **Proceso:**
  1. `SearchPlacesHandler` consulta `ICityRepository.GetByCodeAsync(cityCode)` para obtener el `City.Id` (`long`) interno.
  2. Llama a `IPlaceExternalService.SearchPlacesAsync(query, cityCode, city.Id, maxResults)`.
  3. `FoursquarePlaceService` invoca `IFoursquareApiClient` (HTTP client).
  4. Si Foursquare devuelve error de red, se captura `HttpRequestException` y retorna lista vacía (graceful degradation).

### 4.4 Paso C: Persistencia (Upsert)

- **Responsable:** `PlaceRepository.UpsertRangeAsync(places)`
- **Lógica por cada lugar:**
  1. Busca existente por `ProviderReferenceId`.
  2. Si existe y `IsAutoUpdateEnabled == true`: actualiza campos via `UpdateFromExternalProvider()`.
  3. Si existe y `IsAutoUpdateEnabled == false`: log warning, salta.
  4. Si no existe: `AddAsync()`.
- **Commit:** `SaveChangesAsync()` al final del handler.
- **Razón:** Los resultados de Foursquare se cachean localmente para futuras búsquedas y para que el `Place.Id` (`long`) esté disponible para relaciones del dominio (`MustSee`, `ActivityNode`).

---

## 5. Mapeo de Emergencia (Emergency Mapping)

Cuando un lugar proviene de Foursquare, la respuesta cruda carece de metadatos semánticos. La capa `Infrastructure` inyecta los siguientes valores mediante heurísticas basadas en el `FsqCategoryId` de la primera categoría del lugar.

### 5.1 Inyección de `TypicalDurationMinutes`

| FsqCategoryId | Categoría | Duración |
|---------------|-----------|----------|
| `10000` | Arts & Entertainment | `120` min |
| `10035` | Art Gallery | `120` min |
| `10014` | Theme Park | `120` min |
| `10024` | Historic Site | `60` min |
| `10025` | Monument | `60` min |
| `10033` | Plaza | `60` min |
| `10040` | Park | `60` min |
| `13003` | Restaurant | `90` min |
| `13002` | Café | `90` min |
| `13004` | Food Court | `90` min |
| `10008` | Nightclub | `60` min |
| `10009` | Strip Club | `60` min |
| `10010` | Adult Entertainment | `60` min |
| *(default)* | Cualquier otra | `60` min |

### 5.2 Inyección de `IsIndoor`

| FsqCategoryId | `IsIndoor` |
|---------------|------------|
| `10000`, `10035`, `10014`, `10024`, `10025`, `10033`, `13003`, `13002`, `13004` | `true` |
| `10040` (Park) | `false` |
| `10008`, `10009`, `10010` | `true` |
| *(default)* | `true` |

### 5.3 Inyección de `IsFamilyFriendly`

| FsqCategoryId | `IsFamilyFriendly` |
|---------------|-------------------|
| `10008` (Nightclub), `10009` (Strip Club), `10010` (Adult Entertainment) | `false` |
| *(default)* | `true` |

### 5.4 Implementación

- **Archivo:** `FoursquareCategoryHeuristics.cs`
- **Método:** `Map(IEnumerable<FoursquareCategory>)` → `(int, bool, bool)`
- **Nota:** Las heurísticas usan IDs numéricos de categoría (`FsqCategoryId`) en lugar de nombres de texto para evitar falsos positivos por traducción o renombramiento.

---

## 6. Criterios de Aceptación Técnicos

1. **Transparencia Absoluta (UX):** El usuario no debe percibir si el resultado provino de la BD local o de Foursquare. Ambas respuestas lucen idénticas en la API.
2. **Preservación de Identidad Interna:** El sistema usa `Place.Id` (`long`) para todas las relaciones internas (`MustSee.PlaceId`, `ActivityNode.PlaceId`). El `ProviderReferenceId` (`string`) se usa solo para comunicación externa y upsert.
3. **Aislamiento de Infraestructura:** Ningún componente fuera de `SmartTripPlanner.Infrastructure` conoce los contratos de Foursquare. Todo sale como `Place` de dominio.
4. **Graceful Degradation:** Si Foursquare falla, el sistema retorna `[]` (HTTP 200) en lugar de propagar el error. Solo errores de red inesperados en el controller se mapean a `EXTERNAL_SERVICE_FAILURE` (422).
5. **Idempotencia del Upsert:** Llamar dos veces con el mismo `ProviderReferenceId` no crea duplicados. Si `IsAutoUpdateEnabled == false`, el registro local se preserva intacto.

---

## 7. Decisiones de Diseño y Trade-offs

### 7.1 ¿Por qué `POST` en lugar de `GET`?

- **Razón:** El body `PlaceSearchRequest` encapsula mejor los parámetros, permite extensión futura (filtros, coordenadas, preferencias) sin ensuciar la URL, y es consistente con el resto de la API (que usa `POST` para comandos de creación y búsqueda compleja).

### 7.2 ¿Por qué `ProviderReferenceId` en la API en lugar de `Id`?

- **Razón:** El `Id` interno (`long`) es volátil entre entornos (dev, staging, prod). El `ProviderReferenceId` (`fsq_id`) es estable y universal. El frontend no necesita conocer el esquema interno de BD.
- **Consecuencia:** Cuando el frontend selecciona un Must-See, envía el `ProviderReferenceId`. El backend (Flow 0) lo resuelve a `Place.Id` interno antes de persistir en `Trip.OriginalMustSees`.

### 7.3 ¿Por qué persistir resultados de Foursquare inmediatamente?

- **Razón:** Dos objetivos:
  1. **Cache:** Futuras búsquedas del mismo lugar evitan llamadas a Foursquare.
  2. **Identidad Interna:** El `Place` necesita un `Id` (`long`) para ser referenciado por `MustSee` y `ActivityNode`. Ese `Id` solo existe después de insertar en PostgreSQL.

### 7.4 ¿Por qué `Upsert` en lugar de `Insert` puro?

- **Razón:** Un lugar puede ser encontrado por diferentes queries en distintas sesiones. El `Upsert` garantiza que no hay duplicados por `ProviderReferenceId` y permite refrescar datos si `IsAutoUpdateEnabled` está activo.

---

## 8. Gaps Identificados (Pendientes)

### 8.1 `OpeningHours` en Mapeo de Emergencia

**Estado: RESUELTO** — `FoursquarePlaceService.MapToPlace()` inyecta horarios por defecto 09:00–18:00 para compatibilidad con el solver.

**Implementación actual en `FoursquarePlaceService.cs` (líneas 54-56):**
```csharp
// Inject default opening hours for solver compatibility
foreach (var day in Enum.GetValues<DayOfWeek>())
    place.OpeningHours.Add(new OpeningHoursWindow(day, 540, 1080)); // 09:00-18:00
```

**Impacto resuelto:** Flow 2 puede leer `OpeningHours` de cualquier `Place` proveniente de Foursquare y aplicar restricciones de horarios sin fallos.

### 8.2 PlaceModel no expone `Id` interno

**Estado:** `PlaceModel` solo tiene `ProviderReferenceId`. No expone `Place.Id` (`long`).

**Impacto:** Si el frontend necesita referenciar el lugar internamente de forma eficiente (sin pasar por `ProviderReferenceId`), no tiene el `Id`. Sin embargo, esto es intencional por desacoplamiento.

**Decisión:** Mantener como está. El `Id` interno es un detalle de implementación. El `ProviderReferenceId` es el contrato público.

---

## 9. Tests de Cobertura

| Suite | Clase | Cobertura |
|-------|-------|-----------|
| Handler | `SearchPlacesHandlerTests` | 10 casos: local, cascada, externo vacío, externo error, default maxResults, atributos, etc. |
| Controller | `PlacesControllerTests` | 2 casos: 200 OK, 422 EXTERNAL_SERVICE_FAILURE |
| Validator | `SearchPlacesRequestValidatorTests` | 8 casos: query, cityCode, maxResults, combinaciones |
| Mapping | `PlaceMappingProfileTests` | 5 casos: Place → PlaceModel, campos, location, openingHours, attributes |
| Infraestructura | `FoursquarePlaceServiceTests` | Mapeo y heurísticas |
| Infraestructura | `FoursquareCategoryHeuristicsTests` | Validación de categorías |
| Infraestructura | `PlaceRepositoryTests` | Search, Upsert, GetByProviderReferenceId |

**Total de tests del proyecto:** +300 pasando, 0 fallos.

---

## 10. Referencias de Código

- `SmartTripPlanner.Domain/AggregatesModel/Place.cs`
- `SmartTripPlanner.Domain/Repository/IPlaceRepository.cs`
- `SmartTripPlanner.Domain/Ports/IPlaceExternalService.cs`
- `SmartTripPlanner.Domain/ApiModels/PlaceSearchRequest.cs`
- `SmartTripPlanner.ApplicationServices/Handlers/SearchPlacesHandler.cs`
- `SmartTripPlanner.ApplicationServices/Validators/SearchPlacesRequestValidator.cs`
- `SmartTripPlanner.ApplicationServices/Commands/SearchPlacesResponse.cs`
- `SmartTripPlanner.API/Controllers/PlacesController.cs`
- `SmartTripPlanner.API/Configurations/AutoMapperProfile.cs`
- `SmartTripPlanner.Infrastructure/Repositories/PlaceRepository.cs`
- `SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/FoursquarePlaceService.cs`
- `SmartTripPlanner.Infrastructure/ExternalServices/Foursquare/Mapping/FoursquareCategoryHeuristics.cs`

---

*Última actualización: 2026-06-22*
*Versión: 1.1 (refleja estado actual del código, no estado deseado)*
