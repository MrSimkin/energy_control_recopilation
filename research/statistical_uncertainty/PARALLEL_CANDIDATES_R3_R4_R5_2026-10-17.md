# Candidatos estadísticos paralelos R3 / R4 / R5 — Registro de investigación

Fecha: 2026-10-17 (America/Santiago).
Estado de este documento: **PROPUESTA DE INVESTIGACIÓN / REGISTRO DE CANDIDATOS**.
No es protocolo congelado, implementación, validación, aceptación de producción ni autorización para modificar reportes.

## 1. Principio de coexistencia

Mantener **R3 exactamente como está**, sin retocar algoritmo, parámetros, reglas de parada, corpus reservado, métricas ni umbrales de avance. R4 y R5 serán **comparadores independientes en paralelo**, no reemplazos, extensiones automáticas ni fuentes para recalibrar R3.

El objetivo común es predecir la energía **de importación desde la red** (kWh) durante vacíos de `grid_import_power_w`, con P5/P50/P95 y señalización honesta de insuficiencia. La incertidumbre de atribución de consumo doméstico es un problema distinto y no debe sumarse a esta variable. Los valores de Enel/boletas/medidor externo **no pueden entrar** en entrenamiento, selección, ajuste, calibración ni ranking.

## 2. Identificadores reservados

| Etapa | Identificador técnico | Estado |
|---|---|---|
| R3 | `grid-import-boundary-residual-dayweighted-240m.candidate-v4` | Candidato EXISTENTE, protocolo de validación futura congelado; no validado ni aprobado para producción. |
| R4 | `grid-import-boundary-hurdle-dayweighted.candidate-v5` | NUEVO, recomendado para implementación **solo experimental**; diseño detallado y validación pendientes. |
| R5 | `grid-import-hidden-semi-markov-duration.candidate-v6` | NUEVO, investigación **condicionada a factibilidad de datos**; diseño detallado y validación pendientes. |

**Aclaración:** R3, R4, R5 son identificadores de líneas de investigación; `candidate-v4/v5/v6` identifica la versión secuencial del candidato y no implica que exista un "modelo R4" anterior por llevar el sufijo `v4`.

## 3. R4 — Hurdle condicionado por extremos y ponderado por día

**Hipótesis a contrastar:** debido a la gran cantidad de importación cero en esta instalación, puede mejorar la predicción de energía total de un vacío modelar por separado (1) probabilidad de energía nula/casi nula y (2) distribución de energía positiva.

Esquema:
- Variable objetivo: **energía total del intervalo** en kWh, calculada por integración sobre intervalos completos observados con tiempos reales, nunca muestras de 5 minutos supuestas independientes.
- Entrada disponible al predecir: potencia observada a ambos lados cuando existe, duración real, franja horaria y tipo de día; incorporar otros predictores exclusivamente si están efectivamente disponibles y son de confianza en el momento de la predicción.
- Capa 1: probabilidad de clase de energía cero/casi cero con umbral físico/instrumental **prefijado y justificado**, no optimizado sobre datos futuros.
- Capa 2: distribución de energía positiva con familia y regularización a determinar en desarrollo; evitar complejidad innecesaria y ajuste por superposición masiva de ventanas.
- Evidencia efectiva: agrupar/ponderar por fecha local, usando solo fechas anteriores al objetivo. Calibrar P5/P50/P95 sin confundir prevalencia de ceros con buena cobertura de importación activa.
- Si hay pocos días positivos comparables, no fabricar intervalos: emitir **INSUFFICIENT**.

**Razón para incluir:** la telemetría histórica examinada tiene muchos ceros y fuerte persistencia, y los modelos anteriores ocultaban fallos en períodos activos al mostrar buenos promedios globales.

## 4. R5 — Semi-Markov oculto de estados y duraciones

**Hipótesis a contrastar:** la duración de episodios persistentes sin importación, con importación baja y con importación alta podría ser esencial para estimar vacíos, y no queda representada explícitamente por el R3.

**Compuerta previa obligatoria de factibilidad:** contar fechas independientes, episodios completos, transiciones, duraciones de los estados y casos activos por grupo. Si la evidencia es insuficiente o las estimaciones resultan inestables, **NO IMPLEMENTAR** R5 como modelo predictivo; registrar `NOT FEASIBLE`.

Si supera esa compuerta:
- Prototipo pequeño con **dos o tres estados latentes** y distribución explícita de duración de episodios; transiciones y potencia asociada a cada estado.
- Para un vacío, inferir/simular trayectorias compatibles con extremos válidos y duración real; integrar cada trayectoria para obtener distribución de kWh y P5/P50/P95.
- Entrenar solo con historia disponible al comienzo del objetivo y controlar sobreajuste, incertidumbre paramétrica y dependencia entre episodios.
- Mostrar límites de aplicabilidad y `INSUFFICIENT` cuando falten estados, soporte temporal o extremos necesarios.

**Razón para incluir:** los experimentos del repositorio demuestran que tratar segmentos sucesivos como independientes destruye la persistencia de episodios ON/OFF.

## 5. Evaluación común, control de fuga y selección

1. **Conservar R3 congelado.** Su futuro protocolo y su conteo/score existentes siguen siendo autoridad para su validación; ninguna modificación por la incorporación de R4/R5.
2. **Investigación histórica R4/R5:** usar historia de desarrollo y evaluación cronológica retrospectiva sin fuga, separando días completos. Los datos anteriores al 2026-09-28 ya participaron en diseños previos y **no son una validación independiente definitiva** de R4/R5.
3. **Congelar R4 y R5 antes de evaluar resultados futuros.** Documentar versiones ejecutables, parámetros, selección de objetivos, umbrales de datos y compuertas estadísticas. No usar resultados futuros de R3 para ajustar R4/R5.
4. **Comparación futura justa:** escoger un período verdaderamente nuevo para R4/R5, posterior a sus respectivos congelamientos y sin inspección previa de sus resultados; evaluar R3 como comparador fijo sobre los mismos objetivos elegibles. No llamar "validación pristine R4/R5" a datos ya inspeccionados para rediseñarlos.
5. **Mismos ocultamientos artificiales de verdad conocida**, idénticas reglas de calidad de medidores y timestamps, mismos casos, sin intervención de Enel; informar rendimiento tanto en la **intersección elegible** como en la cobertura de aplicabilidad de cada candidato. Los vacíos naturales pueden diferir de los artificiales y esa limitación debe declararse.
6. **Métricas predeterminadas:** cobertura predictiva P5–P95, sesgo/MAE/RMSE del P50, interval score (alfa=0.10), anchura de intervalos, insuficiencia de evidencia, estabilidad temporal, intervalos de confianza agrupados por día. Estratos: inicio ACTIVO/INACTIVO, duración, reloj local/día tipo, intervalos de varios vacíos y más de 240 minutos cuando existan casos suficientes.
7. **No elegir solo por cobertura:** intervalos innecesariamente gigantes son un fallo de utilidad. No proclamar superioridad por una diferencia pequeña/inestable. Registrar cualquier fallo en periodos activos o vacíos largos.
8. **Vacíos superiores a 240 minutos:** R3 sigue **NO APLICABLE**; R4/R5 solo podrían emitir afirmaciones calibradas si se obtuviera validación independiente suficiente para ese rango. Nunca extrapolar silenciosamente.
9. **Múltiples candidatos:** ajustar interpretación por comparaciones múltiples; un ranking exploratorio no equivale a aceptación estadística. Si se usa evidencia futura para elegir/retocar un ganador, este requiere **otra validación independiente**.
10. **Separación operativa:** investigar inicialmente como herramientas/scripts reproducibles independientes, **sin reemplazar servicios C#**, sin builds, sin cambiar PDFs de Enel ni afirmar aprobación de modelos.

## 6. Orden recomendado y siguientes entregables

- **Primero, R4:** diseño matemático/factibilidad/implementación experimental y backtest histórico reproducible.
- **Luego, compuerta R5:** decidir con conteos de episodios y fechas si existen datos suficientes; implementar únicamente si PASA.
- Preparar protocolos futuros independientes R4/R5 con compuertas fijadas **antes de ver sus resultados**.
- Continuar la acumulación de evidencia y el procedimiento `count` de R3 sin alteración.

Fuentes canónicas previas:
- `research/statistical_uncertainty/PHASE2R_R3_FUTURE_VALIDATION_PROTOCOL_2026-09-30.md`
- `research/statistical_uncertainty/PHASE2R_R3_FUTURE_COUNT_REPORT_2026-10-06.md`
- `research/statistical_uncertainty/PHASE3_V2_RERUN_REPORT_2026-09-30.md`
- `research/statistical_uncertainty/PHASE1_LOT5_PERSISTENT_REGIME_REPORT_2026-09-30.md`

**Control de estado:** Este archivo registra la decisión de **probar en paralelo**, pero por sí mismo **no implementa, congela ni valida** R4/R5. La rama se conserva separada de `main` y de las ramas/PR de la aplicación.
