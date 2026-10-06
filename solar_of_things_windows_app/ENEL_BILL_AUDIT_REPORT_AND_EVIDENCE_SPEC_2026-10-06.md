# Especificación canónica — Auditoría de boleta Enel y expediente de evidencia

Fecha: 2026-10-06

Estado: **CANÓNICO / APROBADO POR OWNER PARA IMPLEMENTACIÓN**

Ruta crítica inmediata:
- producir un expediente técnico impreso, autoexplicativo y reproducible para revisar una boleta Enel;
- el documento debe poder ser entregado a un tercero sin depender de que el usuario principal esté presente para explicarlo;
- el objetivo operativo es disponer del expediente lo antes posible y con margen suficiente antes de la gestión presencial de octubre de 2026.

Esta especificación consolida y supersede, para el flujo de auditoría de boletas, cualquier diseño anterior que entre en conflicto con lo aquí establecido.

---

# 1. Objetivo del producto

El producto principal de este flujo es un **Informe técnico de revisión de consumo eléctrico facturado**.

El informe debe comparar, para exactamente el mismo período:

1. consumo/lecturas declaradas por Enel;
2. importación desde red derivada de registros del inversor;
3. incertidumbre asociada a discontinuidades de telemetría;
4. efecto monetario de las diferencias usando tarifas oficiales aplicables;
5. cargos de la boleta que no dependen del consumo, preservados sin alteración;
6. fuentes, versiones, hashes y metodología suficiente para reproducir cada cifra importante.

El informe no debe presentarse como una afirmación automática de “error de Enel”. Debe exponer evidencia, diferencias e inconsistencias de forma cuantitativa y auditable.

El informe tampoco debe denominar la fuente energética externa como “Solar of Things” en su narrativa principal. Debe hablar de:
- **registros del inversor**;
- **telemetría del inversor**;
- **importación desde red derivada de los registros del inversor**.

“Solar of Things” puede aparecer en trazabilidad técnica interna cuando corresponda a software/fuente API, pero no debe ser la autoridad semántica del informe.

---

# 2. Caso real prioritario de aceptación

El primer caso real de aceptación corresponde a una boleta cuyo período impreso es:

- desde: 2026-08-28;
- hasta: 2026-09-28;
- consumo facturado: 97 kWh;
- tarifa impresa: BT1-T5;
- Área Típica impresa: 1A.

No se debe almacenar ni publicar en esta especificación información personal del cliente, dirección, RUT, número de cliente u otros identificadores innecesarios.

La tesis operativa inmediata a contrastar es:

> si los 97 kWh facturados son coherentes o no con la importación desde red observada/estimada por el inversor para el mismo intervalo.

---

# 3. Convención temporal Enel

Por decisión operativa del owner, y hasta que aparezca evidencia contraria, la aplicación debe interpretar silenciosamente una boleta con período de fechas X–Y como:

- inicio: X 00:00:00 local;
- término: final completo del día Y;
- internamente se recomienda representar el intervalo como semiabierto:
  - [X 00:00:00, Y+1 00:00:00).

Motivación empírica:
- el owner ha revisado más de 20 boletas consecutivas;
- la lectura de término de una boleta coincide sistemáticamente con la lectura de inicio de la siguiente.

Esta convención debe quedar registrada internamente como una inferencia operativa revisable, pero:
- no debe bloquear cálculos;
- no debe convertirse en una advertencia prominente al usuario;
- la UI no debe obligar a interpretar manualmente artefactos 00:00 vs 23:59.

---

# 4. Separación obligatoria de productos

Deben existir dos flujos analíticos distintos.

## A. Comparación de lecturas / período

Pregunta:

> Entre dos límites seleccionados, ¿qué consumo indica una lectura/medidor y qué importación desde red deriva el inversor para el mismo intervalo?

Características:
- puede utilizarse sin boleta;
- sirve para comparaciones exploratorias o técnicas;
- tiene su propio reporte;
- no reconstruye una boleta.

## B. Auditoría de boleta Enel

Pregunta:

> Para esta boleta concreta, ¿son coherentes las lecturas, el consumo facturado, la importación derivada del inversor, las tarifas aplicadas y los montos resultantes?

Características:
- parte siempre de una boleta;
- usa sus límites oficiales;
- preserva todas las líneas impresas;
- aplica tarifas oficiales/versionadas donde exista evidencia;
- reconstruye sólo lo que sea legítimamente reconstruible;
- compara Enel vs inversor/escenarios;
- genera su propio PDF de auditoría.

No volver a fusionar ambos productos en una sola pantalla genérica.

---

# 5. Fuentes de evidencia

El expediente debe distinguir, como mínimo:

## S1 — Boleta Enel
Fuente documental primaria para:
- período impreso;
- lecturas;
- consumo facturado;
- tarifa/área declaradas;
- cargos;
- impuestos;
- subsidios/abonos;
- total.

## S2 — Registros del inversor
Fuente para:
- potencia importada desde red;
- timestamps reales;
- posibles contadores/aggregates de importación cuando sean validados específicamente para el dispositivo;
- metadatos de calidad/telemetría.

## S3 — Tarifa oficial Enel
Fuente preferente para:
- tarifa final aplicable al cliente;
- componentes finales de suministro;
- vigencia;
- comuna/territorio;
- columnas/tipos de tarifa;
- retroactividad/correcciones.

## S4 — Evidencia regulatoria CNE
Fuente para:
- reglas regulatorias;
- vigencias/correcciones;
- VAD/índices y otras piezas de validación o cruce;
- delimitación de períodos tarifarios.

## S5 — Método estadístico
Debe indicar:
- versión;
- algoritmo;
- parámetros;
- código/harness o servicio que produjo los resultados;
- estado de validación;
- semillas/simulaciones cuando correspondan.

Cada cifra material del informe debe poder responder:
1. ¿qué es?;
2. ¿de dónde viene?;
3. ¿es observada, impresa, derivada, estimada o contrafactual?;
4. ¿cómo se reproduce?

---

# 6. Regla de lenguaje P5 / P50 / P95

Nunca mostrar solamente las etiquetas P5/P50/P95 sin explicación adyacente en el informe principal.

Definición layman obligatoria:

- **P5**: valor hacia el extremo inferior; aproximadamente 5% de los resultados producidos por el modelo queda por debajo.
- **P50**: mediana y estimación central; aproximadamente la mitad de los resultados queda por debajo y la mitad por encima.
- **P95**: valor hacia el extremo superior; aproximadamente 95% de los resultados queda por debajo.
- **P5–P95**: contiene el 90% central de los resultados producidos por el método estadístico utilizado.

No llamar P5–P95 “intervalo de confianza” salvo que el método implementado lo justifique formalmente.
No llamar P5/P50/P95 “tests”.

---

# 7. Regla sobre datos faltantes / gaps

La defensa metodológica no debe afirmar que los gaps “no importan”.

Debe afirmar y demostrar:

- los gaps existen;
- se detectan;
- se cuantifican;
- no se convierten en consumo cero;
- no se fabrican frames inexistentes;
- la energía observada se mantiene separada de la contribución estimada;
- la incertidumbre de los gaps se refleja explícitamente en P5/P50/P95.

Siempre distinguir:

- **energía directamente observada**;
- **tiempo sin cobertura suficiente**;
- **contribución estimada de los gaps**;
- **total completado**.

La cobertura temporal de grid import debe mantenerse separada de cualquier cobertura de atribución Solar/Batería/Grid.

---

# 8. Captura e integración de potencia

El informe debe explicar tempranamente que:

- el inversor entrega mediciones periódicas de potencia importada desde red;
- cada frame tiene timestamp real;
- la cadencia típica ronda varios minutos, pero no se debe imponer una grilla artificial exacta;
- energía y potencia son magnitudes distintas;
- la energía observada se obtiene integrando potencia sobre el tiempo real entre observaciones válidas;
- los enlaces con separación excesiva no se tratan como continuidad observada.

Explicación matemática resumida permitida:

```
Ei ≈ ((Pi + Pi+1) / 2) × Δti
```

La metodología técnica completa debe indicar exactamente la regla real usada por la implementación vigente.

---

# 9. Modelo económico

Nunca calcular:

```
total_boleta / kWh_enel × kWh_inversor
```

Cada línea de la boleta debe clasificarse internamente en una de estas categorías:

1. **VARIABLE_POR_CONSUMO**
   - cambia según kWh u otra magnitud de consumo;
   - puede tener monto contrafactual P5/P50/P95.

2. **FIJO**
   - no cambia al sustituir 97 kWh por P5/P50/P95;
   - debe conservar exactamente el mismo valor en los escenarios.

3. **CONDICIONAL_REGULADO**
   - depende de bandas, subsidios, FET, ETR u otra regla;
   - sólo se recalcula si la regla aplicable está sustentada.

4. **NO_RECONSTRUIBLE**
   - se preserva tal como aparece en la boleta;
   - no se inventa un monto esperado.

El motor debe distinguir:

- **monto impreso**: lo que dice la boleta;
- **monto reconstruido**: monto esperado usando 97 kWh y tarifa/regla oficial;
- **monto contrafactual**: monto que produciría la misma regla usando P5/P50/P95 del inversor.

Los cargos no dependientes de kWh no deben presentarse como “discutidos” por una diferencia energética.

---

# 10. Estructura canónica del informe principal

Objetivo: aproximadamente 8 páginas, permitiendo expansión cuando la evidencia lo requiera.

## Página 1 — Resumen ejecutivo

Debe ser autosuficiente.

Contenido mínimo:
- identificación no sensible de la boleta/período;
- consumo facturado Enel;
- energía directamente observada por el inversor;
- P50;
- P5–P95;
- explicación layman inmediata de P5/P50/P95;
- diferencia Enel vs P5;
- diferencia Enel vs P50;
- diferencia Enel vs P95;
- diferencias en kWh y porcentaje;
- mensaje técnico corto sobre existencia o no de discrepancia material.

Wireframe conceptual:

```text
┌──────────────────────────────────────────────────────────────┐
│ INFORME TÉCNICO DE REVISIÓN DE CONSUMO ELÉCTRICO           │
│ Período auditado: ...                                       │
├──────────────────────────────────────────────────────────────┤
│ ENEL FACTURÓ                         97,0 kWh                │
│ ENERGÍA OBSERVADA INVERSOR           XX,X kWh                │
│ ESTIMACIÓN CENTRAL P50               XX,X kWh                │
│ RANGO P5–P95                         XX,X–XX,X kWh            │
├──────────────────────────────────────────────────────────────┤
│ ¿Qué significan P5, P50 y P95? ...                          │
├──────────────────────────────────────────────────────────────┤
│ DIFERENCIAS VS ENEL                                         │
│ Enel vs P5    XX,X kWh / XX,X%                              │
│ Enel vs P50   XX,X kWh / XX,X%                              │
│ Enel vs P95   XX,X kWh / XX,X%                              │
└──────────────────────────────────────────────────────────────┘
```

Punto especialmente importante:
- si Enel queda materialmente por encima de P95, debe mostrarse con claridad y sin exageración;
- esto responde directamente a si los gaps por sí solos pueden explicar la diferencia.

## Página 2 — Comparación económica

Tabla principal paralela:

```text
Concepto                 Enel      P5       P50      P95
Energía                  ...
Electricidad consumida   ...
Transporte               ...
Cargos no dependientes   ...
Total comparable         ...
```

Debe indicar claramente:

> Los cargos que no dependen del consumo se mantienen sin cambios en todos los escenarios.

## Página 3 — Descomposición económica completa

Secciones mínimas:

### 3.1 Componentes variables
- cantidad;
- unidad;
- tarifa;
- monto Enel;
- monto reconstruido;
- P5/P50/P95.

### 3.2 Componentes fijos
- Enel;
- P5;
- P50;
- P95;
- diferencias cero cuando corresponda.

### 3.3 Componentes condicionales
- regla;
- fuente;
- tratamiento;
- estado de verificación.

### 3.4 Reconciliación final
- variables;
- fijos;
- condicionales;
- impuestos;
- ajustes;
- total comparable.

## Página 4 — Evidencia energética y respuesta temprana a los gaps

Debe responder:
- cómo captura datos el inversor;
- cómo potencia se convierte a energía;
- qué es observado;
- qué es estimado;
- cuánto falta;
- cuánto aporta estadísticamente cada escenario.

Tabla/box obligatoria:

```text
Energía directamente observada           XX,X kWh
Contribución estimada de gaps P5           X,X kWh
Contribución estimada de gaps P50          X,X kWh
Contribución estimada de gaps P95          X,X kWh
-------------------------------------------------
Total P5                                  XX,X kWh
Total P50                                 XX,X kWh
Total P95                                 XX,X kWh
```

Bloque de robustez:

```text
¿Puede la diferencia explicarse sólo por los datos faltantes?

Consumo Enel                97,0 kWh
P5                          XX,X kWh
P50                         XX,X kWh
P95                         XX,X kWh
Enel - P95                  XX,X kWh / XX,X%
```

## Página 5 — Calidad, cobertura y trazabilidad cuantitativa

Debe ser más dura, numérica y verificable que la página 4.

Mínimos:
- inicio/fin;
- duración total;
- frames originales;
- frames válidos;
- frames no resolubles/rechazados;
- mediana de cadencia;
- p90 de cadencia;
- separación máxima;
- tiempo cubierto;
- tiempo faltante;
- cobertura temporal;
- número de gaps;
- gap medio;
- gap máximo;
- conteos >1h, >2h, >4h cuando existan;
- energía observada;
- contribución estimada P5/P50/P95;
- tabla diaria de cobertura.

Controles metodológicos visibles:
- timestamps reales;
- ausencia != cero;
- no grilla artificial;
- no fabricación de observaciones;
- Enel no entra en construcción/calibración del modelo estadístico;
- versión del algoritmo;
- resultados reproducibles;
- datos fuente disponibles en anexo.

## Página 6 — Hallazgos e inconsistencias

Presentar hallazgos numerados.

Orden sugerido:
- H01 diferencia de energía facturada;
- H02 diferencia incluso frente a P95;
- H03 impacto monetario;
- H04 discrepancias tarifarias si existen;
- H05 otras inconsistencias de reconstrucción/calidad.

Estados visuales sobrios:
- COINCIDE;
- DIFERENCIA MENOR;
- REQUIERE REVISIÓN;
- NO VERIFICABLE.

No usar automáticamente “ERROR ENEL”.

## Página 7 — Fuentes y trazabilidad

Fuentes etiquetadas S1...Sn.

Debe incluir:
- documento/fuente;
- vigencia/período;
- versión;
- hash cuando corresponda;
- fecha de captura;
- origen oficial;
- relación con los cálculos.

## Página 8 — Metodología técnica

Debe incluir, al menos:
- variable primaria;
- unidades;
- timezone;
- límites temporales;
- integración;
- definición de continuidad/gap;
- método estadístico;
- selección de evidencia histórica;
- percentiles;
- simulaciones cuando correspondan;
- reproducibilidad;
- validaciones efectuadas;
- limitaciones;
- versión del método;
- confirmación de que valores Enel no se utilizaron para calibrar el modelo.

No incluir una página de “solicitud a Enel”.
La negociación/reclamación verbal es externa al informe técnico.

---

# 11. Anexo técnico de evidencia

Debe ser una exportación separada del informe principal.

Objetivo:
- exhaustividad;
- trazabilidad;
- posibilidad de entrega impresa;
- permitir reconstrucción independiente.

No necesita ser una pieza de UX editorial elegante.

Contenido mínimo:

## A. Serie fuente
Columnas mínimas orientativas:
- número de fila;
- timestamp;
- potencia importada;
- Δt;
- energía integrada;
- confianza/calidad;
- estado del enlace;
- origen.

## B. Gaps
- inicio;
- fin;
- duración;
- regla aplicada;
- elegibilidad estadística;
- contribución/escenario donde corresponda.

## C. Tarifas
- vigencia;
- componente;
- unidad;
- valor;
- tipo de columna;
- fuente;
- página;
- versión;
- hash/publicación.

## D. Cálculo económico por escenario
- escenario;
- kWh;
- componente;
- tarifa;
- cantidad;
- monto;
- regla/fuente.

## E. Integridad
- número de registros;
- válidos;
- descartados/no resolubles;
- hashes;
- versiones de código;
- parámetros;
- semillas/simulaciones cuando correspondan.

Salidas deseables:
- PDF técnico imprimible;
- CSV;
- XLSX cuando sea útil.

---

# 12. Fuentes tarifarias y adquisición

La arquitectura canónica sigue siendo:

> official-source adapters + evidencia local versionada

Preferencia:
1. tarifa final oficial del distribuidor;
2. evidencia CNE como regulación/validación;
3. importación manual controlada únicamente cuando la fuente oficial bloquea automatización.

Estado conocido:
- CNE es accesible automáticamente;
- Enel puede interponer protección web/JavaScript que hace poco confiable el scraping por HttpClient simple;
- por eso se permite navegador + importación de PDF oficial Enel como fallback;
- el usuario no debe tener que escribir manualmente una tarifa por kWh.

Cada tarifa utilizada debe conservar:
- fuente;
- vigencia;
- documento;
- hash;
- parser;
- aplicabilidad;
- estado de autoridad.

---

# 13. Contador/aggregate de importación del inversor — abierto y prioritario

La investigación API previa demuestra que la plataforma puede exponer conceptos como:
- `buyElectricityQuantity`;
- `dayPurchaseElectricityConsumption`.

Pero:
- no todo aggregate es real en todas las instalaciones;
- `buyElectricityQuantity` fue placeholder en al menos un corpus externo;
- la implementación HPVINV02 actual usa `mainsPower` -> `grid_import_power_w`;
- todavía no hay evidencia suficiente de un contador de importación real/fiable en el dispositivo objetivo.

Tarea futura prioritaria:
- comprobar si el HPVINV02 objetivo expone un contador/aggregate de importación usable;
- si existe, validarlo contra integración de `mainsPower`;
- si coincide, usarlo como comprobación independiente;
- si es placeholder/inconsistente, conservarlo como diagnóstico y no como autoridad.

No asumir su disponibilidad antes de validarlo.

---

# 14. Estado estadístico y excepción controlada al gate R3

R3:
- candidato congelado;
- future validation todavía `INSUFFICIENT`;
- no se puede puntuar hasta cumplir el stopping rule;
- no se debe retocar usando outcomes futuros;
- no se debe presentar como “validado” antes de pasar sus gates.

Sin embargo, la necesidad inmediata del expediente de boleta crea una ruta paralela autorizada.

## Excepción controlada

Queda autorizado:
- diseñar/implementar el informe de auditoría;
- implementar tablas/cálculos energéticos y económicos;
- mejorar la UX específica del flujo;
- generar builds de trabajo;
- producir un expediente usando un método estadístico explícitamente identificado con su verdadero estado de validación.

No queda autorizado:
- llamar “R3” al método usado si R3 no ha sido puntuado/aprobado;
- afirmar que un intervalo es validado si no lo está;
- usar el valor Enel para calibrar/seleccionar el modelo;
- alterar el protocolo/gates de R3 para acomodar la boleta;
- usar la boleta como training/selection evidence del candidato futuro.

La selección del método estadístico provisional para el expediente sigue abierta y debe cerrarse técnicamente antes de emitir el PDF final.

---

# 15. Política UX durante la implementación urgente

No se hará primero un rediseño completo de toda la aplicación.

Pero cada build funcional debe mejorar el UX de la pieza que toca.

Ejemplos:

## Ingreso/revisión de boleta
Debe evolucionar hacia:
- Resumen;
- Lecturas;
- Cargos;
- evidencia/prerrequisitos visibles.

## Tarifas
Debe mostrar claramente:
- fuente;
- vigencia;
- versión/corrección;
- estado de normalización;
- aplicabilidad;
- acciones faltantes.

## Auditoría
Debe priorizar:
- vista previa semejante al informe;
- hallazgos claros;
- energía y dinero en paralelo;
- ausencia de formularios técnicos innecesarios.

## Exportación
Acciones claras:
- Exportar informe de auditoría PDF;
- Exportar anexo técnico;
- Exportar datos CSV/XLSX.

Cada pantalla compleja debe tener una tarea dominante y evitar formularios verticales densos.

Accesibilidad objetivo de largo plazo:
- un usuario no técnico debe poder identificar sin ayuda dónde ingresar una nueva boleta y cómo auditarla.

---

# 16. Prioridad operativa de implementación

Ruta crítica inmediata:

1. consolidar esta especificación;
2. construir la tabla de verdad autoritativa del caso real:
   - boleta;
   - intervalo exacto;
   - frames del inversor;
   - cobertura/gaps;
   - energía observada;
   - método estadístico seleccionado;
   - P5/P50/P95;
   - tarifas;
   - cargos;
   - inconsistencias;
3. validar internamente cada número;
4. producir informe principal;
5. producir anexo técnico;
6. imprimir/revisar el expediente;
7. convertir el flujo demostrado en UX reusable;
8. continuar mejora general de la aplicación.

No empezar por el diseño gráfico del PDF antes de que la tabla de verdad sea correcta.

---

# 17. Asuntos abiertos que NO bloquean esta especificación

1. **Método estadístico provisional del expediente**
   - R3 sigue sin validación futura suficiente;
   - debe elegirse un método honesto y reproducible para el informe urgente.

2. **Contador de importación HPVINV02**
   - investigar si existe y es fiable.

3. **Tarifas exactas del período**
   - resolver publicación/versiones/aplicabilidad para el caso real.

4. **Subsidio y cargos condicionales**
   - no asumir invariancia sin verificar la regla.

5. **Números finales del caso**
   - observado, cobertura, gaps, P5/P50/P95, CLP e inconsistencias todavía deben calcularse.

Estos son problemas técnicos de ejecución, no decisiones conceptuales pendientes.

---

# 18. Criterio de cierre del expediente

El expediente está listo sólo cuando:

- cada cifra material tiene fuente y categoría semántica;
- el período Enel y el período del inversor son comparables;
- energía observada y estimada están separadas;
- P5/P50/P95 están explicados en lenguaje no especializado;
- se muestran diferencias contra P5, P50 y P95;
- los gaps están cuantificados y metodológicamente defendidos;
- los cargos fijos no son alterados artificialmente;
- los cargos variables usan evidencia tarifaria oficial;
- cargos condicionales/no reconstruibles están etiquetados;
- existe trazabilidad suficiente para reproducir resultados;
- el PDF se entiende sin explicación oral del autor;
- el anexo contiene la evidencia numérica completa;
- ninguna afirmación excede el estado real de validación del método empleado.

**Esta especificación queda congelada como autoridad de producto para el expediente urgente, salvo decisión explícita posterior del owner.**
