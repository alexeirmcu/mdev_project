# Requisitos Técnicos — Flujo 0: Creación del Viaje (Trip Aggregate Root & Must-Sees)

## 1. Resumen del Flujo

Este flujo define la **creación del agregado de dominio `Trip`** y la **selección inicial de Must-Sees**. Es el punto de entrada obligatorio de todo el sistema: sin un `Trip` creado y persistido, no pueden ejecutarse los flujos de búsqueda de lugares (Flow 1), optimización (Flow 2), enriquecimiento (Flow 3), ni ejecución del día (Flow 4).

El flujo expone un endpoint REST que recibe los parámetros del viaje (ciudad, fechas, hotel base, viajeros, preferencias) y la lista de Must-Sees con prioridad. El sistema valida, materializa el agregado `Trip`, lo persiste, y devuelve una respuesta estructurada con el `tripId` generado.

---

## 2. Entidades de Dominio Involucradas

### 2.1 `Trip` — Aggregate Root

Ubicación: `SmartTripPlanner.Domain/AggregatesModel/Trip.cs`

Responsabilidad: Contener toda la información del viaje y actuar como root del aggregate. Ninguna entidad hija (`DayPlan`, `ActivityNode`) puede existir sin un `Trip`.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         Trip (Aggregate Root)                           │
├─────────────────────────────────────────────────────────────────────────┤
│  TripId           : Guid (PK)                                           │
│  TripCode         : string (unique, readable, ej: "MAD-2026-7X9K")    │
│  CityId           : long (FK → City.Id)                                  │
│  StartDate        : DateOnly                                            │
│  EndDate          : DateOnly                                            │
│  BaseHotel        : Location (Value Object)                             │
│  Travelers        : Travelers (Value Object)                            │
│  Preferences      : TripPreferences (Value Object)                        │
│  DefaultStartTime : TimeOnly (default: 09:00)                           │
│  OriginalMustSees : List<MustSee> (Value Object)                        │
│  Days             : List<DayPlan> (inicialmente vacía)                  │
│  Status           : TripStatus enum (CREATED / GENERATED / COMPLETED)    │
│  CreatedAt        : DateTimeOffset                                      │
└─────────────────────────────────────────────────────────────────────────┘
```

**Nota sobre el estado actual del código:**
- El `Trip` actual tiene `SelectedPlaces` (colección de `Place` entities). Esto es incorrecto desde el punto de vista de DDD: `Trip` no debería mantener referencias a entidades del catálogo (`Place`) como colección de navegación.
- **Corrección:** El agregado `Trip` debe contener `OriginalMustSees` como una lista de **Value Objects** (`MustSee`) que almacenan solo el `PlaceId` (long, FK interna a `Place.Id`), `Priority` y `PinnedDayIndex`/`PinnedBlock`. Esto desacopla el agregado del catálogo de lugares.

### 2.2 `MustSee` — Value Object

```csharp
public class MustSee : ValueObject
{
    public long PlaceId { get; }          // FK interna → Place.Id (long)
    public Priority Priority { get; }     // HIGH / MEDIUM / LOW
    public int? PinnedDayIndex { get; }    // null = sin día fijado
    public BlockType? PinnedBlock { get; } // null = sin bloque fijado
    
    public MustSee(long placeId, Priority priority, int? pinnedDayIndex = null, BlockType? pinnedBlock = null)
    {
        PlaceId = placeId;
        Priority = priority;
        PinnedDayIndex = pinnedDayIndex;
        PinnedBlock = pinnedBlock;
    }
    
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return PlaceId;
        yield return Priority;
        yield return PinnedDayIndex ?? -1;
        yield return PinnedBlock ?? -1;
    }
}

**Reglas de negocio:**
- `PinnedDayIndex` debe estar en el rango `[0, N-1]` donde `N = EndDate - StartDate + 1`.
- `PinnedBlock` solo puede tener valor si `PinnedDayIndex` también tiene valor.
- No puede haber `PlaceId` duplicados en la lista de `OriginalMustSees`.
- **Cada `PlaceId` debe existir en el catálogo de lugares (BD local).** El handler valida existencia via `IPlaceRepository.GetManyByIdsAsync()` con `long` IDs y falla con `422` si hay IDs inexistentes.
- **Nota:** `PlaceId` es la FK interna (`long`) que apunta a `Place.Id`. Nunca se usa `ProviderReferenceId` (string) para relaciones internas del dominio.

### 2.3 `Travelers` — Value Object

```csharp
public class Travelers : ValueObject
{
    public int Adults { get; }
    public int Children { get; }
    public int Infants { get; }
    
    public Travelers(int adults, int children = 0, int infants = 0)
    {
        Adults = adults;
        Children = children;
        Infants = infants;
    }
    
    public int Total => Adults + Children + Infants;
}
```

**Reglas de negocio:**
- `Adults` >= 1 (mínimo 1 adulto responsable).
- `Children` >= 0, `Infants` >= 0.
- `Total` <= 10 (límite arbitrario para evitar abuso en MVP).

### 2.4 `TripPreferences` — Value Object (MVP: mínimo)

```csharp
public class TripPreferences : ValueObject
{
    public bool CarAvailable { get; } = false;          // ¿Tienen coche de alquiler?
    public int MaxWalkingMinutes { get; } = 30;          // Máx caminata tolerable entre paradas
    public bool WeatherAwareEnabled { get; } = true;    // ¿Activar lógica de indoor/outdoor?
    
    public TripPreferences(bool carAvailable = false, int maxWalkingMinutes = 30, bool weatherAwareEnabled = true)
    {
        CarAvailable = carAvailable;
        MaxWalkingMinutes = maxWalkingMinutes;
        WeatherAwareEnabled = weatherAwareEnabled;
    }
}
```

### 2.5 `Location` — Value Object (existente)

Ya definido en `SmartTripPlanner.Domain/AggregatesModel/Location.cs`. Reutilizar sin cambios.

### 2.6 `TripCode` — Generador

El `TripCode` es un string único y legible generado en la creación del trip. No se modifica jamás.

**Formato:** `{CITY-CODE}-{YYYY}-{RANDOM}`
- `CITY-CODE`: código de ciudad en mayúsculas (ej: `MAD` para Madrid). Se extrae de `City.CityCode`.
- `YYYY`: año de `StartDate`.
- `RANDOM`: 4 caracteres alfanuméricos en mayúsculas (base36: 0-9, A-Z), verificando unicidad contra `ITripRepository.ExistsByTripCodeAsync()`.

**Ejemplo:** `MAD-2026-7X9K`

**Implementación:**
```csharp
public static class TripCodeGenerator
{
    private static readonly Random Random = new();
    private const string Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    
    public static string Generate(string cityCode, int year, ITripRepository tripRepository)
    {
        var prefix = $"{cityCode.ToUpperInvariant()}-{year}-";
        string tripCode;
        
        do
        {
            var randomPart = new string(Enumerable.Repeat(Chars, 4)
                .Select(s => s[Random.Next(s.Length)]).ToArray());
            tripCode = prefix + randomPart;
        } while (tripRepository.ExistsByTripCodeAsync(tripCode).Result); // Sync en creación
        
        return tripCode;
    }
}
```

**Nota:** En el MVP con EF InMemory, la probabilidad de colisión es negligible. En producción con alta concurrencia, considerar un índice único en BD y retry con backoff.

---

## 3. API Contract — POST /api/trips

### 3.1 Request: `TripGenerationRequest`

Ubicación: `SmartTripPlanner.Domain/ApiModels/TripGenerationRequest.cs`

```csharp
public record TripGenerationRequest(
    string CityCode,        // Ej: "madrid-es" — el handler resuelve el CityId (long) real
    DateOnly StartDate,
    DateOnly EndDate,
    LocationModel BaseHotel,
    IReadOnlyList<MustSeeInput> MustSees,
    TravelersInput? Travelers = null,
    TripPreferencesInput? Preferences = null,
    string DefaultStartHour = "09:00"
);
```

**Modelos anidados:**

```csharp
public record MustSeeInput(
    long PlaceId,           // FK interna → Place.Id (long). El frontend envía el Id del sistema.
    Priority Priority,
    int? PinnedDayIndex = null,
    BlockType? PinnedBlock = null
);

public record TravelersInput(
    int Adults = 2,
    int Children = 0,
    int Infants = 0
);

public record TripPreferencesInput(
    bool CarAvailable = false,
    int MaxWalkingMinutes = 30,
    bool WeatherAwareEnabled = true
);
```

### 3.2 Response: `TripPlanResponse` (inmediato)

```csharp
public record TripPlanResponse(
    Guid TripId,
    string TripCode,        // Ej: "MAD-2026-7X9K"
    long CityId,            // FK real → City.Id
    string CityCode,        // Ej: "madrid-es" (legible)
    string CityName,
    DateOnly StartDate,
    DateOnly EndDate,
    LocationModel BaseHotel,
    TravelersInput Travelers,
    TripPreferencesInput Preferences,
    IReadOnlyList<MustSeeResponse> MustSees,
    string Status,
    string DefaultStartHour
);

public record MustSeeResponse(
    long PlaceId,           // FK interna → Place.Id
    string Priority,
    int? PinnedDayIndex,
    string? PinnedBlock
);
```

**Nota:** En el MVP, la respuesta de creación NO incluye `Days[]` (itinerario). El itinerario se genera en un paso posterior (Flow 2 + Flow 3) o en el mismo handler pero la respuesta no incluye los `DayPlan` todavía. Esto permite desacoplar la creación del trip de la optimización.

**Status codes:**
- `201 Created` — Trip creado exitosamente. Header `Location: /api/trips/{tripId}`
- `400 Bad Request` — Validación fallida (FluentValidation)
- `422 Unprocessable Entity` — Reglas de negocio violadas (ej: ciudad no soportada, must-sees duplicados)

### 3.3 PATCH /api/trips/{tripId} — Actualización del Viaje

Permite modificar los datos del trip antes de generar el itinerario (status `CREATED`). Una vez generado (`GENERATED`), solo permitir edición de `MustSees` adicionales (no eliminar must-sees ya usados en el solver).

**Request:** `TripUpdateRequest`
```csharp
public record TripUpdateRequest(
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    LocationModel? BaseHotel = null,
    TravelersInput? Travelers = null,
    TripPreferencesInput? Preferences = null,
    string? DefaultStartHour = null,
    List<MustSeeInput>? MustSeesToAdd = null,
    List<long>? MustSeesToRemove = null  // PlaceIds (long) a quitar
);
```

**Response:** `TripPlanResponse` (misma estructura que POST)

**Status codes:**
- `200 OK` — Trip actualizado
- `400 Bad Request` — Validación fallida
- `404 Not Found` — Trip no existe
- `422 Unprocessable Entity` — Intentar modificar fechas/baseHotel cuando Status != CREATED

**Reglas de negocio:**
- Si `Status == GENERATED`, no se permite modificar `StartDate`, `EndDate`, `BaseHotel`, `DefaultStartHour`.
- Si `Status == GENERATED`, solo se permite: agregar must-sees nuevos, quitar must-sees que aún no estén en el itinerario (no usados en ningún `DayPlan`), o modificar `Travelers`/`Preferences`.
- Si se elimina un `MustSee` que ya está en el itinerario generado, lanzar `422` con mensaje: "Cannot remove must-see that is already planned in the itinerary. Use replan instead."

---

## 4. Diagrama del Proceso

```
[Usuario] ──POST /api/trips──> [TripsController]
                                      │
                                      ▼
                         ┌────────────────────────┐
                         │  Build GenerateTrip    │
                         │  Command               │
                         └────────────────────────┘
                                      │
                                      ▼
                         ┌────────────────────────┐
                         │  Mediator.Send()       │
                         │  → ValidationBehavior    │
                         │  → LoggingBehavior       │
                         └────────────────────────┘
                                      │
                                      ▼
                         ┌────────────────────────┐
                         │  GenerateTripHandler    │
                         │  (Application Layer)     │
                         └────────────────────────┘
                                      │
                    ┌─────────────────┼─────────────────┐
                    ▼                 ▼                 ▼
          ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
          │ Validar Ciudad  │ │ Validar Must-   │ │ Validar Fechas │
          │ (ICityRepository)│ │ Sees (PlaceIds) │ │ (rango, lógica)│
          └─────────────────┘ └─────────────────┘ └─────────────────┘
                    │                 │                 │
                    └─────────────────┼─────────────────┘
                                      │
                                      ▼
                         ┌────────────────────────┐
                         │  Materializar Trip     │
                         │  Aggregate Root          │
                         │  (Domain Factory)        │
                         └────────────────────────┘
                                      │
                                      ▼
                         ┌────────────────────────┐
                         │  Persistir en BD       │
                         │  (ITripRepository)     │
                         └────────────────────────┘
                                      │
                                      ▼
                         ┌────────────────────────┐
                         │  Mapear a Response     │
                         │  (AutoMapper)          │
                         └────────────────────────┘
                                      │
                                      ▼
                              [201 Created]
                         Location: /api/trips/{id}
```

---

## 5. Pasos Detallados del Handler

### 5.1 Validación de Entrada (FluentValidation)

Ubicación: `SmartTripPlanner.ApplicationServices/Commands/GenerateTripValidator.cs`

```csharp
public class GenerateTripValidator : AbstractValidator<GenerateTrip>
{
    public GenerateTripValidator()
    {
        RuleFor(x => x.Payload.CityCode)
            .NotEmpty().WithMessage("CityCode is required")
            .MaximumLength(50);
            
        RuleFor(x => x.Payload.StartDate)
            .NotEmpty()
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("StartDate cannot be in the past");
            
        RuleFor(x => x.Payload.EndDate)
            .NotEmpty()
            .GreaterThanOrEqualTo(x => x.Payload.StartDate)
            .WithMessage("EndDate must be >= StartDate");
            
        RuleFor(x => x.Payload.DefaultStartHour)
            .NotEmpty()
            .Matches("^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$")
            .WithMessage("Invalid time format. Expected HH:mm");
            
        RuleFor(x => x.Payload.BaseHotel)
            .NotNull()
            .ChildRules(hotel => {
                hotel.RuleFor(h => h.Name).NotEmpty().MaximumLength(200);
                hotel.RuleFor(h => h.Latitude).InclusiveRange(-90, 90);
                hotel.RuleFor(h => h.Longitude).InclusiveRange(-180, 180);
            });
            
        RuleFor(x => x.Payload.MustSees)
            .NotEmpty().WithMessage("At least one Must-See is required")
            .Must(list => list.Select(m => m.PlaceId).Distinct().Count() == list.Count)
            .WithMessage("Duplicate PlaceIds are not allowed in MustSees");
            
        RuleFor(x => x.Payload.Travelers)
            .ChildRules(t => {
                t.RuleFor(x => x.Adults).GreaterThanOrEqualTo(1);
                t.RuleFor(x => x.Children).GreaterThanOrEqualTo(0);
                t.RuleFor(x => x.Infants).GreaterThanOrEqualTo(0);
                t.RuleFor(x => x.Adults + x.Children + x.Infants).LessThanOrEqualTo(10);
            })
            .When(x => x.Payload.Travelers is not null);
            
        RuleFor(x => x.Payload.Preferences)
            .ChildRules(p => {
                p.RuleFor(x => x.MaxWalkingMinutes).InclusiveRange(5, 120);
            })
            .When(x => x.Payload.Preferences is not null);
    }
}
```

### 5.2 Validación de Negocio (Handler)

Validaciones que requieren acceso a repositorios o lógica de dominio:

**A. Validar que la ciudad existe y está habilitada (resolviendo CityCode → CityId):**
```csharp
var city = await _cityRepository.GetByCodeAsync(request.Payload.CityCode, ct);
if (city is null)
    throw new CityNotFoundException(request.Payload.CityCode);
    
if (!city.IsAllowed)
    throw new BusinessRuleException($"City '{request.Payload.CityCode}' is not available for planning");

// CityId real (long) para el aggregate
var cityId = city.Id;
```

**B. Validar que los `PlaceId` de los Must-Sees existen (obligatorio):**
```csharp
var placeIds = request.Payload.MustSees.Select(m => m.PlaceId).ToList();
var existingPlaces = await _placeRepository.GetManyByIdsAsync(placeIds, ct);
var missingIds = placeIds.Except(existingPlaces.Select(p => p.Id)).ToList();

if (missingIds.Any())
    throw new BusinessRuleException(
        $"Some Must-See places were not found: {string.Join(", ", missingIds)}",
        missingIds
    );
```

**Esta validación es obligatoria.** El `PlaceId` es la FK interna (`long`). El usuario debe haber buscado y persistido los lugares en Flow 1 antes de crear el trip.

**C. Validar que `PinnedDayIndex` y `PinnedBlock` son coherentes:**
```csharp
var tripDuration = request.Payload.EndDate.DayNumber - request.Payload.StartDate.DayNumber + 1;

foreach (var mustSee in request.Payload.MustSees)
{
    if (mustSee.PinnedDayIndex.HasValue)
    {
        if (mustSee.PinnedDayIndex < 0 || mustSee.PinnedDayIndex >= tripDuration)
            throw new BusinessRuleException(
                $"PinnedDayIndex {mustSee.PinnedDayIndex} is out of range [0, {tripDuration - 1}]"
            );
            
        if (mustSee.PinnedBlock.HasValue && mustSee.PinnedDayIndex is null)
            throw new BusinessRuleException(
                "PinnedBlock cannot be set without PinnedDayIndex"
            );
    }
}
```

**D. Validar duración máxima del viaje:**
```csharp
var maxTripDurationDays = 14; // 2 semanas máximo
if (tripDuration > maxTripDurationDays)
    throw new BusinessRuleException(
        $"Trip duration ({tripDuration} days) exceeds maximum allowed ({maxTripDurationDays} days)"
    );
```

### 5.3 Materialización del Agregado

```csharp
var trip = new Trip
{
    TripCode = TripCodeGenerator.Generate(city.CityCode, request.Payload.StartDate.Year), // Ej: "MAD-2026-7X9K"
    CityId = cityId,        // FK real (long) → City.Id
    StartDate = request.Payload.StartDate,
    EndDate = request.Payload.EndDate,
    BaseHotel = new Location(
        request.Payload.BaseHotel.Name,
        request.Payload.BaseHotel.Latitude,
        request.Payload.BaseHotel.Longitude
    ),
    Travelers = new Travelers(
        request.Payload.Travelers?.Adults ?? 2,
        request.Payload.Travelers?.Children ?? 0,
        request.Payload.Travelers?.Infants ?? 0
    ),
    Preferences = new TripPreferences(
        request.Payload.Preferences?.CarAvailable ?? false,
        request.Payload.Preferences?.MaxWalkingMinutes ?? 30,
        request.Payload.Preferences?.WeatherAwareEnabled ?? true
    ),
    DefaultStartTime = TimeOnly.Parse(request.Payload.DefaultStartHour),
    Status = TripStatus.CREATED,
    CreatedAt = DateTimeOffset.UtcNow
};

// Agregar Must-Sees
foreach (var mustSeeInput in request.Payload.MustSees)
{
    trip.AddMustSee(new MustSee(
        mustSeeInput.PlaceId,
        mustSeeInput.Priority,
        mustSeeInput.PinnedDayIndex,
        mustSeeInput.PinnedBlock
    ));
}
```

**Nota:** `Trip` debe tener un método de fábrica (`Create`) o un constructor privado con un método público `AddMustSee`. El ejemplo anterior usa inicialización de propiedades pero debería encapsularse en un factory method.

### 5.4 Persistencia

```csharp
await _tripRepository.AddAsync(trip, ct);
await _unitOfWork.SaveChangesAsync(ct); // Si UnitOfWork está separado del repository
```

**Nota:** En el MVP con EF InMemory, `AddAsync` en el repositorio puede incluir `SaveChangesAsync` internamente. Para el futuro con BD real, se recomienda separar `IUnitOfWork` para transacciones explícitas.

### 5.5 Mapeo y Respuesta

```csharp
var response = new TripPlanResponse(
    trip.Id,
    trip.TripCode,
    trip.CityId,                    // FK real (long)
    city.CityCode,                  // Legible: "madrid-es"
    city.CityName,                  // "Madrid"
    trip.StartDate,
    trip.EndDate,
    new LocationModel(trip.BaseHotel.Name, trip.BaseHotel.Latitude, trip.BaseHotel.Longitude),
    new TravelersInput(trip.Travelers.Adults, trip.Travelers.Children, trip.Travelers.Infants),
    new TripPreferencesInput(trip.Preferences.CarAvailable, trip.Preferences.MaxWalkingMinutes, trip.Preferences.WeatherAwareEnabled),
    trip.OriginalMustSees.Select(m => new MustSeeResponse(
        m.PlaceId,
        m.Priority.ToString(),
        m.PinnedDayIndex,
        m.PinnedBlock?.ToString()
    )).ToList(),
    trip.Status.ToString(),
    trip.DefaultStartTime.ToString("HH:mm")
);

return response;
```

---

## 6. Cambios Requeridos en el Modelo de Dominio Existente

### 6.1 `Trip.cs` — Refactorizar

**Problema actual:** `Trip` tiene `SelectedPlaces` como `ICollection<Place>` (referencia a entidad de catálogo).

**Solución:** Reemplazar por `OriginalMustSees` como lista de Value Objects.

```csharp
public class Trip : Entity, IAggregateRoot
{
    public long CityId { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public required Location BaseHotel { get; init; }
    public Travelers Travelers { get; private set; } = new Travelers(2, 0, 0);
    public TripPreferences Preferences { get; private set; } = new TripPreferences();
    public TimeOnly DefaultStartTime { get; private set; } = new TimeOnly(9, 0);
    public List<MustSee> OriginalMustSees { get; private set; } = new();
    public List<DayPlan> Days { get; private set; } = new();
    public TripStatus Status { get; private set; } = TripStatus.CREATED;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    
    // TripCode se genera en la fábrica, no se modifica post-creación
    public string TripCode { get; init; } = null!;
    
    // Métodos de dominio
    public void AddMustSee(MustSee mustSee)
    {
        if (OriginalMustSees.Any(m => m.PlaceId == mustSee.PlaceId))
            throw new DomainException($"PlaceId {mustSee.PlaceId} is already in MustSees");
            
        OriginalMustSees.Add(mustSee);
    }
    
    public bool RemoveMustSee(long placeId)
    {
        var mustSee = OriginalMustSees.FirstOrDefault(m => m.PlaceId == placeId);
        if (mustSee is not null)
        {
            OriginalMustSees.Remove(mustSee);
            return true;
        }
        return false;
    }
    
    public void UpdateStatus(TripStatus newStatus)
    {
        // Validar transiciones de estado permitidas
        Status = newStatus;
    }
    
    public void GenerateDays(IEnumerable<DayPlan> days)
    {
        if (Days.Any())
            throw new DomainException("Days have already been generated for this trip");
            
        Days.AddRange(days);
        Status = TripStatus.GENERATED;
    }
}
```

### 6.2 `TripStatus` — Nuevo Enum

```csharp
public enum TripStatus
{
    CREATED,    // Trip creado, aún sin itinerario
    GENERATED,  // Itinerario generado (Flow 2 completado)
    COMPLETED   // Viaje finalizado (todos los días completados)
}
```

### 6.3 `Travelers` — Nuevo Value Object

(Ver sección 2.3)

### 6.4 `TripPreferences` — Nuevo Value Object

(Ver sección 2.4)

### 6.5 `MustSee` — Nuevo Value Object

(Ver sección 2.2)

---

## 7. Repositorios e Interfaces

### 7.1 `ITripRepository` (existente)

```csharp
public interface ITripRepository
{
    Task<Trip?> GetByIdAsync(Guid tripId, CancellationToken ct);
    Task<Trip?> GetByTripCodeAsync(string tripCode, CancellationToken ct);
    Task<bool> ExistsByTripCodeAsync(string tripCode, CancellationToken ct);
    Task<IEnumerable<Trip>> ListAsync(long? cityId, DateOnly? startDate, DateOnly? endDate, CancellationToken ct);
    Task AddAsync(Trip trip, CancellationToken ct);
    Task UpdateAsync(Trip trip, CancellationToken ct);
    Task DeleteAsync(Guid tripId, CancellationToken ct);
}
```

### 7.2 `ICityRepository` (existente)

```csharp
public interface ICityRepository
{
    Task<City?> GetByIdAsync(long id, CancellationToken ct);
    Task<City?> GetByCodeAsync(string cityCode, CancellationToken ct);
    Task<IEnumerable<City>> ListAllowedAsync(CancellationToken ct);
}
```

### 7.3 `IPlaceRepository` (existente)

```csharp
public interface IPlaceRepository
{
    Task<Place?> GetByIdAsync(long id, CancellationToken ct);                          // PK interna
    Task<Place?> GetByProviderReferenceIdAsync(string providerReferenceId, CancellationToken ct); // ID externo
    Task<IEnumerable<Place>> GetManyByIdsAsync(IEnumerable<long> placeIds, CancellationToken ct);
    // ... otros métodos
}
```

---

## 8. Criterios de Aceptación Técnicos

1. **Endpoint disponible:** `POST /api/trips` acepta `TripGenerationRequest` y devuelve `201 Created` con `TripPlanResponse`.
2. **Validación completa:** Todos los campos obligatorios son validados. Errores de validación devuelven `400 Bad Request` con mensajes claros.
3. **Reglas de negocio:** El handler valida ciudad existente, rango de fechas, duración máxima, y coherencia de PinnedDay/PinnedBlock.
4. **Desacoplamiento:** `Trip` no mantiene referencias a `Place` entities. Solo almacena `PlaceId` (long, FK interna) en `MustSee` Value Objects.
5. **Estado inicial:** El `Trip` se crea con `Status = CREATED` y `Days` vacía. No se genera itinerario en este flujo.
6. **Idempotencia:** Crear el mismo trip dos veces genera dos `TripId` diferentes (no hay conflicto por datos de entrada).
7. **Persistencia:** El trip se almacena en EF InMemory (MVP) y es recuperable vía `GET /api/trips/{tripId}`.
8. **Fire-and-forget:** El handler NO dispara el enriquecimiento en Flow 0. Eso se hace en Flow 2 (post-generación) o en Flow 1 (post-búsqueda).
9. **Tests:** Existen tests unitarios para `GenerateTripHandler` que cubren:
   - Happy path (creación exitosa)
   - Validación fallida (ciudad inexistente, fechas inválidas, must-sees duplicados)
   - Reglas de negocio (pinned day fuera de rango, duración máxima excedida)

---

## 9. Conexión con Otros Flujos

### 9.1 Flow 0 → Flow 1 (Búsqueda de Must-Sees)

- El usuario puede no tener `PlaceId` al crear el trip. En ese caso, Flow 0 debería permitir una lista vacía de `MustSees` (opcional en el MVP) o el usuario debe usar Flow 1 primero para buscar y luego volver a Flow 0.
- **Decisión:** En el MVP, `MustSees` es **obligatorio** (mínimo 1). El usuario debe haber buscado previamente en Flow 1 o tener los IDs de antemano.

### 9.2 Flow 0 → Flow 2 (Preparación del Solver)

- Flow 2 recibe un `TripId` y carga el `Trip` desde `ITripRepository`.
- Lee `Trip.OriginalMustSees`, `Trip.BaseHotel`, `Trip.StartDate`, `Trip.EndDate`, `Trip.DefaultStartTime` para construir la matriz de optimización.
- Si `Trip.Status != CREATED`, Flow 2 podría lanzar excepción o re-generar (depende de la decisión de producto).

### 9.3 Flow 0 → Flow 3 (Enriquecimiento)

- Flow 3 se ejecuta DESPUÉS de Flow 2, no en Flow 0.
- Si el itinerario generado (Flow 2) incluye lugares de Foursquare que no están en la BD local, Flow 3 los enriquece.

### 9.4 Flow 0 → Flow 4 (Ejecución del Día)

- Flow 4 requiere que `Trip.Status == GENERATED` (itinerario existente).
- Si un usuario intenta acceder a Flow 4 con `Status == CREATED`, el sistema debe mostrar un mensaje: "Primero debes generar el itinerario".

---

## 10. Decisiones de Negocio (Resueltas)

1. **✅ MustSees obligatorio:** Mínimo 1 must-see por trip. Validación estricta en `GenerateTripValidator`.
2. **✅ Validación estricta de PlaceIds:** El handler verifica existencia via `IPlaceRepository.GetManyByIdsAsync()`. Si falta alguno, falla con `422`.
3. **✅ Edición post-creación:** Sí. Se expone `PATCH /api/trips/{tripId}` con restricciones según `Status`. Solo editable en `CREATED` sin restricciones; en `GENERATED` solo agregar/quitar must-sees no usados.
4. **✅ TripCode legible y único:** Formato `{CITY-CODE}-{YYYY}-{RANDOM}` (4 chars). Ej: `MAD-2026-7X9K`. Generado automáticamente en creación. Verificación de unicidad contra BD.
5. **✅ Travelers y Preferences incluidos:** Son parte del request/response. Aunque el solver MVP no los use aún, el dominio los modela correctamente para futuras iteraciones.

---

## 11. Tests Requeridos

### 11.1 Handler Tests (`GenerateTripHandlerTests.cs`)

```csharp
[TestClass]
public class GenerateTripHandlerTests
{
    private readonly Mock<ITripRepository> _tripRepoMock = new();
    private readonly Mock<ICityRepository> _cityRepoMock = new();
    private readonly Mock<IMapper> _mapperMock = new();
    private readonly GenerateTripHandler _handler;
    
    public GenerateTripHandlerTests()
    {
        _handler = new GenerateTripHandler(
            _tripRepoMock.Object,
            _cityRepoMock.Object,
            _mapperMock.Object,
            Mock.Of<ILogger<GenerateTripHandler>>()
        );
    }
    
    [TestMethod]
    public async Task Handle_ValidRequest_ReturnsTripPlanResponse()
    {
        // Arrange
        var request = CreateValidRequest();
        var city = new City("madrid-es", "Madrid", true);
        // Simular que EF asignó Id=1
        typeof(City).GetProperty("Id")!.SetValue(city, 1L);
        
        _cityRepoMock.Setup(r => r.GetByCodeAsync("madrid-es", It.IsAny<CancellationToken>()))
            .ReturnsAsync(city);
            
        // Act
        var result = await _handler.Handle(new GenerateTrip(request), CancellationToken.None);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.AreNotEqual(Guid.Empty, result.TripId);
        Assert.AreEqual(1L, result.CityId);
        Assert.AreEqual("madrid-es", result.CityCode);
        _tripRepoMock.Verify(r => r.AddAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [TestMethod]
    public async Task Handle_CityNotFound_ThrowsCityNotFoundException()
    {
        // Arrange
        var request = CreateValidRequest();
        _cityRepoMock.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((City?)null);
            
        // Act & Assert
        await Assert.ThrowsExceptionAsync<CityNotFoundException>(
            () => _handler.Handle(new GenerateTrip(request), CancellationToken.None)
        );
    }
    
    [TestMethod]
    public async Task Handle_CityNotAllowed_ThrowsBusinessRuleException()
    {
        // Arrange
        var request = CreateValidRequest();
        var city = new City("madrid-es", "Madrid", false); // Not allowed
        typeof(City).GetProperty("Id")!.SetValue(city, 1L);
        
        _cityRepoMock.Setup(r => r.GetByCodeAsync("madrid-es", It.IsAny<CancellationToken>()))
            .ReturnsAsync(city);
            
        // Act & Assert
        await Assert.ThrowsExceptionAsync<BusinessRuleException>(
            () => _handler.Handle(new GenerateTrip(request), CancellationToken.None)
        );
    }
    
    [TestMethod]
    public async Task Handle_PinnedDayOutOfRange_ThrowsBusinessRuleException()
    {
        // Arrange: 3-day trip, pinned day = 5
        var request = CreateValidRequest(pinnedDayIndex: 5);
        _cityRepoMock.Setup(r => r.GetByIdAsync("madrid-es", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new City("madrid-es", "Madrid", true));
            
        // Act & Assert
        await Assert.ThrowsExceptionAsync<BusinessRuleException>(
            () => _handler.Handle(new GenerateTrip(request), CancellationToken.None)
        );
    }
}
```

### 11.2 Domain Tests (`TripTests.cs`)

```csharp
    private static Trip CreateTrip() => new()
    {
        CityId = 1L,
        StartDate = new DateOnly(2026, 6, 1),
        EndDate = new DateOnly(2026, 6, 3),
        BaseHotel = new Location("Hotel", 0, 0)
    };

    [TestMethod]
    public void AddMustSee_DuplicatePlaceId_ThrowsDomainException()
    {
        var trip = CreateTrip();
        var mustSee = new MustSee(42L, Priority.HIGH);  // PlaceId interno (long)
        
        trip.AddMustSee(mustSee);
        
        Assert.ThrowsException<DomainException>(() => trip.AddMustSee(mustSee));
    }

[TestMethod]
public void GenerateDays_AlreadyGenerated_ThrowsDomainException()
{
    var trip = CreateTrip();
    var dayPlan = new DayPlan { DayIndex = 0, Date = new DateOnly(2026, 6, 1) };
    
    trip.GenerateDays(new[] { dayPlan });
    
    Assert.ThrowsException<DomainException>(
        () => trip.GenerateDays(new[] { dayPlan })
    );
}

[TestMethod]
public void GenerateDays_UpdatesStatusToGenerated()
{
    var trip = CreateTrip();
    var dayPlan = new DayPlan { DayIndex = 0, Date = new DateOnly(2026, 6, 1) };
    
    trip.GenerateDays(new[] { dayPlan });
    
    Assert.AreEqual(TripStatus.GENERATED, trip.Status);
}
```

---

## 12. Checklist de Implementación

- [ ] Refactorizar `Trip.cs`: reemplazar `SelectedPlaces` por `OriginalMustSees` (List<MustSee>)
- [ ] Crear `MustSee.cs` (Value Object)
- [ ] Crear `Travelers.cs` (Value Object)
- [ ] Crear `TripPreferences.cs` (Value Object)
- [ ] Crear `TripStatus.cs` (Enum)
- [ ] Crear `TripCodeGenerator.cs` (generador de códigos únicos)
- [ ] Actualizar `TripGenerationRequest.cs` con `TravelersInput` y `TripPreferencesInput`
- [ ] Actualizar `TripPlanResponse.cs` con `TripCode`, `CityName`, `BaseHotel`, `Travelers`, `Preferences`, `MustSees`, `Status`, `DefaultStartHour`
- [ ] Crear `TripUpdateRequest.cs` (PATCH request)
- [ ] Crear `GenerateTrip.cs` (Command record)
- [ ] Crear `GenerateTripHandler.cs` (Handler)
- [ ] Crear `GenerateTripValidator.cs` (FluentValidation)
- [ ] Crear `UpdateTrip.cs` (Command record) + `UpdateTripHandler.cs` + `UpdateTripValidator.cs`
- [ ] Crear `TripsController.cs` con `POST /api/trips` y `PATCH /api/trips/{tripId}`
- [ ] Crear `ExceptionHandlingMiddleware` (si no existe) para `BusinessRuleException`
- [ ] Crear `BusinessRuleException.cs` (Domain)
- [ ] Actualizar `TripRepository.cs` (EF mapping) para nuevo schema
- [ ] Actualizar `ITripRepository.cs` con `GetByTripCodeAsync` y `ExistsByTripCodeAsync`
- [ ] Actualizar `AutoMapperProfile.cs` con nuevos mappings
- [ ] Crear tests unitarios para `Trip` domain (`TripTests.cs`)
- [ ] Crear tests unitarios para `GenerateTripHandler` (`GenerateTripHandlerTests.cs`)
- [ ] Crear tests unitarios para `UpdateTripHandler` (`UpdateTripHandlerTests.cs`)
- [ ] Crear tests de integración para `POST /api/trips` (opcional, MVP)
- [ ] Actualizar `endpoints.yaml` (si existe) con el nuevo schema de request/response

---

## 13. Notas de Implementación

### 13.1 EF Core InMemory (MVP)

Como se usa EF Core InMemory, el `TripDbContext` debe configurar la relación de `Trip` con `MustSee` como **owned type** (value object) o **complex type** (EF Core 8+):

```csharp
// En TripDbContext.OnModelCreating
modelBuilder.Entity<Trip>().OwnsMany(t => t.OriginalMustSees, mustSee =>
{
    mustSee.WithOwner().HasForeignKey("TripId");
    mustSee.Property(m => m.PlaceId);  // long, no max length
    mustSee.Property(m => m.Priority).HasConversion<string>();
    mustSee.Property(m => m.PinnedDayIndex);
    mustSee.Property(m => m.PinnedBlock).HasConversion<string>();
});

modelBuilder.Entity<Trip>().OwnsOne(t => t.BaseHotel);
modelBuilder.Entity<Trip>().OwnsOne(t => t.Travelers);
modelBuilder.Entity<Trip>().OwnsOne(t => t.Preferences);
modelBuilder.Entity<Trip>().HasIndex(t => t.TripCode).IsUnique();
```

### 13.2 AutoMapper

```csharp
// En AutoMapperProfile
CreateMap<TripGenerationRequest, Trip>()
    .ForMember(dest => dest.Id, opt => opt.MapFrom(_ => Guid.NewGuid()))
    .ForMember(dest => dest.OriginalMustSees, opt => opt.MapFrom(src => src.MustSees))
    .ForMember(dest => dest.Days, opt => opt.Ignore())
    .ForMember(dest => dest.Status, opt => opt.MapFrom(_ => TripStatus.CREATED));

CreateMap<MustSeeInput, MustSee>();
CreateMap<TravelersInput, Travelers>();
CreateMap<TripPreferencesInput, TripPreferences>();
CreateMap<LocationModel, Location>();

CreateMap<Trip, TripPlanResponse>()
    .ForMember(dest => dest.CityName, opt => opt.Ignore()) // Resuelto en handler
    .ForMember(dest => dest.CityCode, opt => opt.Ignore()) // Resuelto en handler
    .ForMember(dest => dest.MustSees, opt => opt.MapFrom(src => src.OriginalMustSees))
    .ForMember(dest => dest.TripCode, opt => opt.MapFrom(src => src.TripCode));

CreateMap<MustSee, MustSeeResponse>();
CreateMap<Location, LocationModel>();
```

---

*Documento generado como especificación técnica del Flujo 0. Sujeto a revisión y ajustes tras la validación de negocio.*
