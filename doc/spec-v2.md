# MVP Mini‑PRD — Basecamp Family Trip Planner (Europe) v2

## Resumen

Un **planificador de itinerarios por bloques** para **familias en viajes basecamp en Europa** que ayuda a ver lo máximo posible organizando:

- **Qué hacer** (must-sees + recomendaciones)
- **Cuándo hacerlo** (bloques Mañana / Tarde / Noche con duración estimada)
- **Cómo llegar** (coche vs transporte público + a pie)
- **Adaptación al clima** (selección indoor vs outdoor)

**Defaults**
- Inicio de día por defecto: **09:00** (editable por viaje y por día)
- Formato de salida: **bloques Mañana / Tarde / Noche** con **duración estimada por bloque y por visita**
- Idioma inicial: **Inglés**
- Clima (MVP): afecta únicamente la selección indoor/outdoor

---

## Problema

Planificar un viaje de 2–7 días en familia lleva horas:
- investigar atracciones y leer reseñas,
- estimar tiempos de desplazamiento y ordenar paradas lógicamente,
- lidiar con **horarios de apertura**,
- adaptarse a cambios (retrasos, clima, nivel de energía),
- evitar la fricción del coche (aparcamiento, tráfico) en zonas densas.

---

## Usuario objetivo

- **Familias** (2 adultos + niños)
- Viajes a **ciudades europeas** de varios días (también útil para excursiones de un día)
- Preferencia: **maximizar el turismo** (eficiente / alta intensidad)

---

## Propuesta de valor

"Genera un itinerario realista y ejecutable organizado en bloques, que maximiza lo que puedes ver, usa el transporte más sensato y se adapta rápido cuando los planes cambian o el clima es malo."

---

## Objetivos (MVP)

1. Crear un viaje y generar un **itinerario día a día** organizado en bloques con **duración estimada**.
2. Soportar **selección de must-sees** via búsqueda interna y asegurar que se priorizan en el plan.
3. Elegir el mejor **modo de transporte** (coche vs TP+caminar) por tramo usando reglas pragmáticas.
4. Ofrecer **swaps indoor/outdoor** según el clima.
5. Permitir **seguimiento ligero del día** (checklist) y **replanning** cuando el plan se descarrila.

---

## No-goals (MVP)

- Routing multi-ciudad con cambio de hotel
- Pagos / reservas
- Comunidad de reseñas
- Reglas de clima más allá de indoor/outdoor
- Tracking GPS en background
- Horarios exactos de inicio por visita

---

## Historias de usuario principales

1. **Creación del viaje**
   - Como usuario, puedo crear un viaje (ciudad, fechas, ubicación base) para que el planificador calcule tiempos realistas.

2. **Must-sees**
   - Como usuario, puedo buscar lugares y añadirlos como **must-sees** con prioridad, para que el plan se construya alrededor de ellos.

3. **Itinerario por bloques**
   - Como usuario, recibo un **itinerario organizado en bloques** (Mañana / Tarde / Noche) con duración estimada por bloque y por visita, para poder seguirlo sin organizarlo manualmente.

4. **Checklist del día + replan**
   - Como usuario, puedo marcar visitas como completadas y, si el plan se descarrila, pedir un **replan del resto del día**.

5. **Swaps por clima**
   - Como usuario, puedo activar el modo "mal tiempo" para priorizar actividades **indoor**.

---

## Criterios de aceptación (MVP)

- La app genera un itinerario para N días donde:
  - cada día contiene **3 bloques** (Mañana / Tarde / Noche),
  - cada bloque muestra:
    - lista de visitas con **duración estimada** (~1h 30min),
    - duración total estimada del bloque (~3h),
  - cada desplazamiento entre visitas muestra:
    - **modo sugerido** (`coche` o `TP+caminar`),
    - tiempo estimado (~20min),
  - los **must-sees de alta prioridad** se incluyen salvo que sea imposible (en cuyo caso se marca la razón).

- **Checklist**: el usuario puede marcar cada visita como hecha / no hecha.

- **Replan manual**:
  - el usuario activa el replan explícitamente,
  - el sistema actualiza el bloque actual y los siguientes,
  - puede eliminar/reemplazar ítems de baja prioridad para equilibrar la carga.

- **Clima**:
  - activar "mal tiempo esperado" resulta en selección indoor-first y swaps donde sea posible.

---

## UX / Decisiones de producto

### Estructura del itinerario

El día se muestra como tres bloques:

```
☀️  Mañana  (~3h estimadas)
    📍 Lugar A         ~1h 30min
    🚌 TP+caminar      ~15min
    📍 Lugar B         ~1h
    🚶 Caminar         ~10min
    📍 Lugar C         ~45min

🌤  Tarde   (~2h 30min estimadas)
    📍 Lugar D         ~1h
    🚗 Coche           ~20min
    📍 Lugar E         ~1h 30min

🌙  Noche   (~1h 30min estimadas)
    📍 Lugar F         ~1h 30min
```

### Checklist del día

- El usuario ve la lista de visitas del día
- Puede marcar cada una como **✓ Hecho**
- Sin tracking de tiempos ni alertas automáticas

### Replan (activación manual)

El usuario pulsa "Replanificar el resto del día". El sistema ofrece opciones:
1. Eliminar una parada nice-to-have
2. Mover una parada a otro día
3. Cambiar outdoor → indoor
4. Cambiar modo de transporte en los tramos restantes

---

## Lógica de planificación (heurísticas MVP)

### Inputs

- Must-sees con prioridad (Alta/Media) y día pinned opcional
- Lugares candidatos con:
  - ubicación, categorías, indoor/outdoor, duración típica, horarios de apertura,
  - puntuación family-friendly, popularidad/rating (opcional)
- Restricciones:
  - hora de inicio del día (default 09:00), ventanas de comida, límite de caminata opcional

### Algoritmo de alto nivel

1. Colocar **must-sees** primero (respetar horarios de apertura y día pinned).
2. Agrupar por **zonas/barrios** para reducir backtracking.
3. Llenar los slots restantes con los candidatos de mejor puntuación.
4. Insertar buffers entre visitas (slack para familias).
5. Validar capacidad del bloque (máx. ~3 visitas por bloque); si se desborda:
   - eliminar o reemplazar los ítems de menor valor.

### Capacidad por bloque (heurística)

| Bloque   | Duración disponible | Máx. visitas sugeridas |
|----------|---------------------|------------------------|
| Mañana   | ~3–4h               | 2–3                    |
| Tarde    | ~3h                 | 2–3                    |
| Noche    | ~1.5–2h             | 1–2                    |

### Scoring (ejemplo)

- **+** Bonus por prioridad must-see
- **+** Match de intereses + puntuación family-friendly
- **+** Popularidad (opcional)
- **−** Tiempo de desplazamiento
- **−** Penalización por fricción de aparcamiento al usar coche en zonas densas
- **−** Riesgo de horario (cierre cercano, buffer insuficiente)

### Regla de selección de transporte

- Default en ciudad: **TP+caminar**
- Cambiar a coche cuando:
  - TP+caminar es significativamente más lento (ej. > +20 min),
  - la caminata es excesiva,
  - o es un tramo de larga distancia.
- Penalizar coche en zonas de alta fricción de aparcamiento.

### Regla de clima (MVP)

- Si "mal tiempo" para un día/bloque:
  - preferir candidatos **indoor**,
  - outdoor solo si es necesario.

---

## Pantallas MVP

1. **Crear Viaje** — ciudad, fechas, ubicación base, viajeros, preferencias
2. **Búsqueda de Must-sees** — añadir prioridad, día pinned opcional
3. **Itinerario (Bloques)** — vista día a día con bloques y duraciones estimadas
4. **Día en ejecución** — checklist + botón de replan

---

## Decisión abierta (próximo paso)

Para habilitar la búsqueda interna de must-sees en Europa desde el día 1:

- **Opción A**: API externa de Places/Maps (cobertura inmediata, dependencia de API; requiere enriquecimiento manual de `typical_duration_minutes` y `family_friendly_score`)
- **Opción B**: Catálogo curado para 1–2 ciudades piloto (control total, alcance limitado)
- **Opción C** *(recomendada)*: API externa para estructura y búsqueda + enriquecimiento manual de los top-50 lugares de la ciudad piloto

Elegir una opción y definir la primera ciudad piloto (ej. Bucarest) para establecer el plan de validación y las métricas del MVP.
