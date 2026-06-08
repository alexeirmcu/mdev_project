# Requisitos Técnicos — Flujo 2: Preparación de Datos para el Motor (El Pre-Solver Multi-Día)

## 1. Resumen del Flujo
Este flujo se ejecuta de manera síncrona dentro del caso de uso `GenerateTripHandler` (capa de Aplicación). Toma los datos globales del viaje (fechas, hotel base, preferencias) y la lista de deseos (*Must-sees*) seleccionados en el Flujo 1, y los empaqueta en una estructura matemática multi-dimensional apta para **Google OR-Tools**. 

La responsabilidad del Pre-Solver aquí es doble: proveer los datos para que el motor **distribuya las actividades entre los $N$ días del viaje** y, simultáneamente, **optimice la ruta y los bloques horariales dentro de cada día**.

---

## 2. Diagrama del Proceso de Mapeo Multi-Día

[GenerateTripHandler]
│
├── 1. Calcular Duración ──> Rango de Días (Ej: Día 1 a Día 5)
│
├── 2. Hidratar Lugares ───> GetManyByIdsAsync() [Places + Horarios específicos por día]
│
├── 3. Expandir Matriz ────> Matriz Global Completa (Incluye Hotel Base como Nodos Espejo)
│
├── 4. Ventanas de Tiempo ──> Conversión a Minutos Absorbiendo el Vector del Calendario
│
▼
[DTO TripOptimizationInput] ───> Listo para OR-Tools (Modelo Multi-Vehículo/Multi-Día)

## 3. Pasos Detallados del Proceso de Preparación Multi-Día

### Paso 2.1: Inicialización del Vector de Días (Modelado de "Vehículos")
El Pre-Solver debe determinar el horizonte temporal del viaje calculando la diferencia entre `StartDate` y `EndDate`.
* Si el viaje va del 15 de Julio al 19 de Julio, el sistema inicializa **5 días de simulación**.
* Cada uno de estos días se registrará en el solver como un "vehículo virtual" disponible para realizar "entregas" (visitas a lugares).
* Cada día heredará su propia `defaultStartHour` (ej: 09:00 $\rightarrow$ 540 minutos) y su hora límite de regreso al hotel (ej: 21:00 $\rightarrow$ 1260 minutos).

### Paso 2.2: Hidratación de Horarios Dinámicos por Día de la Semana
El Pre-Solver recupera las entidades `Place` de los *Must-sees*. Al ser un viaje de varios días, el validador de horarios no puede usar una ventana fija; debe cruzar el calendario:
* Si el "Día 1" del viaje cae en *Lunes* y el "Día 2" en *Martes*, el Pre-Solver debe buscar en la colección `OpeningHours` del `Place` las ventanas correspondientes a esos días específicos de la semana, ya que los museos o atracciones en Madrid suelen cerrar los lunes o cambiar horarios los fines de semana.

### Paso 2.3: Construcción de la Matriz de Distancia Global (El Nodo Hotel)
En un viaje multi-día tipo *Basecamp*, el hotel es el punto de partida y de retorno obligatorio **cada uno de los días**. Para modelar esto sin confundir al Solver matemática de OR-Tools, el hotel se trata como el "Depósito" (Start/End Node) de todos los vehículos.
* Si el usuario seleccionó 8 *Must-sees* para un viaje de 4 días, la matriz de distancias no es de $8 \times 8$, sino de **$9 \times 9$** (8 lugares + 1 Hotel Base).
* El cálculo de tramos (`TransitDetails` + `BufferMinutes` familiares) se ejecuta para las combinaciones de todos los lugares entre sí, **más los trayectos de ida y vuelta desde el hotel a cada uno de ellos**.

### Paso 2.4: Mapeo de Restricciones del Calendario y Clima Multi-Día
El Pre-Solver procesa el pronóstico del clima mapeando el vector de días:
* Si el servicio meteorológico indica que el **Día 3** va a llover intensamente en Madrid, el Pre-Solver altera las variables de penalización *exclusivamente para el vehículo (Día) 3*.
* Los lugares con `IsIndoor == false` verán restringidas sus ventanas de tiempo o se les aplicará un coste matemático negativo si el Solver intenta agendarlos en el **Día 3**, forzando a OR-Tools a mover los lugares *Outdoor* hacia los días 1, 2, 4 o 5 (días de sol) y los *Indoor* al día de lluvia.

---

## 4. Estrategia de Poda y Alivio de Carga Multi-Día (Fallback de Inviabilidad)

Cuando el viaje es de varios días, la sobre-restricción suele ocurrir porque el usuario quiere concentrar demasiadas actividades en poco tiempo o porque los horarios de apertura de los lugares seleccionados entran en conflicto directo en los días del viaje. 

El bucle de relajación se adapta al entorno multi-día de la siguiente manera:

1. **Evaluación de Capacidad Global:** El Pre-Solver calcula la suma total de los `TypicalDurationMinutes` de todos los lugares solicitados + una estimación mínima de traslados. Si esta suma supera las horas útiles totales de todo el viaje (ej. piden 40 horas de turismo en un viaje de 2 días), el sistema aborta inmediatamente disparando la poda, sin perder tiempo invocando a OR-Tools.
2. **Poda Escalonada en el Solver:** Si el Solver multi-vehículo retorna *Inviable*, el Pre-Solver inicia la remoción selectiva en bloque:
   * **Fase 1:** Quita del pool global los lugares `LOW` y reintenta la distribución multi-día.
   * **Fase 2:** Quita los lugares `MEDIUM`.
   * **Fase 3 (Excepción):** Si el viaje sigue siendo inviable solo con los `HIGH`, el Pre-Solver analiza qué día está bloqueado y lanza `OverConstrainedRouteException`, notificando por ejemplo: *"El itinerario es inviable porque seleccionaste 3 museos de prioridad ALTA que cierran los lunes (Día 1) y no se pueden reubicar en el resto de los días"*.

---

## 5. Criterios de Aceptación Técnicos

1. **Cohesión Multi-Día:** El DTO de salida enviado a la infraestructura de optimización debe contener la definición explícita de los $N$ días del viaje, permitiendo que un lugar sea asignado a cualquier día, siempre que cumpla con los horarios de apertura de ese día de la semana y las reglas del clima.