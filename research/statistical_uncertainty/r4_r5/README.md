# Investigación paralela R4/R5: índice consolidado

**Corte:** 2026-10-28 (Chile). **Estado:** investigación experimental y auditoría; SIN validación prospectiva ni aprobación de producción.

Esta carpeta consolida el trabajo anterior del usuario y los resultados de las rondas 1–6. La autoridad para la intención de los candidatos sigue siendo [PARALLEL_CANDIDATES_R3_R4_R5_2026-10-17.md](../PARALLEL_CANDIDATES_R3_R4_R5_2026-10-17.md).

## Reglas invariables

- R3 sigue congelado, con su `count` outcome-blind y `score` bloqueado hasta `READY`. No alterar su protocolo, harness, umbrales o cohortes.
- R4 y R5 se exploraron usando exclusivamente fechas locales **anteriores a 2026-09-28**, ya consumidas en desarrollo. No comparar ni recalibrar con los resultados futuros reservados de R3.
- Ninguna boleta Enel, tarifa ni medición externa participa.
- Ningún resultado aquí autoriza modificar la app Windows, informes, base de producción, backups ni `main`.
- El registro privado de origen, `SolarOfThings-ResearchPackage-20261028-131104.zip`, SHA-256 `ea21dda1f2c6527864431a8b6b6f091e0b495a869efdc9f1ebeeb7eade0538f4`, **no se almacena en GitHub**; tampoco los CSV de medición o predicción por caso.

## Documentos y utilidades

- [RESULTADOS_CONSOLIDADOS_2026-10-28.md](RESULTADOS_CONSOLIDADOS_2026-10-28.md): decisiones y evolución retrospectiva, rondas 1–6.
- [PROTOCOLO_PROSPECTIVO_BORRADOR.md](PROTOCOLO_PROSPECTIVO_BORRADOR.md): propuesta auditable para suficiencia, bloqueo de resultados y comparadores; **NO CONGELADO**.
- [reproduce_round6_repo.py](reproduce_round6_repo.py): reproduce diagnósticos de la sexta ronda a partir de **dos CSV derivados privados** de la quinta; `numpy` + `pandas`.
- [ROUND6_SUMMARY.json](ROUND6_SUMMARY.json): datos numéricos resumidos sin telemetría privada.

## Reproducción y trazabilidad

Las rondas originales se entregaron como ZIP privados con código, informes, tablas, auditorías y manifiestos SHA-256. Los ZIP no se suben ni se sustituyen por los resúmenes de esta rama. Hashes de integridad de los paquetes originales:

| Paquete | SHA-256 |
|---|---|
| Ronda 1 `SolarEnergyMonitor_R4_Experimento_R5_Factibilidad_20261028.zip` | `70945ac730a8469d90449380a945f0f038a9759d613c74f79ceb4af03eb68b61` |
| Ronda 2 `SolarEnergyMonitor_R4_R5_Segunda_Ronda_20261028.zip` | `a6c5902bd31899bb560ba3286b6a5ba17b6510d583a60ee23672eaea2ae154e6` |
| Ronda 3 `SolarEnergyMonitor_R4_R5_Ronda3_20261028.zip` | `30405a3ef4394dce20e38f593ff1bcfee893d4b089bda1f8ac329f9b4c970900` |
| Ronda 4 `SolarEnergyMonitor_R4_R5_Ronda4_20261028.zip` | `67c479799e1a6ceb57da223e71005a5620f728fb619b7b41ba72255f79a6c10f` |
| Ronda 5 `SolarEnergyMonitor_R4_R5_Ronda5_20261028.zip` | `5ed26b91b62c67a2bf689248b59cedc8d38266f0f4176403c8bb042ad50229dc` |

La **reproducción integral de rondas 1–5 requiere sus ZIP originales privados**, con dependencias y datos auxiliares. El archivo `reproduce_round6_repo.py` es reproducible localmente una vez que se suministran los dos CSV derivados privados de la quinta ronda. Un PASS de código NO equivale a calidad predictiva prospectiva.

## Séptima ronda: robustez y caso influyente

- [RONDA7_AUDITORIA_ROBUSTEZ.md](RONDA7_AUDITORIA_ROBUSTEZ.md): R4 frecuencia y severidad de casos ocultos; R5 sensibilidad por duración y al eliminar un caso.
- [reproduce_round7_repo.py](reproduce_round7_repo.py): reproducción compacta de hallazgos desde el ZIP **privado** de quinta ronda, comprobando hash y manifiesto.
- [ROUND7_SUMMARY.json](ROUND7_SUMMARY.json): resultados agregados.
- **Corrección de interpretación:** la ventaja leve del híbrido R5 descrita en la sexta ronda NO es robusta; cambia de signo retirando un único caso de 240 minutos. No promover el híbrido a candidato de producción.

## Octava ronda: suficiencia y guardarraíles

- [RONDA8_SUFICIENCIA_Y_QA.md](RONDA8_SUFICIENCIA_Y_QA.md): planificación de evidencia de cola rara y comparaciones R5 desagregadas.
- [round8_sufficiency_replay.py](round8_sufficiency_replay.py): script reproducible compacto sobre ZIP privado ronda 5; fuente validada por SHA.
- [prospective_count_scaffold.py](prospective_count_scaffold.py): interfaz estructural outcome-blind **NO congelada**, probada solo con datos sintéticos; nunca declara READY.
- [ROUND8_SUMMARY.json](ROUND8_SUMMARY.json): cifras agregadas sin mediciones privadas.

**Advertencia:** 213 días comparables adicionales representan un ejemplo hipotético de planificación al 80% para cinco nuevos días R4 con episodio oculto; **NO una fecha ni requisito oficial**. La ventaja R5 sigue no robusta.


## Novena ronda: abstención y suficiencia estructural (desarrollo, 2026-10-28)

- [INFORME_R4_R5_RONDA9_2026-10-28.md](INFORME_R4_R5_RONDA9_2026-10-28.md): pruebas de falsas señales de riesgo bajo de R4 y evaluación R5 por duración.
- [round9_research.py](round9_research.py): reproducción sobre ZIP **privado** de ronda 5; **SHA-256 y manifiesto verificados**, corte estricto 2026-09-28.
- [round9_blind_count_draft.py](round9_blind_count_draft.py): nuevo monitor estructural **NO congelado**, probado solo sobre fechas y datos sintéticos; **jamás emite READY**.
- [test_round9.py](test_round9.py): 29 tests PASS; requiere `R4R5_ROUND5_ZIP` para los tests privados.
- [ROUND9_AGGREGATE.json](ROUND9_AGGREGATE.json) y [README_REPRODUCCION.md](README_REPRODUCCION.md): resultados agregados e instrucciones.

**Hallazgo importante:** una señal verde hipotética R4 con tolerancia 5% habría sido falsa en un día con 6,40 kWh ocultos; avisar siempre sobre los 119 días genera 114 avisos sin evento. Propuesta solo investigativa: estado `RISK_NOT_CHARACTERIZED` sin semáforo individual. R5 sigue sin superioridad robusta y requiere más días con importación real.

## Décima ronda: calendario de evidencia y diseño de evaluación

- [RONDA10_AUDITORIA_SENCILLA.md](RONDA10_AUDITORIA_SENCILLA.md): cinco días con eventos raros R4 distribuidos junio–agosto; un día influyente R5.
- [PLAN_VALIDACION_FUTURA_BORRADOR_R10.md](PLAN_VALIDACION_FUTURA_BORRADOR_R10.md): tres cerraduras: congelar, contar a ciegas desde fuente auditada, puntuar una sola vez.
- [ROUND10_SUMMARY_PUBLIC.json](ROUND10_SUMMARY_PUBLIC.json): cifras agregadas sin telemetría.
- [round10_compact_replay.py](round10_compact_replay.py): reproduce las cifras agregadas sobre ZIP *privado* de desarrollo con verificación SHA y manifiesto; requiere numpy/pandas.
- Paquete ZIP privado de ronda 10 contiene scripts completos (incluyendo contador solo sintético), **26 pruebas PASS**, gráficas e informe. ZIP SHA-256: `5b35a3e9bbe5eb47d9912a039ed36b2a3895c56208638431ed80c904651288a9`. **No contiene datos originales.**

La herramienta de conteo sintético NO está autorizada para evaluar fechas futuras y no se debe considerar prueba integral de ausencia de filtraciones de resultados. Requiere auditar también su selector de ventanas. `main`, R3 y la app permanecen intactos.

## Lote agrupado de rondas 11–13: STOP por barrera de evidencia

- [LOTE_R11_R13_CIERRE_2026-10-28.md](LOTE_R11_R13_CIERRE_2026-10-28.md): tres rondas ejecutadas en un encargo, control temporal, robustez por días, DST de Chile y justificación del STOP.
- [BATCH_R11_R13_SUMMARY_PUBLIC.json](BATCH_R11_R13_SUMMARY_PUBLIC.json): métricas agregadas; sin consumo privado por caso.
- Código completo y 19 controles de regresión adicionales en ZIP privado para el propietario, SHA-256 `9895d6b8fb66c4fe9edc13ab6183a487fde345d9c3e6654d7cccef42f7a9210e`.

**La diferencia de interval-score R4 excluye cero solo dentro del corpus usado para elegir variantes. NO equivale a validación independiente.** R5 sigue sin ventaja robusta. Más rondas de ajuste sobre esta historia no agregan evidencia nueva. Lote detenido hasta nueva evidencia y congelamiento explícito de futuros protocolos.

## Mapa operativo y auditoría de las dos aplicaciones (28/10/2026)

- [MAPA_DE_EVIDENCIA_R3_R4_R5_2026-10-28.md](MAPA_DE_EVIDENCIA_R3_R4_R5_2026-10-28.md): qué busca y qué espera cada modelo, fechas y requisitos reales frente a orientaciones no aprobadas.
- [AUDITORIA_APPS_Y_CAPTURA_EVIDENCIA_2026-10-28.md](AUDITORIA_APPS_Y_CAPTURA_EVIDENCIA_2026-10-28.md): revisión de Windows y Research Exporter R1, mejoras opcionales sin cambios productivos.
- [APP_EVIDENCE_METADATA_AGGREGATE_2026-10-28.json](APP_EVIDENCE_METADATA_AGGREGATE_2026-10-28.json): recuentos estructurales de un ZIP ResearchPackage, sin valores de potencia ni resultados de modelos.

Auditoría local externa, código y pruebas (cuatro PASS) entregables al propietario; no se publican series privadas ni ZIP de origen. Resultado operativo: **NINGUNA de las dos apps necesita actualizarse ahora para seguir acumulando evidencia**. R3 congelado; R4/R5 sin protocolo futuro aprobado.
