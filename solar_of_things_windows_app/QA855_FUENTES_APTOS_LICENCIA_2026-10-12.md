# QA 855 — Registro de fuentes Aptos y condición de licencia (sin binarios)

**2026-10-12.** El propietario adjuntó `Microsoft Aptos Fonts.zip` para tipografía numérica en PDF y XLSX. Este documento conserva **selección, manifest y plan**; **NO** incluye/autoriza distribución de fuentes.

## 1. Inspección verificable del archivo adjunto

Archivo local entregado: `Microsoft Aptos Fonts.zip` (debe ser adjuntado nuevamente al Worker que implementa si lo necesita para inspección local).

- SHA256 ZIP: `6528fd120e719a9f985e94214eca6887d1653b88456916a792a630b02e95b025`.
- 29 entradas: fuentes Microsoft Aptos y `Microsoft Aptos Fonts EULA.rtf`.
- Variantes relevantes para números (SHA256 de contenido descomprimido):

| Archivo | SHA256 | OpenType OS/2.fsType |
|---|---|---|
| Aptos-Mono.ttf | `8f31bec58e172359c99801268ea8a50e15bd1a2975a1cb982dfa340c69d844fa` | 8 |
| Aptos-Mono-Bold.ttf | `d0c5fbc35f48eccce8b890abff13445a173ae5c8fb2de69f84cd06b97ee88a47` | 8 |
| Aptos-Mono-Italic.ttf | `a7be2fe98f3d3cc7e54dc0ea36626505adf6d88e52cc8502a3db7796d510068e` | 8 |
| Aptos-Mono-Bold-Italic.ttf | `c37db2c4b70af5eff3e8d932201a1ef1ab219d01996ab9ae45c023212c86e57d` | 8 |

`fsType=8` es indicador de **restricted license embedding**; no equivale a permiso de incrustación. Comprobar políticas de librería renderer y EULA antes de embebido de cualquier fuente.

## 2. Resultado de la revisión legal

La EULA incluida por Microsoft concede instalación/uso de copias en dispositivos, pero **no concede derechos de distribución ni sublicencia a terceros**. Por tanto:

- **NO** publicar estos .ttf/.otf en GitHub (el repositorio es PUBLICO).
- **NO** empaquetarlos en ZIP de release/artefacto de GitHub Actions.
- **NO** transferirlos a un CDN, base64 en documentos, git LFS ni repositorio privado compartido como evasión de condiciones.
- **NO** forzar embedding en PDF ignorando el bit fsType.
- **SÍ** documentar nombre de familia pretendida e implementar estilos compatibles para usar una fuente instalada legalmente en entorno propio.
- **SÍ** evaluar sustituto con licencia redistribuible (OFL/Apache/etc.) y colocar licencia junto a los binarios sólo tras aprobación del propietario; no sustituir la elección sin avisar.
- Se pueden preparar pruebas de layout con una fuente disponible localmente; nunca publicar el archivo propietario como fixture.

Este registro es gestión de cumplimiento y no afirma que todas las formas de uso/embedding no distributivo sean legales; si surge duda, optar por fuente abierta o autorización específica.

## 3. Contrato UI/exportador

- Objetivo visual: monoespaciada para cantidades, fechas, kWh, CLP, %, puntajes y cifras en tablas; mantener textos y cabeceras en fuente proporcional.
- Familia seleccionada owner: `Aptos Mono` Regular/Bold (instalada en equipo del propietario bajo sus derechos); para XLSX, asignar fuente por estilo sin convertir cantidades a texto. Número, porcentaje y dinero deben conservar formato numérico, precisión y agregabilidad; fecha debe conservar semántica de fecha.
- Para PDF portable: incrustar únicamente una tipografía cuyo uso/embedding/redistribución esté permitido. Un TTF Aptos Mono con fsType 8 **NO** satisface ese requisito por sí solo.
- Diferencia entre PDF y XLSX documentada: XLSX típicamente almacena referencia a familia de fuente, el equipo receptor debe tenerla instalada; PDF usa fuente incrustada o sustitución, según formato y licencias.
- Criterios de QA: columnas numéricas alineadas, tabulación de decimales, barras y leyenda visibles, títulos sin clipping, exportaciones ES/EN y PDF inspeccionado visualmente. Prueba de fuente instalada ausente: fallback explícito sin alterar valores.

## 4. Bloqueador parcial, no del resto de la Build

La distribución/incrustación de **Aptos Mono** en Build pública queda **BLOCKED por licencia actual**. No detiene reparar gráficos, pie de página, rendimiento, tablas, contratos Enel y prueba numérica.

Al handoff, informar al propietario en una frase precisa: «Aptos Mono queda como preferencia local; para incluir una misma fuente en los PDF redistribuibles y el paquete de la aplicación necesitamos aprobar una fuente libre monoespaciada o aportar derechos explícitos suficientes sobre Aptos». No declarar fuentes subidas.
