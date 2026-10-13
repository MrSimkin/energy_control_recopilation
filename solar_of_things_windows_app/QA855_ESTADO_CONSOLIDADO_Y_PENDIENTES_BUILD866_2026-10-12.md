# QA855 — Estado consolidado, Build 866 y próximos pasos

**Actualización:** 2026-10-12 (Chile), posterior a entrega de Build 866.  
**Repositorio:** `MrSimkin/energy_control_recopilation`  
**Rama de cambios:** `work/qa855-corrections-package-20261012`  
**Commit de código validado:** `11437e2a115504485edfdc03664d78115efc53bf`  
**Autoridad:** `QA855_REGISTRO_QA_2026-10-12.md`, `QA855_CONTRATO_CORRECCIONES_2026-10-12.md`, decisiones posteriores documentadas en el recibo y en el código; `BUILD_HANDOFF_RULE.md`, `PRE_BUILD_OBSERVATION_REVIEW_RULE.md`. **La última decisión explícita del owner prevalece en presentación Enel.**

## 1. Resumen operacional

**Correcciones QA855: CODE IMPLEMENTED / WINDOWS CI SUCCESS / ACEPTACIÓN FUNCIONAL DEL OWNER PENDIENTE.** No es release. Los tres defectos materiales originados en la Build 855 tienen intervención de código; no afirmar que quedaron cerrados hasta la regresión dirigida en PC con datos copiados y revisión de PDFs.

- **Build 866**: [Actions run 38333170633](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/38333170633), `SUCCESS`, source `11437e2a115504485edfdc03664d78115efc53bf`.
- **Artefacto**: ID GitHub `11783103782`, nombre Actions `SolarEnergyMonitor-win-x64-dev`, handoff `SolarEnergyMonitor-Build-866-win-x64.zip`, 93.058.939 bytes, SHA-256 según GitHub `b3df3015dce65d466c488d643c035b866e03524d770012c38f8159c37780e8b8`.
- **CI verificado**: .NET 10/WPF Release; recursos ES/EN; smoke SQLite con pruebas PDF/XLSX y reglas Enel; corpus sintético aproximado 24k y 96k; publicación win-x64 portable; artefacto subido. Pasos externos Enel/CNE `live` intencionalmente `SKIPPED`, no son PASS.
- **No acceso del worker a DB ni archivos productivos**; sin restauración, eliminación de backups, merge, release ni modificación de `main`.

## 2. Qué se implementó

| ID / eje | Implementación en la rama | Estado comprobado |
|---|---|---|
| DEF-REPORT-01 | Detalle de energía y Batería renderizan trazas reales de energía/SOC vía imágenes ScottPlot/PNG en PDF. | **CODE + CI PASS**; smoke comprueba imágenes embebidas; **inspección visual/igualdad numérica con XLSX owner pendiente** |
| DEF-REPORT-02 y OBS-REPORT-03 | Más espacio para footer, encabezados de tablas repetibles, ajustes de paginación y glosario de Batería en nueva página. | **CODE + CI PASS**; **sin evidencia de inspección de PDF real corto/largo** |
| DEF-PERF-01 | Lectura de perfil SQLite de Batería trasladada fuera del hilo WPF y descarte por generación al navegar. | **CODE + CI PASS**; **sin medida de interacción/p95 en PC con corpus grande** |
| PDF fuentes | IBM Plex Mono Regular/Bold con SIL OFL empaquetadas; resolver y formato numérico en exportadores/filas relevantes. | **CODE + CI PASS**; pendiente inspección de incrustación y alcance exacto en todas las cifras/PDF |
| Excel fuentes | Consolas para celdas tipadas como números/fechas/horas en libros de informes, conservando datos como tipos Excel. | **CODE + CI PASS**; smoke inspecciona tipo y fuentes; pendiente revisar visualmente resto de formatos/libros |
| Enel estructura | Informe técnico principal completo, no carta ni solicitud; distribución empírica y comparación energética en página 1, tarifas y efectos CLP en página 2, desarrollo técnico posterior y ZIP de anexo aparte. No se alteró la convención de días de boleta ni motor de cálculos. | **CODE + CI PASS**; requiere PDF real exportado con Build 866 |
| Enel revisión posterior | Cards restauradas. Eliminados punto 5 antiguo (manteniendo cobertura diaria), H04/H05, «Lectura global», frases de hora exacta, «Estado de validación», «Contador energético alternativo», «Limitación principal» y frase de campana. Fuente S2 describe datos del inversor y recopilación mediante aplicación oficial. Numeración **«5. Cobertura diaria del período»** en ES/EN. | **CODE + CI PASS** y comprobaciones de texto con PDF sintético; **owner QA visual pendiente** |
| Seguridad | Anexo separado, aislamiento de datos, límites de cálculo y controles internos sin cambios de engine. | **Sin regresión detectada en CI sintética**; no afirmar verificación de fuentes externas `live` ni prueba de producción |

**Correcciones ejecutadas** en commits sucesivos `c7c4c610`, `92e0d0b2`, `34fa9d06`, `ebf78aaf`, `ebd4c659`, `351a27eb`, `618d0991`, `625ff5db`, `cea5d587`, `507cbac6`, `11437e2a`. Algunos commits intermedios dieron Builds 858/859 con fallo de compilación de tests; el resultado definitivo para este corte es **Build 866 SUCCESS**. No volver a testear ni entregar versiones intermedias.

## 3. Pendientes necesarios para cerrar QA855 (P0 práctico)

1. **Enel, dueño/PC:** exportar PDF desde Build 866 usando exclusivamente **copia aislada** del corpus QA; comprobar visualmente cards/lectura, página 1 (Enel, inversor, cobertura y P5/P50/P95 + gráfico), página 2 (tarifas y CLP), puntos 3–8 coherentes, **punto 5 sin subnumeración**, sin textos suprimidos, números/leyendas legibles y anexo independiente. Entregar PDF exportado + observaciones por página, únicamente si hay desviación.
2. **Detalle y Batería, dueño/PC:** exportar PDF y XLSX del mismo rango con copia histórica; series no vacías, SOC legible, tablas sin superposición con pie de página, glosario bien distribuido, cifras PDF/XLSX comparables. Verificar fuente de cifras en PDF/XLSX sin cambio general de estilo.
3. **Batería, dueño/PC:** navegación rápida Batería ↔ Dashboard ↔ Análisis con corpus QA grande, no bloquear UI 1–2 s; recopilar medidas de `UI.Navigation`, `UI.Dispatcher.TickLateness` y `UI.Battery.Refresh` con número suficiente de repeticiones para evaluar p95. Los servicios sintéticos no prueban latencia real.
4. **Cierre:** registrar PASS/FAIL con archivo/captura/datos de diagnóstico mínimo; reparar exclusivamente fallos reproducidos; después decidir con owner si se integra el stack de PRs. **No ejecutar los 129 pasos originales ni hacer merge automático por CI verde.**

**Precondición de datos:** jamás apuntar el ejecutable de pruebas directamente a `D:\SolarEnergyMonitorTest\Data` productivo; usar `D:\SolarEnergyMonitorQA855\DataHistorica` o copia aislada validada y comprobar la ruta efectiva. Los documentos/credenciales privados deben quedar fuera del repo y chats públicos.

## 4. Deuda posterior (no mezclar con la QA dirigida ni declarar solucionada)

| Clasificación | Identificador / pendiente | Próxima acción acotada |
|---|---|---|
| INVESTIGAR | **OBS-NUM-01**: discrepancia entre sumas de filas XLSX y total de energía (incluidas batería/casa) | Reproducir granularidad, unidades y límites de integración; no ajustar fórmulas sin evidencia |
| INVESTIGAR, aislado | **OBS-H01-04**: segmentos que cruzan medianoche/DST asignados al día inicial; horas diarias ocasionalmente >24 h | Corpus sintético que parte segmentos por frontera local, incluyendo día 23/25 h en `America/Santiago`; no sustituir `Δt` real |
| BLOCKED/ENTORNO | **OBS-BACKUP-01**: DB histórica copiada referencia PDF fuera del conjunto Bills/Tariffs | Preservar barrera de integridad; resolver inventario/fixture seguro, **no** copiar arbitrariamente ni tocar rutas externas productivas |
| NO EJECUTADO | Restauración histórica integral y prueba larga/soak de varias horas | Tranche de seguridad separada y consentimiento específico antes de cualquier prueba real; mantener protección de backups |
| BAJA / DIFERIDO | **OBS-REPORT-05**: visibilidad «Cargar plantilla»; **OBS-UX-01** presets 15 días/mes móvil; **OBS-SQL-01** rótulo F5 vs atajo personalizable | Corregir sólo en próxima tanda UX coherente, sin builds individuales |
| VALIDACIÓN EXTERNA | Resolución real Enel/CNE, procedencia y modelo de percentiles: CI `live` se saltó | Validación específica futura; conservar trazabilidad técnica en anexo, sin alterar datos ni reclamar certeza no probada |
| VALIDACIÓN VISUAL | Paginación PDF, fuentes embebidas y numeración completa después del retiro de sección 5 | Verificar un PDF generado con Build 866 y corpus copiado antes de declarar PASS |

## 5. Deuda global / integración de ramas (separada del alcance QA855)

- **PR #1:** `work/phase10-12-consolidated-20261007` → `main` (draft). Alcance general: fases 10–12, SQL, respaldos y UX; no confundir `CI PASS` parciales con fases completamente aceptadas. Fase 10 tiene línea base funcional previa aceptada (Build 684), fases 11/12 seguían parciales en el estado documentado.
- **PR #2/#3:** ramales H-01 y distribución Enel; **PR #4** integra desde rama base del stack; **PR #5** (esta corrección) depende de **PR #4**, y PR #4 depende del stack PR #1. Evitar merge directo no planificado de PR #5 a `main` y evitar duplicados de commits entre PRs.
- El hito **v0.11.0** sigue siendo objetivo, **no release**. En particular, inventario/rescate selectivo de respaldos, compatibilidad de esquemas y QA de uso real deben evaluarse en su rama/plan autorizado, con datos sintéticos. El código de backup existente no concede permiso para restaurar una base productiva.
- **Estado de las PR verificado:** PR #1–#5 abiertas, draft; PR #5 apunta a `work/qa-unified-h01-enel-family-pdf-20261012`. `main` SHA observado `59120a630b0f56684ba7672d960673f5c5c1797b`. No merge/release autorizado.

## 6. Siguiente ejecución recomendable

1. **Prioridad inmediata:** revisión owner de **Build 866**, únicamente P0 dirigido. No pedir más capturas/artefactos salvo desvíos.
2. Clasificar cualquier fallo residual entre formato/energía/performance; intervenir mínimo y volver a correr CI sobre **la misma rama**.
3. Cuando QA855 quede aceptado, actualizar el recibo con evidencias del propietario; definir integración escalonada del stack PR #1→#4→#5 y riesgos de cambios paralelos, **sin merge antes de instrucción explícita**.
4. Después atender OBS-NUM-01 y OBS-H01-04 con tests de límites, y OBS-BACKUP-01/recuperación en tranche separada segura; UX P1 no bloquea la aceptación de este paquete.

**No repetir** investigación API, arquitectura inicial, rediseño SQL ya implantado, 129 pruebas de QA 855, ni otra compilación de cortesía por esta consolidación documental.

**Fuentes del repositorio:** [Recibo de correcciones](QA855_RECIBO_CORRECCIONES_2026-10-12.md) · [Registro QA original](QA855_REGISTRO_QA_2026-10-12.md) · [Contrato vigente](QA855_CONTRATO_CORRECCIONES_2026-10-12.md) · [Continuidad](CONTINUITY_STATUS.md).
