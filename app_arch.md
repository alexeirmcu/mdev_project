# Arquitectura conceptual — Travel Planner App con Planner + Tools

## Visión general

La aplicación sigue una arquitectura híbrida:

- **Planner determinista** para generar y validar itinerarios
- **Tool layer** para consultar conocimiento y datos operativos externos
- **LLM** para orquestar tools, explicar decisiones, sugerir alternativas y responder preguntas libres
- **Backend** como fuente de verdad y capa de validación final

---

## Diagrama conceptual

```text
┌──────────────────────────────────────────────────────────────────────────────┐
│                           CLIENT APPS                                       │
│                                                                              │
│  Mobile App / Web App                                                        │
│  - Crear viaje                                                               │
│  - Buscar must-sees                                                          │
│  - Ver itinerario por bloques                                                │
│  - Checklist del día                                                         │
│  - Replan manual                                                             │
│  - Preguntas libres ("qué hacemos si llueve?")                              │
└──────────────────────────────┬───────────────────────────────────────────────┘
                               │ HTTPS / REST
                               ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│                         API BACKEND (JAVA)                                   │
│                    Spring Boot / Modular Monolith                            │
│                                                                              │
│  Modules:                                                                    │
│  - trip                                                                      │
│  - place                                                                     │
│  - planning                                                                  │
│  - transport                                                                 │
│  - weather                                                                   │
│  - knowledge                                                                 │
│  - user/auth                                                                 │
└───────────────┬──────────────────────────────┬───────────────────────────────┘
                │                              │
                │                              │
                ▼                              ▼
┌───────────────────────────────┐   ┌─────────────────────────────────────────┐
│      PLANNER / RULE ENGINE    │   │           AI / TOOL LAYER              │
│                               │   │                                         │
│  - ItineraryGenerationService │   │  - ToolOrchestrationService            │
│  - ReplanningService          │   │  - PlaceSearchToolAdapter              │
│  - BlockCapacityPolicy        │   │  - RouteLookupToolAdapter              │
│  - PlaceScoringService        │   │  - WeatherLookupToolAdapter            │
│  - TransportDecisionService   │   │  - PromptBuilder                       │
│  - WeatherSwapPolicy          │   │  - RecommendationAssistant             │
│                               │   │  - ExplanationGenerator                │
│  Deterministic logic for:     │   │                                         │
│  - must-sees first            │   │  Uses:                                 │
│  - opening hours              │   │  - tool/function calling               │
│  - travel time                │   │  - LLM                                 │
│  - block fit                  │   │  - structured API responses            │
│  - car vs TP                  │   │                                         │
│  - replan validation          │   └─────────────────────────────────────────┘
└───────────────┬───────────────┘
                │
                │ uses
                ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│                    OPERATIONAL DATA LAYER                                    │
│                         PostgreSQL (+ PostGIS)                               │
│                                                                              │
│  Structured entities:                                                        │
│  - trips                                                                     │
│  - trip_days                                                                 │
│  - must_sees                                                                 │
│  - places                                                                    │
│  - itinerary_days                                                            │
│  - itinerary_blocks                                                          │
│  - itinerary_items                                                           │
│  - checklist_status                                                          │
│  - replan_events                                                             │
│  - weather_mode                                                              │
│  - manual_enrichments                                                        │
└───────────────┬──────────────────────────────────────────────────────────────┘
                │
                │ curated enrichment / product knowledge
                ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│                    CURATED KNOWLEDGE LAYER                                   │
│                         PostgreSQL tables                                    │
│                                                                              │
│  Product-owned knowledge:                                                    │
│  - PlaceProfile                                                              │
│  - AreaGuide                                                                 │
│  - ScenarioGuide                                                             │
│  - CityPlaybook                                                              │
│  - PlanningHeuristicNote                                                     │
│                                                                              │
│  Example attributes:                                                         │
│  - city                                                                      │
│  - place_id                                                                  │
│  - indoor_outdoor                                                            │
│  - weather_fit                                                               │
│  - family_fit                                                                │
│  - duration_band                                                             │
│  - recommended_block                                                         │
│  - area_name                                                                 │
└───────────────┬──────────────────────────────────────────────────────────────┘
                │
                │ generation / orchestration
                ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│                    EXTERNAL AI SERVICES                                      │
│                                                                              │
│  - LLM Provider                                                              │
│                                                                              │
│  Used for:                                                                   │
│  - tool selection / orchestration                                            │
│  - explanation generation                                                    │
│  - suggesting alternatives                                                   │
│  - summarizing structured and curated knowledge                              │
└───────────────┬──────────────────────────────────────────────────────────────┘
                │
                │ factual integrations
                ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│                     EXTERNAL OPERATIONAL APIS                                │
│                                                                              │
│  - Places / Maps API                                                         │
│  - Routing / Travel time API                                                 │
│  - Weather API                                                               │
│  - Optional: reviews / popularity signals APIs                               │
│                                                                              │
│  Used for:                                                                   │
│  - search places                                                             │
│  - geocoding                                                                 │
│  - route durations                                                           │
│  - weather conditions                                                        │
│  - opening hours if available                                                │
│  - popularity / social proof signals if available                            │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## Idea principal

La arquitectura separa claramente tres responsabilidades:

### 1. Planner
Responsable de:
- generar el itinerario
- respetar restricciones
- validar capacidad por bloque
- decidir transporte
- replanificar de forma consistente

Es la capa:
- determinista
- auditable
- controlable

### 2. Tool layer
Responsable de:
- consultar fuentes externas bajo demanda
- recuperar hechos operativos actualizados
- buscar lugares, rutas, clima y señales externas
- entregar resultados estructurados al sistema

Es la capa:
- conectada a datos vivos
- orientada a integración
- basada en tools/function calling

### 3. LLM
Responsable de:
- decidir qué tools usar en una consulta libre
- resumir y explicar resultados
- proponer alternativas razonables
- traducir respuestas técnicas a UX conversacional

Es la capa:
- flexible
- conversacional
- orientada a asistencia

---

## Principio clave

**El LLM nunca debería escribir directamente el itinerario final.**

Siempre debe pasar por el planner y por la validación del backend.

### Flujo correcto
1. El usuario pide plan o hace una pregunta
2. El sistema consulta tools/APIs relevantes si hace falta
3. El LLM resume o propone opciones
4. El planner valida restricciones y genera/ajusta el resultado
5. El backend persiste el resultado válido

---

## Qué hace cada capa

### Client Apps
Interfaz para:
- crear viajes
- buscar must-sees
- visualizar bloques
- marcar checklist
- lanzar replan
- hacer preguntas libres

### API Backend
Coordina todos los módulos y expone la API REST.

### Planner / Rule Engine
Implementa la lógica principal del producto:
- prioridad de must-sees
- agrupación por zonas
- validación de horarios
- cálculo de bloques
- decisión de transporte
- replanificación

### AI / Tool Layer
Recupera datos externos y los pone a disposición del sistema para:
- búsqueda de lugares
- consulta de clima
- consulta de rutas/tiempos
- enriquecimiento de respuestas
- soporte a preguntas abiertas

### Operational Data Layer
Guarda la verdad operativa del sistema:
- viajes
- lugares
- bloques
- items
- checklist
- eventos de replan

### Curated Knowledge Layer
Guarda conocimiento propio del producto:
- perfiles de lugares enriquecidos
- guías de zonas
- escenarios
- playbooks de ciudad
- heurísticas de planificación

### External AI Services
Se usan para:
- tool calling
- generación de texto
- explicaciones
- resumen de resultados

### External Operational APIs
Se usan para hechos operativos:
- búsqueda de lugares
- geocoding
- tiempos de trayecto
- clima
- horarios si existen

---

## Resumen

La app debería funcionar con este principio:

- **Planner = cerebro operativo**
- **Tools/APIs = acceso a hechos externos**
- **LLM = capa de interacción inteligente**
- **Backend = validador y fuente de verdad**
- **Conocimiento curado = diferenciación de producto**

---

## Versión simplificada para MVP

```text
Cliente
  ↓
Spring Boot Backend
  ├── Planner
  ├── PostgreSQL
  ├── Knowledge module
  ├── Tool adapters
  └── LLM orchestration
       ↓
 Places / Routing / Weather APIs
```

---

## Beneficio de esta arquitectura

Permite que la app:

- use datos actualizados sin depender de un índice vectorial desde el día 1
- mantenga la lógica crítica en una capa determinista
- use IA donde realmente aporta valor
- sea más explicable
- sea más robusta para un MVP
- permita introducir RAG más adelante solo si el conocimiento curado crece lo suficiente
