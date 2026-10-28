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
