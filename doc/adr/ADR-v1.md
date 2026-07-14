# Architecture Decision Record (ADR)

## ADR 001: Motor de Planificación Heurístico por Fases + Enriquecimiento LLM en .NET

### Estatus
Aprobado

### Contexto
El MVP del *Smart Trip Planner* requiere una API capaz de generar itinerarios de viaje optimizados en tiempo real, organizados en tres bloques diarios (Mañana/Tarde/Noche). El sistema debe manejar restricciones complejas como ventanas de horarios de apertura, tiempos de traslado variables según el transporte (coche vs. transporte público + caminar) y selección dinámica según el clima. Adicionalmente, debe soportar una función de replanificación instantánea si el itinerario se descarrila.

Se evaluaron tres enfoques para el motor de planificación:
1. Un enfoque basado 100% en un Modelo de Lenguaje (LLM).
2. Un algoritmo heurístico/greedy propietario desarrollado desde cero.
3. Un enfoque híbrido que delega la optimización matemática a un *solver* de restricciones (Google OR-Tools) y el enriquecimiento semántico a un LLM en segundo plano.

### Decisión
Hemos decidido implementar un **Enfoque Heurístico por Fases sobre la plataforma .NET (C#)** para el MVP, con enriquecimiento LLM en background, dividiendo las responsabilidades de la siguiente manera:

1. **Cálculo y Optimización en Tiempo Real (Síncrono):** Se utilizará un **algoritmo heurístico por 5 fases** (`HeuristicItineraryGenerator`) que distribuye must-sees y candidatos en bloques (Mañana/Tarde/Noche) aplicando reglas pragmáticas de proximidad, horarios, clima y transporte. Todo el procesamiento se ejecuta en memoria en el backend en milisegundos.
2. **Enriquecimiento Semántico del Catálogo (Asíncrono/Background):** Se utilizará una **API de LLM externa** únicamente de forma *offline* o en tareas en segundo plano. Su propósito exclusivo es digerir lugares de la ciudad piloto y deducir metadatos estructurados de negocio que no proveen las APIs de mapas tradicionales (ej. `typical_duration_minutes`, `family_friendly_score`, y banderas estrictas de `indoor/outdoor`).
3. **Plataforma Tecnológica:** Todo el ecosistema de la API se desarrollará en **.NET (C#)** debido al excelente rendimiento de ASP.NET Core para endpoints de baja latencia.

**Razón de la decisión:** El enfoque heurístico fue elegido sobre OR-Tools para el MVP por las siguientes razones:
- **Velocidad de implementación:** Un algoritmo heurístico pragmatico entrega resultados razonables en <100ms sin dependencias de librerías de optimización matemática.
- **Control total:** Permite ajustar reglas de negocio (scoring, transporte, clima) sin modelado matemático complejo.
- **Sin dependencias externas:** No requiere matrices de distancia reales ni modelado VRP completo.
- **Swappable:** La interfaz `IItineraryGenerator` permite cambiar a OR-Tools en el futuro sin modificar handlers ni contratos.

**Google OR-Tools (VRPTW)** fue evaluado como opción 3 y queda **deferred a post-MVP** para iteraciones futuras que requieran mayor rigor matemático en la optimización de rutas.

### Consecuencias

#### Positivas (Pros):
* **Rapidez de desarrollo:** El algoritmo heurístico se implementó en días, no semanas.
* **Baja Latencia (Milisegundos):** El endpoint de `/replan` responde instantáneamente porque el procesamiento heurístico en memoria toma menos de 100ms, permitiendo una experiencia de usuario fluida en condiciones de viaje reales.
* **Flexibilidad de reglas de negocio:** El scoring, la selección de transporte y la adaptación al clima se ajustan fácilmente sin reformular modelos matemáticos.
* **Eficiencia de Costes:** No pagamos tokens de LLM por cada itinerario generado ni por cada replanificación. El costo del LLM se mitiga a una inversión única por cada lugar añadido al catálogo.
* **Mantenibilidad:** El uso de .NET nos permite estructurar una solución limpia con separación de capas clara (*Domain*, *ApplicationServices*, *Infrastructure*, *API*).
* **Flexibilidad del LLM:** Al mantener la capa del LLM desacoplada en el proceso de ingesta, podemos cambiar de proveedor o modelo de lenguaje en el futuro sin alterar el motor de optimización principal.
* **Interfaz swappable:** `IItineraryGenerator` permite reemplazar el generador heurístico por OR-Tools u otro solver sin cambiar handlers ni controllers.

#### Negativas/Riesgos (Contras):
* **No garantiza optimalidad matemática:** A diferencia de OR-Tools, el heurístico no prueba que la solución sea la óptima global. Es una solución "suficientemente buena" para familias.
* **Ajuste manual de parámetros:** Los pesos del scoring, los umbrales de transporte y los límites de capacidad por bloque requieren tuning empírico.
* **Rigidez futura:** Si el producto escala a cientos de ciudades con miles de lugares, el heurístico podría necesitar optimización. OR-Tools queda como mejora futura documentada.

### Nota sobre OR-Tools
Google OR-Tools fue la opción preferida en la fase de diseño inicial (ver documentos de exploración en `openspec/changes/archive/2026-06-17-flow-2-itinerary-generation/`). Se decidió diferir su implementación al post-MVP para validar el producto con un enfoque más ágil. La arquitectura actual mantiene la puerta abierta para su adopción futura sin cambios en los contratos de la API ni en los handlers.
