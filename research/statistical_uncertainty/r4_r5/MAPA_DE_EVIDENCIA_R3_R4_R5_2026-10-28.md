# Mapa operativo de evidencia: R3, R4 y R5

**Corte de información:** 2026-10-28 (America/Santiago). **Autoridad:** R3 congelado en su propio protocolo; R4/R5 siguen en desarrollo y su protocolo futuro es un BORRADOR. Documento de lectura para el propietario, no nuevo protocolo ni autorización de scoring.

## Vista general de estados

| Línea | Qué intenta predecir | Desarrollo | Protocolo del examen | Barrera actual | Próxima acción |
|---|---|---|---|---|---|
| **R3** | kWh de importación durante un hueco usando extremos + residuos históricos | Cerrado/congelado | **Congelado** | `INSUFFICIENT` en último recuento de 2026-10-28 | Seguir recabando datos y ejecutar el `count` canónico sin desenlaces; `score` solo con READY |
| **R4** | Probabilidad de energía nula/positiva y distribución de kWh positivos | Prototipos probados en desarrollo | **NO congelado** | Falta examen nuevo, evidencia rara y compuertas predefinidas | Seleccionar una única versión, auditar selección de ventanas, fijar protocolo y futura cohorte independiente |
| **R5** | Duración de episodios de importación y su posible utilidad para estimar kWh | Dos estados y variantes híbridas exploradas | **NO congelado** | Pocos días activos, edades inciertas y ninguna mejora robusta | Congelar solo si supera compuerta de factibilidad; de otro modo `INCONCLUSIVE/NOT_FEASIBLE` |

## Marcador R3 (snapshot de reproducción del conteo, no ejecución formal de scoring)

Último ResearchPackage conocido generado **2026-10-28 13:11**. Conteo estructural reproducido en la investigación previa, sin calcular desenlaces:

| Condición | Observado | Mínimo congelado | Avance |
|---|---:|---:|---|
| Fechas distintas con ACTIVE elegible | **10** | 15 | Faltan **5** |
| Casos ACTIVE totales | **115** | 100 | Cumple |
| Grupo ACTIVE G20_30 | **45** | 10 | Cumple |
| Grupo ACTIVE G60_120 | **61** | 10 | Cumple |
| Grupo ACTIVE G240 | **9** | 10 | Falta **1** |

**Resultado:** `INSUFFICIENT`. El conteo de 2026-10-28 se obtuvo como reproducción local de la lógica de `count`; para constancia oficial debe utilizarse el harness congelado, sin alterarlo. La recolección sugerida de **2026-11-04** es para revisar progreso, NO una promesa de READY. No puntuar por anticipado, no usar Enel.

## Mapa R4: qué esperamos

1. **Hoy:** existen aproximadamente 141 días históricos de buena calidad usados para desarrollo; en el análisis de 240 min con extremos cero/cero hubo **cinco fechas con importación oculta entre 139 comparables**. Los eventos son raros, pero algunos de importancia energética.
2. **Primero falta una decisión técnica, no esperar pasivamente:** elegir candidato y versión, justificar regla de abstención, auditar los datos disponibles cuando se produce la predicción y congelar código/semillas/ventanas/criterios.
3. **Después:** obtener una cohorte nueva posterior al congelamiento, con fechas distintas y representatividad por duración, extremos y franjas horarias; realizar `count` sin leer consumo interior.
4. **Examen:** comparar mismos objetivos y ventanas con un puente sencillo y R3 cuando corresponda; valorar cobertura P5–P95, ancho de intervalo, interval score, error P50, errores en energía positiva y frecuencia de abstenciones.
5. **Posibles decisiones:** viable en comportamiento general pero `INCONCLUSIVE_RARE_EVENTS` para la cola; mejora demostrada solo si persiste sobre fechas nuevas y diferentes situaciones; o descartar si no aporta.

El borrador menciona **30 fechas evaluables** como orientación y un escenario ilustrativo de **213 días comparables adicionales** para lograr 80% de probabilidad de observar *cinco nuevos días con evento oculto*, suponiendo estacionariedad. **NINGUNA cifra es un umbral oficial** ni los cinco eventos bastan para aprobación. Un conteo outcome-blind no puede saber si hubo importación oculta sin leer resultados; ese número solo se conoce en la etapa de examen.

## Mapa R5: qué esperamos

1. **Hoy:** backtest de desarrollo sobre **1.302 casos, 22 fechas**, pero solo **96 casos positivos repartidos en 7 fechas**; el híbrido se aplica en 64 casos/6 fechas. La ventaja media frente al puente se revierte al quitar una fecha influyente.
2. **Barrera A:** demostrar cobertura estructural y suficientes días con extremos activos, episodios de transición y edades identificables/censuradas sin usar energía interior de futuras ventanas.
3. **Barrera B:** seleccionar un candidato estable antes de congelarlo, o declarar `NOT_FEASIBLE` si sigue necesitando supuestos no identificables. No promover automáticamente el híbrido actual.
4. **Después del congelamiento:** cohorte realmente nueva, criterios de soporte y abstención predefinidos; examen futuro sobre los mismos objetivos que el puente simple; reporte separado de grupos con y sin energía real.
5. **Posibles decisiones:** superioridad consistente, utilidad limitada a alguna situación respaldada, `INCONCLUSIVE` por falta de días positivos o abandono sin mejora demostrada.

La orientación de **30 fechas generales y 20 fechas con energía positiva** aparece en un borrador, pero **NO es un criterio aprobado**. El número real de futuros casos con energía positiva solo se conoce durante score, no en el conteo ciego.

## Tres cerraduras de ambos protocolos futuros

**Congelar** (código, entrenamiento, objetivos y regla de parada) → **contar** sin verdad interior ni filtro por desenlace → **puntuar una vez** hasta la primera fecha de parada fijada. La procedencia del selector importa: que un CSV carezca de columna de energía NO garantiza que los casos se eligieron a ciegas.

Los datos desde 2026-09-28 ya se reservaron para el procedimiento R3; **no declararlos examen independiente de R4/R5** sin congelamiento y nueva cohorte explícita. Las investigaciones R4/R5 1–13 de historia anterior a esa fecha son desarrollo consumido, no prueba final.

## Próximos hitos y decisiones del propietario

- **2026-11-04:** exportación acumulativa opcional para nuevo conteo R3; no se requiere cambio de app para producirla.
- **Antes de evaluar R4:** aprobar candidato/protocolo/harness y fecha de congelamiento separada.
- **Antes de evaluar R5:** decidir factibilidad de la estructura elegida; si no supera, mantener en estudio o descartar.
- **Después:** nuevos datos independientes, compuertas de suficiencia acordadas y scoring una sola vez, con resultados que puedan ser favorables, desfavorables o inconclusos.

Fuentes: `PHASE2R_R3_FUTURE_VALIDATION_PROTOCOL_2026-09-30.md`, `PARALLEL_CANDIDATES_R3_R4_R5_2026-10-17.md`, `LOTE_R11_R13_CIERRE_2026-10-28.md`, `PROTOCOLO_PROSPECTIVO_BORRADOR.md`.
