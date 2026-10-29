# Auditoría de las dos aplicaciones y necesidad de nuevos datos

**Fecha:** 2026-10-28. **Tipo:** lectura de código y análisis SOLO de metadatos de un ZIP exportado; sin modificar ejecutables, servicio, SQLite ni `main`. Punto de referencia del código: `main` `affd957135d19fc47090329d79cfd5d3c163b499`.

**Interpretación de «dos apps» del repositorio:** aplicación **Solar Energy Monitor Windows** (`src/SolarOfThings.App`, `src/SolarOfThings.Core`) y programa independiente **Research Exporter R1** (`research/statistical_uncertainty/research_exporter/`). La app oficial de un tercero (web/móvil Solar of Things) no es código nuestro y no se propone modificarla.

## Qué comprobamos en código

**Aplicación Windows:**
- `HistoryIngestionService.SyncAsync`: sincroniza fechas, intenta primero historial por claves y usa `record/list` como fallback; registra `history_day_status` con `COMPLETE`, `OPEN`, `PARTIAL`, `EMPTY`, `UNAVAILABLE`; admite reconsulta manual de **un solo día** y retry acotado.
- `HistoryRepository`: conserva `history_sample`, estado diario, `raw_api_capture`, `sync_run`, sin requerir una nueva columna de energía para R4/R5.
- La marca `COMPLETE` expresa que terminó la respuesta de fuente, **no** que haya llegado exactamente una lectura cada cinco minutos. No reinterpretar el estado a posteriori.
- `MainWindow.xaml.cs` tiene temporizador de lectura del estado vivo de 150 s; eso **no convierte** el historial exportado en más eventos estadísticamente independientes y no justifica reducir el polling.

**Research Exporter R1:**
- `ResearchPackageExporter.cs` abre SQLite con `SqliteOpenMode.ReadOnly`, `SqliteCacheMode.Private` y selecciona el dispositivo con más `grid_import_power_w`.
- El ZIP contiene `normalized_metrics.csv` (red/casa/PV/potencia batería/SOC), `mode_context.csv`, `historical_soc_config.csv`, `history_day_status.csv`, ajustes, README y manifiesto con SHA-256. Sin datos de Enel, IDs reales, credenciales ni payloads crudos.
- El CSV de estado diario **no exporta** `detail_json`, aunque el código de ingesta guarda en él resúmenes de intervalos de tiempo, error y reintentos. No se requiere para evaluar energía, pero podría servir para **diagnosticar el origen** de días incompletos; exigir sanitización antes de pensar en exportarlo.

## Qué comprobamos en la exportación real

Archivo privado: `SolarOfThings-ResearchPackage-20261028-131104.zip`, verificado previamente con SHA/manifest. Auditoría nueva **solo de timestamp, metric_key, confidence, quality y estado diario; no se cargó `normalized_value`, no se calculó consumo ni score**.

| Observación | Resultado agregado |
|---|---:|
| Registros de `grid_import_power_w` | **52.509** |
| Filas de red con `confidence=CONFIRMED` | **52.509** |
| Registros para cada una de las cinco series | **52.509** |
| Instantes distintos por serie | **52.509** |
| Días con alguna muestra de red | **192** |
| Días con estado de historial `COMPLETE` | **191** |
| Días con menos de 280 muestras | **23**, incluido el último día aún abierto |
| Días con `COMPLETE` y menos de 280 muestras | **22** |

**Riesgo real:** el estado de sincronización `COMPLETE` no garantiza suficiente densidad estadística, pero las cinco series sí tienen igual número de timestamps y las muestras de red están confirmadas. No hay evidencia en este paquete de que falten columnas fundamentales para R4/R5.

**DST Chile:** el 2026-09-06 tuvo **274 muestras frente a 276 posiciones nominales de cinco minutos** en un día de 23 h. Un corte fijo de 280 lo excluiría aunque esté casi completo. Se debe diseñar una regla propia compatible con DST para **futuros** R4/R5, nunca alterar el criterio congelado de R3 para favorecer una aprobación.

## ¿Actualizar alguno de los dos programas ahora?

| Componente | Decisión | Justificación |
|---|---|---|
| Windows: captura de métricas | **NO ACTUALIZAR AHORA** | Las cinco series ya están presentes y sincronizadas; más llamadas o un modo de operación artificial no producen más días de importación real |
| Windows: visualización de cobertura diaria | **MEJORA OPCIONAL, diferida** | Mostrar muestras vs expectativa del día y distinguir «descarga completa» de «día con evidencia suficiente» podría ayudar a diagnosticar sin confundir a usuario |
| Windows: reconsulta de días concretos | **YA EXISTE, con precaución** | Solo tras confirmar beneficio, límites de API, copia/QA y consentimiento; una reconsulta no inventa muestras que el origen no entregó |
| Research Exporter: contenido energético | **NO ACTUALIZAR AHORA** | Exporta todos los campos estructurales necesarios; análisis de calidad se puede derivar por fuera, sin romper compatibilidad |
| Research Exporter: archivo opcional de calidad por día | **P1 opcional** | Una exportación anónima de conteos por día, huecos temporales, expectativas DST y fuente ayudaría al seguimiento; exigir versión/compatibilidad y tests, mantener v1 válido |
| Research Exporter: causas de errores/gaps | **P2, si se necesita** | Extraer SOLO métricas numéricas seguras de `detail_json`, nunca errores textuales, rutas, payloads o secretos |
| R4/R5: multiplicar artificialmente eventos | **PROHIBIDO como estrategia** | No cambiar inversor, batería, prioridad de red ni consumo doméstico para «completar» evidencia estadística |

**Distinción importante:** cobertura de datos y presencia de eventos son problemas separados. Mejorar cobertura ayuda a conservar días útiles, pero no garantiza más eventos positivos ni episodios raros. Los días nuevos solo serán test verdaderamente independiente para R4/R5 si se congelan primero sus modelos y criterios.

## Recomendación de ejecución

**P0 ahora (solo investigación, sin builds):** auditoría read-only de calidad usando los CSV ya exportados, sin leer potencia/energía interior. Validar ZIP/manifiesto, recuentos por fecha, metadatos `CONFIRMED`, co-presencia de series, continuidad y cambio de hora de Chile. No afecta R3.

**P1 para backlog, NO ejecutar ahora:** diseño de pantalla de cobertura diaria Windows o reporte de calidad del Exporter (con tests sintéticos de 23/24/25 h), únicamente si reduce trabajo manual real, previa revisión de las prioridades de Build 866 y sin crear nueva build independiente.

**Solo ante evidencia de fallo:** abrir incidencia de ingestión con fecha y síntoma reproducibles. Separar de protocolo R3, QA de Windows y mejoras estadísticas; no escribir en la base de datos de producción durante auditorías.

**Seguridad:** no se modificó la app ni Research Exporter, no se hicieron commits a `main`, no se tocó `energy.db`, backups, WAL ni datos Enel. Este documento es evaluación y propuesta, no autorización para cambios productivos.
