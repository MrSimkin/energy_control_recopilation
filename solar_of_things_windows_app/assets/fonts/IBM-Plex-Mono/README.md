# IBM Plex Mono — tipografía numérica para Build posterior a 855

**Estado:** fuente monoespaciada abierta incorporada a la rama preparatoria por petición del propietario. **No se ha compilado ni integrado en código aún.**

## Contenido

- `IBMPlexMono-Regular.ttf` — estilo Regular.
- `IBMPlexMono-Bold.ttf` — estilo Bold.
- `OFL.txt` — licencia SIL Open Font License 1.1 del fabricante (IBM).

**Fuente oficial:** `https://github.com/IBM/plex`, subdirectorio `packages/plex-mono/fonts/complete/ttf/`.  
**Revisión de origen fijada:** `763c36ef9117782905ae010056dfbe8fd2653a25` (IBM/plex).

| Archivo | Git blob SHA del archivo original |
|---|---|
| IBMPlexMono-Regular.ttf | `4254c37f20ba57e829a3315fdc91cda4e63589f5` |
| IBMPlexMono-Bold.ttf | `9a168d21d5f8a951fa1940cf4703926fcccc5ee0` |
| OFL.txt | `f72f76504cd73cf2c00b140b2743c138320597b6` |

La licencia OFL 1.1 permite **uso, incrustación, empaquetado y redistribución junto con software** sujeto a conservar aviso de copyright/licencia y demás condiciones OFL. Las fuentes no pueden venderse por sí solas. No modificar ni renombrar los binarios si no es necesario.

## Contrato de implementación — decisión final owner

1. **Sólo PDF:** incorporar **IBM Plex Mono Regular y Bold** para números, moneda CLP, porcentajes, kWh, fechas y tablas numéricas; texto narrativo/títulos pueden seguir usando fuente proporcional.
2. Incorporar los dos binarios en el paquete de la aplicación Windows con licencia **OFL.txt**. Configurar el resolver de fuentes de PDF para leer estos TTF desde la ruta administrada e incrustar legalmente la fuente. Probar PDF renderizado, fuente presente e incrustada.
3. **Excel (XLSX):** elegir **Aptos Mono si está disponible en Office**, o usar la **fuente estándar/predeterminada de Excel**. **No forzar IBM Plex Mono en Excel** ni crear dependencia de instalación de IBM en el equipo lector de XLSX. Conservar celdas numéricas, formatos y fórmulas.
4. Garantizar que `OFL.txt` acompañe los TTF distribuidos. Los TTF Aptos de Microsoft **NO** son parte de GitHub ni de la Build; no incrustarlos en PDF ni empaquetarlos.
5. Tests de PDF y Excel con tildes, signos, pesos Regular/Bold en PDF, CLP, kWh, alineación decimal, y comprobación de formatos nativos de XLSX.


## Estado técnico

**Fuentes + licencia en repositorio:** DONE.  
**Integración y uso en los exportadores:** PENDIENTE DE WORKER.  
**ZIP/Build correctiva:** AÚN NO GENERADA.
