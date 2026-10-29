# R4/R5: décima ronda, robustez por fechas y reglas del examen

**Estado:** investigación retrospectiva solamente, 28/10/2026. Sin score futuro, sin cambios a R3 ni modelos productivos. Se utilizó el ZIP privado de ronda 5, verificado por SHA-256 y manifiesto. Los resultados de antes del 28/09/2026 ya se habían usado para desarrollo.

## R4: fechas importan más que ventanas

| Mes 2026 | Fechas comparables | Fechas con importación oculta de 4 h |
|---|---:|---:|
| Abril | 8 | 0 |
| Mayo | 26 | 0 |
| Junio | 27 | 2 |
| Julio | 29 | 2 |
| Agosto | 25 | 1 |
| Septiembre (hasta 27) | 24 | 0 |
| **Total** | **139** | **5** |

**Interpretación:** cinco fechas con eventos raros son insuficientes para demostrar que el riesgo varíe según el mes o para encender semáforos individuales. Tampoco sabemos si la frecuencia histórica seguirá igual. El modelo debe ser capaz de abstenerse de una falsa seguridad.

## R5: el pequeño beneficio depende de pocas fechas

Comparación histórica de 1.302 ventanas repartidas en 22 fechas; solo 96 ventanas tuvieron energía positiva, repartidas en siete fechas. Puente sencillo MAE 0,022814 kWh; híbrido R5 MAE 0,022606 kWh. La diferencia es 0,000208 kWh por ventana, favorable al híbrido **solo en el promedio total**.

Una prueba que retira una de las siete fechas positivas en cada repetición muestra que al retirar una fecha la ventaja desaparece y R5 queda peor. La prueba no selecciona un ganador: revela una fragilidad. Además, el corpus de desarrollo R5 solo cubre agosto/septiembre, no una evaluación anual de estaciones distintas.

## Lo nuevo construido

- Código de reproducción retrospectiva y gráfico/JSON en ZIP privado `SolarEnergyMonitor_R4_R5_Ronda10_20261028.zip`, SHA-256 `5b35a3e9bbe5eb47d9912a039ed36b2a3895c56208638431ed80c904651288a9`.
- 26 pruebas automáticas PASS. Incluyen intento de ingresar resultados, fechas y números inválidos al contador simulado.
- Un contador **solo sintético**, que nunca emite READY o SCORE y cuya procedencia real todavía NO está auditada.
- Borrador de protocolo de validación futura para revisar y congelar antes de usar nuevos datos.

**Advertencia clave:** filtrar columnas prohibidas no demuestra evaluación ciega si la elección de ventanas ya utilizó energía verdadera. Se requiere auditar el colector/selector desde los timestamps y extremos, no solo revisar el CSV. El contador de prueba no puede usar telemetría futura real.

**No aprobación:** R4 y R5 permanecen experimentales; R3 está congelado, la PR permanece draft y no hubo cambio productivo.
