# Requisitos Técnicos — Flujo 3: Pipeline de Enriquecimiento Asíncrono (LLM Background Enricher)

## 1. Resumen del Flujo
Este flujo se dispara de manera asíncrona inmediatamente después de que el comando `GenerateTripHandler` confirma que el itinerario ha sido calculado con éxito y guardado en la base de datos. 

Su propósito es identificar si en el viaje se incluyeron lugares (`PlaceId`) cuyos metadatos semánticos provienen del "Mapeo de Emergencia" (heurísticas por defecto del Flujo 1). Si es así, invoca en segundo plano a la API de un Modelo de Lenguaje (LLM) para extraer datos reales de logística familiar, persistiendo el resultado para optimizar futuras búsquedas.

---

## 2. Diagrama del Proceso en Segundo Plano

[GenerateTripHandler] ──(Dispara Hilo Asíncrono)──> [LLMEnricherService]
│
┌────────────────────────────────────────────────────────┘
▼

Filtrar IDs Externos (fsq_id no registrados en la BD Local)
│
▼

Consultar Foursquare (Place Details Completo: Nombre, Categoría, Tips)
│
▼

Enviar Prompt Estructurado al LLM (Gemini API)
│
▼

Mapear JSON a Entidad Dominio Place (Reemplaza valores por defecto)
│
▼

Persistir en Base de Datos Local ───> (Disponible para el Flujo 1)

---

## 3. Pasos Detallados del Pipeline de Enriquecimiento

### Paso 3.1: Detección de Brechas de Datos (Data Gaps)
1. El servicio en segundo plano recibe la lista de todos los `PlaceId` que componen el itinerario recién generado.
2. Cruza esta lista contra la tabla local de `Places` de la base de datos.
3. **Filtrado:** Selecciona únicamente aquellos identificadores que **no** existen en el almacenamiento local. Si todos los lugares ya estaban curados, el flujo termina de inmediato sin realizar llamadas externas.

### Paso 3.2: Recopilación de Contexto (Foursquare Details)
Por cada ID huérfano detectado, el servicio realiza una llamada al endpoint de *Place Details* de Foursquare utilizando el `fsq_id`. 
* Extrae el nombre oficial, la dirección, la categoría exacta y, fundamentalmente, los **"Tips/Reviews"** (reseñas cortas de usuarios). Este texto libre es el combustible semántico que procesará el LLM.

### Paso 3.3: Invocación Semántica Estructurada (LLM Prompting)
El servicio construye un prompt altamente optimizado y estructurado, exigiendo que la respuesta del LLM sea estrictamente un objeto **JSON** (usando la funcionalidad de *JSON Mode* o *Structured Outputs* de la API de Gemini) para evitar textos introductorios o alucinaciones.

#### Estructura del Prompt:
```text
Eres un experto en logística de viajes familiares en Europa. Analiza el siguiente lugar y sus reseñas para determinar tres variables cruciales para planificar un itinerario con niños pequeños.

LUGAR: [Nombre del Lugar]
CATEGORÍA: [Categoría Foursquare]
RESEÑAS SELECCIONADAS: [Tips de Foursquare]

Devuelve ESTRICTAMENTE un objeto JSON con la siguiente estructura:
{
  "typical_duration_minutes": int (tiempo promedio real que pasa una familia visitando este lugar, mínimo 30, máximo 240),
  "is_indoor": boolean (true si la mayor parte de la experiencia ocurre bajo techo, false si es al aire libre),
  "family_friendly_score": int (del 1 al 5, donde 1 es aburrido/inadecuado para niños y 5 es ideal, con facilidades para carritos o zonas interactivas)
}