# Architecture Decision Record (ADR)

## ADR 001: Arquitectura de Motor de Planificación Híbrido (OR-Tools + LLM) en .NET

### Estatus
Aprobado

### Contexto
El MVP del *Smart Trip Planner* requiere una API capaz de generar itinerarios de viaje optimizados en tiempo real, organizados en tres bloques diarios (Mañana/Tarde/Noche). El sistema debe manejar restricciones complejas como ventanas de horarios de apertura, tiempos de traslado variables según el transporte (coche vs. transporte público + caminar) y selección dinámica según el clima. Adicionalmente, debe soportar una función de replanificación instantánea si el itinerario se descarrila.

Se evaluaron tres enfoques para el motor de planificación:
1. Un enfoque basado 100% en un Modelo de Lenguaje (LLM).
2. Un algoritmo heurístico/greedy propietario desarrollado desde cero.
3. Un enfoque híbrido que delega la optimización matemática a un *solver* de restricciones y el enriquecimiento semántico a un LLM en segundo plano.

### Decisión
Hemos decidido implementar un **Enfoque Híbrido sobre la plataforma .NET (C#)**, dividiendo las responsabilidades de la siguiente manera:

1. **Cálculo y Optimización en Tiempo Real (Síncrono):** Se utilizará **Google OR-Tools** (paquete NuGet oficial para .NET) modelando el problema como un *Vehicle Routing Problem with Time Windows* (VRPTW). Toda la lógica de restricciones duras (horarios, buffers, capacidad de bloques) y optimización de rutas se ejecutará en memoria en el backend en milisegundos.
2. **Enriquecimiento Semántico del Catálogo (Asíncrono/Background):** Se utilizará una **API de LLM externa** únicamente de forma *offline* o en tareas en segundo plano. Su propósito exclusivo es digerir lugares de la ciudad piloto y deducir metadatos estructurados de negocio que no proveen las APIs de mapas tradicionales (ej. `typical_duration_minutes`, `family_friendly_score`, y banderas estrictas de `indoor/outdoor`).
3. **Plataforma Tecnológica:** Todo el ecosistema de la API se desarrollará en **.NET (C#)** debido al excelente rendimiento de ASP.NET Core para endpoints de baja latencia y al soporte nativo de primera clase que ofrece NuGet para empaquetar los binarios de C++ de Google OR-Tools.

### Consecuencias

#### Positivas (Pros):
* **Determinismo y Consistencia:** Al delegar la ruta a OR-Tools, garantizamos matemáticamente que las ventanas de tiempo se respeten y que los *must-sees* de alta prioridad no se omitan por decisiones arbitrarias del sistema.
* **Baja Latencia (Milisegundos):** El endpoint de `/replan` responderá instantáneamente porque resolver un modelo VRPTW en memoria con OR-Tools para un puñado de nodos toma menos de 50ms, permitiendo una experiencia de usuario fluida en condiciones de viaje reales.
* **Eficiencia de Costes:** No pagamos tokens de LLM por cada itinerario generado ni por cada replanificación. El costo del LLM se mitiga a una inversión única por cada lugar añadido al catálogo.
* **Mantenibilidad:** El uso de .NET nos permite estructurar una solución limpia con separación de capas clara (*Core*, *Infrastructure*, *API*).
* **Flexibilidad del LLM:** Al mantener la capa del LLM desacoplada en el proceso de ingesta, podemos cambiar de proveedor o modelo de lenguaje en el futuro sin alterar el motor de optimización principal.

#### Negativas/Riesgos (Contras):
* **Curva de Aprendizaje Matemática:** Modelar el itinerario familiar en variables de OR-Tools (convertir horas a enteros basados en "minutos transcurridos desde las 00:00") requiere un diseño técnico riguroso.
* **Rigidez Inicial:** Si el solver de OR-Tools está sobre-restringido (por ejemplo, demasiadas actividades obligatorias en un horario imposible), devolverá un fallo explícito en lugar de una solución "degradada pero aceptable". El backend deberá manejar estos escenarios relajando restricciones (removiendo paradas de baja prioridad).