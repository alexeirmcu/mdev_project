# Backlog de Mejoras Post-MVP — Smart Trip Planner

## 1. Propósito

Este documento consolida las mejoras identificadas tras la finalización del MVP. Las items están organizadas por flujo funcional y área transversal, con priorización **MoSCoW** para guiar la planificación de iteraciones.

**Fecha de referencia:** 2026-06-22  
**Estado del MVP:** 4 flujos implementados, ~405 tests pasando.

---

## 2. Flujo 0 — Creación del Viaje

| ID | Mejora | Descripción | Justificación | Prioridad |
|----|--------|-------------|---------------|-----------|
| F0-1 | **OwnerUserId en Trip** | Agregar `OwnerUserId` (Guid/string) al agregado `Trip`. Filtrar todos los endpoints de trip por este owner. | Prerrequisito para autorización y multi-tenancy. Sin esto, cualquier usuario con el `tripId` puede acceder a cualquier viaje. | **Must** |
| F0-2 | **Edición colaborativa** | Permitir invitar colaboradores (`Collaborators[]`) con permisos de lectura/escritura. | Enga-familiar: padres comparten planificación. | **Could** |
| F0-3 | **Plantillas de viaje** | Flag `IsTemplate` o entidad `TripTemplate` para reutilizar configuraciones (ej: "Madrid 4 días familiar"). | Reduce fricción en creación de viajes recurrentes. | **Could** |
| F0-4 | **Validación geográfica del hotel** | Verificar que `BaseHotel.Location` caiga dentro del bounding box de la ciudad o a menos de X km del centro. | Previene errores de usuario (hotel en otra ciudad). | **Could** |

---

## 3. Flujo 1 — Descubrimiento de Lugares

| ID | Mejora | Descripción | Justificación | Prioridad |
|----|--------|-------------|---------------|-----------|
| F1-1 | **Filtros avanzados de búsqueda** | Extender `PlaceSearchRequest` con filtros por categoría, indoor/outdoor, family-friendly, duración máxima. | Mejora descubrimiento; reduce ruido en resultados. | **Must** |
| F1-2 | **Favoritos globales del usuario** | Lista personal de lugares favoritos, independiente de un trip. Importar favoritos como must-sees al crear un trip. | Desacopla descubrimiento de planificación; mejora retención. | **Should** |
| F1-3 | **Fotos y reseñas en resultados** | Consumir fotos y tips de Foursquare (o del enriquecimiento LLM) para mostrar en la respuesta de búsqueda. | Mejora UX de descubrimiento. Puede reutilizar enriquecimiento de Flow 3. | **Could** |

---

## 4. Flujo 2 — Generación de Itinerario

| ID | Mejora | Descripción | Justificación | Prioridad |
|----|--------|-------------|---------------|-----------|
| F2-1 | **Popularity real en scoring** | Reemplazar `PopularityRaw: 0.5` hardcodeado en `CandidateFiller` por `place.Popularity` enriquecido por LLM. | El scoring debe reflejar datos reales ya disponibles. Fix de 1 línea + tests. | **Must** |
| F2-2 | **`MaxWalkingMinutes` efectivo** | Hacer que `TransitEnricher` respete `TripPreferences.MaxWalkingMinutes`. Si la distancia a pie excede el límite, evitar modo walk o advertir. | Preferencia de usuario ignorada hoy. Impacta experiencia de familias con niños pequeños. | **Must** |
| F2-3 | **Weather provider real** | Reemplazar `StubbedWeatherProvider` por integración con API meteorológica (OpenWeatherMap, etc.). | Sin esto, la lógica indoor/outdoor por clima nunca se activa. | **Must** |
| F2-4 | **Re-generación de un solo día** | Permitir `POST /api/trips/{tripId}/days/{dayIndex}/regenerate` para recalcular un día específico sin tocar los demás. | Escenario real: un día se descarrila, el resto está bien. | **Must** |
| F2-5 | **Límite de horario del día** | Validar que el itinerario no exceda una hora límite razonable (ej. 21:00). Alertar si se estira. | Evita itinerarios poco realistas. | **Could** |

---

## 5. Flujo 3 — Enriquecimiento LLM

| ID | Mejora | Descripción | Justificación | Prioridad |
|----|--------|-------------|---------------|-----------|
| F3-1 | **Categorías semánticas por LLM** | Extender el schema JSON del LLM para que también sugiera tags/categorías semánticas ("arte", "historia", "ciencia"). | Mejora búsqueda y filtrado en Flow 1. | **Could** |
| F3-2 | **Circuit breaker para LLM** | Agregar circuit breaker (Polly) sobre el cliente de LLM para evitar quemar cuota ante fallos masivos. | Protección de costos y estabilidad. | **Could** |
| F3-3 | **Dashboard de enriquecimiento** | Endpoint de estado: "35/50 lugares enriquecidos, 12 pendientes, 3 fallidos". | Transparencia para debugging y UX. | **Could** |

---

## 6. Flujo 4 — Ejecución del Día

| ID | Mejora | Descripción | Justificación | Prioridad |
|----|--------|-------------|---------------|-----------|
| F4-1 | **Checklist API persistente** | Endpoint `PATCH /api/trips/{tripId}/days/{dayIndex}/activities/{placeId}/complete` para marcar `IsCompleted`. | Persiste progreso del día. Hoy el campo existe pero no se expone. | **Could** |
| F4-2 | **Replan manual del resto del día** | Endpoint `POST /api/trips/{tripId}/replan` que recalcule el itinerario desde el bloque actual en adelante, respetando actividades ya completadas. | Core del "día en ejecución". MVP lo menciona pero no está implementado. | **Must** |
| F4-3 | **Replan por clima en tiempo real** | Botón de "swapear a indoor" que reemplace actividades futuras del día por alternativas indoor, manteniendo must-sees. | Adaptación real ante cambio de clima. | **Must** |
| F4-4 | **Tracking de atraso automático** | Detectar si la familia está corriendo tarde vs `EstimatedArrival` y sugerir eliminar nice-to-haves. | "Smart assistant" del día. | **Could** |

---

## 7. Cross-Cutting

| ID | Mejora | Descripción | Justificación | Prioridad |
|----|--------|-------------|---------------|-----------|
| CX-1 | **Autorización por OwnerUser** | Garantizar que solo `OwnerUserId` (y colaboradores autorizados) pueda leer/escribir un `Trip`. Middleware o behavior de autorización. | Seguridad. Sin esto, los trips son públicos por `tripId`. | **Must** |
| CX-2 | **Logs de trazabilidad** | Structured logging (Serilog) con correlation ID por request. Loguear puntos clave: generación de itinerario, llamadas a Foursquare, enriquecimiento LLM. | Observabilidad y debugging en producción. | **Must** |

---

## 8. Infraestructura / DevOps

| ID | Mejora | Descripción | Justificación | Prioridad |
|----|--------|-------------|---------------|-----------|
| INF-1 | **Containerización** | Dockerfile + docker-compose para API + PostgreSQL. | Prerrequisito para cualquier despliegue consistente. | **Must** |
| INF-2 | **CI/CD pipeline** | GitHub Actions: build, test, docker build & push. | Automatización y calidad gate. | **Must** |
| INF-3 | **Despliegue en tier gratuito** | Evaluar y documentar opciones de hosting free:  <br>• **Render** (Web Service free tier + PostgreSQL free) <br>• **Railway** (starter credits) <br>• **Fly.io** (free allowances) <br>• **Azure Container Apps** (free tier limitado) | MVP debe ser demostrable sin costo. Análisis de límites (sleeping, cold start, DB size). | **Must** |

**Nota:** El proyecto ya usa PostgreSQL real (no EF InMemory en producción). La mejora INF-1 asume PostgreSQL persistente.

---

## 9. Features Post-MVP

| ID | Mejora | Descripción | Justificación | Prioridad |
|----|--------|-------------|---------------|-----------|
| PM-1 | **Multi-ciudad / cambio de hotel** | Itinerario que abarque varias ciudades con cambio de base hotel. | Expande mercado a viajes tipo road-trip. | **Could** |
| PM-2 | **Pagos / reservas** | Integración con APIs de booking para reservar entradas o restaurantes. | Monetización. | **Won't** (futuro lejano) |
| PM-3 | **Comunidad de reseñas** | Usuarios dejan reseñas de lugares; alimentan scoring. | Engagement y datos propios. | **Won't** (futuro lejano) |

---

## 10. Resumen Consolidado por Prioridad

### Must (Próxima iteración)
- **F0-1** — OwnerUserId en Trip
- **F1-1** — Filtros avanzados de búsqueda
- **F2-1** — Popularity real en scoring
- **F2-2** — MaxWalkingMinutes efectivo
- **F2-3** — Weather provider real
- **F2-4** — Re-generación de un solo día
- **F4-2** — Replan manual del resto del día
- **F4-3** — Replan por clima en tiempo real
- **CX-1** — Autorización por OwnerUser
- **CX-2** — Logs de trazabilidad
- **INF-1** — Containerización
- **INF-2** — CI/CD pipeline
- **INF-3** — Despliegue en tier gratuito

### Should (Iteración siguiente)
- **F1-2** — Favoritos globales del usuario

### Could (Backlog, a conveniencia)
- **F0-2** — Edición colaborativa
- **F0-3** — Plantillas de viaje
- **F0-4** — Validación geográfica del hotel
- **F1-3** — Fotos y reseñas en resultados
- **F2-5** — Límite de horario del día
- **F3-1** — Categorías semánticas por LLM
- **F3-2** — Circuit breaker para LLM
- **F3-3** — Dashboard de enriquecimiento
- **F4-1** — Checklist API persistente
- **F4-4** — Tracking de atraso automático
- **PM-1** — Multi-ciudad / cambio de hotel

### Won't (Futuro lejano / no planificado)
- **PM-2** — Pagos / reservas
- **PM-3** — Comunidad de reseñas

---

*Documento generado el 2026-06-22. Sujeto a revisión y re-priorización según feedback de usuarios y métricas de uso.*
