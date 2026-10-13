# Build 855 — Registro consolidado de QA en PC del propietario

**Fecha:** 2026-10-12 (Chile). **Estado:** resultado de QA manual + lectura de artefactos; **no certificación de release**.  
**Origen de código:** `c1b0ee4345cc64dbe51921d70cbc1ee02f499465` (rama `work/qa-unified-h01-enel-family-pdf-20261012`, PR #4 sin merge). GitHub Actions Windows **Build 855**, run `38305969226`, SUCCESS; paquete ZIP verificado por propietario SHA-256 `67797eef5110e9c81c4b061e45f7e2e4e161a29139f5fb1b1bd598bd953dfacb`. CI SUCCESS no equivale a QA funcional.

## 1. Seguridad y entornos empleados

- **Producción no autorizada para escrituras ni restauraciones:** `D:\SolarEnergyMonitorTest\Data` (~6,91 GiB; `energy.db` ~1,76 GiB).
- Respaldo independiente en `D:\SolarEnergyMonitorTest - copia\Data` reportado igual por propietario; ubicado en el mismo disco, por tanto no tratar como protección ante fallo físico.
- App QA en `D:\SolarEnergyMonitorQA855\App`, selector `data-path.txt`; entorno **histórico copiado** en `D:\SolarEnergyMonitorQA855\DataHistorica` (energy.db, Bills y Tariffs, sin credenciales ni respaldo anterior); entorno QA **vacío** en `D:\SolarEnergyMonitorQA855\Data`.
- El propietario verificó la ruta activa en la barra inferior durante los cambios. Los tests de conexión/descarga se ejecutaron en `DataHistorica`, sin escritura intencionada a producción. No afirmar identidad binaria de bases sin hashes antes/después.
- No copiar documentos referenciados desde rutas externas sólo para conseguir que pase un backup. No tocar bases reales mediante CI ni scripts de reparación.

## 2. Resultados funcionales observados

| Pruebas | Resultado | Evidencia / alcance |
|---|---|---|
| 01–02, 16–19 | PASS | Descarga/hash del ZIP, arranque, versión v0.10.0 Build 855, dashboard y navegación en copia |
| 20–23 | PASS | ES/EN, etiquetas, persistencia de idioma |
| 24–27 | PASS | Análisis vacío: presets, fechas, gráficas, cobertura |
| 28–36 | PASS | Preparación segura de copia histórica y lectura de datos |
| 37–40 | PASS de exportación | PDF familiar octubre de 2026, 6 páginas; fechas futuras incluidas por período seleccionado, OBS-01 |
| 41–43 | PASS de exportación | XLSX familiar 8 hojas; diferencias menores de redondeo/consistencia a revisar, OBS-02 |
| 44–47 | PASS de generación, **revisión abierta** | PDF Enel 10 páginas, anexo técnico aparte, semántica de presentación rectificada posteriormente por owner |
| 49–55 | PASS | H-01 ZIP cuatro archivos; ~32 filas diarias, 14 episodios, 137.700 registros crudos; límites del reparto diario OBS-H01-04 |
| Anexo Enel | PASS de generación/estructura | ZIP 7 archivos y PDF técnico ~132 páginas, 9.180 muestras, 5 discontinuidades; sin inferir validación metrológica |
| 60–67 | PASS de seguridad básica | SQL SELECT, consulta catálogo, CSV/XLSX, rechazo DELETE, sintaxis incorrecta y recuperación; no constituye pentest |
| 68–71, 75–78 | **FAIL parcial** | Pantalla Batería usable, pero bloqueo de UI ~1–2 s al navegar, DEF-PERF-01 |
| 75–77 | PASS | Protección, Configuración cuatro pestañas, Red/Enel seis pestañas |
| 84 | **BLOCKED (entorno)** | Backup con DB histórica copiada: documento PDF referenciado con ruta fuera de Bills/Tariffs administrados; integridad impide archivo incompleto; no atribuir automáticamente a defecto aplicativo |
| 90–94 | PASS | Backup ZIP y verificación explícita en DB QA vacía; botón de Diagnósticos navega a Protección sin generar otro backup |
| 95–98 | PASS con OBS | Atajos SQL Ctrl+R, rechazo conflictos y persistencia; botón sigue mostrando F5, alias fijo vigente |
| 99–101 | PASS reportado | Cancelación vista previa de Informe simple, recuperación posterior |
| 102–104 | **FAIL parcial** | PDF detallado y batería exportados, Excel generados y comparados; gráficos PDF vacíos y paginación defectuosa; período detallado 01-08 a 07-09 seleccionado intencionalmente |
| 105–109 | PASS con OBS UX | Guardado y persistencia de plantilla, selección reaplica tipo, fechas y agregación; carga poco evidente |
| 110–111 | N/A por decisión owner | Ayuda y exportación de ayuda fuera de alcance de aceptación Build 855 |
| 112 | PASS parcial | Identificación visual versión/build; Novedades N/A |
| 113–115 | PASS reportado | Ventana abierta ~10–15 min, navegación, respuesta y cierre; NO prueba de varias horas |
| 116–118 | PASS reportado | Autenticación y descubrimiento real de estación |
| 119–121 | PASS reportado | Descarga telemetría de un solo día 2026-09-07 sobre DataHistorica, procesamiento y revisión |
| 122–125 | PASS reportado | Lectura de estado actual, cierre, persistencia tras reinicio, aislamiento de ruta |
| 126–128 | PASS reportado | Inicio, cancelación y recuperación de recopilación |
| 129 | **PASS confirmado** | `PRAGMA integrity_check` de SQLite de DataHistorica en modo `ro` respondió `[('ok',)]` |
| Restauración integral de respaldo con PDFs originales | NOT RUN/BLOCKED | No ejecutar sobre producción ni normalizar rutas absolutas a ciegas |

Nota: `PASS reportado` significa observación del propietario; no convertirlo en validación independiente de contadores, archivo SQL o telemetría física.

## 3. Defectos confirmados para corregir

### DEF-REPORT-01 — Gráficos vacíos en PDF (MEDIA, reproducción visual)
- **Ámbito:** informe detallado de energía y batería. En PDF aparecen títulos, ejes y leyenda, pero no barras/puntos de datos.
- **Reproducción:** exportar PDF detallado sobre 2026-08-01 a 2026-09-07 y PDF batería sobre 2026-09-01 a 2026-09-07 con DB histórica copiada; abrir PDF y examinar gráficos.
- **Evidencia:** archivos `Informe detallado de energía_20260801_20260907.pdf/.xlsx` e `Informe de batería_20260901_20260907.pdf/.xlsx` aportados durante QA. XLSX sí contiene datos diarios y SOC; PDF no representa series.
- **Aceptación:** series/valores visibles, ejes y etiquetas legibles en ambos PDF; PDF/XLSX coinciden numéricamente; pruebas de render reales (no solo comprobación de que exportar no lanzó excepción).

### DEF-REPORT-02 — Pie de página superpuesto (MEDIA, reproducción visual)
- **Ámbito:** informe detallado de energía, página 1 del PDF de dos páginas.
- **Evidencia:** `Solar Energy Monitor · 1/2` invade las últimas filas de tabla. El glosario de batería se corta incómodamente entre páginas (OBS-REPORT-03, BAJA).
- **Aceptación:** márgenes/altura de tabla/footers sin solapes en hojas largas, glosario legible; comprobar PDF renderizado a imagen con distintas longitudes de tabla y culturas ES/EN.

### DEF-PERF-01 — Congelamiento UI al entrar en Batería (MEDIA, reproducción owner)
- **Síntoma:** navegación Batería produce ~1–2 segundos durante los cuales no es posible interactuar con los demás menús.
- **Diagnóstico orientativo, NO causa probada:** capturas de Diagnósticos mostraron máximos UI.Navigation ~1273 ms, UI.Dispatcher.TickLateness ~2777 ms y Data.Battery.LatestMetrics ~3508 ms. Series cortas no dan p95 fiable. `ShowPage` en MainWindow invoca refresh de Batería.
- **Aceptación:** navegación permanece interactiva con corpus grande y cambios rápidos de pestaña, sin datos atrasados de páginas abandonadas ni invocaciones remotas redundantes. Medir percentiles y n adecuadamente.

## 4. Observaciones / investigaciones no confundibles con defectos confirmados

| ID | Observación | Tratamiento |
|---|---|---|
| OBS-REPORT-03 | Glosario PDF batería se parte y segunda hoja queda casi vacía | Mejora de paginación junto a DEF-REPORT-02 |
| OBS-REPORT-04 | Fechas de informe detallado desde 01-08 fueron elegidas por usuario | **DESCARTADA** como defecto |
| OBS-REPORT-05 | Recuperar plantillas funciona, pero selector sólo está en «Informes guardados», poco visible desde «Crear/exportar» | UX BAJA: selector «Cargar plantilla» prominente y feedback; sin rediseñar persistencia |
| OBS-NUM-01 | Diferencias superiores a mero redondeo aparente entre suma de filas de XLSX y totales (batería entregada 66,9209 resumen vs 66,9765 suma diaria; casa 122,9106 vs 122,9523) | INVESTIGAR granularidad/integración, no alterar cálculos sin prueba; vincular con H-01 |
| OBS-H01-04 | En salida diaria H-01 algunas horas integradas sobrepasan 24 h por asignación de segmentos enteros al día inicial, sin dividir a medianoche | Corregir sólo si la unidad afectada requiere agregación por calendario; no reabrir motor sin pruebas |
| OBS-UX-01 | Accesos rápidos 15 días y mes móvil pedidos; existen accesos semanales y últimos 7 días | Mejora UX separable, no reconstruir lo ya existente |
| OBS-SQL-01 | Botón «Ejecutar SQL (F5)» aun cuando Ctrl+R personalizado ejecuta y F5 sigue como alias | Texto explicativo/multitecla, BAJA |
| OBS-BACKUP-01 | Historial copiado conserva referencias absolutas a documentos fuera de carpeta administrada y backup rechaza archivo incompleto | BLOCKED/ENTORNO: no eliminar guardarraíl, no tocar rutas productivas |
| OBS-ENEL-01 | Cuestionamiento anterior sobre fechas 32 días | **CERRADO por owner**: convención aprobada en `ENEL_BILL_AUDIT_REPORT_AND_EVIDENCE_SPEC_2026-10-06.md` sección 3: días inclusivos, intervalo [X 00:00, Y+1 00:00). No volver a cuestionarla |
| OBS-ENEL-02 | Informe de auditoría Enel debe exponer «Según Enel» / «Según registros del inversor», diferencia, cobertura, P5/P50/P95 y gráfico, luego tarifas/montos página 2 | Requisito explícito owner, ver especificación QA855 |
| OBS-ENEL-03 | Remover lenguaje defensivo y reiteración de limitaciones del cuerpo de PDF/Excel | Requisito owner; distinguir etiquetas «medido/estimado» y mantener controles internos |
| OBS-ENEL-04 | Anexo H-01/estadístico sigue como archivo separado; informe principal no es carta ni solicitud | Requisito owner, **no resumir** ni reducir artificialmente la auditoría |

## 5. Gate de cierre

- **No release/merge/main** por este documento. El candidato Build 855 queda **NO APROBADO PARA ESTABLE** hasta reparar defectos materiales y superar regresión.
- Un próximo CI `SUCCESS` habilita **QA dirigida**, no aprobación automática.
- Pendientes correctamente etiquetados: backup con documentos históricos reales BLOCKED, restore histórico completo NOT RUN, soak largo NOT RUN, validación del modelo estadístico no acreditada. No convertirlos en PASS por analogía.
- No generar Builds de cortesía: agrupar cambios y ejecutar CI + una QA focalizada del propietario.
