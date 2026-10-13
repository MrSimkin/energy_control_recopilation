# PROMPT PARA WORKER — Correcciones de QA Build 855

> Copiar el bloque «Instrucciones de ejecución» a un chat Worker con acceso al repositorio. El Worker realiza implementación, CI y entrega; este archivo no ejecuta ninguna Build por sí solo.

## Instrucciones de ejecución

Actúa como ingeniero responsable del ciclo correctivo de **Solar Energy Monitor / SolarOfThings, posterior a Windows Build 855**.

### A. Objetivo y autoridad

**Repositorio:** `MrSimkin/energy_control_recopilation`.

**Rama de trabajo inicial obligatoria:** `work/qa855-corrections-package-20261012`, creada desde `work/qa-unified-h01-enel-family-pdf-20261012` en `c1b0ee4345cc64dbe51921d70cbc1ee02f499465`. Esta rama ya incluye registro y decisiones QA; **verifica su HEAD remoto vigente antes de modificar nada**. Consulta PR #4 abierto y `main`, pero **NO escribas en main, NO hagas merge ni release**. Continúa implementando directamente en la rama correctiva tras preflight; no crees otra rama innecesaria.

**Lee en este orden, sólo una vez**:
1. `solar_of_things_windows_app/QA855_REGISTRO_QA_2026-10-12.md`.
2. `solar_of_things_windows_app/QA855_CONTRATO_CORRECCIONES_2026-10-12.md`.
3. `solar_of_things_windows_app/QA855_FUENTES_APTOS_LICENCIA_2026-10-12.md`.
4. `solar_of_things_windows_app/PRE_BUILD_OBSERVATION_REVIEW_RULE.md`.
5. `solar_of_things_windows_app/BUILD_HANDOFF_RULE.md`.
6. `solar_of_things_windows_app/ENEL_BILL_AUDIT_REPORT_AND_EVIDENCE_SPEC_2026-10-06.md` en las secciones de cálculo y convención temporal; si hay conflicto de **redacción externa**, prevalecen las decisiones owner en documento QA855.
7. Sólo los archivos de código/test pertinentes. No volver a leer toda la historia del repo.

El propietario ya ejecutó aproximadamente 129 pasos manuales sobre Build 855; **NO** repetir pruebas aprobadas sin cambio funcional directo.

### B. Seguridad innegociable

- No acceder, tocar, restaurar, limpiar, migrar ni escribir la base productiva `D:\SolarEnergyMonitorTest\Data`.
- La QA del usuario usó copia histórica `D:\SolarEnergyMonitorQA855\DataHistorica` y copia QA vacía `D:\SolarEnergyMonitorQA855\Data`. Los datos reales, boletas/credenciales del propietario NO se deben subir a GitHub ni usar en CI.
- No modificar código de arranque para apuntar silenciosamente a producción. Respetar `data-path.txt` y verificación visible.
- Usar corpus sintéticos y fixtures despersonalizados. No fabricar éxito de backup histórico: bajo rutas absolutas externas quedó BLOCKED por diseño protector.
- No inventar timestamps, cálculos, percentiles, montos ni métricas. Todo hallazgo FAIL se reproduce y corrige con evidencia.
- `main`, PR anteriores y la Build 855 son referencia inmutable hasta autorización expresa.

### C. Correcciones P0/P1 materiales

**P0.1 DEF-REPORT-01: PDF con gráficos vacíos.** En Detailed Energy y Battery, PDF 855 muestra título/ejes/leyenda sin las series reales que sí aparecen en los XLSX. Localizar renderer y probar PDF rasterizado con series; reparar sin cambiar valores. Añadir tests automatizados visuales o verificaciones estructurales suficientes, también en español/inglés.

**P0.2 DEF-REPORT-02: paginación.** Reparar tabla invadiendo footer de Detailed Energy; separar/repetir encabezados y asegurar glosario legible en Battery. Verificar PDF renderizado sin clipping ni solapes, corto/largo, varias páginas.

**P0.3 DEF-PERF-01: bloqueo de navegación a Batería ~1–2 s.** Reproducir en corpus grande, medir origen en hilo UI y corregir minimizando consultas o pasando trabajo pesado a background con cancelación/generation-id. No mostrar resultados obsoletos al salir/entrar. El diagnóstico de máximos anteriores es indicio, no prueba causal. Mantener indicador visual canónico de trabajo si la operación demora.

**P0.4 Auditoría PDF de boleta Enel — presentación aprobada por owner.** NO cambiar naturaleza del informe: NO carta, NO solicitud, NO reducir a dos páginas, NO fusionar anexo con PDF. Reorganizar contenido existente:
- **Página 1:** «Según Enel» consumo/lecturas; «Según registros del inversor» observado; diferencia kWh/%, cobertura observada/faltante, breve descripción del método, tabla P5/P50/P95 con diferencias, **gráfico empírico real** con referencias P5/P50/P95/Enel (no Gauss).
- **Página 2:** tarifa Enel, tasas oficiales reconstruidas, escenario económico observado/P5/P50/P95, diferencias CLP y tabla clara de componentes. Mantener cargos no variables.
- **Siguientes páginas:** conservar detalle, fuentes y método; ordenarlos mejor, quitar lenguaje defensivo o retórico («no inventamos», «no prueba», «no certificado», «limitaciones», etc.) del texto visible PDF/XLSX cuando no aporta un dato operativo. No ocultar la distinción impreso/medido/estimado/derivado ni crear falsa certeza. Controles e hipótesis técnicas quedan trazables en anexo/código.
- **Fechas Enel asunto cerrado:** aplicar la convención temporal vigente aprobada `[X 00:00 local, Y+1 00:00 local)`. NO cuestionar de nuevo 31/32 días ni exigir horas exactas. Preservar DST America/Santiago.
- Anexo técnico permanece como archivo separado con trazabilidad; no escribir 132 páginas dentro del PDF principal.
- Al final revisar formato y claridad de PDF **renderizado**, no solo texto extraído.

**P0.5 OBS-NUM-01:** comparar resúmenes contra sumas diarias en archivos de batería/detallado; investigar diferencias. No corregir matemáticas sin encontrar causa demostrada, deja trazabilidad si no procede.

**P1 condicionados, no bloquear Build por ellos:** OBS-H01-04 (segmentos diarios atravesando medianoche, DST), OBS-REPORT-05 (selector «Cargar plantilla» en pantalla Crear/exportar con feedback), OBS-UX-01 (15 días y mes móvil sin duplicar presets existentes), OBS-SQL-01 (atajo F5 + personalizado). Tratar simultáneamente si son pequeñas correcciones seguras; de lo contrario documentar deferment.

### D. Fuente monoespaciada — requisito con bloqueo de licencia parcial

El usuario proporcionó `Microsoft Aptos Fonts.zip` y quiere **Aptos Mono Regular/Bold** para cifras en PDF y Excel. Existe ya inventario/hash/EULA en `QA855_FUENTES_APTOS_LICENCIA_2026-10-12.md`. **No están ni deben estar los TTF en GitHub**: el repo es público, la licencia Microsoft adjunta prohíbe distribución/sublicencia, y dichos TTF llevan `fsType=8` (embedding restringido). El ZIP debe permanecer fuera del repo; el propietario puede adjuntarlo a tu chat para inspección local, nunca para subirlo a artefactos públicos.

- Implementa estilos numéricos monoespaciados con Aptos Mono cuando legalmente instalada localmente; no degradar valores numéricos XLSX a strings.
- Para PDF portable/redistribuible, exige una fuente monoespaciada con derechos suficientes de incrustación/distribución, o licencia explícita adicional. Si no puede satisfacerse Aptos legalmente, **reportar bloqueo específico** y ofrecer alternativa abierta (ej. licencia SIL OFL) para aprobación; **no reemplazar silenciosamente la elección de fuente**.
- Puedes seguir cerrando P0 PDF, rendimiento y reporte sin resolver primero ese permiso.

### E. Secuencia de implementación / evidencia

1. **Preflight:** HEAD, estado rama/PR y reglas de safety; comprobar condiciones y empezar a corregir código, no iniciar una nueva auditoría documental.
2. **Un bloque coherente de correcciones:** priorizar PDF rendering/paginación, presentación Enel, rendimiento Batería y tests; minimizar builds individuales. No mezclar futuros cambios de arquitectura.
3. **Tests:** .NET 10 WPF Release, suites SQLite/PDF/XLSX, datos sintéticos representativos, ES/EN, cancelación y navegación, DST local, reproducibilidad numérica. Exportar PDFs sintéticos y examinar efectivamente su render a imágenes; que los charts no sean vacíos y tablas no invadan footers. XLSX validar formatos de celda y valores no solo apertura.
4. **CI Windows:** publicar sólo tras tests, esperar resultado CI. `SUCCESS` es comprobación técnica, no aceptación final.
5. **Artefacto:** entregar una Build consolidada. Handoff al propietario según `BUILD_HANDOFF_RULE.md`: ZIP descargable directo si disponible, nombre `SolarEnergyMonitor-Build-XXX-win-x64.zip`, commit, run, SHA256 y 4–8 pasos de QA enfocada. No tocar los Datos ni instalar sin confirmación del propietario.
6. **Recibo:** agregar `solar_of_things_windows_app/QA855_CORRECCIONES_RECIBO.md` con tabla ID → cambio código → test → commit → CI → estado, lista de limitaciones no solucionadas y deuda deferida. No afirmar que un defecto quedó corregido sin test relevante.
7. **STOP:** solo ante riesgo de datos, cambio de contrato físico, pérdida de evidencia, incertidumbre legal de distribución o divergencia material de rama. Ante bloqueo de fuentes, continuar partes independientes.

### F. Criterio final

Resultado esperado: nueva Build en rama **sin merge**, los tres defectos materiales de Build 855 reparados y retesteados, auditoría Enel en presentación aprobada, Excel y PDF con cifras alineadas y formato legible, anexos independientes, QA acotada lista. No afirmar fuente propietaria incorporada si no se resolvió licenciamiento. No consumir sesiones pidiendo al usuario repetir 129 pasos.

**Al cerrar responde en español:** cambios reales, IDs PASS/FAIL/BLOCKED, commit/Build/CI, enlace directo al ZIP cuando posible, instrucciones mínimas de QA e ítems expresamente pendientes.
