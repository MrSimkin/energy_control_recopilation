# Consolidación de repositorio sobre Build 866 — 2026-10-17

Estado: **main adelantado (fast-forward sin force) al último estado documental del código Build 866**. Sin release y sin declarar QA manual aprobada.

## Evidencia verificada
- `main` anterior: `59120a630b0f56684ba7672d960673f5c5c1797b` (Build 652).
- Código exacto del Build 866: `11437e2a115504485edfdc03664d78115efc53bf`.
- GitHub Actions Build 866: run `38333170633`, completed/success, 2026-10-12 (Chile).
- Después del commit de código, solo hubo un commit de documentación QA855: `4b26b703e1e19da973b665386cebdd9c6a590f37`. El código es el del Build 866.
- `main` avanzó desde la versión antigua hasta ese commit mediante actualización **fast-forward, sin force**, comprobando cabeza esperada. Se integró posteriormente **solo** documentación de candidatos paralelos R4/R5 (`research/statistical_uncertainty/PARALLEL_CANDIDATES_R3_R4_R5_2026-10-17.md`). Esto no cambia el binario Build 866.
- Punto de recuperación del main anterior: rama `archive/pre-build866-main-20261017` apuntando a `59120a6`.

## Estado real de QA
- **Build 855**: QA manual del propietario detectó defectos; está documentada en `QA855_REGISTRO_QA_2026-10-12.md`.
- **Build 866**: código corregido, CI Windows SUCCESS y comprobaciones sintéticas. El documento `QA855_ESTADO_CONSOLIDADO_Y_PENDIENTES_BUILD866_2026-10-12.md` registra que la **regresión manual del propietario sobre datos históricos copiados sigue pendiente**.
- Avanzar `main` a la base de código más reciente fue instrucción expresa posterior del propietario y **no convierte Build 866 en release aceptado ni revoca sus restricciones operativas de seguridad**.

## Estado de PRs
- PR #1 (fases 10–12): GitHub lo cerró como incorporado cuando `main` alcanzó su historial.
- PR #2 (diagnóstico H-01): cerrado como histórico; funcionalidad incluida en la rama unificada.
- PR #3 (histograma empírico Enel): cerrado como histórico/sustituido; el código de distribución empírica está presente en `main`; sus commits específicos difieren por la integración ulterior.
- PR #4 (unificación H-01, Enel, PDF): cerrado como histórico; su cabeza es ancestro de `main`.
- PR #5 (correcciones QA855): cerrado como histórico; su cabeza coincidió con la base Build 866 promovida antes de la adición documental.
- Los PR cerrados #2–#5 pueden aparecer como **no fusionados individualmente**; no equivale a pérdida de sus cambios, sino a incorporación por la cadena unificada que avanzó `main`.

## Ramas
- Rama de código Build 866: `work/qa855-corrections-package-20261012`; preservar como referencia.
- `work/phase10-12-consolidated-20261007`, `work/h01-debug-energy-exports-20261012`, `work/enel-bill-empirical-distribution-chart-20261012`, `work/qa-unified-h01-enel-family-pdf-20261012`: ramas históricas, PRs cerrados. No continuar trabajo sobre ellas.
- `work/statistical-parallel-r4-r5-20261017`: rama de origen del documento experimental, cuyo contenido está ya copiado en main; conserva un historial divergente porque se creó antes de la consolidación.
- `archive/pre-build866-main-20261017`: copia de recuperación intencional.
- **No se eliminaron ramas**; la limpieza de PRs fue no destructiva y la eliminación física de referencias históricas requiere un paso posterior con revisión expresa de necesidades de recuperación.

## Pendientes
1. Ejecutar QA manual dirigida de Build 866 usando **copia aislada** de los datos (PDF Enel; PDF/XLSX de Energía/Batería; navegación y latencia) y registrar aceptación real o fallos, sin alterar los datos productivos.
2. Mantener R3 frozen/INSUFFICIENT; no reemplazar `grid-import-empirical-bootstrap.v1` basándose en documentos experimentales.
3. Para R3, obtener un ZIP **acumulativo y reciente** del Research Exporter y efectuar **solo count** con el harness congelado. No puntuar si la compuerta sigue `INSUFFICIENT`.
4. Para R4/R5, el mismo paquete permite la auditoría preliminar de fechas, energía cero/positiva, episodios y duraciones. R4/R5 aún no están implementados ni validados; necesitan diseño/protocolo independientes congelados antes de validación futura.

Fuente de autoridad operativa original: `solar_of_things_windows_app/QA855_ESTADO_CONSOLIDADO_Y_PENDIENTES_BUILD866_2026-10-12.md`.
