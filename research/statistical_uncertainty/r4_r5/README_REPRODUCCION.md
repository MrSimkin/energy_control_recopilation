# Ronda 9 R4/R5: reproducción

**Datos privados no incluidos.** Utilizar exclusivamente el ZIP de desarrollo de ronda 5 con el hash SHA256 fijado en `round9_research.py`. No copiar bases SQLite productivas, ni boletas Enel, ni usar el ResearchPackage reservado para R3.

Requisitos locales: Python 3.11+, NumPy, pandas, SciPy, Matplotlib para gráficos.

```sh
python round9_research.py --round5-zip /ruta/privada/SolarEnergyMonitor_R4_R5_Ronda5_20261028.zip --output ROUND9_AGGREGATE.json
R4R5_ROUND5_ZIP=/ruta/privada/SolarEnergyMonitor_R4_R5_Ronda5_20261028.zip python test_round9.py
```

El segundo comando incluye pruebas sintéticas del conteo ciego. El `round9_blind_count_draft.py` **no es un programa de validación real**, jamás da READY y no se debe usar con datos reales futuros o el período reservado de R3 antes del congelamiento y autorización correspondientes.

El script de investigación lee únicamente los dos CSV derivados del ZIP de ronda 5 y vuelve a validar su manifiesto SHA-256. No abre el ZIP de producción de telemetría. Los agregados/figuras sí pueden publicarse; los datos por observación permanecen privados.
