# Solar Energy Monitor — Línea base consolidada para v0.11.0

**Fecha de corte:** 2026-10-08 (Chile)  
**Estado:** decisiones funcionales confirmadas por el propietario; desarrollo parcial preexistente; nueva etapa de implementación aún pendiente de ejecutar a partir de esta consolidación.  
**Carácter:** resumen **operativo vigente**, no historial de cambios ni certificado de pruebas.  
**Destino exclusivo:** rama \`work/phase10-12-consolidated-20261007\`, PR **#1 Draft**, sin merge a \`main\`.  
**Fuente primaria de las aprobaciones:** [\`NEXT_TRANCHE_DECISIONS_2026-10-08.md\`](NEXT_TRANCHE_DECISIONS_2026-10-08.md).  
**Fuente primaria de lo efectivamente probado:** [\`PHASE_10_12_TRANCHE_STATUS.md\`](PHASE_10_12_TRANCHE_STATUS.md) y [\`CONTINUITY_STATUS.md\`](CONTINUITY_STATUS.md).

> **Aprobación final del propietario en este corte:** «si, cuando llegue el 12 y tengamos la data nueva tendremos que re-estudiar la continuidad; confirmo todo; y ahora vamos a consolidar en repo, justo antes de empezar desarrollo». La autorización previa de implementar lo ya aprobado y técnicamente independiente continúa; esta petición concreta ordena **consolidar documentación primero**, no lanzar otra compilación durante este paso.

## 1. Regla de lectura y autoridad

1. Leer **este documento** como mapa vigente de lo aprobado, lo implementado y lo abierto; leer el histórico de decisiones para parámetros concretos de interfaz, políticas y excepciones.
2. Una decisión posterior explícitamente aprobada **prevalece sobre cualquier borrador anterior** del histórico. En particular: *solo respaldos completos*, recordatorio semanal voluntario, recuperación aditiva A y eliminación individual A; los borradores anteriores con SQLite rápido o copias diarias automáticas quedaron reemplazados.
3. No borrar ni reescribir la evidencia histórica de las Builds 684/685/688, aunque una sección temprana indique un estado que fue superado cronológicamente.
4. Antes de programar, comprobar HEAD, PR Draft, \`main\` y estado del código. No basarse en la fecha de este documento para suponer que el estado ejecutable sigue intacto.
5. Diferenciar rigurosamente: **REQUISITO APROBADO**, **CÓDIGO ESCRITO**, **CI PASS**, **QA PROPIETARIO PASS** y **LIBERADO**. Ninguna etapa implica automáticamente la siguiente.

## 2. Línea base técnica verificada al consolidar

- Aplicación Windows 11 x64, C#/.NET 10 WPF y SQLite WAL; trabajo local-first. La base del equipo del propietario, \`D:\SolarEnergyMonitorTest\Data\energy.db\`, ocupa aproximadamente 2 GB y crece; conservar datos, fuentes, fechas, unidades, claves, incertidumbre, historial y trazabilidad.
- \`main\` permanece en \`59120a630b0f56684ba7672d960673f5c5c1797b\` según el PR al realizar esta consolidación; trabajo pendiente exclusivamente en \`work/phase10-12-consolidated-20261007\`, PR #1 Draft.
- **Build 684:** QA funcional de Fase 10 aceptado, sujeto a las incertidumbres tarifarias documentadas (RED/ETR no automáticamente certificadas). Conservar el resultado sin cambiar sus cálculos por estética.
- **Build 685:** CI PASS, corrección textual/denominador en pantalla y PDF; la inspección visual del propietario sigue pendiente y puede incluirse en una entrega consolidada.
- **Build 688:** Windows CI PASS, build/smoke sobre fixtures sintéticos y ZIP portable producido; incluye primera implementación de menú lateral agrupado y respaldo completo / recordatorio semanal / destino secundario. **No fue aceptada ni probada en el PC del propietario y NO se exige probarla por separado**. El binario continúa mostrando v0.10.0: **v0.11.0 es la versión objetivo, no una versión ya publicada**.
- Usuario conserva una **copia manual separada de la carpeta \`Data\`**. Tratarla como **resguardo provisional NO verificado**: no se conoce el procedimiento de copia, cierre del proceso, situación WAL ni recuperabilidad. Mantenerla sin alteraciones ni borrarla. No afirmar que ya hay respaldo completo oficial o prueba de restauración.
- El código \`FullBackupService\` y la prueba sintética existen en la rama; la creación de paquetes, el inventario reconocible parcial, los botones manuales, el aviso semanal y la segunda carpeta **no equivalen aún a la interfaz final, gestión integral de copias ni a restauración selectiva implementada**.
- El esquema vigente de las pruebas es **SQLite v17** y hay siete vistas SQL descritas en \`PHASE_11_SQL_GUIDE.md\`. No presentar agregación simple de muestras W como energía kWh.

## 3. Alcance funcional cerrado para implementar, sin volver a pedir el diseño

| Componente | Decisión aprobada | Estado a esta fecha |
|---|---|---|
| Versión | **v0.11.0** para la próxima entrega importante; Build CI independiente | Objetivo aprobado, código visible aún v0.10.0 |
| Menú | Cinco grupos: Inicio; Energía; Informes; Datos y herramientas; Sistema y ayuda. Entradas Protección de datos y Explorador SQL solo con pantallas funcionales | Agrupación bilingüe implementada; entradas nuevas pendientes |
| Panel principal | Métricas existentes, frescura/fecha, energía diaria y explicación de estado; presentación clara y carga no bloqueante | Rediseño pendiente |
| Análisis | Resumen / Gráficos / Detalle, filtro de período compartido, energía/calidad/cobertura, tablas y gráficos existentes | Refinamiento pendiente |
| Batería | Resumen y Técnico, SOC, energía ordinaria y reserva; mediciones y su procedencia, jamás falsear cero o autonomía | Refinamiento pendiente |
| Red/Enel | Resumen, Lecturas, Comparar, Boletas, Auditoría y Tarifas. Recorrido contextual por boleta y evidencia en cuatro niveles; preservar trazabilidad | Rediseño aprobado; datos adicionales de octubre NO disponibles |
| Informes | Plantilla y período, vista previa/cobertura, PDF/Excel, progreso, errores y ruta de archivo | Refinamiento pendiente |
| Datos | Cobertura y Recopilación; estado de corpus, vacíos y recopilación con progreso | Refinamiento pendiente |
| Configuración | Conexión, credenciales, exportaciones, idioma/apariencia, atajos y enlace a Protección de datos | Rediseño pendiente; respaldo manual provisional existe |
| Diagnósticos | Herramientas actuales agrupadas, evidencia saneada, consultas explícitas sin carga pesada al abrir | Refinamiento pendiente |
| SQL integrado | Editor tipo consola, números de línea, resaltado, atajos personalizables, error con ubicación cuando sea fiable, árbol de vistas, resultados paginados | NO completo |
| SQL seguro/exportación | Solo lectura real, autorización de consultas, sin DDL/DML, límites/timeout/cancelación, **CSV y XLSX nativo**, acceso externo seguro vía snapshot solicitado, fidelidad W/kWh | Implementación/QA pendientes |
| Rendimiento | Medir primero startup frío/caliente, navegación, consultas, gráficos, exportaciones y uso memoria con corpus representativo; mejora medible sin afectar cálculo | Instrumentación y optimización pendientes |
| Protección de datos | Paquete completo, recordatorio semanal opcional, ejecución manual, destino secundario configurable, verificación SHA/SQLite, aviso de fallo | Código base + smoke sintético PASS; gestión y QA reales pendientes |
| Inventario | Lista de copias físicas local y secundaria, fecha, tamaño, categoría, disponibilidad e integridad; legado SQLite etiquetado como legado | Solo indicador de inventario parcial, UI final pendiente |
| Eliminación | **Opción A:** borrar solamente la copia física elegida, sin cascada; confirmar, proteger última copia completa válida disponible; jamás borrar automáticamente | Comportamiento aprobado, UI/QA faltantes |
| Recuperación | **Opción A:** importar SOLO ausentes, omitir iguales, conflictos explícitos; preview por categoría, dependencias, origen, adaptación por versión sobre staging, rollback y bloqueo ante incompatibilidad | Diseño cerrado; restauración/importación selectiva segura y tests faltantes |

Los wireframes exactos, restricciones de vocabulario, estados de datos, red/boleta/tarifa, atajos y seguridad siguen definidos con mayor precisión en \`NEXT_TRANCHE_DECISIONS_2026-10-08.md\`.

## 4. Contratos de protección que no se negocian en refactor

**Respaldos**
- **No** reintroducir la copia automática SQLite-only diaria, ni una copia semanal ejecutada sin aprobación del usuario. Sí mantener la **obligación de crear un paquete completo verificado ANTES de migrar estructura**; si falla/no hay autorización, no migrar y proteger la instalación anterior.
- Un paquete completo debe contener copia SQLite consistente vía API nativa con WAL confirmado, documentos originales pertinentes (Bills y Tariffs) y ajustes recuperables, sin secretos DPAPI/credenciales ni datos de diagnóstico no pertinentes. Debe indicar formato de paquete, versión del programa, Build, revisión, schema, fecha UTC, archivos/categorías, huellas y verificación independiente. **La consistencia entre referencias SQLite y documentos es una condición de completitud todavía por demostrar**, no una suposición basada en que exista ZIP.
- Secundario opcional e independiente, verificado antes de informar éxito; errores de disco desconectado visibles, pero no invalidan copia local íntegra. No borrar, renombrar ni sobrescribir la copia manual \`Data\` del usuario.
- Inventario sin confundir "se verificó al crear" con "verificada de nuevo ahora"; una unidad desconectada no cuenta como copia válida actualmente disponible. Al eliminar, solo el archivo reconocido elegido, sin cascada ni barrido de la última copia válida. Prohibida purga/retención automática.
- **No realizar restauración real ni escritura/merge sobre la base real como parte del desarrollo o QA automático.** Usar únicamente fixtures sintéticos. La futura operación que afecte la base real requiere autorización explícita posterior, preview e integridad; no inferirla del acuerdo de desarrollo.

**Datos y cálculo**
- Ningún código de QA/desarrollo accede a \`D:\SolarEnergyMonitorTest\Data\energy.db\` ni a copias del usuario. Base real y documentación existente permanecen intactas.
- No alterar resultados aceptados de Enel, no certificar tasas RED/ETR ambiguas, no añadir ajustes inventados para cuadrar CLP, no convertir ausencia en cero, no confundir W instantáneos con kWh intervalados, UTC con día civil local, ni medias aritméticas con cobertura energética.
- Mostrar actividad real, errores y cancelación; no bloquear la navegación con cálculos pesados. Atender especialmente la base ~2 GB y el espacio temporal para respaldos.
- SQL de lectura bajo controles múltiples; exportación XLSX/CSV no debe fingir que la vista previa limitada equivale al resultado completo.
- Mantener ES/EN, funcionalidad existente y accesibilidad; no crear nuevas entradas de navegación vacías.

## 5. Orden de ejecución: desarrollo autorizado, no una nueva ronda de diseño

1. **Protección de datos primero:** cerrar contenido/integridad reales del paquete, ejecución y recordatorio, directorios locales/secundarios, notificación de errores, inventario físico y confirmación de borrado con protección de última copia; probar con bases/documentos artificiales y tamaños representativos. Evaluar correcciones a lo ya implementado en Build 688, sin repetir sus pruebas de forma redundante.
2. **Recuperación segura:** lectura de paquete/versión, reglas de compatibilidad y conversión reproducibles, staging aislado, preview por categoría y conflictos, inserción aditiva idempotente/rollback probada SOLO en datos sintéticos. Mantener la ejecución sobre base real bloqueada pendiente de decisión específica.
3. **SQL usable:** consola/editor, atajos, validación de sintaxis/localización honesta, control de ejecución de lectura, resultados y exportación CSV/XLSX; completar vistas derivadas solo si la equivalencia física con motores de integración/tarifas está probada.
4. **Rediseño y rendimiento:** implementar los wireframes ya aceptados sobre las secciones existentes y la navegación, carga lazy/async, optimización medida y regresiones cuantitativas.
5. **Integración, instrumentación y entrega:** compilar, pruebas estáticas, smoke sintético, revisión de regresiones, resultados de mediciones antes/después, etiquetas ES/EN y ZIP portable; versionar el binario **v0.11.0** solo cuando se prepare la entrega coherente. Entregar UNA próxima Build consolidada para QA del propietario; Build 688 permanece un antecedente CI, no una entrega exigida.
6. No mezclar nuevo análisis de Enel aún no suministrado como requisito supuesto. Mantener funcional lo ya aceptado y registrar incertidumbres y gaps conocidos.

El ejecutor puede elegir componentes y bibliotecas compatibles para los requisitos aprobados, pero **NO puede inventar atajos, permisos de escritura, reglas de restauración o semánticas numéricas no discutidas**; ante un obstáculo material de seguridad/diseño, detener ese subtrabajo y comunicar la alternativa concreta.

## 6. Única próxima revisión de continuidad: 12 de octubre de 2026

**DECISIÓN ACTUAL CONFIRMADA POR EL PROPIETARIO:** Al llegar el **2026-10-12 y TENER LOS NUEVOS DATOS DE ENEL**, **REESTUDIAR EXPLÍCITAMENTE LA CONTINUIDAD**.

- No es una instrucción de ejecutar trabajo el día 12 automáticamente, ni de dar por recibida una fuente todavía inexistente.
- Revisar material efectivamente aportado, identidad de boleta/periodos, lecturas, tarifas, referencias y fiabilidad; comparar con la línea base Fase 10 ya aceptada, y delimitar modificaciones que la evidencia justifica.
- **Antes de cerrar v0.11.0 o seguir una ampliación que dependa del nuevo material**, decidir con el propietario cómo continuar: si incorporar ajustes, separar nueva fase, requerir QA adicional, cambiar prioridades o posponer cierre. No asumir hoy la respuesta, calendario, alcance o validez de esos nuevos datos.
- Mientras ese insumo no está, **sí se pueden implementar los bloques independientes ya aprobados**. El hecho de que se apruebe su desarrollo no equivale a autorizar liberación/merge ni cierre unilateral de Fases 10–12.
- La reevaluación debe ser breve, basada en evidencia y documentada con nueva decisión; no reiniciar desde cero toda la investigación ya aceptada salvo contradicción material.

## 7. Gates de QA, entrega y liberación

1. **Desarrollo**: commits identificables en PR #1 Draft, \`main\` inalterado; no tocar la DB real ni backups del propietario, ni uso de recursos secretos en repositorio/artefacto. Esta consolidación por sí misma **no autoriza cambios destructivos**.
2. **CI y seguridad**: build Windows y smoke pasan; recuperación y borrados probados solo con fixtures aislados; paquete completo incluye archivos referenciados, hashes y verificación real; pruebas ante corrupción, disco offline, corte/espacio, conflictos/duplicados e incompatibilidad; no falsos PASS.
3. **Criterios de rendimiento**: medición de inicio/UI/SQLite, diferencia de resultados con Build 684 cuando corresponda; ninguna aceleración justificará cálculos alterados o UI congelada.
4. **QA del propietario**: próxima Build consolidada con enlace ZIP **directo en el chat**, número real CI, commit/checksum y pasos mínimos [\`BUILD_HANDOFF_RULE.md\`](BUILD_HANDOFF_RULE.md); QA de respaldo verdadero y verificable en PC, UI/SQL/rendimiento y presentación Build 685 acumulada. No exigir una QA separada de Build 688.
5. **Real restore/import**: autorización específica futura antes de aplicar datos a la base real; no ejecutar automáticamente al abrir app ni implícitamente como parte del QA general.
6. **Cierre**: reevaluar con la evidencia Enel del 12 cuando exista; no cerrar v0.11.0, declarar Fase 11/12 completa, ni hacer merge a \`main\` hasta revisión de las evidencias de QA y aprobación final del propietario.

## 8. Puntos realmente abiertos (no confundir con tareas técnicas)

- **12-10-2026, condicionado a nueva evidencia Enel:** continuidad, alcance y posible cierre/repriorización.
- **En el futuro, antes de usarla sobre datos reales:** autorización de operación de recuperación/importación selectiva de la base activa con salvaguardas.
- **Al terminar el desarrollo:** aceptación del paquete consolidado/QA y autorización de cierre/merge.

Las elecciones técnicas ordinarias para construir lo aprobado se delegan al ejecutor sin otra ronda de consultas, siempre que pasen gates de seguridad y no reduzcan funciones. Ante contradicción material, escalarla en vez de declararla aprobada implícitamente.

---

**Referencia de congelación pre-desarrollo:** PR #1 HEAD antes de consolidación \`7ddfa75ee051d7eddc838a5b12b5d26387331302\`. La creación documental de esta línea base moverá el HEAD de la rama, sin alterar \`main\`. No usar el HEAD previo como si fuera el estado actual después de estos commits documentales.
