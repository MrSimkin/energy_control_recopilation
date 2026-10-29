# R4/R5 Ronda 8: suficiencia, incertidumbre y control de fuga

**2026-10-28, solo desarrollo histórico anterior a 2026-09-28.** La cohorte futura reservada a R3 no se ha leído para analizar resultados, entrenar, seleccionar ni puntuar. **No es validación prospectiva, ni protocolo congelado, ni autorización de producción.** Base fuente: ZIP privado de ronda 5 con SHA-256 fija en el script.

## Por qué continuar

La utilidad no es añadir complejidad, sino reconocer intervalos en que estimar energía como cero sería inseguro, reducir errores al importar realmente de la red y distinguir mejoras estables de coincidencias históricas. Un modelo sencillo que gana limpiamente es una conclusión científica válida.

## R4: dimensionar evidencia de eventos ocultos

| Duración | Fechas comparables | Fechas con evento | Ventanas cero/cero | Ventanas positivas |
|---|---:|---:|---:|---:|
| 30 min | 140 | 0 | 5398 | 0 |
| 120 min | 140 | 0 | 1179 | 0 |
| 240 min | 139 | 5 | 489 | 5 |

Para 240 minutos, tasa histórica de días afectados 5/139 = **3,60%**. Intervalo Jeffreys 90%, bajo independencia/estacionariedad **no demostradas**: **1,66%–6,94%**. Cinco fechas con eventos se concentran en junio–agosto, hasta 8,66 kWh. Para 30/120 min, cero eventos no implica imposibilidad: límite unilateral exacto ilustrativo al 95%, si hubiera independencia, ≈2,12% de días.

Simulación de planificación beta-binomial para observar **cinco NUEVOS días con evento** de 240 min: mediana 126 días comparables adicionales; probabilidad 80% a 213 días y 90% a 282 días. **No son días calendario ni stopping rules autorizadas.** La dependencia temporal y cambio estacional pueden invalidar esas cifras. No declarar R4 insuficiente solo por no observar eventos futuros durante semanas; la cola escasa requiere evaluación separada y protocolo previo.

## R5: evaluación que no se esconde detrás de ceros

| Cohorte retrospectiva | Ventanas | Fechas | MAE puente (kWh) | MAE híbrido (kWh) |
|---|---:|---:|---:|---:|
| Todas | 1302 | 22 | 0,022814 | 0,022606 |
| Importación positiva | 96 | 7 | 0,309418 | 0,306595 |
| Híbrido aplicado | 64 | 6 | 0,259801 | 0,255566 |

El híbrido tiene una mejora global muy pequeña que **cambia de signo al retirar un caso influyente de 240 minutos**. Intervalo bootstrap agrupado por fecha de la diferencia media (híbrido menos puente): **[-0,001811;+0,001254]** kWh, incluye cero; para positivos **[-0,018525;+0,017844]**. Ninguna superioridad robusta demostrada. Los casos positivos están distribuidos en solo siete fechas. La decisión es **no promover ni ajustar retrospectivamente** el híbrido.

## Herramientas

- `round8_sufficiency_replay.py`: diagnóstico resumido que verifica SHA-256 del ZIP privado y de todos sus archivos internos antes de contar días R4 y evaluar estratos R5. Usa `numpy`, `pandas`, `scipy`.
- `prospective_count_scaffold.py`: **prototipo de seguridad** con entrada sintética de metadatos estructurales. Rechaza columnas de resultados verdaderos y fechas previas al corte indicado, jamás responde `READY`. No se ha aprobado como harness ni se han procesado datos futuros reales.
- La entrega ZIP privada de ronda 8 incluye el programa completo, 20 pruebas QA PASS, gráficos y resultados agregados. Los ZIP fuente con mediciones/datos derivados por caso no se publican en GitHub.

## Compromisos

R3 congelado y separado. Para R4/R5 la etapa `count` futura, cuando exista un protocolo aprobado, solo puede contar soportes estructurales/estados de extremos, no valores internos de kWh ni errores. Necesitamos una cohorte nueva **posterior a congelamiento**, umbrales y stopping rule autorizados antes de ver outcomes; si la cola rara queda escasa después del `score`, declararla inconclusa. No usar boletas Enel, no tocar app/DB/backup ni fusionar a `main`.
