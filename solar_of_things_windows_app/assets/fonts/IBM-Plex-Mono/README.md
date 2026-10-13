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

## Contrato de implementación

1. Usar **IBM Plex Mono** como familia monoespaciada predeterminada para números, moneda, porcentajes, kWh y fechas de **PDF y Excel**; texto narrativo y títulos generales pueden usar fuente proporcional.
2. Incorporar estos binarios junto a la aplicación para que no dependa de que el usuario instale manualmente IBM Plex Mono.
3. En PDF, resolver los archivos desde ruta administrada y **incrustar legalmente** en el documento; no forzar fuente de Aptos Mono con permisos de incrustación restringidos. Probar la salida PDF con inspección visual y presencia de fuente.
4. XLSX no suele incluir el binario TTF en el archivo: guardar estilo de fuente `IBM Plex Mono`. Asegurar visualización en máquina de destino mediante instalación autorizada de la fuente o fallback; **no convertir números a texto**.
5. Garantizar empaquetado del archivo de licencia `OFL.txt` en el artefacto si van los TTF; verificar que el árbol del repositorio conserve el archivo de licencia.
6. Agregar pruebas de caracteres españoles, pesos Bold/Regular, importes CLP, columnas alineadas y fórmulas Excel.
7. El ZIP `Microsoft Aptos Fonts.zip` del propietario **NO** está ni debe estar en el repositorio público, y no es dependencia de CI.

## Estado técnico

**Fuentes + licencia en repositorio:** DONE.  
**Integración y uso en los exportadores:** PENDIENTE DE WORKER.  
**ZIP/Build correctiva:** AÚN NO GENERADA.
