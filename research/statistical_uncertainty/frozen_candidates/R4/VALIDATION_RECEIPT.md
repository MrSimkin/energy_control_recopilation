# Recibo de cierre de desarrollo R4-v3 | 2026-11-24

Estado: referencia experimental implementada y congelada localmente; aceptación histórica de vacíos individuales; validación prospectiva independiente pendiente. No se modificó GitHub ni C# ni reportes. No se puntuó R3 ni se consultaron resultados de su reserva futura.

Código SHA-256: `98d76656446efb7aca860b6c98e7f3c59a7b1645d9ee4de2e2ca5f8941d237b9`.
Paquete original SHA-256: `57e3d9219b96997ac20c78ce58e49e514a01613065d8a0819daf8cc816bd867a`. numpy 2.3.5, pandas 2.2.3.

## Pruebas
- Compilación Python, self-test, test_r4.py: PASS. Incluye mezcla, cuantiles, no negatividad, abstención, conteo, coincidencia entre selección ciega y score, límite real de 240 min.
- Reproducción exacta por caso de los 2490 resultados previos: verdad, P5, P50, P95 e interval score. Sin cambios a la arquitectura R4-v3.
- Defecto de construcción de targets operativos: casi todos los 240 nominales duran ligeramente más de 240 reales. Corrección previa a ver resultados prospectivos: si 240 no elegible, 225 nominal mapeado a 240. Resultado 270 casos 225 y uno 240; no cambiar donantes, algoritmo ni criterios.
- Operacional histórico: 2496 casos, 80 fechas, 461 activos, 610 positivos. 11/11 gates históricos PASS; cobertura 97.3958%, activa 89.3709%, positiva 89.3443%, inactiva 99.2138%, clase larga 95.9410% (48 activos, cobertura 85.4167%). MAE 0.174806 kWh frente al puente 0.157916; interval score 1.032877 frente al incondicional 3.584921. No confundir con validación futura independiente.
- Alcance histórico natural: 28 intervalos >15 min, 18 <=240, 14 dentro de misma franja/fecha (cribado estructural, no verificación de suficiencia donante individual). 50% del total; no resuelve todas las interrupciones.
- Agregación experimental 240 escenarios, 80 fechas: 92.5% global; 82.42% con componente activo (91 escenarios), 85.83% con verdad positiva (127). Gate numérico global pasa, pero revisión estructural recomienda NO aprobar agregación por ahora. No bloquear aprobación individual por este problema. No sumar cuantiles directamente; no publicar rango para totales múltiples como calibrado.
- Conteo prospectivo real: INSUFFICIENT, cero fechas desde 25/11. Score real rechazado correctamente antes de lock. No puntuación futura real.
- Flujo score mecánico completo con fixture sintético: READY 04/12, 10 fechas, 160 activos; FAIL deliberado por falta de evidencia inactiva/positiva en pool de extremos cero. El software no aprueba automáticamente. Los datos sintéticos NO son evidencia real ni predicción de fecha READY. No incluir fixture en resultado real.

## Decisión
Cerrar desarrollo R4-v3 sin nuevas optimizaciones. Referencia Python local lista. El 8/12 revisar R3 y R4 con un único ZIP acumulativo. Para R4 aplicar una sola regla de parada: 10 fechas activas, 80 casos y 5 por grupo; puntuar únicamente al cumplir. No cambiar reglas R3. Si pasa, evaluar aceptación individual y portar a servicio C# con paridad y QA; no esperar R5 para aprobar un uso individual que ya cumpla. La agregación tiene decisión independiente. Revisar situación de suficiencia el 31/01/2027 si sigue bloqueada, sin debilitar umbrales ni repetir holdouts; puede limitarse uso a experimental en lugar de esperar indefinidamente. No cambiar la aplicación productiva sin autorización de alcance conforme al repositorio.
