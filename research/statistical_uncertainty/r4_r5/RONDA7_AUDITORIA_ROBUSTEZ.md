# Séptima ronda R4/R5: auditoría de robustez

**Fecha:** 2026-10-28. **Estado:** REVISIÓN HISTÓRICA / DESARROLLO. **No validación prospectiva, no congelación, no aprobación de producción.**

Fuente privada fijada: SolarEnergyMonitor_R4_R5_Ronda5_20261028.zip (SHA-256 5ed26b91b62c67a2bf689248b59cedc8d38266f0f4176403c8bb042ad50229dc). Solo fechas anteriores al 2026-09-28; no se usan desenlaces futuros R3 ni documentos de Enel. El script reproduce resultados agregados y verifica SHA del archivo completo y manifiesto de la ronda 5.

## R4: frecuencia vs gravedad

- Para ventanas de 240 minutos con potencia cero en ambos extremos: **5 fechas con episodio oculto sobre 139 fechas comparables**, con **5 ventanas afectadas sobre 489 ventanas cero/cero**. Son dos denominadores diferentes; las ventanas de un día no son independientes.
- Energía verdadera dentro de esas cinco ventanas: 6,402; 7,220; 8,661; 7,425; 1,729 kWh (junio 23/29, julio 6/12 y agosto 31). Un evento poco frecuente puede ser energéticamente importante.
- No aparecieron eventos de 30 o 120 minutos en la selección retrospectiva; esto no demuestra que no existan.
- Cinco fechas no bastan para demostrar habilidad discriminativa individual ni cobertura robusta en cola. No ajustar rangos/umbrales retrospectivamente para cubrir los cinco casos.

## R5: la aparente victoria es frágil

**Comparación sobre los mismos 1.302 casos de 22 fechas** (96 casos positivos en 7 fechas). El híbrido utiliza predicción R5 solo en 64 casos, distribuidos en 6 fechas; en el resto usa el puente simple.

- MAE puente = 0,022814 kWh.
- MAE híbrido = 0,022606 kWh.
- Mejora global aparente = 0,000208 kWh por caso.
- La mayor contribución procede de **una ventana de 240 minutos del 2026-09-11**, en que R5 mejora el error absoluto cerca de **1,00007 kWh**.
- **Sin ese único caso**, el híbrido pasa a **empeorar** frente al puente: delta de error **+0,000560 kWh**.
- Sin todas las ventanas de 240 minutos, también empeora: delta **+0,000242 kWh**.
- Intervalo bootstrap percentil 95% por fecha, 10.000 iteraciones, delta híbrido menos puente: **[-0,001811; +0,001254] kWh**. Incluye cero y es post-selección, no una prueba prospectiva independiente.

**Conclusión R5 revisada:** no promover el híbrido como una mejora predictiva general. Conservamos el código y la hipótesis, pero necesitamos más fechas con casos activos, estados y duraciones identificables antes de cualquier prueba de superioridad.

## QA, reproducibilidad y seguridad

- Se ejecutó el script completo privado de esta séptima ronda: **15 controles PASS**, incluyendo hashes, corte temporal, duplicados, valores no negativos, coherencia de selección y sensibilidad.
- El script compacto de este repositorio vuelve a calcular los puntos principales **directamente desde el ZIP privado de quinta ronda**; no incluye los CSV privados por caso.
- Las pruebas utilizan exclusivamente el corpus de desarrollo ya visto, por lo que múltiples variantes pueden sobreajustarse aunque la fecha de entrenamiento de cada predicción sea anterior a su objetivo.
- R3 congelado, base SQLite, app Windows, boletas Enel y main intactos.

## Siguiente investigación

R4: estudiar abstención y calibración de eventos escasos sin falsear probabilidades, separando frecuencia y severidad.

R5: analizar sensibilidad por fecha y calidad de edades observadas; no añadir estados ni ajustar el híbrido para perseguir el resultado del 11 de septiembre.

Los próximos protocolos prospectivos R4/R5 requerirán aprobación y congelación previa de objetivos, reglas de parada, datos y métodos. Este informe NO autoriza scoring futuro.
