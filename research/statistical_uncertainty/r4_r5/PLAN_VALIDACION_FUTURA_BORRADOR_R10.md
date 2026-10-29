# Diseño de evaluación futura R4 y R5 (NO CONGELADO)

**Estado:** DOCUMENTO PARA REVISIÓN. No permite scoring, ni selección de modelos, ni declares READY. R3 y su corte 2026-09-28 mantienen total independencia.

## Tres cerraduras

1. **Fijar el examen:** para cada R4/R5 congelar por separado código SHA, parámetros, modelo candidato, calibración, ventanas objetivo, distribución de P5/P50/P95, comparadores, fechas prospectivas y regla de parada. Congelar antes de ver resultados futuros. Historia anterior al 28/09/2026: desarrollo ya consumido, nunca validación pristine.
2. **Contar sin saber la respuesta:** desde un selector de ventanas revisado y de procedencia verificada, contar solo fechas con muestras regulares, extremos observados, duraciones y edades de estados estructuralmente observables. No integrar kWh dentro del objetivo ni filtrar por la verdad. Un CSV con columnas permitidas no garantiza eso por sí solo.
3. **Examinar una sola vez:** después de cumplir la parada previamente fijada, score únicamente la cohorte inmovilizada hasta su primera fecha de parada, con comparación justa sobre las mismas ventanas. Si faltan eventos raros, declarar `INCONCLUSIVE_RARE_EVENTS`, no ajustar a posteriori.

## R4

Contar fecha elegible, extremos cero/cero por 30/120/240 minutos y franja de reloj local. Durante score evaluar comportamiento general **y** cola rara; solo tras score se conocerá cuántas fechas tienen energía positiva oculta. La frecuencia rara de desarrollo (5/139 fechas para 240 min) no autoriza un umbral de seguridad ni una regla automática de READY.

## R5

Contar días con extremos activos, duraciones y comienzos de episodios conocidos, sin mirar energía dentro del vacío. Solo en score mostrar días con importación efectiva, error de energía para esos casos y cobertura del modelo. Comparar puente sencillo, R5 fijado y abstenciones sobre la intersección común; informar porcentaje de casos donde R5 no ofrece predicción.

## Requisitos pendientes antes de aprobación

- Auditoría completa del origen de datos y del código de selección de ventanas.
- Fijar de manera ex ante tamaño de cohorte, número de fechas, estratos, reglas de parada y política de evaluación en cola rara.
- Resolver dependencia entre ventanas, DST en America/Santiago, muestreo irregular y múltiples comparaciones.
- Asegurar una cohorte genuinamente nueva posterior a cada congelamiento, sin reutilizar el futuro observado de R3 como test limpio R4/R5.
- Aprobación explícita de propietario antes de congelar, puntuar o cambiar la app.

No publicar telemetría, ZIP, base SQLite, boletas, credenciales ni CSV por objetivo en GitHub.
