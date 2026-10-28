# R4 y R5: resultados consolidados, seis rondas

**Fecha de referencia:** 2026-10-28. **Evidencia:** Research Exporter R1, corpus de desarrollo estrictamente anterior al 2026-09-28, protocolos y ZIP privados con hashes en README. Son comparaciones retrospectivas exploratorias, no validación independiente. Separación de R3 inviolable.

## Traducción práctica

**R4 (hurdle, cero/positivo):** pregunta primero si durante un vacío se importó algo y luego, de ser así, cuánto. Los numerosos ceros no deben ocultar grandes importaciones breves.

**R5 (duración y estados):** aprende cuándo se producen los cambios entre importar y no importar, considerando cuánto tiempo duró el estado, y ensaya convertir esa información en kWh. La edad de muchos episodios es desconocida o parcialmente conocida.

## Historial verificable por ronda

1. **R1, arranque:** R4 se justifica por aproximadamente 84% de lecturas de red iguales a cero dentro de 141 días históricos de buena calidad; R5 identifica persistencia pero escasez de episodios completos. No confundir proporción de lecturas cero con probabilidad de intervalo de energía cero.
2. **R2:** R4 mantiene la interpolación central de extremos y aplica residuos de calibración por fecha. En 263 casos con energía positiva (12 fechas), MAE central baja de 0,355 a 0,201 kWh frente al prototipo inicial; comparar con el puente simple, no adjudicar la mejora central a la capa hurdle. R5 ensaya tasas de cambio ligadas a la edad del episodio.
3. **R3 experimental de R4/R5 (no confundir con el candidato R3 congelado):** control experimental de cero-cero para huecos de 240 min; 90 de 90 casos alertados en aquella evaluación, con un episodio oculto de ~1,73 kWh. R5 logra predictor preliminar de energía en 276 ventanas, no supera consistentemente al puente.
4. **R4 experimental de R4/R5:** R5 amplía elegibilidad a 1.302 casos en 22 fechas con edades mínimas observables. Solo 96 casos de energía positiva en siete fechas; MAE puente 0,022814 y R5 edad mínima 0,024822 kWh. R4 concluye que no debe alertar indiscriminadamente.
5. **R5 experimental de R4/R5:** R4 encuentra días comparables 30/120/240 min = 140/140/139; días con episodio oculto = 0/0/5. R5 prueba inferencia de edad incierta ponderada por estado final: MAE 0,028628 kWh, peor que puente y R5 anterior. **Resultado negativo: no promover esa variante.**
6. **R6 nueva:** auditoría de robustez temporal para R4 y R5 híbrido restringido por elegibilidad estructural, con comparadores, métricas y CI por fechas (detalles abajo).

## Sexta ronda, R4: rareza y no estacionariedad

Se localizaron cinco fechas distintas con alguna ventana de 240 min cero/cero y energía positiva: **2026-06-23, 06-29, 07-06, 07-12 y 08-31**, entre 139 días históricos comparables (3,60%). Esta prevalencia mezcla estaciones y no permite predecir un día concreto.

Prueba cronológica del riesgo diario, mismos últimos 119 días y cinco eventos, score Brier menor es mejor:

| Historia previa utilizada | Brier |
|---|---:|
| 30 días comparables | 0,042227 |
| 60 días | 0,041627 |
| Toda la anterior | 0,040884 |
| Probabilidad fija entrenada en los primeros 20 días | 0,040583 |
| Siempre 0% (referencia ilustrativa; no da predicción probabilística útil) | 0,042017 |

El modelo de riesgo basado en 30/60 días **no demuestra ventaja consistente**, y un riesgo 0% es epistemológicamente falso aunque puntúe relativamente bien. La concentración de cuatro eventos en tres semanas y uno al final de agosto ilustra el riesgo de suponer estacionariedad. Usar más historial no ha demostrado mejor discriminación. **No ajustar umbrales de alerta basándose en estos cinco eventos.**

## Sexta ronda, R5: híbrido estructural, sin selección por resultado

Regla exploratoria predeterminada antes de calcular errores de esta ronda: aplicar el R5 de edad mínima **solo** cuando hay un extremo con importación positiva y se conoce el comienzo del episodio; utilizar el puente de extremos para los demás períodos. No utiliza la energía real del interior para decidir el método.

Comparación sobre **los mismos 1.302 períodos / 22 fechas**, de los cuales 96 son positivos en siete fechas:

| Método | MAE total (kWh) |
|---|---:|
| Puente simple | 0,022814 |
| R5 edad mínima | 0,024822 |
| **Híbrido selectivo** | **0,022606** |
| R5 mezcla de edades (anterior) | 0,028628 |

El híbrido seleccionó **64 casos**; su ventaja aparente frente al puente es **0,000208 kWh de MAE**, cerca de 0,9% relativo y muy pequeña en valor absoluto. La comparación bootstrap agrupada por fecha (2.000 repeticiones) de diferencia híbrido menos puente produjo IC 95% descriptivo **[-0,001853; +0,001274] kWh**; incluye tanto ganar como perder. Solo 4/22 fechas favorecen al híbrido, 2 lo empeoran y 16 empatan por aplicar el puente. Los 96 positivos se distribuyen en siete fechas; su IC también cruza cero. **NO SUPERIORIDAD DEMOSTRADA, NO CONGELAR.**

La variante que aplica R5 a todos los casos de extremo positivo obtuvo MAE 0,022516 kWh, aún exploratoria, sin respaldo para cambiar la regla tras mirar este resultado.

## Suficiencia y decisiones

- **R3:** estado anterior `INSUFFICIENT`; esta carpeta no consulta el período futuro y no ejecuta `score`. Un nuevo ZIP acumulativo exigirá nueva comprobación `count` congelada.
- **R4:** conviene seguir como investigación estructural y calibración; cinco días con eventos ocultos son insuficientes para un clasificador individual fiable o demostrar cobertura de cola rara.
- **R5:** la línea de dos estados es técnicamente factible en subconjuntos, pero ni los modelos de duración ni el híbrido tienen una ventaja independiente demostrada. Tres estados no se justifican con tan pocos valores intermedios.
- **En ambos:** fechas independientes, régimen temporal, activos y eventos raros importan más que los cientos de ventanas superpuestas. Las muchas comparaciones retrospectivas no autorizan elegir un ganador y declararlo validado.

No se publica serie de usuario, ID de dispositivo, credenciales, ZIP de datos, boleta Enel, ni resultados de futuras fechas R3. No se toca software productivo.

## Adenda séptima ronda (robustez)

La aparente mejora de MAE del híbrido R5 se revierte al retirar una única ventana de 240 min del 2026-09-11 (delta MAE +0,000560 kWh) o al quitar todas las ventanas de 240 min (delta +0,000242 kWh). Por lo tanto, no se sostiene una ventaja general. Los cinco días R4 con importación oculta de 240 min corresponden a episodios de entre 1,73 y 8,66 kWh, aun cuando su frecuencia diaria sea 5/139. Véase [RONDA7_AUDITORIA_ROBUSTEZ.md](RONDA7_AUDITORIA_ROBUSTEZ.md) para trazabilidad y controles. R3 no fue consultado ni alterado.
