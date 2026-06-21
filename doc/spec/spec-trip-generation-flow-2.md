# Especificacion Tecnica — Flujo 2: Generacion de Itinerario por Bloques (Heuristico)

## 1. Resumen del Flujo

Este flujo genera el itinerario multi-dia de un viaje previamente creado (Flujo 0) mediante un **algoritmo heuristico por fases**. No utiliza OR-Tools ni un solver de optimizacion matematica; en su lugar, distribuye must-sees y candidatos en bloques (Manana / Tarde / Noche) aplicando reglas pragmaticas de proximidad, horarios, clima y transporte.

**Separacion de responsabilidades:**
- `POST /api/trips` (Flujo 0): Crea el `Trip` con estado `"CREATED"` y `Days` vacia. **NO** dispara la generacion.
- `POST /api/trips/{tripId}/generate` (Flujo 2): Ejecuta `GenerateTripItineraryHandler`, que invoca `IItineraryGenerator.GenerateAsync()`, popula `Trip.Days` y devuelve estado `"GENERATED"`.

**Alcance:**
- Distribucion de must-sees (pinned y unpinned) en dias y bloques.
- Llenado de slots restantes con lugares candidatos puntuados.
- Asignacion de modo de transporte por tramo.
- Adaptacion climatica (indoor/outdoor scoring).
- Validacion de capacidad por bloque y fallback por prioridad.
- **Horarios exactos de inicio y fin por visita** (`EstimatedArrival` / `EstimatedDeparture`).
- **Tramos de ida y vuelta al hotel** (`TransitFromHotel` / `TransitToHotel`).
- **Transit directo entre bloques consecutivos** (`InterBlockTransit`) via `ReturnToHotelStrategy`.

**No Alcance:**
- OR-Tools VRP solver (post-MVP).
- Routing real con API externa (Google Maps, HERE, etc.).
- Replanificacion automatica (Flow 4).
- Multi-ciudad / cambio de hotel.

---

## 2. Entidades de Dominio Involucradas

### 2.1 `DayPlan`

Ubicacion: `SmartTripPlanner.Domain/AggregatesModel/DayPlan.cs`

Representa un dia del viaje con 3 bloques horarios.

```
DayIndex              : int (0..N-1)
Date                  : DateOnly
WeatherSummary        : WeatherCondition (Clear | Good | Bad)
StartTime             : TimeOnly (default 09:00)
Morning               : BlockTimeline
Afternoon             : BlockTimeline
Evening               : BlockTimeline
```

- `UpdateStartTime(TimeOnly)` — permite desplazar el inicio del dia.
- `SetWeather(WeatherCondition)` — asignado por `TransitEnricher` a partir del diccionario de clima.
- `GetBlock(BlockType)` — resuelve Morning/Afternoon/Evening.

### 2.2 `BlockTimeline`

Ubicacion: `SmartTripPlanner.Domain/AggregatesModel/BlockTimeline.cs`

Contenedor de actividades dentro de un bloque horario.

```
BlockType             : BlockType (Morning | Afternoon | Evening)
Activities            : List<ActivityNode>
BlockTotalDurationMinutes => suma de DurationMinutes + TransitToNext.DurationMinutes
TransitFromHotel      : TransitDetails? (Hotel → primera actividad)
TransitToHotel        : TransitDetails? (ultima actividad → Hotel)
InterBlockTransit     : TransitDetails? (ultima actividad → primera actividad del siguiente bloque)
```

- `AddActivity(ActivityNode)` — agrega si hay capacidad (visitas + duracion).
- `CanFitActivity(int durationMinutes)` — valida contra limites de `TripPlanningConstants`.
- `GetBlockConstraints()` — resuelve `(maxVisits, maxDuration)` segun el tipo de bloque.

**Nota:** `BlockTotalDurationMinutes` **no** incluye `TransitFromHotel`, `TransitToHotel`, ni `InterBlockTransit`. Solo suma actividades + transit interno.

### 2.3 `ActivityNode`

Ubicacion: `SmartTripPlanner.Domain/AggregatesModel/ActivityNode.cs`

Cada visita dentro de un bloque.

```
SequenceOrder         : int (1-based dentro del bloque)
PlaceId               : long (FK interna → Place.Id)
Name                  : string
DurationMinutes       : int (de Place.TypicalDurationMinutes)
IsIndoor              : bool
Priority              : Priority (High | Medium | Low)
Location              : PlaceLocation (copia de Place.Location)
TransitToNext         : TransitDetails? (null para la ultima actividad del bloque)
EstimatedArrival      : int (minutos desde medianoche; computado por TimelineScheduler)
EstimatedDeparture    : int (minutos desde medianoche; computado por TimelineScheduler)
IsCompleted           : bool (para Flow 4 / checklist)
```

**Nota:** `EstimatedArrival` y `EstimatedDeparture` son computados por `TimelineScheduler` (Phase 6) despues de que `TransitEnricher` calcula todos los tramos de transit.

### 2.4 `TransitDetails`

Ubicacion: `SmartTripPlanner.Domain/AggregatesModel/TransitDetails.cs`

```
TransportMode         : TransportMode (WALK_AND_PUBLIC_TRANSPORT | CAR)
DurationMinutes       : int
BufferMinutes         : int (default 10)
FrictionAlert         : bool (true si hay alerta de friccion, e.g. aparcamiento denso)
```

### 2.5 `TripPreferences`

Ubicacion: `SmartTripPlanner.Domain/AggregatesModel/TripPreferences.cs`

```
CarAvailable          : bool (default false)
MaxWalkingMinutes     : int (default 30)
WeatherAwareEnabled   : bool (default true)
ReturnToHotelStrategy : ReturnToHotelStrategy (default Always)
```

**`ReturnToHotelStrategy`:**
- **`Always`** (default): Cada bloque empieza y termina en el hotel. Hotel → Bloque → Hotel.
- **`Never`**: El itinerario fluye continuo entre bloques. BloqueA[ultima] → BloqueB[primera]. Solo la Noche vuelve al hotel.
- **`ProximityBased`**: En cada limite entre bloques, compara la ruta directa vs via hotel y elige la mas corta. Empate = favorece volver al hotel.

### 2.6 `TripPlanningConstants`

Ubicacion: `SmartTripPlanner.Domain/Constants/TripPlanningConstants.cs`

| Constante | Valor | Descripcion |
|-----------|-------|-------------|
| `MorningBlockDurationMinutes` | 210 | ~3.5h disponibles |
| `AfternoonBlockDurationMinutes` | 180 | ~3h disponibles |
| `EveningBlockDurationMinutes` | 105 | ~1.75h disponibles |
| `MaxVisitsPerMorningBlock` | 3 | Maximo de visitas |
| `MaxVisitsPerAfternoonBlock` | 3 | Maximo de visitas |
| `MaxVisitsPerEveningBlock` | 2 | Maximo de visitas |
| `DefaultTransitBufferMinutes` | 10 | Buffer entre actividades |
| `DefaultActivityBufferMinutes` | 15 | Buffer adicional por actividad |
| `ZoneRadiusKm` | 2.0 | Radio para clustering de zonas |
| `CarFasterThresholdMinutes` | 20 | Diferencia minima para preferir coche |
| `InterZoneThresholdKm` | 10.0 | Distancia que fuerza coche si hay coche disponible |
| `FamilyFriendlyBonus` | 15.0 | Puntaje extra si es family-friendly |
| `PopularityWeight` | 20.0 | Peso de popularidad (stub: hardcoded 0.5) |
| `DistancePenaltyWeight` | 5.0 | Penalizacion por distancia (Haversine) |
| `IndoorWeatherBonus` | 20.0 | Bonus indoor en dia malo |
| `OutdoorWeatherPenalty` | -20.0 | Penalizacion outdoor en dia malo |
| `MaxCandidatesPerCity` | 50 | Limite de candidatos por ciudad |
| `InterestAttributeKey` | "category" | Clave de atributo para filtrar intereses |

### 2.7 `OverConstrainedRouteException`

Ubicacion: `SmartTripPlanner.Domain/Exceptions/OverConstrainedRouteException.cs`

Lanzada cuando un must-see de prioridad `High` no puede ubicarse tras agotar el fallback por prioridad.

```csharp
public class OverConstrainedRouteException : SmartTripDomainException
{
    public IReadOnlyList<long> ConflictingPlaceIds { get; }
}
```

---

## 3. API REST

### 3.1 Contrato

```http
POST /api/trips/{tripId}/generate
```

**Request body:** Vacio (el `tripId` en la URL es suficiente).

**Response body:** `TripPlanResponse` (misma estructura que Flujo 0, pero con `Status = "GENERATED"` y `Days` poblado).

```json
{
  "tripId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "tripCode": "MAD-2026-7X9K",
  "cityId": 1,
  "cityCode": "madrid-es",
  "cityName": "Madrid",
  "startDate": "2026-07-15",
  "endDate": "2026-07-19",
  "baseHotel": { "name": "Hotel Example", "latitude": 40.4168, "longitude": -3.7038 },
  "travelers": { "adults": 2, "children": 1, "infants": 0 },
  "preferences": {
    "carAvailable": false,
    "maxWalkingMinutes": 30,
    "weatherAwareEnabled": true,
    "returnToHotelStrategy": "Always"
  },
  "mustSees": [...],
  "status": "GENERATED",
  "defaultStartHour": "09:00",
  "days": [
    {
      "dayIndex": 0,
      "date": "2026-07-15",
      "weatherSummary": "Clear",
      "blocks": [
        {
          "blockType": "Morning",
          "totalDurationMinutes": 195,
          "transitFromHotel": {
            "transportMode": "WALK_AND_PUBLIC_TRANSPORT",
            "durationMinutes": 12,
            "bufferMinutes": 10,
            "frictionAlert": false
          },
          "transitToHotel": {
            "transportMode": "WALK_AND_PUBLIC_TRANSPORT",
            "durationMinutes": 8,
            "bufferMinutes": 10,
            "frictionAlert": false
          },
          "interBlockTransit": null,
          "activities": [
            {
              "placeId": 42,
              "placeName": "Museo del Prado",
              "durationMinutes": 120,
              "sequenceOrder": 1,
              "isIndoor": true,
              "priority": "High",
              "transportMode": "WALK_AND_PUBLIC_TRANSPORT",
              "transitDurationMinutes": 15,
              "bufferMinutes": 10,
              "frictionAlert": false,
              "estimatedArrival": 562,
              "estimatedDeparture": 682
            }
          ]
        },
        { "blockType": "Afternoon", ... },
        { "blockType": "Evening", ... }
      ]
    }
  ]
}
```

### 3.2 Status Codes

| HTTP | Escenario |
|------|-----------|
| 200 OK | Itinerario generado exitosamente |
| 404 Not Found | `tripId` no existe |
| 422 Unprocessable Entity | `BaseHotel` es null; no hay candidatos; over-constrained |

### 3.3 Implementacion

- **Controller:** `TripsController.GenerateTripItinerary()` (`SmartTripPlanner.API`)
- **Handler:** `GenerateTripItineraryHandler` (`SmartTripPlanner.ApplicationServices`)
- **Command:** `GenerateTripItinerary(Guid TripId)`

---

## 4. Algoritmo Heuristico (6 Pasos)

El generador `HeuristicItineraryGenerator` ejecuta 6 pasos secuenciales, delegando cada fase a un colaborador inyectable.

```
[GenerateTripItineraryHandler]
│
├── 1. Validar BaseHotel != null
├── 2. Cargar candidatos: IPlaceRepository.GetManyByCityIdAsync(cityId, interests)
├── 3. Cargar clima: IWeatherProvider.GetWeatherAsync(cityId, start, end)
│
▼
[IItineraryGenerator.GenerateAsync(trip, candidates, weather)]
│
├── Paso 1: trip.GenerateDays() ──> N DayPlans vacios (Morning/Afternoon/Evening)
│
├── Paso 2: Pinned Placement ──> IPinnedMustSeePlacer.Place()
│     • Must-sees con PinnedDayIndex fijado
│     • Si PinnedBlock == null, prueba Morning → Afternoon → Evening
│     • Si el bloque objetivo esta lleno, intenta bloques adyacentes del mismo dia
│
├── Paso 3: Unpinned Placement ──> ZoneClusteringHelper.Cluster() + IUnpinnedMustSeePlacer.Place()
│     • Clustering geografico (greedy, radio 2.0 km)
│     • Por cluster, ordena dias por: abierto primero, mas slots libres
│     • Intenta Morning → Afternoon → Evening
│
├── Paso 4: Candidate Filling ──> ICandidateFiller.FillAsync()
│     • Filtra candidatos: no usados aun, abiertos ese dia
│     • Calcula distancia Haversine desde la actividad mas cercana del bloque
│     • Puntua via ICandidateScorer (family-friendly, popularidad, distancia, clima)
│     • Coloca hasta agotar capacidad del bloque
│
├── Paso 5: Transit & Weather Enrichment ──> ITransitEnricher.EnrichAsync()
│     • Asigna WeatherSummary por dia desde el diccionario
│     • Calcula transit entre actividades consecutivas del bloque
│     • Calcula TransitFromHotel y TransitToHotel por bloque
│     • Aplica ReturnToHotelStrategy:
│       - Always: Hotel → Bloque → Hotel (cada bloque independiente)
│       - Never: BloqueA[ultima] → BloqueB[primera] (InterBlockTransit)
│       - ProximityBased: elige directo o via hotel segun distancia mas corta
│     • Evening SIEMPRE retorna al hotel (fin de dia)
│
├── Paso 6: Timeline Scheduling ──> ITimelineScheduler.Schedule()
│     • Recorre cada bloque y computa EstimatedArrival / EstimatedDeparture
│     • Si TransitFromHotel != null: bloque empieza en DayPlan.StartTime
│     • Si InterBlockTransit != null: bloque empieza despues del anterior + inter-block
│     • Avanza: currentTime += Duration + TransitToNext.Duration + TransitToNext.Buffer
│
▼
[Guardar trip actualizado + retornar TripPlanResponse]
```

### 4.1 Colaboradores Inyectables (Domain Ports)

| Puerto / Interfaz | Implementacion | Responsabilidad |
|-------------------|----------------|-----------------|
| `IItineraryGenerator` | `HeuristicItineraryGenerator` | Orquesta los 6 pasos |
| `IPinnedMustSeePlacer` | `PinnedMustSeePlacer` | Ubica must-sees con dia/bloque fijado |
| `IUnpinnedMustSeePlacer` | `UnpinnedMustSeePlacer` | Ubica must-sees sin fijar |
| `ICandidateFiller` | `CandidateFiller` | Llena slots restantes |
| `ITransitEnricher` | `TransitEnricher` | Transit + clima + hotel transit + strategy |
| `ICandidateScorer` | `CandidateScorer` | Formula de puntaje por candidato |
| `ITransitCalculator` | `HaversineTransitCalculator` | Estimacion de duracion/modo de traslado |
| `IWeatherProvider` | `StubbedWeatherProvider` | Provee pronostico (stub MVP) |
| `ITimelineScheduler` | `TimelineScheduler` | Computa horarios exactos por actividad |

### 4.2 Regla de Seleccion de Transporte

Implementada en `TransitEnricher` con estimaciones de `HaversineTransitCalculator`:

1. **Distancia < 1.5 km** → siempre `WALK_AND_PUBLIC_TRANSPORT`.
2. **`CarAvailable == true`**:
   - Calcula estimacion PT+walk vs CAR.
   - Usa `CAR` si PT es **>= 20 min mas lento** O la distancia es **>= 10 km**.
3. **Default** → `WALK_AND_PUBLIC_TRANSPORT`.

El `HaversineTransitCalculator` usa distancia haversine / velocidad por modo:
- Caminar: 5 km/h
- Transporte publico: 15 km/h
- Coche: 30 km/h

**Nota:** No hay integracion con API de routing real. Es una heuristica basada en distancia en linea recta.

### 4.3 ReturnToHotelStrategy

Configurada via `TripPreferences.ReturnToHotelStrategy` (default `Always`).

#### Always (default)
```
Hotel → [Morning] → Hotel → [Afternoon] → Hotel → [Evening] → Hotel
```
Cada bloque es independiente. Se calculan `TransitFromHotel` y `TransitToHotel` para todos los bloques.

#### Never
```
Hotel → [Morning] → [Afternoon] → [Evening] → Hotel
```
- `TransitFromHotel` se calcula solo para el **primer bloque no vacio** del dia.
- `TransitToHotel` se calcula solo para la **Noche** (fin de dia).
- En los limites Mañana↔Tarde y Tarde↔Noche: se calcula `InterBlockTransit` (directo entre ultima y primera actividad).
- Si un bloque esta vacio, el siguiente bloque recalcula `TransitFromHotel` desde el hotel.

#### ProximityBased
En cada limite entre bloques con actividades:
```csharp
var direct = await AssignTransitAsync(lastActivity.Location, nextFirstActivity.Location, preferences, ct);
var toHotel = await AssignTransitAsync(lastActivity.Location, hotelLocation, preferences, ct);
var fromHotel = await AssignTransitAsync(hotelLocation, nextFirstActivity.Location, preferences, ct);

var directTotal = direct.DurationMinutes + direct.BufferMinutes;
var viaHotelTotal = toHotel.DurationMinutes + toHotel.BufferMinutes 
                  + fromHotel.DurationMinutes + fromHotel.BufferMinutes;

// Elige la ruta mas corta. Empate (<=) favorece volver al hotel.
if (viaHotelTotal <= directTotal) {
    currentBlock.TransitToHotel = toHotel;
    nextBlock.TransitFromHotel = fromHotel;
} else {
    currentBlock.InterBlockTransit = direct;
}
```

### 4.4 Timeline Scheduling

`TimelineScheduler.Schedule(trip)` es **sincrono puro** (sin I/O). Recorre cada `DayPlan` y cada `BlockTimeline`:

```csharp
var currentTime = dayPlan.StartTime.ToMinutes(); // default 540 = 09:00

foreach (var block in dayPlan.Blocks)
{
    if (block.TransitFromHotel != null)
        currentTime = dayPlan.StartTime.ToMinutes() + block.TransitFromHotel.DurationMinutes + block.TransitFromHotel.BufferMinutes;
    else if (block.InterBlockTransit != null)
        currentTime += block.InterBlockTransit.DurationMinutes + block.InterBlockTransit.BufferMinutes;
    
    foreach (var activity in block.Activities)
    {
        activity.EstimatedArrival = currentTime;
        activity.EstimatedDeparture = currentTime + activity.DurationMinutes;
        
        if (activity.TransitToNext != null)
            currentTime = activity.EstimatedDeparture + activity.TransitToNext.DurationMinutes + activity.TransitToNext.BufferMinutes;
    }
}
```

**MVP limitation:** Cada bloque arranca independiente (no se encadenan Mañana→Tarde a menos que `InterBlockTransit` lo indique). Los tiempos son estimados, no horarios fijos de compromiso.

### 4.5 Formula de Scoring de Candidatos

`CandidateScorer.Score(Place, ScoringContext)`:

```
score = (IsFamilyTrip && place.IsFamilyFriendly ? FamilyFriendlyBonus : 0)
      + (PopularityRaw * PopularityWeight)
      - (DistanceFromBlockCenterKm * DistancePenaltyWeight)
      + (IsBadWeather ? (place.IsIndoor ? IndoorWeatherBonus : OutdoorWeatherPenalty) : 0)
```

Donde `ScoringContext` recibe:
- `DistanceFromBlockCenterKm` = distancia Haversine desde la actividad mas cercana ya colocada en el bloque (0 si el bloque esta vacio).
- `PopularityRaw` = **hardcoded a 0.5** (stub; `Place` no tiene campo de popularidad).

### 4.6 Fallback por Prioridad en Overflow

Si no todos los lugares caben:

1. Se intenta colocar todos los must-sees primero (pinned, luego unpinned).
2. Si un must-see de prioridad `Low` no cabe, se descarta silenciosamente.
3. Si un must-see de prioridad `Medium` no cabe, se descarta despues de agotar los `Low`.
4. Si un must-see de prioridad `High` no cabe tras descartar `Low` y `Medium` → lanza `OverConstrainedRouteException` con los `PlaceId` conflictivos.

---

## 5. Criterios de Aceptacion Tecnicos

1. **Separacion de endpoints:** `POST /api/trips` crea el trip vacio; `POST /api/trips/{id}/generate` produce el itinerario.
2. **Bloques por dia:** Cada `DayPlan` tiene exactamente 3 `BlockTimeline`: Morning, Afternoon, Evening.
3. **Pinned must-sees:** Aparecen en el dia y bloque especificado. Si el bloque esta lleno, intenta adyacentes del mismo dia.
4. **Unpinned must-sees:** Respetan `OpeningHoursWindow.DayOfWeek`. Se clusterizan por proximidad geografica (2.0 km).
5. **Candidatos por intereses:** Si `TripPreferences.Interests` tiene valores, se filtran candidatos por atributo `category`. Si no hay coincidencias, fallback a todos los candidatos de la ciudad.
6. **Clima:** Cuando `WeatherAwareEnabled == true` y `WeatherSummary == Bad`, los candidatos indoor reciben bonus +20 y outdoor penalizacion -20.
7. **Transporte:** Default `WALK_AND_PUBLIC_TRANSPORT`. Cambia a `CAR` solo si el usuario tiene coche y la diferencia es >= 20 min o distancia >= 10 km.
8. **Capacidad de bloque:** Morning <= 3 visitas / 210 min; Afternoon <= 3 / 180 min; Evening <= 2 / 105 min.
9. **Over-constrained:** Si un must-see `High` no cabe tras descartar `Low` y `Medium`, se lanza `OverConstrainedRouteException` con `ConflictingPlaceIds`.
10. **Todos los must-sees incluidos:** Salvo que sea fisicamente imposible (razon expuesta en la excepcion).
11. **Horarios exactos:** `EstimatedArrival` y `EstimatedDeparture` se computan para cada `ActivityNode` via `TimelineScheduler`.
12. **Hotel transit:** Cada bloque expone `TransitFromHotel` y `TransitToHotel` cuando aplica (segun `ReturnToHotelStrategy`).
13. **Inter-block transit:** `InterBlockTransit` se calcula en limites entre bloques cuando `ReturnToHotelStrategy` es `Never` o `ProximityBased` elige la ruta directa.
14. **Evening siempre retorna:** `TransitToHotel` se calcula siempre para el bloque Evening, independientemente de la estrategia.
15. **333 tests existentes continuan pasando** tras cualquier modificacion.

---

## 6. Decisiones de Diseno y Trade-offs

### 6.1 Por que un generador heuristico en lugar de OR-Tools?

- **Razon:** OR-Tools requiere modelado VRP complejo, matrices de distancia reales y mantenimiento de restricciones duras. Para el MVP, un heuristico pragmatico entrega resultados razonables en <100ms sin dependencias externas.
- **Consecuencia:** La solucion puede no ser matematicamente optima. Se documenta como limitacion conocida.

### 6.2 Por que separar creacion de trip de generacion de itinerario?

- **Razon:** Permite al usuario crear el viaje, revisar must-sees, y luego explicitamente solicitar la generacion. Facilita edicion intermedia y re-generacion.
- **Consecuencia:** El frontend debe hacer 2 llamadas: `POST /api/trips` seguido de `POST /api/trips/{id}/generate`.

### 6.3 Por que `PlaceLocation` esta embebido en `ActivityNode`?

- **Razon:** Evita lookups de `Place` durante el scoring y el calculo de transit. El generador trabaja solo con `ActivityNode.Location`.
- **Consecuencia:** Si `Place.Location` cambia post-generacion, el itinerario no se actualiza automaticamente. Requiere re-generacion.

### 6.4 Por que ReturnToHotelStrategy en lugar de optimizar automaticamente?

- **Razon:** Diferentes familias tienen diferentes necesidades. Algunas prefieren volver al hotel a descansar/comer; otras quieren maximizar el tiempo turistico. Es una preferencia de usuario, no una optimizacion impuesta.
- **Consecuencia:** El frontend debe exponer esta opcion en el formulario de preferencias del viaje.

### 6.5 Por que cada bloque arranca en StartTime por defecto?

- **Razon:** Simplifica el scheduler MVP. Sin inter-block transit, cada bloque es independiente. Con inter-block transit, el scheduler encadena automaticamente.
- **Consecuencia:** Los horarios mostrados son estimaciones, no compromisos rigidos. La familia puede ajustar el ritmo real.

---

## 7. Gaps Identificados (Pendientes)

### 7.1 `PopularityRaw` hardcodeado a 0.5

**Estado:** `CandidateFiller` pasa `PopularityRaw = 0.5` a `CandidateScorer`. La entidad `Place` no tiene campo de popularidad.

**Impacto:** El scoring no refleja popularidad real del lugar.

**Fix requerido:** Agregar `Popularity` (double 0..1) a `Place` y poblarlo desde Foursquare o el pipeline de enriquecimiento (Flow 3).

**Prioridad:** Baja (MVP funciona sin ello).

### 7.2 `MaxWalkingMinutes` no se usa en el calculo de transporte

**Estado:** `TripPreferences.MaxWalkingMinutes` existe en el request/response pero `TransitEnricher` no lo consulta.

**Impacto:** El usuario puede indicar que no quiere caminar mas de X minutos, pero el sistema ignora esa preferencia.

**Fix requerido:** En `TransitEnricher`, si `CarAvailable == false` y la distancia a pie supera `MaxWalkingMinutes`, forzar uso de transporte publico (o lanzar advertencia).

**Prioridad:** Baja.

### 7.3 `WeatherProvider` es un stub

**Estado:** `StubbedWeatherProvider` devuelve `WeatherCondition.Clear` para todas las fechas.

**Impacto:** La logica de indoor/outdoor por clima nunca se activa en produccion real.

**Fix requerido:** Implementar integracion con API meteorologica (OpenWeatherMap, etc.).

**Prioridad:** Media.

### 7.4 Encadenamiento de bloques limitado al MVP

**Estado:** `TimelineScheduler` encadena bloques cuando hay `InterBlockTransit`, pero no optimiza el `DayPlan.StartTime` de bloques posteriores si el dia se "estira".

**Impacto:** Si Mañana y Tarde estan muy cargadas, el scheduler puede mostrar horarios que exceden las 21:00 sin advertencia.

**Fix requerido:** Agregar validacion de "hora limite del dia" (ej. 21:00) y ajustar/alertar si el itinerario excede el horario razonable.

**Prioridad:** Baja.

---

## 8. Tests de Cobertura

| Suite | Clase | Cobertura |
|-------|-------|-----------|
| Domain | `HeuristicItineraryGeneratorTests` | ~24 casos: pinned, unpinned, clima, capacidad, over-constrained, transporte, hotel transit, timeline scheduling |
| Domain | `PinnedMustSeePlacerTests` | 5 casos: dia/bloque correcto, sin bloque, dia invalido, overflow, bloques llenos |
| Domain | `UnpinnedMustSeePlacerTests` | 4 casos: primer dia, dia cerrado, dias llenos, slots libres |
| Domain | `CandidateFillerTests` | 5 casos: pool vacio, colocacion, Haversine real, scoring, distancia cero |
| Domain | `TransitEnricherTests` | 11 casos: clima, transit consecutivo, actividad unica, Location, hotel transit (Always/Never/ProximityBased), strategy boundaries |
| Domain | `TimelineSchedulerTests` | 11 casos: reset por TransitFromHotel, chaining por InterBlockTransit, mixed boundaries, empty blocks, hotel transit timing |
| Domain | `CandidateScorerTests` | Formula de scoring |
| Domain | `ZoneClusteringHelperTests` | Clustering geografico |
| Domain | `ReturnToHotelStrategyTests` | Enum values |
| Domain | `TripPreferencesTests` | Propiedad ReturnToHotelStrategy |
| Domain | `BlockTimelineTests` | InterBlockTransit property |
| Application | `GenerateTripItineraryHandlerTests` | 4 casos: not found, sin hotel, generar, re-generar |
| Application | `GenerateTripHandlerTests` | ~11 casos (Flujo 0) |
| API | `TripsControllerTests` | 4 casos: creacion, generacion, bloques con actividades, get trip |

**Total del proyecto:** 333 pasando, 0 fallos.

---

## 9. Referencias de Codigo

- `SmartTripPlanner.Domain/AggregatesModel/Trip.cs`
- `SmartTripPlanner.Domain/AggregatesModel/DayPlan.cs`
- `SmartTripPlanner.Domain/AggregatesModel/BlockTimeline.cs`
- `SmartTripPlanner.Domain/AggregatesModel/ActivityNode.cs`
- `SmartTripPlanner.Domain/AggregatesModel/TransitDetails.cs`
- `SmartTripPlanner.Domain/AggregatesModel/MustSee.cs`
- `SmartTripPlanner.Domain/AggregatesModel/TripPreferences.cs`
- `SmartTripPlanner.Domain/Enums/WeatherCondition.cs`
- `SmartTripPlanner.Domain/Enums/TransportMode.cs`
- `SmartTripPlanner.Domain/Enums/BlockType.cs`
- `SmartTripPlanner.Domain/Enums/Priority.cs`
- `SmartTripPlanner.Domain/Enums/ReturnToHotelStrategy.cs`
- `SmartTripPlanner.Domain/Constants/TripPlanningConstants.cs`
- `SmartTripPlanner.Domain/Exceptions/OverConstrainedRouteException.cs`
- `SmartTripPlanner.Domain/Ports/IItineraryGenerator.cs`
- `SmartTripPlanner.Domain/Ports/ICandidateScorer.cs`
- `SmartTripPlanner.Domain/Ports/ITransitCalculator.cs`
- `SmartTripPlanner.Domain/Ports/IWeatherProvider.cs`
- `SmartTripPlanner.Domain/Ports/ITimelineScheduler.cs`
- `SmartTripPlanner.Domain/Services/HeuristicItineraryGenerator.cs`
- `SmartTripPlanner.Domain/Services/PinnedMustSeePlacer.cs`
- `SmartTripPlanner.Domain/Services/UnpinnedMustSeePlacer.cs`
- `SmartTripPlanner.Domain/Services/CandidateFiller.cs`
- `SmartTripPlanner.Domain/Services/TransitEnricher.cs`
- `SmartTripPlanner.Domain/Services/CandidateScorer.cs`
- `SmartTripPlanner.Domain/Services/TimelineScheduler.cs`
- `SmartTripPlanner.Domain/Services/ZoneClusteringHelper.cs`
- `SmartTripPlanner.Domain/Services/ItineraryGeneratorHelpers.cs`
- `SmartTripPlanner.ApplicationServices/Handlers/GenerateTripItineraryHandler.cs`
- `SmartTripPlanner.ApplicationServices/Commands/GenerateTripItinerary.cs`
- `SmartTripPlanner.ApplicationServices/ApplicationServicesRegistration.cs`
- `SmartTripPlanner.API/Controllers/TripsController.cs`
- `SmartTripPlanner.API/Configurations/AutoMapperProfile.cs`
- `SmartTripPlanner.Infrastructure/Services/HaversineTransitCalculator.cs`
- `SmartTripPlanner.Infrastructure/Services/StubbedWeatherProvider.cs`
- `SmartTripPlanner.Infrastructure/Configurations/TripConfiguration.cs`

---

*Ultima actualizacion: 2026-06-21*
*Version: 2.1 (incluye ReturnToHotelStrategy, TimelineScheduler, hotel transit)*
