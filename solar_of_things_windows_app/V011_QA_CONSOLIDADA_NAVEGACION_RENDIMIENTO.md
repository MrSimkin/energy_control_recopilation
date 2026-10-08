# v0.11.0 — QA consolidada de navegación, filtros y rendimiento

**Estado:** PLAN para una única prueba posterior con el propietario; no ejecutado ni aceptado en su Windows. No requiere probar cada Build intermedia. La entrega final debe seguir BUILD_HANDOFF_RULE.md con ZIP verificable y commit de origen.

## Seguridad antes de probar

- No reemplazar ni restaurar la base existente D:\\SolarEnergyMonitorTest\\Data\\energy.db ni borrar/copiar automáticamente los respaldos. Cualquier operación de migración requiere los resguardos y permisos propios ya establecidos; detenerse ante advertencia de riesgo.
- Los corpus SQLite de CI son sintéticos: sus tiempos no demuestran rapidez con los datos reales. Cualquier prueba de lectura en la aplicación del propietario solo durante la QA acordada, sin suministrar el archivo real al CI o a terceros.
- No mostrar credenciales, claves, contenido de boletas, SQL privado ni rutas sensibles en capturas.

## Recorrido de verificación, en una sesión

1. **Navegación:** Dashboard → Battery → Analysis → Dashboard, varias veces, alternando lentamente y luego con rapidez. Verificar que no aparezcan datos de un dispositivo anterior ni que una pantalla abandonada siga sobrescribiendo otra.
2. **Battery:** entrar y salir rápidamente. Si la lectura actual está fresca, no debería dispararse una actualización remota redundante; si está vencida, solo la navegación vigente con sesión activa puede solicitar actualización. Observar el indicador de actividad y los kWh sin afirmar validación física de esas cifras por esta prueba.
3. **Analysis: presets:** alternar últimos 7 días, último mes y último año; verificar que el resultado corresponda al último rango elegido, no a uno previo que terminó más tarde.
4. **Analysis: selección manual:** cambiar una fecha. Debe aparecer «Rango modificado. Presiona Aplicar para calcularlo». Antes de Aplicar, el gráfico no debe ser reemplazado por una lectura de otro rango que termine atrasada. Pulsar Aplicar y revisar el período resultante.
5. **Analysis: series:** con rango ya calculado, marcar/desmarcar Solar, Casa y Red. Solo debe cambiar la serie de energía visible; los datos históricos no deberían recalcularse por esta acción. Verificar también el gráfico Battery y cambios de agregación.
6. **Idioma y errores:** alternar español e inglés y observar etiquetas, estados de cálculo, valores faltantes y controles durante navegación.
7. **Diagnóstico:** entrar a la pantalla Diagnóstico al terminar. Revisar p95 ordenado, cantidad de muestras n, media y máximo de las operaciones disponibles. Prestar atención a UI.Navigation, UI.Analysis.EnergyChart, UI.Analysis.BatteryChart, UI.Analysis.SeriesToggle, Data.Analysis.BackgroundFetch, Data.Analysis.PresetCoverage, Data.Battery.NavigationSnapshot y UI.Dispatcher.TickLateness. Puede no haber muestras de alguna operación; eso no implica que la función haya fallado.

## Interpretación y reporte

- Una muestra pequeña no da un p95 robusto. El contador de latencia del temporizador solo indica retraso de despacho; **no demuestra por sí solo una congelación ni su causa**. El límite de muestras es acotado y solo se guarda en memoria.
- Reportar PASS/FAIL/NO PROBADO por cada recorrido. En FAIL, registrar Build, operación, pasos reproducibles, síntoma y captura recortada sin datos privados. No inferir mejoras cuantificadas entre Builds sin observaciones comparables.
- Bloquean aceptación: crash, datos de otro dispositivo/rango, congelamiento reproducible, valores físicos alterados o cualquier cambio no autorizado de DB/respaldos.
- Este plan es solo una parte de la QA final: las aceptaciones y pendientes de Enel, SQL y recuperación tienen sus propios gates. Ni CI verde ni este documento autorizan merge, restore o declaración de fases completas.
