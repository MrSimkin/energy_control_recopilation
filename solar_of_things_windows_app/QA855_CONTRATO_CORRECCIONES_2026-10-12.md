# Build posterior a 855 — Contrato de corrección funcional, informes y experiencia

**2026-10-12 · Decisiones expresas del propietario · DOCUMENTO VIGENTE para esta tranche de correcciones.**  
Aplica a la rama de preparación `work/qa855-corrections-package-20261012` derivada del commit QA 855 `c1b0ee4345cc64dbe51921d70cbc1ee02f499465`. No autoriza un merge/release o cambios en producción.

## 1. Orden de autoridad, compatibilidad y criterio de intervención

1. Leer `QA855_REGISTRO_QA_2026-10-12.md` (hechos y estados).
2. Este documento prevalece sobre **redacción y presentación externa** incompatible en `ENEL_BILL_AUDIT_REPORT_AND_EVIDENCE_SPEC_2026-10-06.md`; la especificación previa permanece vigente en **semántica energética, convención temporal, fuentes y modelo económico**.
3. Respetar `PRE_BUILD_OBSERVATION_REVIEW_RULE.md`, `BUILD_HANDOFF_RULE.md` y las reglas de protección de datos.
4. No rehacer modelos ni introducir supuestos ajenos para simplificar. Corregir con pruebas demostrativas y delimitación precisa.

## 2. Informe Enel: el formato actual es el correcto, CAMBIAR CLARIDAD NO NATURALEZA

El PDF es un **informe técnico de auditoría de boleta**, **NO** una carta/reclamo/solicitud. **NO** reducir el PDF a dos páginas. El anexo técnico es **otro archivo** ya previsto; mantener exportación separada con integridad, manifiesto y trazabilidad, no incorporarlo indiscriminadamente al PDF principal.

### 2.1. Página 1: energía y distribución estadística

Presentar en este orden y con títulos que identifiquen fuentes:
1. **«Según Enel»**: consumo facturado 97 kWh y lecturas disponibles (caso de aceptación de agosto/septiembre 2026).
2. **«Según registros del inversor»**: importación observada integrada (caso Build 855: 88,065 kWh). No llamar esta fuente «Solar of Things» en la narrativa.
3. **Diferencia aritmética:** Enel − inversor, kWh y porcentaje con base explícita (caso Build 855: +8,935 kWh / +9,21% respecto del consumo Enel).
4. **Cobertura:** tiempo cubierto 99,33%; faltante 0,67% (caso 855). Expresar cuánto se midió, cuánto falta y período.
5. **Método de estimación, breve y afirmativo:** para el tiempo restante, muestreo de ventanas históricas comparables por horario/día; estimaciones con P5, P50, P95. Explicar qué representa cada percentil en una frase simple, sin un manifiesto de advertencias.
6. **Tabla P5/P50/P95:** energía total calculada + diferencia contra Enel en kWh y %. Caso Build 855 P5 88,103, P50 88,103, P95 96,606 kWh; todos los números se recalculan con motor vigente, no hard-code.
7. **Gráfico empírico REAL en página 1:** eje kWh y frecuencias, concentración discreta del mínimo, marcadores y leyenda identificables P5/P50/P95 y línea Enel 97. No fabricar normalidad/Gauss ni barras con altura falsa. Para este corpus 2184/3375 escenarios comparten mínimo (64,71%). El gráfico debe **verse en PDF renderizado**.
8. Conservar narrativa breve «Qué representa el resultado» únicamente para explicar resultados de modo ordenado, sin defensa anticipada.

**No duplicar gráfico de frecuencia en página 5 si ya está en página 1 salvo que la página posterior aporte nuevo nivel analítico**; mantener material técnico adicional donde corresponda.

### 2.2. Página 2: tarifa e implicancias de las diferencias

Presentar, en este orden:
1. **«Según Enel»**: cargo total $26.854 y componentes impresos relevantes.
2. **«Tarifa reconstruida por el sistema»**: tasas oficiales aplicadas, tarifa/peso de vigencias por período y componentes fijos/variables, sin afirmar identificación tarifaria única por coincidencia parcial.
3. **«Diferencias calculadas»**: total económico comparable por escenarios observado / P5 / P50 / P95, conservando cargos no variables y subsidios realistas sin recálculo infundado.
4. Diferencias CLP respecto al total facturado; caso 855 P50 $24.702 / dif. $2.152 y P95 $26.759 / dif. $95.
5. Fórmula/criterio de reconstrucción explicada de manera concisa; tabla de montos claros.
6. No convertir importes hipotéticos en montos exigibles ni multiplicar todo el total por ratio kWh; las etiquetas «calculado» y «escenario» bastan.

### 2.3. Páginas 3+ y anexo

Preservar el **detalle completo** útil para auditar: conciliación tarifaria, componentes, muestras, gaps, cobertura diaria, hallazgos, tablas, fuentes/hash y método. Reorganizar títulos, jerarquía y tablas para explicar «qué es / de dónde sale / cómo se calcula». No quitar los percentiles, no ocultar la estadística ni perder referencias.

**Estilo expresamente rechazado por el propietario en informes PDF/XLSX:** bloques retóricos de «limitaciones», «no inventamos datos», «no podemos considerar X», «esto no demuestra Y», «no es certificación estadística», «no se puede comparar» y equivalentes como advertencia, autodefensa o hipotética réplica de Enel. No insistir con horas desconocidas de lectura.

**Preservar precisión factual y controles de ingeniería sin presentarlos como advertencias**: distinguir valores impresos, observados, integrados, estimados y contrafactuales; documentar supuestos/metadatos en el anexo, logs o documentación técnica donde sea necesario; no atribuir certeza ni origen incorrectos, ni suprimir información indispensable para que una cifra resulte comprensible.

### 2.4. Convención temporal: decisión cerrada

**NO reabrir en esta tranche** el debate 31 vs 32 días ni reconstruir hipótesis horarias. El propietario determinó cómo interpretar las fechas de Enel; está documentado en especificación canónica del 2026-10-06, sección 3: `[X 00:00:00 local, (Y+1) 00:00:00 local)`; días inicial y final completos incluidos. Los datos completos están en archivo anexo; su evaluación posterior corresponde a un contraste técnico, no al encabezado del PDF. Mantener pruebas DST America/Santiago y no modificar ese contrato por una observación antigua ya cerrada.

## 3. Tipografía numérica en PDF y Excel — FUENTE LIBRE YA INCORPORADA

- El propietario adjuntó Microsoft Aptos y precisó que quiere **usar una fuente mono en la App**, no redistribuir los archivos Microsoft. Dada la EULA del paquete, **no empaquetar Aptos Mono**.
- El propietario autorizó sustituir por una fuente legalmente distribuible; **se seleccionó y SUBIÓ al repo IBM Plex Mono Regular/Bold**, bajo SIL Open Font License 1.1.
- Fuente predeterminada y común de la Build correctiva: **IBM Plex Mono** para valores numéricos, importes CLP, fechas, kWh, porcentajes y tablas cuantitativas de **todos los PDF y Excel pertinentes**. Textos y títulos siguen con fuente proporcional legible.
- Ruta: `solar_of_things_windows_app/assets/fonts/IBM-Plex-Mono/` con los binarios `IBMPlexMono-Regular.ttf` y `IBMPlexMono-Bold.ttf`, la licencia `OFL.txt` y el `README.md` de integración.
- Los TTF deben copiarse al artefacto Windows con su licencia; PDF debe incrustarlos correctamente mediante resolver de fuentes; Excel debe conservar celdas numéricas/fecha reales con estilo `IBM Plex Mono`. Si la familia no está instalada en equipo lector de XLSX, se necesita disponibilidad o fallback claro. No forzar embedding de Microsoft Aptos.
- **Fuentes y licencia: EN REPOSITORIO. Integración en el exportador/Build: pendiente de Worker.** No solicitar más archivos al propietario para este paso.
- QA: inspeccionar nombres reales de las fuentes PDF, legibilidad, grosor regular/bold, signos/decimales y formato nativo numérico en XLSX; mantención de uso de licencia.

## 4. DEF-REPORT-01: gráficos PDF

Localizar ruta de rendering (MigraDoc/PDFsharp y gráficos). Las series figuran en código pero son invisibles en PDF 855. Probar implementación real no sólo existencia de objetos: exportar, rasterizar página, confirmar trazos/barras reales, cantidad de etiquetas y alturas >0 en corpus con valores no nulos; test con paneles de SOC y energía de dos o más muestras; idioma ES/EN. No modificar dataset para esconder fallo. Mantener consistencia con XLSX.

## 5. DEF-REPORT-02: saltos de página

Garantizar área útil que reserve pie/encabezado y salto de tablas antes de invadir footer, repetición adecuada de cabeceras y filas indivisibles. Aceptación: páginas de informe detallado / batería sin solapes y glosario legible con corpus corto/largo. Inspección visual de PDF renderizado obligatoria.

## 6. DEF-PERF-01: navegación Batería

Medir localmente con corpus grande y un conjunto repetible. Identificar paso en hilo UI (consulta SQLite, LINQ, binding WPF, formateo, dispatcher, actualización remota) que explique el bloqueo. Corregir con trabajos asíncronos/cancelación por generación y actualización segura de UI, sin cambiar semántica energética ni mostrar resultados obsoletos. Probar navegación rápida Batería↔Análisis↔Dashboard y capacidad real de pulsar otros menús durante carga. No adjudicar causa basada solo en máximos del instrumento.

## 7. Diferencias numéricas, H-01 y backlog UX

- **OBS-NUM-01**: determinar origen de diferencias por agregación de filas vs resumen mediante caso de regresión y revisión de granularidad; SOLO corregir si defecto reproducido; no alterar resultados del motor por mera conciliación visual.
- **OBS-H01-04**: días diarios con cobertura integrada >24h, examinar segmentos que cruzan medianoche; si aplica corrección, dividir en frontera temporal local y validar DST/días de 23/25h; no asumir todo día es 24h.
- **OBS-REPORT-05**: mostrar «Cargar plantilla» claramente en pantalla de creación; al seleccionar actualizar formulario y dar confirmación. Mantener persistencia que ya pasa QA.
- **OBS-UX-01**: conservar presets existentes y agregar 15 días y mes móvil, sin duplicar semana/últimos 7 días.
- **OBS-SQL-01**: etiqueta atajos describir F5 fijo + shortcut personalizado; mejora menor.
- **OBS-BACKUP-01**: el respaldo con rutas antiguas no pasó porque referenced PDFs están fuera de managed folders; no desactivar protección ni tocar DB real. Registrar BLOCKED hasta fixture seguro.
- No ensanchar alcance: primero defectos confirmados, informe Enel, tipografía con licencia; UX menores pueden incluirse si coste local sin riesgo, o marcarse diferidas.

## 8. QA y salida

- Implementación en rama derivada de PR #4 no fusionada; verificar HEAD actual antes de editar. `main` sin escrituras, no hacer merge/release sin instrucción.
- Pruebas unit/integración PDF, Excel, cálculos, CSS/WPF/Async, DST y corpus sintético; CI Windows Build. Revisar visuales finales realmente, no dar PASS sólo porque exportó.
- Ningún QA en datos productivos. Si owner prueba, usar `D:\SolarEnergyMonitorQA855\DataHistorica` con respaldo local actual y `data-path.txt` revisado; no reinstalar/reemplazar su Data.
- No pedir 129 pruebas otra vez: QA dirigida a PDF/Excel Enel, informe detallado/batería, rendimiento Batería, plantillas y, si fue modificada, consistencia numérica.
- Entrega de una Build consolidada con ZIP directo si herramientas lo permiten, SHA256, commit, CI, pasos acotados. El propietario decide aceptar/merge.
