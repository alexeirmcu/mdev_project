# Requisitos Técnicos — Flujo 1: Descubrimiento de Must-Sees (Búsqueda e Ingesta de Places)

## 1. Resumen del Flujo

Este flujo regula el proceso en el cual un usuario **busca lugares de interés** (*Must-sees*) para una ciudad específica. El sistema combina una base de datos local de alta velocidad con la API externa de Foursquare mediante un **Pipeline de Búsqueda en Cascada**.

**Diferencia clave con el spec anterior:** Este flujo **solo busca y devuelve lugares**. No selecciona must-sees (eso es Flow 0) ni genera el itinerario (eso es Flow 2).

**Relación con Flow 0:**
- Flow 1 devuelve `Place` con su `Id` (long) interno.
- El frontend usa ese `Id` (long) para construir `MustSeeInput` y enviarlo a Flow 0 (`POST /api/trips`).
- Si un lugar de Foursquare no existe en la BD local, Flow 1 lo **persiste** primero para obtener su `Id` interno antes de devolverlo.

---

## 2. Modelado de la Entidad de Dominio: `Place`

**Ubicación:** `SmartTripPlanner.Domain/AggregatesModel/Place.cs`

### Estructura Actual de `Place` (ya implementada)

```
┌─────────────────────────────────────────────────────────────────────────┐
│                            Place (Aggregate Root)                       │
├─────────────────────────────────────────────────────────────────────────┤
│  Id                     : long (PK, autogenerado por EF)               │
│  ProviderReferenceId    : string (ej: "fsq_123456789") — UNIQUE INDEX  │
│  Provider               : enum (Foursquare, Local, etc.)                 │
│  Name                   : string (Nombre del lugar)                     │
│  CityId                 : long (FK → City.Id)                          │
│  Location               : ValueObject (Latitude, Longitude)             │
│  TypicalDurationMinutes : int (default: 60)                            │
│  IsIndoor               : bool (default: false)                        │
│  IsFamilyFriendly       : bool (default: true)                         │
│  IsAutoUpdateEnabled    : bool (default: true) — para enriquecimiento │
│  OpeningHours           : List<OpeningHoursWindow>                     │
│  Attributes             : List<PlaceAttribute>                           │
└─────────────────────────────────────────────────────────────────────────┘
```

### Correcciones respecto al spec anterior

| Campo (spec anterior) | Era (spec anterior) | Es (ahora) | Razón |
|----------------------|-------------------|-----------|-------|
| `Id` | `string` | `long` | PK interna del sistema. `ProviderReferenceId` es el string externo. |
| `CityId` | `string` | `long` | FK interna a `City.Id`. El string es `City.CityCode`. |
| `PlaceId` | `fsq_id` | `long` | `PlaceId` en el dominio es `Place.Id` (long). `fsq_id` es `ProviderReferenceId`. |

### `Place` en el dominio (no se modifica, ya existe)

```csharp
public class Place : Entity, IAggregateRoot
{
    public string ProviderReferenceId { get; private set; }  // Ej: "fsq_123456789"
    public Provider Provider { get; private set; }           // Foursquare, Local, etc.
    public string Name { get; private set; }
    public long CityId { get; private set; }               // FK → City.Id
    public City? City { get; private set; }
    public PlaceLocation Location { get; private set; }
    public int TypicalDurationMinutes { get; private set; } = 60;
    public bool IsIndoor { get; private set; } = false;
    public bool IsFamilyFriendly { get; private set; } = true;
    public bool IsAutoUpdateEnabled { get; private set; } = true;
    public List<OpeningHoursWindow> OpeningHours { get; private set; } = new();
    public List<PlaceAttribute> Attributes { get; private set; } = new();
}
```

---

## 3. API Contract — GET /api/places/search

### 3.1 Request

```
GET /api/places/search?query={texto}&cityCode={city-slug}
```

**Query Parameters:**
| Parámetro | Tipo | Requerido | Descripción |
|-----------|------|-----------|-------------|
| `query` | `string` | Sí | Texto de búsqueda (mínimo 2 caracteres) |
| `cityCode` | `string` | Sí | Slug de la ciudad (ej: `"madrid-es"`) |

**Ejemplo:**
```
GET /api/places/search?query=prado&cityCode=madrid-es
```

### 3.2 Response: `PlaceSearchResponse`

```csharp
public record PlaceSearchResponse(
    IReadOnlyList<PlaceModel> Places,
    int TotalResults,
    bool HasMoreResults,
    string Source           // "local" | "foursquare" | "mixed"
);

public record PlaceModel(
    long Id,                          // PK interna del sistema
    string ProviderReferenceId,       // Ej: "fsq_123456789" (puede ser null si es local)
    string Name,
    string CityCode,                  // Ej: "madrid-es" (legible)
    long CityId,                      // FK interna (para referencia)
    PlaceLocationModel Location,
    int TypicalDurationMinutes,
    bool IsIndoor,
    bool IsFamilyFriendly,
    IReadOnlyList<OpeningHoursWindowModel> OpeningHours
);

public record PlaceLocationModel(
    double Latitude,
    double Longitude
);

public record OpeningHoursWindowModel(
    DayOfWeek DayOfWeek,
    int OpenMinutes,      // Minutos desde 00:00 (ej: 540 = 09:00)
    int CloseMinutes      // Minutos desde 00:00 (ej: 1080 = 18:00)
);
```

**Status codes:**
- `200 OK` — Resultados encontrados (puede ser lista vacía)
- `400 Bad Request` — Query inválido (menos de 2 caracteres, cityCode vacío)
- `404 Not Found` — Ciudad no existe o no está habilitada

---

## 4. Diagrama del Proceso (Pipeline en Cascada)

```
[Usuario] ──GET /api/places/search?query=prado&cityCode=madrid-es──> [PlacesController]
                                      │
                                      ▼
                         ┌────────────────────────┐
                         │  Build SearchPlaces    │
                         │  Command               │
                         └────────────────────────┘
                                      │
                                      ▼
                         ┌────────────────────────┐
                         │  Mediator.Send()         │
                         │  → ValidationBehavior    │
                         │  → LoggingBehavior       │
                         └────────────────────────┘
                                      │
                                      ▼
                         ┌────────────────────────┐
                         │  SearchPlacesHandler     │
                         │  (Application Layer)     │
                         └────────────────────────┘
                                      │
                    ┌─────────────────┼─────────────────┐
                    ▼                 ▼                 ▼
          ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
          │ Validar Ciudad  │ │ Validar Query   │ │ Resolver        │
          │ (ICityRepository)│ │ (min 2 chars)   │ │ CityCode →      │
          │ GetByCodeAsync() │ │                 │ │ CityId (long)   │
          └─────────────────┘ └─────────────────┘ └─────────────────┘
                    │                 │                 │
                    └─────────────────┼─────────────────┘
                                      │
                                      ▼
                         ┌────────────────────────┐
                         │  Paso A: Consulta      │
                         │  en BD Local           │
                         │  (IPlaceRepository)    │
                         │  SearchAsync()         │
                         └────────────────────────┘
                                      │
                    ┌─────────────────┴─────────────────┐
                    │                                     │
                    ▼                                     ▼
          ┌─────────────────┐                   ┌─────────────────┐
          │  Resultados     │                   │  Vacío o pocos  │
          │  encontrados    │                   │  resultados     │
          │  (≥ minUmbral)  │                   │  (< minUmbral)  │
          └─────────────────┘                   └─────────────────┘
                    │                                     │
                    │                                     ▼
                    │                          ┌────────────────────────┐
                    │                          │  Paso B: Invocación    │
                    │                          │  a Foursquare API      │
                    │                          │  (IPlaceExternalService)│
                    │                          └────────────────────────┘
                    │                                     │
                    │                                     ▼
                    │                          ┌────────────────────────┐
                    │                          │  Paso C: Mapeo de      │
                    │                          │  Emergencia +          │
                    │                          │  Aplicar Heurísticas   │
                    │                          └────────────────────────┘
                    │                                     │
                    │                                     ▼
                    │                          ┌────────────────────────┐
                    │                          │  Paso D: Persistir     │
                    │                          │  en BD Local           │
                    │                          │  (IPlaceRepository)    │
                    │                          │  UpsertRangeAsync()    │
                    │                          └────────────────────────┘
                    │                                     │
                    │                                     ▼
                    │                          ┌────────────────────────┐
                    │                          │  Paso E: Recargar de   │
                    │                          │  BD para obtener Ids   │
                    │                          │  internos (long)       │
                    │                          └────────────────────────┘
                    │                                     │
                    └─────────────────┬─────────────────┘
                                      │
                                      ▼
                         ┌────────────────────────┐
                         │  Mapear a Response     │
                         │  (AutoMapper)          │
                         └────────────────────────┘
                                      │
                                      ▼
                              [200 OK]
                        PlaceSearchResponse JSON
```

---

## 5. Pasos Detallados del Handler

### 5.1 Validación de Entrada

```csharp
public class SearchPlacesValidator : AbstractValidator<SearchPlacesRequest>
{
    public SearchPlacesValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty().WithMessage("Query is required")
            .MinimumLength(2).WithMessage("Query must be at least 2 characters");
            
        RuleFor(x => x.CityCode)
            .NotEmpty().WithMessage("CityCode is required");
    }
}
```

### 5.2 Validación de Negocio

```csharp
var city = await _cityRepository.GetByCodeAsync(request.CityCode, ct);
if (city is null)
    throw new CityNotFoundException(request.CityCode);
    
if (!city.IsAllowed)
    throw new BusinessRuleException($"City '{request.CityCode}' is not available");

var cityId = city.Id;  // long
```

### 5.3 Paso A: Consulta en BD Local

```csharp
var localResults = await _placeRepository.SearchAsync(
    query: request.Query,
    cityCode: request.CityCode,
    maxResults: 20
);

var minResultsThreshold = 5;  // Umbral mínimo de resultados

if (localResults.Count >= minResultsThreshold)
{
    // Suficientes resultados locales. Retornar directamente.
    return BuildResponse(localResults, source: "local");
}
```

**Nota:** `IPlaceRepository.SearchAsync` busca por texto en la BD local:
- Matching por nombre (contains, starts with, o similaridad)
- Filtrado por `CityId` (long)
- Limitado a `maxResults`
- Ordenado por relevancia (nombre match exacto primero, luego parcial)

### 5.4 Paso B: Invocación a Foursquare API

```csharp
// Si no hay suficientes resultados locales
var externalResults = await _placeExternalService.SearchAsync(
    query: request.Query,
    cityCode: request.CityCode,
    cityLat: city.Location.Latitude,    // Si City tiene Location
    cityLng: city.Location.Longitude,
    radiusMeters: 15000,  // 15km radio de la ciudad
    maxResults: 20,
    ct
);
```

**Nota:** `IPlaceExternalService` es una interfaz de Infrastructure. Solo Infrastructure conoce Foursquare.

### 5.5 Paso C: Mapeo de Emergencia (Inyección de Datos)

Por cada resultado de Foursquare, aplicar heurísticas antes de persistir:

```csharp
foreach (var externalPlace in externalResults)
{
    // C.1: Inyección de TypicalDurationMinutes
    externalPlace.TypicalDurationMinutes = MapDurationFromCategory(externalPlace.Category);
    
    // C.2: Inyección de IsIndoor
    externalPlace.IsIndoor = MapIndoorFromCategory(externalPlace.Category);
    
    // C.3: Inyección de IsFamilyFriendly
    externalPlace.IsFamilyFriendly = MapFamilyFriendlyFromCategory(externalPlace.Category);
    
    // C.4: Inyección de OpeningHours (default si Foursquare no los devuelve)
    if (!externalPlace.OpeningHours.Any())
    {
        externalPlace.OpeningHours = CreateDefaultOpeningHours();
    }
}
```

#### Reglas de Mapeo de Emergencia

**A. `TypicalDurationMinutes`:**
| Categoría Foursquare | Duración |
|---------------------|----------|
| Museum, Art Gallery, Theme Park | 120 min |
| Historic Site, Monument, Plaza, Park | 60 min |
| Restaurant, Café, Food Court | 90 min |
| Otras / No identificada | 60 min |

**B. `IsIndoor`:**
| Categoría Foursquare | Valor |
|---------------------|-------|
| Museum, Art Gallery, Theater, Church, Shopping Mall | `true` |
| Park, Monument, Plaza, Lookout, Natural Feature | `false` |
| Ambigua / No identificada | `true` (default conservador) |

**C. `IsFamilyFriendly`:**
| Categoría Foursquare | Valor |
|---------------------|-------|
| Nightclub, Strip Club, Adult Entertainment | `false` |
| Otras / No identificada | `true` (default inclusivo) |

**D. `OpeningHours` (default):**
```csharp
private List<OpeningHoursWindow> CreateDefaultOpeningHours()
{
    return Enum.GetValues<DayOfWeek>()
        .Select(day => new OpeningHoursWindow(day, 540, 1080))  // 09:00 - 18:00
        .ToList();
}
```

### 5.6 Paso D: Persistir en BD Local

```csharp
// Upsert: si el ProviderReferenceId ya existe, actualizar; si no, crear
await _placeRepository.UpsertRangeAsync(externalResults, ct);

await _unitOfWork.SaveChangesAsync(ct);
```

**Reglas de Upsert:**
- Si `ProviderReferenceId` ya existe en BD local Y `IsAutoUpdateEnabled == true`: actualizar datos (nombre, location, horarios, etc.) pero NO sobreescribir `TypicalDurationMinutes`, `IsIndoor`, `IsFamilyFriendly` si ya fueron enriquecidos por LLM (Flow 3).
- Si `ProviderReferenceId` no existe: crear nuevo `Place` con todos los datos mapeados.
- Si `ProviderReferenceId` existe pero `IsAutoUpdateEnabled == false`: no actualizar.

### 5.7 Paso E: Recargar de BD para obtener Ids Internos

```csharp
// Después del upsert, recargar los lugares para obtener sus Id (long) internos
var providerIds = externalResults.Select(p => p.ProviderReferenceId).ToList();
var persistedPlaces = await _placeRepository.GetManyByProviderReferenceIdsAsync(providerIds, ct);

// Combinar resultados locales + externos persistidos
var allResults = localResults.Concat(persistedPlaces).ToList();

return BuildResponse(allResults, source: "mixed");
```

### 5.8 Mapeo a Response

```csharp
private PlaceSearchResponse BuildResponse(List<Place> places, string source)
{
    var models = places.Select(p => new PlaceModel(
        Id: p.Id,                              // long — PK interna
        ProviderReferenceId: p.ProviderReferenceId,
        Name: p.Name,
        CityCode: city.CityCode,               // "madrid-es"
        CityId: p.CityId,                      // long
        Location: new PlaceLocationModel(p.Location.Latitude, p.Location.Longitude),
        TypicalDurationMinutes: p.TypicalDurationMinutes,
        IsIndoor: p.IsIndoor,
        IsFamilyFriendly: p.IsFamilyFriendly,
        OpeningHours: p.OpeningHours.Select(oh => new OpeningHoursWindowModel(
            oh.DayOfWeek, oh.OpenMinutes, oh.CloseMinutes
        )).ToList()
    )).ToList();
    
    return new PlaceSearchResponse(
        Places: models,
        TotalResults: models.Count,
        HasMoreResults: false,  // MVP: no paginación
        Source: source
    );
}
```

---

## 6. Criterios de Aceptación Técnicos

1. **Transparencia Absoluta (UX):** El usuario no percibe si el resultado vino de BD local o Foursquare. Ambos se ven idénticos.
2. **FK Interna expuesta:** La respuesta devuelve `Id` (long) como identificador principal del lugar. El frontend usa este `Id` para construir `MustSeeInput` en Flow 0.
3. **Preservación de Identidad Externa:** `ProviderReferenceId` (ej: `fsq_id`) se almacena en BD local pero **NO se usa** para relaciones internas. Solo sirve para:
   - Reconocer duplicados en búsquedas futuras.
   - Enriquecimiento por LLM (Flow 3).
   - Referencia al proveedor externo.
4. **Aislamiento de Infraestructura:** Solo `SmartTripPlanner.Infrastructure` conoce Foursquare. `IPlaceExternalService` es una abstracción de Infrastructure. `SearchPlacesHandler` solo inyecta `IPlaceRepository` (dominio) y `IPlaceExternalService` (infraestructura, pero la interfaz vive en Application).
5. **Persistencia Automática:** Todo resultado de Foursquare se persiste en BD local para futuras búsquedas.
6. **No Duplicados:** Si un lugar de Foursquare ya existe en BD local (mismo `ProviderReferenceId`), se actualiza (si `IsAutoUpdateEnabled`) pero no se crea duplicado.
7. **OpeningHours Default:** Si Foursquare no devuelve horarios, se asignan default `[09:00-18:00]` todos los días. Esto es crítico para Flow 2 (OR-Tools).
8. **Umbral de Resultados:** Si la búsqueda local devuelve menos de 5 resultados, se activa automáticamente el fallback a Foursquare.

---

## 7. Conexión con Otros Flujos

### 7.1 Flow 1 → Flow 0 (Creación del Trip)

- El usuario busca en Flow 1 → recibe `PlaceModel[]` con `Id` (long).
- El frontend selecciona los que quiere → arma `MustSeeInput[]` con `PlaceId` (long, que es el `Id` del `Place`).
- Envía a `POST /api/trips` (Flow 0).
- Flow 0 valida que los `PlaceId` (long) existen en `IPlaceRepository.GetManyByIdsAsync()`.

### 7.2 Flow 1 → Flow 3 (Enriquecimiento Asíncrono)

- Flow 3 no se ejecuta en Flow 1.
- Flow 3 se ejecuta DESPUÉS de Flow 2 (generación de itinerario), identificando los `PlaceId` del itinerario que tienen `IsAutoUpdateEnabled == true` y `Provider == Foursquare`.
- Flow 3 busca en Foursquare por `ProviderReferenceId` y enriquece con LLM.

### 7.3 Flow 1 → Flow 2 (Preparación del Solver)

- Flow 2 lee `Trip.OriginalMustSees` que contiene `PlaceId` (long).
- Flow 2 usa `IPlaceRepository.GetManyByIdsAsync()` para hidratar los `Place` completos (con horarios, location, etc.).
- Si un `PlaceId` no existe (imposible si Flow 0 validó correctamente), Flow 2 lanza excepción.

---

## 8. Preguntas Abiertas / Decisiones Pendientes

1. **¿Paginación?** El MVP no tiene paginación (`HasMoreResults = false` siempre). ¿Es suficiente?
2. **¿Umbral de 5 resultados es correcto?** Si busco "prado" y la BD local tiene 3 lugares que coinciden, ¿salto a Foursquare o devuelvo esos 3?
3. **¿AutoUpdateEnabled en creación?** Cuando un lugar nuevo de Foursquare se persiste, ¿`IsAutoUpdateEnabled` debe ser `true` o `false`? Si es `true`, Flow 3 lo intentará enriquecer. Si es `false`, se queda con los valores de emergencia para siempre.
4. **¿Horarios por defecto en Foursquare?** Foursquare **sí** devuelve horarios de apertura en su API. ¿Los usamos o siempre asignamos default `[09:00-18:00]` y dejamos que Flow 3 los corrija con LLM?
5. **¿City.Location?** El `City` actual no tiene `Location` (lat/lng). Para la búsqueda de Foursquare necesitamos un punto central + radio. ¿Agregamos `Location` a `City` o lo resolvemos en el handler de otra forma?

---

## 9. Tests Requeridos

### 9.1 Handler Tests (`SearchPlacesHandlerTests.cs`)

```csharp
[TestClass]
public class SearchPlacesHandlerTests
{
    private readonly Mock<IPlaceRepository> _placeRepoMock = new();
    private readonly Mock<ICityRepository> _cityRepoMock = new();
    private readonly Mock<IPlaceExternalService> _externalServiceMock = new();
    private readonly SearchPlacesHandler _handler;
    
    public SearchPlacesHandlerTests()
    {
        _handler = new SearchPlacesHandler(
            _placeRepoMock.Object,
            _cityRepoMock.Object,
            _externalServiceMock.Object,
            Mock.Of<ILogger<SearchPlacesHandler>>()
        );
    }
    
    [TestMethod]
    public async Task Handle_LocalResultsAboveThreshold_ReturnsLocalOnly()
    {
        // Arrange: 10 resultados locales
        var request = new SearchPlacesRequest("prado", "madrid-es");
        var city = new City("madrid-es", "Madrid", true);
        typeof(City).GetProperty("Id")!.SetValue(city, 1L);
        
        _cityRepoMock.Setup(r => r.GetByCodeAsync("madrid-es", It.IsAny<CancellationToken>()))
            .ReturnsAsync(city);
        _placeRepoMock.Setup(r => r.SearchAsync("prado", "madrid-es", 20))
            .ReturnsAsync(CreatePlaces(10));
            
        // Act
        var result = await _handler.Handle(request, CancellationToken.None);
        
        // Assert
        Assert.AreEqual(10, result.TotalResults);
        Assert.AreEqual("local", result.Source);
        _externalServiceMock.Verify(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), 
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<int>(), 
            It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [TestMethod]
    public async Task Handle_LocalResultsBelowThreshold_CallsFoursquare()
    {
        // Arrange: 2 resultados locales (umbral = 5)
        var request = new SearchPlacesRequest("prado", "madrid-es");
        var city = new City("madrid-es", "Madrid", true);
        typeof(City).GetProperty("Id")!.SetValue(city, 1L);
        
        _cityRepoMock.Setup(r => r.GetByCodeAsync("madrid-es", It.IsAny<CancellationToken>()))
            .ReturnsAsync(city);
        _placeRepoMock.Setup(r => r.SearchAsync("prado", "madrid-es", 20))
            .ReturnsAsync(CreatePlaces(2));
        _externalServiceMock.Setup(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateExternalPlaces(5));
        _placeRepoMock.Setup(r => r.UpsertRangeAsync(It.IsAny<IEnumerable<Place>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _placeRepoMock.Setup(r => r.GetManyByProviderReferenceIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePlaces(5)); // Los persistidos con Id interno
            
        // Act
        var result = await _handler.Handle(request, CancellationToken.None);
        
        // Assert
        Assert.AreEqual(7, result.TotalResults);  // 2 local + 5 external
        Assert.AreEqual("mixed", result.Source);
        _externalServiceMock.Verify(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [TestMethod]
    public async Task Handle_CityNotFound_ThrowsCityNotFoundException()
    {
        var request = new SearchPlacesRequest("prado", "paris-fr");
        _cityRepoMock.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((City?)null);
            
        await Assert.ThrowsExceptionAsync<CityNotFoundException>(
            () => _handler.Handle(request, CancellationToken.None)
        );
    }
    
    [TestMethod]
    public async Task Handle_CityNotAllowed_ThrowsBusinessRuleException()
    {
        var request = new SearchPlacesRequest("prado", "paris-fr");
        var city = new City("paris-fr", "Paris", false);
        _cityRepoMock.Setup(r => r.GetByCodeAsync("paris-fr", It.IsAny<CancellationToken>()))
            .ReturnsAsync(city);
            
        await Assert.ThrowsExceptionAsync<BusinessRuleException>(
            () => _handler.Handle(request, CancellationToken.None)
        );
    }
}
```

---

## 10. Checklist de Implementación

- [ ] Crear `SearchPlacesRequest` (Command record)
- [ ] Crear `SearchPlacesResponse` (ApiModel record)
- [ ] Crear `PlaceModel` (ApiModel record) con `Id` (long), `ProviderReferenceId`, etc.
- [ ] Crear `SearchPlacesHandler` (Handler)
- [ ] Crear `SearchPlacesValidator` (FluentValidation)
- [ ] Crear `PlacesController` con `GET /api/places/search`
- [ ] Crear `IPlaceExternalService` (interfaz en Application, implementación en Infrastructure)
- [ ] Crear `PlaceExternalService` (Infrastructure) — cliente Foursquare
- [ ] Implementar `IPlaceRepository.SearchAsync(string query, string cityCode, int maxResults)`
- [ ] Implementar `IPlaceRepository.UpsertRangeAsync(IEnumerable<Place> places)`
- [ ] Implementar `IPlaceRepository.GetManyByProviderReferenceIdsAsync(IEnumerable<string> ids)`
- [ ] Implementar `EmergencyMapper` (Infrastructure) — mapeo de categorías Foursquare a heurísticas
- [ ] Definir `DefaultOpeningHours` generator
- [ ] Actualizar `AutoMapperProfile` con Place → PlaceModel
- [ ] Crear tests para `SearchPlacesHandler`
- [ ] Crear tests para `EmergencyMapper`
- [ ] Crear tests de integración para `GET /api/places/search`

---

*Documento generado como especificación técnica del Flujo 1. Sujeto a revisión y ajustes tras la validación de negocio.*
