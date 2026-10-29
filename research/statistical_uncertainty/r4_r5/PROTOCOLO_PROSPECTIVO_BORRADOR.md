# R4/R5: propuesta de validación futura (NO CONGELADA)

**Estado:** borrador de diseño, no prerregistro, no congelación y no permiso para puntuar. La autoridad de R3 es independiente y prevalece para su cohorte reservada.

## 1. Principios

1. Congelar R4 y R5 **por separado** (SHA del código y configuración, datos de entrenamiento cerrados, predictores realmente disponibles durante huecos, fuentes de reloj, criterios de elegibilidad, predicción P5/P50/P95 o abstención, lista de candidatos).
2. Separar futuras fechas **posteriores al congelamiento** de los días ya examinados. No usar fechas reservadas para R3 como entrenamiento, tuning, selección o test provisional R4/R5.
3. Preparar etapa `count` outcome-blind basada solo en cantidad de fechas, muestras regulares, duraciones, potencia en extremos y calibración anterior. Prohibido calcular energía verdadera dentro del objetivo, coberturas o scores en `count`.
4. Especificar antes de `count` el primer día de parada y los mínimos de evidencia estructural. Una vez `READY`, ejecutar `score` una sola vez hasta el primer día de parada; no reexaminar resultados repetidamente para elegir otra fecha.
5. Dos clases de informes: cobertura de elegibilidad y rendimiento en intersección común. Todos los comparadores evaluados en exactamente los mismos objetivos; subconjuntos y fechas también mostrados.
6. Métricas: P50 bias / MAE / RMSE, P5-P95 coverage, anchura y proper interval score al 90%, abstención y casos con energía positiva; IC por bloque de fecha. Evitar usar los resultados de las etiquetas posteriores para crear/ajustar gates.
7. Si faltan días positivos o eventos ocultos, emitir `INCONCLUSIVE_RARE_EVENTS`; si faltan episodios/estados/edad para R5, `INSUFFICIENT_AGE_IDENTIFICATION`. No convertir la insuficiencia en P95=0 por conveniencia.
8. Usar sólo medición del inversor, jamás Enel, medidor externo o tarifas; estricta separación de bases productivas.

## 2. Propuesta de inventario de suficiencia (NO cifras aprobadas)

- R4: fechas futuras elegibles por duración, fechas con extremos cero/cero, fechas con extremos ACTIVE y soporte de calibración anterior; posterior al score registrar la cantidad real de fechas con episodios ocultos. La cola rara exige probablemente muchas más fechas que el comportamiento habitual.
- R5: fechas con episodios completos, transiciones observadas, duración izquierda censurada y fechas con potencia positiva; segregar edad conocida/desconocida y aplicabilidad del algoritmo. Evitar comparar solo las ventanas donde el modelo logra emitir números.
- Para ambos, planificar **>=30 fechas evaluables** de la cohorte general y **>=20 fechas con energía positiva** como orientaciones de potencia, **no** gates implementados ni decisión estadística oficial. Los números requieren justificación y análisis previo de potencia.
- Definir anticipadamente cómo proceder si la cola rara sigue siendo insuficiente al cierre: reportar `INCONCLUSIVE` sobre esa cola sin repetir pruebas selectivas ni usar sus valores para recalibrar modelos sin nueva cohorte.

## 3. Independencia de R3

El período desde 2026-09-28 pertenece al procedimiento congelado R3. Las rondas R4/R5 1–6 no lo utilizaron como etiquetas. Para un test prospectivo independiente de R4/R5 escoger una cohorte nueva **después** del congelamiento de cada candidato y garantizar su aislamiento, incluso si R3 permanece `INSUFFICIENT`. No reusar el tramo abierto de R3 como si fuera test limpio de R4/R5.

## 4. Aprobación y cambios

Este archivo es material para revisión. No se autoriza ejecutar scoring, cambiar aplicación, adoptar un modelo, publicar release ni modificar `main`. Versiones futuras del protocolo deben tener registro de cambios, invariantes, SHA del harness y aprobación explícita antes de cualquier test prospectivo.
