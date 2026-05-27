# Arquitectura conceptual — Travel Planner App con Planner + RAG

## Visión general

La aplicación sigue una arquitectura híbrida:

- **Planner determinista** para generar y validar itinerarios
- **RAG** para recuperar conocimiento contextual
- **LLM** para explicaciones, sugerencias y propuestas de alternativas
- **Backend** como fuente de verdad y capa de validación final

---

## Diagrama conceptual

```text
┌─────────────────────────────────────────────────────────────────────┐
│                           CLIENT APPS                              │
│                                                                     │
│  Mobile App / Web App                                               │
│  - Crear viaje                                                      │
│  - Buscar must-sees                                                 │
│  - Ver itinerario por bloques                                       │
│  - Checklist del día                                                │
│  - Replan manual                                                    │
│  - Preguntas libres ("qué hacemos si llueve?")                      │
└──────────────────────────────┬──────────────────────────────────────┘
                               │ HTTPS / REST
                               ▼
┌─────────────────────────────────────────────────────────────────────┐
│                         API BACKEND (JAVA)                          │
│                    Spring Boot / Modular Monolith                   │
│                                                                     │
│  Modules:                                                           │
│  - trip                                                             │
│  - place                                                            │
│  - planning                                                         │
│  - transport                                                        │
│  - weather                                                          │
│  - knowledge                                                        │
│  - user/auth                                                        │
└───────────────┬──────────────────────────────┬──────────────────────┘
                │                              │
                │                              │
                ▼                              ▼
┌───────────────────────────────┐   ┌────────────────────────────────┐
│      PLANNER / RULE ENGINE    │   │       RAG / AI LAYER           │
│                               │   │                                │
│  - ItineraryGenerationService │   │  - KnowledgeRetrievalService   │
│  - ReplanningService          │   │  - EmbeddingSearch             │
│  - BlockCapacityPolicy        │   │  - PromptBuilder               │
│  - PlaceScoringService        │   │  - RecommendationAssistant     │
│  - TransportDecisionService   │   │  - ExplanationGenerator        │
│  - WeatherSwapPolicy          │   │                                │
│                               │   │  Uses:                         │
│  Deterministic logic for:     │   │  - Vector search              │
│  - must-sees first            │   │  - LLM                        │
│  - opening hours              │   │  - metadata filters           │
│  - travel time                │   │                                │
│  - block fit                  │   └────────────────────────────────┘
│  - car vs TP                  │
│  - replan validation          │
└───────────────┬───────────────┘
                │
                │ uses
                ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    OPERATIONAL DATA LAYER                          │
│                         PostgreSQL (+ PostGIS)                     │
│                                                                     │
│  Structured entities:                                               │
│  - trips                                                            │
│  - trip_days                                                        │
│  - must_sees                                                        │
│  - places                                                           │
│  - itinerary_days                                                   │
│  - itinerary_blocks                                                 │
│  - itinerary_items                                                  │
│  - checklist_status                                                 │
│  - replan_events                                                    │
│  - weather_mode                                                     │
│  - manual_enrichments                                               │
└───────────────┬─────────────────────────────────────────────────────┘
                │
                │ related knowledge / enrichment
                ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     KNOWLEDGE / VECTOR LAYER                       │
│                    PostgreSQL + pgvector (or Qdrant)               │
│                                                                     │
│  RAG documents/chunks:                                              │
│  - PlaceProfile                                                     │
│  - AreaGuide                                                        │
│  - ScenarioGuide                                                    │
│  - CityPlaybook                                                     │
│  - PlanningHeuristicNote                                            │
│                                                                     │
│  Metadata examples:                                                 │
│  - city                                                             │
│  - place_id                                                         │
│  - indoor_outdoor                                                   │
│  - weather_fit                                                      │
│  - family_fit                                                       │
│  - duration_band                                                    │
│  - recommended_block                                                │
│  - area_name                                                        │
└───────────────┬─────────────────────────────────────────────────────┘
                │
                │ embeddings / generation
                ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    EXTERNAL AI / KNOWLEDGE SERVICES                │
│                                                                     │
│  - Embedding Model                                                  │
│  - LLM Provider                                                     │
│                                                                     │
│  Used for:                                                          │
│  - generate document embeddings                                     │
│  - produce explanations                                             │
│  - suggest alternatives                                             │
│  - summarize curated knowledge                                      │
└───────────────┬─────────────────────────────────────────────────────┘
                │
                │ factual integrations
                ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     EXTERNAL OPERATIONAL APIS                      │
│                                                                     │
│  - Places / Maps API                                                │
│  - Routing / Travel time API                                        │
│  - Weather API                                                      │
│                                                                     │
│  Used for:                                                          │
│  - search places                                                    │
│  - geocoding                                                        │
│  - route durations                                                  │
│  - weather conditions                                               │
│  - opening hours if available                                       │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Idea principal

La arquitectura separa claramente dos responsabilidades:

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

### 2. RAG
Responsable de:
- recuperar conocimiento contextual
- enriquecer recomendaciones
- explicar decisiones
- proponer alternativas razonables

Es la capa:
- semántica
- flexible
- orientada a IA

---

## Principio clave

**El LLM/RAG nunca debería escribir directamente el itinerario final.**

Siempre debe pasar por el planner y por la validación del backend.

### Flujo correcto
1. RAG recupera contexto relevante
2. LLM propone explicación o alternativas
3. Planner valida restricciones
4. Backend persiste el resultado válido

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

### RAG / AI Layer
Recupera conocimiento semántico y alimenta al LLM para:
- explicaciones
- sugerencias
- alternativas
- respuestas abiertas

### Operational Data Layer
Guarda la verdad operativa del sistema:
- viajes
- lugares
- bloques
- items
- checklist
- eventos de replan

### Knowledge / Vector Layer
Guarda el conocimiento semántico:
- perfiles de lugares
- guías de zonas
- escenarios
- playbooks de ciudad
- heurísticas de planificación

### External AI Services
Se usan para:
- embeddings
- generación de texto
- resumen de conocimiento curado

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
- **RAG = memoria semántica**
- **LLM = capa de interacción inteligente**
- **Backend = validador y fuente de verdad**

---

## Versión simplificada para MVP

```text
Cliente
  ↓
Spring Boot Backend
  ├── Planner
  ├── PostgreSQL
  ├── Knowledge module
  └── pgvector
       ↓
   LLM / Embeddings
       ↓
 Maps / Weather APIs
```

---

## Beneficio de esta arquitectura

Permite que la app:

- funcione bien incluso sin IA generativa en el núcleo
- use IA donde realmente aporta valor
- sea más explicable
- sea más robusta
- sea defendible como proyecto de MSc/TFM
