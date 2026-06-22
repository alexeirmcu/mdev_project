# Requisitos Técnicos — Flujo 3: Pipeline de Enriquecimiento Asíncrono (LLM Background Enricher)

## 1. Resumen del Flujo

Este flujo se dispara de manera asíncrona inmediatamente después de que el comando `GenerateTripItineraryHandler` confirma que el itinerario ha sido calculado con éxito y guardado en la base de datos.

Su propósito es identificar si en el viaje se incluyeron lugares cuyos metadatos semánticos provienen del "Mapeo de Emergencia" (heurísticas por defecto del Flujo 1). Si es así, encola mensajes en una tabla Outbox para que un servicio en segundo plano los procese invocando a un Modelo de Lenguaje (LLM) y persista el resultado para optimizar futuras búsquedas.

---

## 2. Diagrama del Proceso en Segundo Plano

```
[GenerateTripItineraryHandler]
         │
         ├── Genera itinerario ──> Guarda en BD
         │
         └── Filtra lugares no enriquecidos (IsEnriched == false)
                  │
                  ▼
         [OutboxWriter.EnqueueAsync]
                  │
                  ▼
         [LlmEnrichmentBackgroundService] (HostedService con polling)
                  │
                  ├── Reclama mensajes vencidos (lease timeout)
                  ├── Obtiene batch pendiente
                  │
                  ▼
         [LlmEnrichmentProcessor.ProcessAsync]
                  │
                  ├── Marca mensaje como Processing
                  ├── (Opcional) Consulta Foursquare Place Details + Tips
                  ├── Construye prompt y envía al LLM (Microsoft.Extensions.AI)
                  ├── Deserializa JSON de respuesta
                  ├── Valida rangos (Duration, Score, Popularity)
                  ├── Aplica enriquecimiento al Place (MarkEnriched)
                  └── Marca mensaje como Completed / Failed / Retry
```

---

## 3. Pasos Detallados del Pipeline de Enriquecimiento

### Paso 3.1: Detección de Lugares No Enriquecidos (Data Gaps)

1. El `GenerateTripItineraryHandler` recorre el itinerario recién generado y extrae todos los `PlaceId` de las actividades de cada bloque (Morning, Afternoon, Evening).
2. Cruza esos IDs contra la lista de candidatos que ya fueron cargados desde la BD local.
3. **Filtrado:** Selecciona únicamente aquellos lugares que **ya existen** en la BD local pero cuyo flag `IsEnriched` es `false`. Si todos los lugares ya están enriquecidos, no se encola nada.
4. De los lugares filtrados, extrae los `ProviderReferenceId` (ej. `fsq_id` de Foursquare), los deduplica y los pasa al `OutboxWriter`.

### Paso 3.2: Encolado en Outbox (Patron Outbox)

El `OutboxWriter` recibe una lista de `ProviderReferenceId` y realiza lo siguiente:

1. Consulta la tabla `OutboxMessages` buscando referencias que ya estén en estado `Pending` o `Processing`.
2. Para cada `ProviderReferenceId` que **no** tenga un mensaje pendiente, crea un nuevo `OutboxMessage` con:
   - `Status = Pending`
   - `RetryCount = 0`
   - `MaxRetries = 3`
   - `CreatedAt = UpdatedAt = UtcNow`
3. Agrega los mensajes al `DbContext` (sin llamar a `SaveChanges`; es responsabilidad del caller).

#### Estados del Mensaje

| Estado | Significado |
|--------|-------------|
| `Pending` | Listo para ser procesado. |
| `Processing` | Adquirido por el worker actual. |
| `Completed` | Procesado exitosamente. |
| `Failed` | Agotó los reintentos. |

#### Reintentos

- Si el procesamiento falla, se incrementa `RetryCount` y se programa el siguiente intento con **backoff exponencial**:
  - `NextAttemptAt = Now + (2^RetryCount * 30 segundos)`.
- Si `RetryCount >= MaxRetries`, el mensaje pasa a `Failed` con el mensaje de error.

### Paso 3.3: Procesamiento en Background (LlmEnrichmentBackgroundService)

Es un `BackgroundService` de .NET que corre en un loop infinito hasta recibir señal de cancelación:

1. **Reclamo de leases vencidos:** `ReclaimExpiredLeasesAsync` libera mensajes atascados en `Processing` cuyo lease expiró (configurable vía `LeaseTimeoutSeconds`).
2. **Adquisición de batch:** `GetPendingAsync` toma hasta `BatchSize` mensajes en estado `Pending`, los marca como `Processing` y les asigna un lease.
3. **Procesamiento:** para cada mensaje, invoca `ILlmEnrichmentProcessor.ProcessAsync(messageId)`.
4. **Delay:** espera `PollingIntervalSeconds` antes de la siguiente iteración.
5. **Manejo de errores:** los errores individuales se loguean y el loop continúa; no se detiene el servicio por un mensaje fallido.

### Paso 3.4: Recopilación de Contexto (Foursquare Details) — Condicional

Dentro del `LlmEnrichmentProcessor`, por cada mensaje:

1. Busca el `Place` en la BD local usando su `ProviderReferenceId`.
2. Si no se encuentra, marca el mensaje como `Failed` y termina.
3. Si la opción `UseFoursquarePremiumFields` está habilitada (`true`), realiza una llamada al endpoint de **Place Details** de Foursquare usando el `ProviderReferenceId` (`fsq_id`) con `includeTips = true`.
4. Extrae los **Tips/Reviews** del lugar. Si existen, los concatena con separador ` | ` para formar el `tipsText`.
5. Si `UseFoursquarePremiumFields` es `false`, el enriquecimiento se realiza **únicamente** con los metadatos locales que ya posee el `Place` (nombre, categorías, horarios).

### Paso 3.5: Invocación Semántica Estructurada (LLM Prompting)

El `PlaceEnrichmentPromptBuilder` construye un prompt estructurado a partir de los datos del `Place`:

- `Place: {Name}`
- `Categories: {valores de atributos con Key == "category"}`
- `Opening Hours:` (lista de días con horarios formateados HH:mm)
- `Visitor Tips: {tipsText}` (solo si se obtuvo de Foursquare)

El prompt finaliza exigiendo una respuesta en **JSON estricto** con el siguiente esquema:

```json
{
  "TypicalDurationMinutes": <int, 15-480>,
  "IsIndoor": <bool>,
  "FamilyFriendlyScore": <int, 1-5>,
  "Popularity": <double, 0.0-1.0>
}
```

> **Nota:** El campo `Popularity` no estaba en la versión original del spec y fue agregado en la implementación.

#### Cliente LLM

El sistema utiliza `Microsoft.Extensions.AI` (`IChatClient`) para enviar el prompt. Es **agnóstico al proveedor**; puede configurarse para Gemini, OpenAI u otro compatible. La llamada se realiza con:

- Rol de sistema: `"You are a place metadata assistant. Respond ONLY with valid JSON."`
- `ResponseFormat = ChatResponseFormat.Json`
- `ModelId` tomado de la configuración (`LlmApiOptions.Model`).

### Paso 3.6: Mapeo, Validación y Persistencia

1. La respuesta JSON del LLM se deserializa en un `PlaceEnrichmentResponse`.
2. Se validan los rangos:
   - `TypicalDurationMinutes`: 15 a 480.
   - `FamilyFriendlyScore`: 1 a 5.
   - `Popularity`: 0.0 a 1.0.
3. Si la validación falla, se lanza excepción y el mensaje se reencola o marca como fallido según reintentos.
4. Si la validación pasa, se invoca `place.MarkEnriched(...)` actualizando:
   - `TypicalDurationMinutes`
   - `IsIndoor`
   - `FamilyFriendlyScore`
   - `Popularity`
   - `IsEnriched = true`
5. Se marca el mensaje de Outbox como `Completed`.
6. Se ejecuta `SaveChangesAsync` en el `DbContext` para persistir tanto el `Place` enriquecido como el estado final del mensaje.

---

## 4. Entidades y Componentes Clave

### OutboxMessage

```csharp
public class OutboxMessage
{
    public Guid Id { get; private set; }
    public string PlaceProviderReferenceId { get; private set; }
    public OutboxMessageStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public int MaxRetries { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? NextAttemptAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public string? Error { get; private set; }
}
```

### Place (campos relevantes)

```csharp
public class Place
{
    public string ProviderReferenceId { get; private set; }  // fsq_id
    public int TypicalDurationMinutes { get; private set; } = 60;
    public bool IsIndoor { get; private set; } = false;
    public int FamilyFriendlyScore { get; private set; } = 3;
    public double Popularity { get; private set; } = 0.5;
    public bool IsEnriched { get; private set; } = false;
    public bool IsAutoUpdateEnabled { get; private set; } = true;
    public ICollection<PlaceAttribute> Attributes { get; private set; }
    public List<OpeningHoursWindow> OpeningHours { get; private set; }
}
```

### Opciones de Configuración

| Opción | Descripción | Default |
|--------|-------------|---------|
| `LlmEnrichmentOptions.BatchSize` | Cantidad de mensajes a procesar por iteración. | — |
| `LlmEnrichmentOptions.PollingIntervalSeconds` | Tiempo de espera entre polls del background service. | — |
| `LlmEnrichmentOptions.LeaseTimeoutSeconds` | Tiempo antes de que un mensaje en `Processing` pueda ser reclamado por otro worker. | — |
| `LlmEnrichmentOptions.UseFoursquarePremiumFields` | Habilita la consulta a Foursquare para obtener tips. | `false` |
| `LlmApiOptions.BaseUrl` | URL base del servicio LLM. | — |
| `LlmApiOptions.Model` | Identificador del modelo LLM. | — |

---

## 5. Diferencias respecto a versiones anteriores del spec

| Aspecto | Versión anterior | Implementación actual |
|---------|------------------|----------------------|
| **Disparador** | `GenerateTripHandler` | `GenerateTripItineraryHandler` |
| **Mecanismo async** | "Hilo asíncrono" directo | Patrón Outbox + `BackgroundService` con polling |
| **Criterio de filtrado** | IDs que **no existen** en BD local | Lugares que **existen** pero `IsEnriched == false` |
| **Schema JSON LLM** | Solo `typical_duration_minutes`, `is_indoor`, `family_friendly_score` | Agrega `Popularity` (0.0-1.0) |
| **Rango Duration** | 30-240 minutos | 15-480 minutos |
| **Consulta Foursquare** | Siempre | Condicional (`UseFoursquarePremiumFields`) |
| **Proveedor LLM** | Específico (Gemini) | Agnóstico (`Microsoft.Extensions.AI`) |
| **Idempotencia** | No definida | Evita duplicados en Outbox (`Pending`/`Processing`) |
| **Reintentos** | No definidos | Backoff exponencial, max 3 retries |
