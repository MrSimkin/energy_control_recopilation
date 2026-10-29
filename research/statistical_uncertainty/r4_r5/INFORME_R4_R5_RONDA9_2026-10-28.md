# Investigación R4/R5: novena ronda (28 de octubre de 2026)

**Estado: desarrollo retrospectivo y control metodológico. NO validación futura, NO protocolo congelado, NO autorización de producción.**

## Resumen comprensible

- R4 intenta describir si dentro de un vacío de red hubo alguna importación oculta y, en ese caso, cuánta energía. Es un fenómeno raro: en 240 minutos hubo cinco fechas con importación oculta entre 139 fechas comparables, con energías individuales de entre 1,73 y 8,66 kWh. No confundamos una «probabilidad baja en promedio» con que un período concreto sea seguro.
- R5 intenta aprovechar cuánto tiempo estuvo el sistema en un estado de importación. Su variante híbrida no ha demostrado mejorar de forma robusta al método simple entre extremos; el problema principal es la escasez de casos positivos distribuidos entre fechas diferentes.
- El trabajo de esta ronda no añade un nuevo modelo arbitrario. Prueba la **comunicación de incertidumbre y abstención** para R4 y construye un **conteo estructural ciego** para preparar futuros datos sin examinar resultados.

## Datos, integridad y separación

- Solo se usa el ZIP privado `SolarEnergyMonitor_R4_R5_Ronda5_20261028.zip`, con SHA-256 `5ed26b91b62c67a2bf689248b59cedc8d38266f0f4176403c8bb042ad50229dc`. `round9_research.py` valida hash, ZIP, entradas del manifiesto y condiciones estructurales antes de analizar.
- Todas las fechas de desarrollo son **estrictamente anteriores al 2026-09-28**. El período posterior sigue reservado para R3. No se abre su archivo de métricas para selección ni scoring R4/R5. Enel y bases de datos productivas, excluidos.
- La evidencia histórica ya se ha usado en rondas anteriores y por tanto **no es una validación independiente**. Todas las cifras de esta ronda son retrospectivas.

## R4: ¿advertir siempre, tranquilizar prematuramente o abstenerse?

El estudio de los 139 días comparables para vacíos 240 min con extremos cero encuentra 5 días con eventos (5 de 489 ventanas en total). Son denominadores distintos. En 140 días comparables para 30 y 120 minutos no hubo eventos; esto **no prueba frecuencia futura cero**.

Se probaron contrafactualmente reglas *hipotéticas* y **no aprobadas** sobre los últimos 119 días, pronosticando cada día con información exclusivamente anterior. La señal de «riesgo bajo» se activa si el límite superior de credibilidad unilateral del 95% para la frecuencia diaria con priori Beta(1/2,1/2) cae por debajo de una tolerancia hipotética de riesgo.

| Tolerancia experimental | Días señalados «riesgo bajo» | Días señalados que realmente tuvieron evento oculto |
|---|---:|---:|
| 1% | 0 | 0 |
| 3% | 0 | 0 |
| 5% | 16 | **1** |
| 10% | 107 | **5** |

La señal al 5% habría dado una tranquilización falsa en un día con **6,40 kWh** importados durante un vacío cero/cero de 240 min. En contraste, **advertir siempre** los 119 días habría generado 114 alertas sin evento.

Estos límites de credibilidad no son garantías de que un día concreto sea seguro: presuposiciones de estacionariedad e independencia son débiles ante los cambios estacionales observados. Tampoco los umbrales 1/3/5/10% han sido aprobados ni seleccionados para el producto.

**Decisión R4 de esta ronda:** mientras haya pocos eventos independientes y no se demuestre discriminación por contexto, usar internamente estado de investigación `RISK_NOT_CHARACTERIZED` / «incertidumbre no caracterizada», **sin activar semáforos individuales ni alterar P5/P50/P95**. Nunca convertir el control histórico en un gatillo de producción.

## R5: vigilar aplicabilidad, no solo promedio global

Sobre los mismos **1.302 casos** retrospectivos de **22 fechas**:

| Duración | Casos totales | Casos con energía positiva | Fechas con casos positivos | MAE puente (kWh) | MAE híbrido (kWh) |
|---|---:|---:|---:|---:|---:|
| 30 min | 993 | 65 | 7 | 0,005881 | 0,005921 |
| 120 min | 217 | 19 | 7 | 0,053006 | 0,054173 |
| 240 min | 92 | 12 | 7 | 0,134369 | 0,128240 |

- Solo **96 casos positivos**, provenientes de **7 fechas**. El híbrido se aplica realmente en **64 casos**, de **6 fechas**.
- A 30 y 120 min el híbrido empeora; a 240 min mejora, pero rondas anteriores encontraron que esa ventaja depende de un caso influyente. La supuesta ventaja global **no es robusta**.
- IC 95% por bootstrap de fechas, diferencia media equivalente por fecha (híbrido menos puente): **[-0,001811; +0,001254] kWh**. Incluye tanto mejorar como empeorar. No debe publicarse como validación.
- Orientaciones `>=10`, `>=15`, `>=20` **días positivos** son referencias ilustrativas de volumen, **NO umbrales aprobados**. En desarrollo 7 fechas no satisfacen ninguna. Este indicador mira el contenido del vacío y **no puede ser criterio de `count` ciego** sobre cohortes futuras.

## Monitor estructural ciego de R4/R5

`round9_blind_count_draft.py` es un **prototipo de diseño probado solo con CSV sintéticos**. Admite exclusivamente cinco columnas con fechas, muestras regulares y candidatos estructurales de R4 y R5 observables en los extremos. Rechaza columnas extras de resultado, fechas duplicadas, fechas previas al supuesto congelamiento, recuentos negativos, fraccionales o imposibles.

**Nunca emite READY ni invoca scoring**. El estado siempre es `DRAFT_NO_READY_OR_SCORE`; el programa no sabe si hubo energía positiva dentro de ningún vacío, no calcula P50, errores ni cobertura. La garantía outcome-blind en un sistema futuro exigirá auditar también la procedencia del CSV, no solo validar los nombres de las columnas.

La cohorte reservada de R3 no se ha usado para probar este monitor. El protocolo prospectivo verdadero de R4/R5 requiere congelamiento, revisión independiente, SHA-256, reglas de parada/insuficiencia y fechas posteriores al congelamiento, todo antes de leer valores futuros.

## Pruebas y límites

- `test_round9.py`: **29 pruebas PASS**, incluyendo fuente por SHA, corte temporal, recuentos históricos, escenarios R4, no robustez R5, rechazo de columnas de outcomes, fechas inválidas, enteros imposibles y prohibición implícita de READY.
- Los tests sintéticos verifican la lógica, **no prueban que un colector futuro nunca filtre resultados por accidente**.
- No existe mejora predictiva nueva validada ni código de producción listo. Respetar R3 congelado y esperar nuevas observaciones independientes.

## Siguiente paso razonable

1. R4: preparar una compuerta futura por evidencia estructural y un estrato separado para eventos raros; mantener `INCONCLUSIVE_RARE_EVENTS` cuando la cola tenga pocas fechas. No comunicar «riesgo bajo» personalizado.
2. R5: exigir análisis por duración y días distintos con endpoints activos; no utilizar el número de casos positivos reales como entrada a `count`, porque eso violaría el blindness. Preparar un protocolo futuro sin seleccionar ganador usando datos vistos.
3. Mantener la PR de investigación en borrador, y posponer cualquier cambio de la aplicación o scoring hasta autorización del propietario.
