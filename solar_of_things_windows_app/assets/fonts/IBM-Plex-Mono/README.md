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

1. **PDF:** IBM Plex Mono Regular/Bold **sólo para los números** (CLP, kWh, porcentajes, cantidades, fechas, cifras en tablas). Integrar TTF al paquete Windows con `OFL.txt`, configurar resolver PDF e incrustar. **No cambiar el resto de fuentes y estilos del PDF.**
2. **Excel/XLSX:** los números usarán **Consolas**, familia monoespaciada incorporada a Windows/Office y disponible para Excel, sin distribuir binarios. **Aptos Mono** es alternativa cuando se confirme su presencia. **No usar Aptos normal/Calibri/u otra fuente proporcional para los valores numéricos.** No utilizar IBM Plex Mono en Excel, ni modificar la fuente del resto de celdas/textos/títulos que ya estaban correctos.
3. Preservar tipos numéricos, fechas, formatos, fórmulas, tablas, encabezados, colores y el diseño anterior. Aplicar **solamente** fuente a celdas/segmentos numéricos pertinentes.
4. Verificar PDFs renderizados, IBM incrustada, Excel con mono **sólo cifras** y estilos del resto intactos. Los TTF Aptos Microsoft no se suben al repositorio ni se empaquetan.


## Estado técnico

**Fuentes + licencia en repositorio:** DONE.  
**Integración y uso en los exportadores:** PENDIENTE DE WORKER.  
**ZIP/Build correctiva:** AÚN NO GENERADA.
