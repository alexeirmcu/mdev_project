# Requisitos Técnicos — Flujo 1: Generación del Trip (Descubrimiento e Ingesta en Madrid)

## 1. Resumen del Flujo
Este flujo regula el proceso en el cual un usuario busca, selecciona lugares de interés (*Must-sees*) y configura las bases de su itinerario en la ciudad piloto de **Madrid**. El sistema debe priorizar el rendimiento, la experiencia de usuario (UX) y el control de costes combinando una base de datos local de alta velocidad con la API externa de Foursquare mediante un **Pipeline de Búsqueda en Cascada**.

---

## 2. Modelado de la Entidad de Dominio: `Place`

Para asegurar el desacoplamiento de capas bajo los principios de *Clean Architecture* y *Domain-Driven Design (DDD)*, los datos crudos obtenidos de cualquier origen deben mapearse a una entidad de dominio pura dentro de `SmartTripPlanner.Domain` antes de ser expuestos a la capa de aplicación o al frontend.

### Estructura de la Entidad `Place`

* **`Id`** (`string`): Identificador único del lugar. Si el lugar proviene de Foursquare, este campo almacena el `fsq_id` original de forma obligatoria.
* **`Name`** (`string`): Nombre comercial o legible del punto de interés (ej. *"Museo del Prado"*).
* **`CityId`** (`string`): Identificador de la ciudad (fijo en `"madrid-es"` para este alcance).
* **`Location`** (`ValueObject`): Contiene la información geoespacial exacta necesaria para los cálculos de rutas:
    * `Latitude` (`double`): Restringido estrictamente a un rango de $[-90, 90]$.
    * `Longitude` (`double`): Restringido estrictamente a un rango de $[-180, 180]$.
* **`TypicalDurationMinutes`** (`int`): Tiempo estimado de permanencia promedio para una familia en el lugar.
* **`IsIndoor`** (`bool`): Indicador binario para la lógica de adaptación al clima (`true` si es techado/indoor, `false` si es al aire libre/outdoor).
* **`IsFamilyFriendly`** (`bool`): Flag de negocio que valida si el lugar es apto o recomendado para la dinámica de viaje con niños.
* **`OpeningHours`** (`List<OpeningHoursWindow>`): Listado estructurado con las ventanas de tiempo por día de la semana (Hora de apertura y cierre expresada en minutos desde las 00:00) necesarias para las restricciones duras de Google OR-Tools.

---

## 3. Diagrama del Proceso (Pipeline en Cascada)

[Usuario escribe Query]
│
▼
┌─────────────────────────────────┐
│ Paso A: Consulta en BD Local    │
└─────────────────────────────────┘
│
├── ¿Se encontraron resultados? ── SÍ ──> [Retornar Datos Curados] ──> (Fin)
│
└── NO (Salto Automático)
│
▼
┌─────────────────────────────────┐
│ Paso B: Invocación HTTP a       │
│ API de Foursquare (Filtro Madrid)│
└─────────────────────────────────┘
│
▼
┌─────────────────────────────────┐
│ Paso C: Aplicar Mapeo de        │
│ Emergencia (Inyección de Datos) │
└─────────────────────────────────┘
│
▼
┌─────────────────────────────────┐
│ Retornar a UI / Almacenar Trip   │ (Usa fsq_id como PlaceId de Dominio)

---

## 4. Descripción Detallada del Algoritmo de Búsqueda

Cuando la API recibe una petición de búsqueda en la ruta `/api/trips/places/search?query={texto}&cityId=madrid-es`, el `PlaceRepository` (capa de Infraestructura) debe ejecutar de forma obligatoria los siguientes pasos:

### Paso 4.1: Interrogación del Almacenamiento Local (Caso A)
1. El sistema realiza una consulta de texto parcial sobre la tabla/colección local de `Places` filtrando por la ciudad de Madrid. Esto incluye el top-50 de lugares curados inicialmente más cualquier lugar que haya sido enriquecido con anterioridad por el pipeline asíncrono.
2. Si se encuentran coincidencias que satisfagan la búsqueda, el repositorio hidrata las entidades `Place` con sus metadatos nativos optimizados de negocio y corta la ejecución devolviendo un código HTTP 200 de forma inmediata.

### Paso 4.2: Salto Automático a Red Externa (Caso B)
1. Si el resultado del paso anterior es **vacío** o no supera un umbral mínimo de coincidencia textual, el repositorio activa el cliente HTTP provisto por `IHttpClientFactory` de forma automática e invisible para el usuario.
2. Se realiza una petición segura al endpoint de *Text Search* de la API de Foursquare, parametrizando la búsqueda con el texto introducido por el usuario y acotando el radio geográfico a las coordenadas e influencia de la ciudad de Madrid.

### Paso 4.3: Aplicación del "Mapeo de Emergencia" (Caso C)
Dado que la respuesta cruda de Foursquare carece de las propiedades semánticas y de negocio requeridas por el planificador familiar, la capa de infraestructura interceptará los nodos y les inyectará los siguientes valores heurísticos por defecto basados en las categorías oficiales de Foursquare antes de devolverlos:

#### A. Inyección de `TypicalDurationMinutes`
* Si la categoría de Foursquare mapea con `Museum` (Museo), `Art Gallery` (Galería de Arte) o `Theme Park` (Parque de Atracciones): **120 minutos**.
* Si la categoría mapea con `Historic Site` (Sitio Histórico), `Monument` (Monumento), `Plaza` o `Park` (Parque público): **60 minutos**.
* Si la categoría mapea con `Restaurant` (Restaurante), `Café` o `Food Court`: **90 minutos** (garantizando la reserva de un bloque holgado para las comidas principales de la familia).
* Cualquier otra categoría no identificada explícitamente: **60 minutos** por defecto.

#### B. Inyección de `IsIndoor`
* Categorías de entretenimiento cerrado, museos, iglesias, teatros y centros comerciales: `true`.
* Categorías de parques naturales, monumentos públicos, calles peatonales y miradores: `false`.
* Ante un fallo de clasificación o categoría ambigua: Se asigna `true` por defecto (un entorno cubierto mitiga riesgos operativos y de logística familiar frente al mal tiempo).

#### C. Inyección de `IsFamilyFriendly`
* Por defecto en el desborde de emergencia se asume `true` a menos que la categoría de Foursquare se clasifique explícitamente como ocio nocturno o exclusivo de adultos (`Nightclub`, `Strip Club`, `Adult Entertainment`), evitando la exclusión agresiva de atracciones genéricas populares.

---

## 5. Criterios de Aceptación Técnicos

1. **Transparencia Absoluta (UX):** El usuario no debe percibir en la interfaz si el resultado provino de la base de datos local enriquecida o de un desborde en tiempo real hacia la API de Foursquare. Ambas respuestas deben lucir idénticas.
2. **Preservación de la Identidad:** El sistema debe utilizar de manera obligatoria el identificador alfanumérico devuelto por Foursquare (`fsq_id`) como el `PlaceId` definitivo dentro de la colección de `OriginalMustSees` en el agregado del viaje (`Trip`).
3. **Aislamiento de Infraestructura:** Ningún componente fuera del proyecto `SmartTripPlanner.Infrastructure` puede conocer los esquemas de respuesta, formatos de horas o contratos específicos de la API de Foursquare. Todo debe salir del repositorio transformado estrictamente a la entidad `Place` de Dominio.
└─────────────────────────────────┘