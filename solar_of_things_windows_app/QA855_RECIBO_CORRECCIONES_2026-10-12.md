# Recibo QA855 — correcciones dirigidas posteriores a Build 855

**Fecha local:** 2026-10-12 (Chile). **Repositorio:** `MrSimkin/energy_control_recopilation`. **Rama:** `work/qa855-corrections-package-20261012`. **Alcance:** desarrollo aislado, sin merge, release ni acceso a base productiva.

## Autoridad y seguridad
- Se leyeron el prompt Worker QA855, registro QA, contrato de correcciones, decisiones de fuente y licencias, reglas pre-build/build y contrato Enel.
- Convención temporal Enel sin modificaciones: inicio local 00:00 del primer día, término exclusivo 00:00 del día posterior al último; DST America/Santiago.
- Fuentes: IBM Plex Mono Regular/Bold del repositorio con OFL para PDF; nombre de familia Consolas para valores tipados numéricos en XLSX. No se subieron ni distribuyeron TTF Microsoft.
- Corpus de validación: datos sintéticos de smoke y pruebas Windows CI. No se abrió ni escribió `Data/energy.db` del propietario.

## Matriz de modificaciones / estado de aceptación
| Ítem | Implementación realizada | Verificación pendiente |
|---|---|---|
| DEF-REPORT-01 | PDF Detalle/Batería usa imágenes PNG ScottPlot generadas con series reales y cobertura >0; smoke comprueba imágenes incrustadas | Confirmación de trazos/etiquetas sobre páginas rasterizadas, idioma ES/EN |
| DEF-REPORT-02 | Margen inferior/pie reservado, repetición de encabezados y filas de encabezado pegadas a la primera fila; glosario Batería comienza en página aparte | Inspección visual de solapes, filas indivisibles y glosario corto/largo |
| DEF-PERF-01 | Lecturas de perfil SQLite de navegación Batería trasladadas de Dispatcher a Task.Run; conserva generación/descarta resultados obsoletos | Medir latencia UI p95 y capacidad de navegar inmediatamente en equipo destino |
| Enel, informe principal | Conserva estructura de informe técnico y detalle posterior; primera página compara Enel, registros del inversor, cobertura, P5/P50/P95 y distribución empírica de barras reales con referencias; segunda presenta tasa/cargo fijo, escenarios y diferencias económicas | Confirmar visualmente que el gráfico queda efectivamente en página 1 y el detalle monetario en página 2; cifras y escenarios con corpus QA |
| Enel, anexo | Ruta de exportación ZIP independiente sin cambios | Validar integridad/manifiesto del anexo tras exportación |
| PDF tipografía | Resolver de IBM Plex Mono desde assets con cara regular/bold, aplicado a cifras en cards, filas relevantes y tablas Detalle/Batería y Enel; texto proporcional se conserva | Auditar fuentes incrustadas, ausencia de cambios ajenos en tipografía, verificar los demás tramos numéricos |
| XLSX tipografía | Consolas en celdas de tipos numéricos, fecha y duración del libro familiar/técnico, conservando otros estilos y celdas texto | Comparativa visual y de celdas/fórmulas para todos los exportadores del alcance |

## Cobertura y límites
- La corrección de gráficos y presentación **no altera** fórmulas de kWh, integración, modelo estadístico, tarifa, subsidios o convención de fechas.
- **OBS-NUM-01:** diagnóstico de granularidad/reconciliación pendiente. No se cambia el motor sin reproducción.
- **OBS-H01-04:** investigación de segmentos que cruzan medianoche y DST pendiente; no se asume día de 24 h.
- **OBS-REPORT-05, OBS-UX-01, OBS-SQL-01:** mejoras UX menores aplazadas hasta una tranche sin riesgo. No confundir con defectos confirmados resueltos.
- **OBS-BACKUP-01:** BLOCKED: rutas externas sin fixture seguro; protecciones de respaldos intactas.
- **DEF-PERF-01** no puede declararse PASS funcional por una prueba de C# sin medición de interacción en WPF.

## Pruebas automatizadas y regresión dirigida
1. Windows CI .NET 10 Release + localización ES/EN + SQLite smoke + corpus 24k/96k sintéticos.
2. XLSX: valores reales en celdas numéricas/fecha se mantienen tipados y sólo éstos llevan Consolas; las cabeceras conservan tipografía proporcional.
3. PDF: informes técnico y batería sintéticos contienen imágenes trazadas (consulta PDF por imágenes), no sólo objetos de gráfico en memoria.
4. Enel: revisar página 1 cifra Enel, observado, diferencia sobre consumo facturado, cobertura/P5/P50/P95 y gráfico; página 2 tasa oficial reconstruida, total facturado y diferencias CLP. Verificar que páginas 3+ conservan fuentes, tasas, métodos y anexo aparte.
5. WPF: repetir navegación Batería↔Análisis↔Dashboard (sin congelar controles), ver progreso local, probar plantillas sólo si esa UX cambia.
6. Descargar y usar exclusivamente la Build consolidada cuya CI sea SUCCESS. **No repetir las 129 pruebas** de QA original.

## Historial CI
- Build 856: SUCCESS (implementación inicial).
- Build 857: SUCCESS (primera reorganización Enel + perfil Batería asíncrono).
- Build 858: FAILED, error de compilación en test nuevo por nombre local duplicado (`inspected`); corregido en `ebd4c659`.
- Build 859: FAILED en compilación del smoke por el mismo conflicto de variable en el commit anterior a la reparación.
- **Build 860: SUCCESS** en [Windows Build / Run 38330508409](https://github.com/MrSimkin/energy_control_recopilation/actions/runs/38330508409), compilado desde `ebd4c6591ce7cb7817322d92eed133b41a9d7a32`. Localización ES/EN, .NET 10 WPF Release, smoke SQLite (incluida detección de PNG en PDFs y Consolas XLSX), corpus sintéticos 24k y 96k, publicación portable Win-x64 y upload de artefacto: **PASS**. Enel/CNE *live* intencionalmente SKIPPED.
- Artefacto GitHub **11781724744**, `SolarEnergyMonitor-win-x64-dev`, ZIP de 93.061.799 bytes, SHA-256 `1ed3f2d9a1f8b8aa95b3c7c972c9c48b5581f052076bdf7c1a475b3b35a87bdb`. Validado por ZIP CRC `testzip() == None`, 568 entradas, con `SolarEnergyMonitor.exe`, `portable.mode`, IBM Plex Mono Regular/Bold y `OFL.txt`. Sólo esta **Build 860** se entrega para regresión del propietario.

**Estado de aceptación:** **CODE DONE / CI PASS / QA DIRIGIDA PENDIENTE**. Tests automatizados no certifican pixel-perfect, ubicación final en páginas, ni p95 de navegación WPF del propietario. El informe mantiene las secciones técnicas completas posteriores a las dos primeras páginas y el anexo sigue siendo independiente.

**Seguridad y cierre de rama:** `main` permanece `59120a630b0f56684ba7672d960673f5c5c1797b`. PR #5 abierta DRAFT sin merge; sin release ni acciones contra base productiva. Sólo se usaron corpus sintéticos. Ningún pendiente se ha declarado PASS por inferencia.
