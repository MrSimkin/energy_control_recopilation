# Grid & Utility — UX redesign proposal before next substantive build

Date: 2026-09-28

Status: **PROPOSAL — REQUIRES USER CONFIRMATION BEFORE IMPLEMENTATION**

## Why redesign is required

Target-PC QA confirms that the current single long Grid & Utility surface is too dense and conceptually mixes distinct tasks:
- personal readings;
- official Enel readings;
- arbitrary physical-energy comparison;
- bill capture;
- bill line evidence;
- official tariff capture;
- future tariff reconstruction/audit.

The current XAML implements these as vertically stacked cards with many fixed-width controls and dense DataGrids. This makes the workflow hard to discover, hard to read, and fragile at different usable window widths.

The next substantive Grid/Utility build must therefore change the task architecture before adding more tariff/audit logic.

## Product distinction that must remain explicit

There are three different analytical jobs:

1. **Readings**
   Preserve cumulative meter evidence, whether official Enel date-only readings or exact-time personal readings.

2. **Free comparison**
   Compare any two readings chosen by the user against Solar of Things total grid import over the equivalent interval.

3. **Enel bill audit**
   Start from one actual Enel bill, use its official reading boundaries, obtain the applicable official tariff versions, reconstruct expected bill components, compare against the actual printed bill, and explain differences with uncertainty/sensitivity.

These workflows may reuse the same data but must not be presented as if they were the same task.

## Proposed navigation inside Grid & Utility

Use an internal tab/task navigation instead of one long page.

Recommended tabs:

1. **Resumen**
2. **Lecturas**
3. **Comparar lecturas**
4. **Boletas Enel**
5. **Auditoría de boleta**
6. **Tarifas oficiales**

The sidebar can continue to expose one top-level “Red eléctrica / Grid & Utility” destination.

---

## Tab 1 — Resumen

Purpose: answer “what do I have and what needs attention?” without editing anything.

Wireframe:

```text
┌──────────────────────────────────────────────────────────────────────┐
│ RED ELÉCTRICA / ENEL                                                │
│ Estado de evidencia y auditoría                                     │
├────────────────────┬────────────────────┬────────────────────────────┤
│ Lecturas Enel      │ Lecturas personales│ Boletas guardadas          │
│  N                 │  N                 │  N                         │
├────────────────────┴────────────────────┴────────────────────────────┤
│ Tarifas oficiales                                                   │
│ Periodos requeridos: 2025-07 → 2026-09                              │
│ Cubiertos: ...   Faltantes: ...   Última actualización: ...         │
│ [Actualizar tarifas faltantes]                                      │
├──────────────────────────────────────────────────────────────────────┤
│ Última boleta                                                       │
│ Periodo | kWh Enel | kWh Solar of Things | Estado auditoría          │
│ [Abrir boleta] [Auditar]                                            │
└──────────────────────────────────────────────────────────────────────┘
```

No large editable forms on this tab.

---

## Tab 2 — Lecturas

Purpose: create, inspect and correct meter observations.

Use a two-pane/master-detail design when width permits, and vertical stacking at narrower widths.

```text
┌──────────────────────────────────────────────────────────────────────┐
│ LECTURAS                                                            │
│ [Todas] [Enel oficiales] [Personales]        [+ Nueva lectura]      │
├───────────────────────────────────┬──────────────────────────────────┤
│ Historial                         │ Detalle / edición                 │
│ Fecha       Fuente      kWh       │ Fuente: Enel / Personal          │
│ 27-08-26    Enel        ....      │ Fecha:                           │
│ 27-09-26    Personal    ....      │ Hora: sólo si corresponde        │
│ ...                               │ kWh acumulado:                    │
│                                   │ Referencia / notas                │
│                                   │ [Guardar] [Eliminar]              │
└───────────────────────────────────┴──────────────────────────────────┘
```

### Enel date-boundary rule

Official Enel date-only readings must be represented as a **boundary**, not as a falsely precise timestamp.

Canonical interpretation:
- Enel date X at 00:00;
- is the same interval boundary as date X-1 at 23:59/end-of-day for the intended comparison.

Internally calculations should use one consistent half-open boundary convention. UI/report wording should avoid suggesting Enel supplied an exact 00:00 measurement time.

Suggested display:
- **“27-08-2026 · límite de fecha Enel”**
- secondary explanation: **“equivale al cierre del 26-08 / inicio del 27-08 para el intervalo”**

Do not force the user to reason manually about the one-day visual difference.

---

## Tab 3 — Comparar lecturas

Purpose: private/exploratory comparison of any two stored readings.

```text
┌──────────────────────────────────────────────────────────────────────┐
│ COMPARAR LECTURAS                                                   │
├──────────────────────────────────────────────────────────────────────┤
│ Desde                                                              │
│ [fuente: Todas ▼] [lectura seleccionada ▼]                          │
│ 27-08-2026 · Enel oficial · 1.234,5 kWh                             │
│                                                                      │
│ Hasta                                                               │
│ [fuente: Todas ▼] [lectura seleccionada ▼]                          │
│ 27-09-2026 17:56 · Personal · 1.331,9 kWh                           │
│                                                                      │
│ [Comparar]                                                          │
├──────────────────────┬──────────────────────┬────────────────────────┤
│ Medidor              │ Solar of Things      │ Diferencia             │
│ 97,4 kWh             │ 84,08 kWh            │ -13,32 kWh / -13,68%   │
├──────────────────────────────────────────────────────────────────────┤
│ Calidad del intervalo                                               │
│ Cobertura telemetry | límites temporales | gaps | sensibilidad       │
│ Explicación en lenguaje normal                                      │
├──────────────────────────────────────────────────────────────────────┤
│ [Exportar comparación personal]                                     │
└──────────────────────────────────────────────────────────────────────┘
```

Important:
- permit Enel→Enel, Personal→Personal and mixed Enel↔Personal comparisons;
- clearly label this as **comparison**, not bill audit;
- coverage must not be labelled confidence.

---

## Tab 4 — Boletas Enel

Purpose: preserve actual bill evidence in a readable master/detail workflow.

The current large form should not be shown all at once.

```text
┌──────────────────────────────────────────────────────────────────────┐
│ BOLETAS ENEL                                      [+ Agregar boleta]│
├──────────────────────────────────┬───────────────────────────────────┤
│ Boletas                          │ Boleta seleccionada                │
│ Ago 2026  84 kWh   $...          │ Resumen | Lecturas | Cargos       │
│ Jul 2026  ...                    │                                   │
│ ...                              │                                   │
└──────────────────────────────────┴───────────────────────────────────┘
```

Inside the selected bill use secondary tabs/sections:

### Resumen
- reference/document number;
- printed period;
- billed kWh;
- tariff plan as printed;
- taxable / VAT / exempt / gross / other adjustments / total due.

### Lecturas
- previous official reading;
- current official reading;
- cumulative values;
- date-only boundary explanation;
- manual date fallback only when no reading evidence exists.

### Cargos
Readable table preserving every printed line:
- section;
- printed description;
- quantity;
- unit;
- printed unit rate when present;
- amount;
- tax treatment.

Editing should happen in a small dialog or side panel, not through a dense wrap of text boxes above the table.

---

## Tab 5 — Auditoría de boleta

This becomes the main Enel-facing workflow.

The user starts with the bill, never by manually reconstructing its reading pair.

```text
┌──────────────────────────────────────────────────────────────────────┐
│ AUDITORÍA DE BOLETA ENEL                                            │
├──────────────────────────────────────────────────────────────────────┤
│ 1. Seleccionar boleta                                               │
│ [Agosto 2026 · ref ... ▼]                                           │
│ Estado evidencia: ✓ lecturas  ✓ líneas  ! tarifa pendiente          │
├──────────────────────────────────────────────────────────────────────┤
│ 2. Intervalo oficial                                                │
│ Desde: lectura Enel ...      Hasta: lectura Enel ...                │
│ Convención de límite de fecha explicada automáticamente             │
├──────────────────────────────────────────────────────────────────────┤
│ 3. Energía                                                          │
│ Enel facturado | diferencia de medidor | Solar of Things | rango    │
│ de sensibilidad / incertidumbre | interpretación                    │
├──────────────────────────────────────────────────────────────────────┤
│ 4. Tarifa aplicable                                                 │
│ Fuente oficial | vigencia | versión | retroactividad | servicio     │
│ [Ver fuente]                                                        │
├──────────────────────────────────────────────────────────────────────┤
│ 5. Reconstrucción de la boleta                                      │
│ Concepto impreso | Real | Esperado | Diferencia | Fuente/regla      │
│ ...                                                                  │
├──────────────────────────────────────────────────────────────────────┤
│ 6. Conclusión técnica                                               │
│ Explicación sin afirmar error de Enel más allá de la evidencia      │
│                                                                      │
│ [Exportar PDF de auditoría]                                         │
└──────────────────────────────────────────────────────────────────────┘
```

If required evidence is missing, show that explicitly and offer the direct action:
- missing official reading → add/link reading;
- missing tariff → acquire required tariff period;
- missing bill lines → complete bill evidence.

Do not permit a visually authoritative “audit complete” state while these prerequisites are absent.

---

## Tab 6 — Tarifas oficiales

Purpose: acquisition, evidence, versioning and diagnostics.

Replace the fixed 2026 button.

```text
┌──────────────────────────────────────────────────────────────────────┐
│ TARIFAS OFICIALES                                                   │
├──────────────────────────────────────────────────────────────────────┤
│ Periodo a cubrir                                                    │
│ (•) Según mis boletas   Desde [mes/año] Hasta [mes/año]             │
│ ( ) Periodo manual      Desde [mes/año] Hasta [mes/año]             │
│                                                                      │
│ [Buscar / actualizar tarifas oficiales]                             │
├──────────────────────────────────────────────────────────────────────┤
│ Cobertura                                                           │
│ Mes      Estado      Publicación usada      Retroactiva   Fuente     │
│ ...                                                                  │
├──────────────────────────────────────────────────────────────────────┤
│ Diagnóstico de adquisición                                          │
│ Descubiertas N | descargadas N | fallidas N                         │
│ [Ver detalle de error]                                              │
└──────────────────────────────────────────────────────────────────────┘
```

Preferred default:
- **Según mis boletas**: derive the minimum historical range required by saved bills/readings.
- allow manual broader historical acquisition.

Tariff states should distinguish:
- source discovered;
- PDF cached;
- text extracted;
- normalized;
- service applicability resolved;
- authoritative for a given bill;
- superseded/retroactive.

A cached PDF alone must never appear as “tariff applied”.

---

## Responsive/layout rules for the redesign

1. Stop relying on one long StackPanel containing every workflow.
2. Use task tabs to reduce simultaneous information density.
3. Avoid four fixed summary columns at widths where they become cramped; wrap cards into 2x2 or 1-column layouts based on available width.
4. Avoid giant editable WrapPanels with many fixed-width TextBoxes.
5. Use master/detail patterns and modal/side-panel editors for records with many fields.
6. DataGrids may scroll horizontally only as a last resort; prioritize essential columns and expose the rest in the detail panel.
7. Preserve a comfortable minimum readable width for labels and values.
8. Do not let nested DataGrids fight the page scroll.
9. At narrower widths, stack panes vertically rather than compressing them.
10. Every tab should have one dominant user task and one obvious primary action.

## Reporting consequences

There should be two distinct exports:

### Personal comparison PDF
- selected arbitrary reading pair;
- physical meter delta;
- Solar of Things total grid import;
- coverage and boundary/sensitivity caveats;
- no implication that this is an Enel bill audit.

### Enel bill audit PDF
- starts from one bill;
- exact official reading evidence;
- boundary convention;
- actual billed energy;
- independent Solar of Things comparison;
- uncertainty/sensitivity;
- applicable official tariff source/version;
- expected bill reconstruction;
- actual printed bill lines;
- differences and explanation;
- traceability suitable for a technical discussion with Enel.

## Preconditions before implementation

Before the next substantive build:
1. user confirms or adjusts this navigation/workflow;
2. remaining tariff acquisition behavior is investigated against the real official source;
3. ambiguities listed in the product questions are resolved;
4. implementation plan is split into small testable tranches rather than rebuilding every Phase 8–10 feature at once.

No code implementation is authorized by this proposal alone.


## Cross-application tabbed UX rule — clarification

The tab/task-separation principle in this proposal is not limited to Grid & Utility.

For any materially complex page/window that currently combines several distinct user tasks in one long vertical surface:
- prefer tabs or equivalent task-level navigation;
- keep one dominant task per tab;
- move dense edit forms out of always-visible stacked layouts;
- use responsive stacking rather than shrinking controls until they become hard to read;
- preserve consistent visual language across the application.

This rule should be considered during the next UX pass across all relevant screens.

## Confirmed comparison/report split

The application must expose two clearly distinct analytical products:

### A. Reading comparison report
User selects readings and compares meter-derived consumption against Solar of Things total grid import over the equivalent interval.

This report is independent of bill reconstruction.

### B. Enel bill audit report
User starts from an actual bill and receives:
- linked official reading boundaries;
- inverter comparison over the bill interval;
- verifiable tariff-based reconstruction of bill components;
- actual-versus-expected component comparison;
- explicit handling of charges that cannot be independently reconstructed;
- a separate audit PDF.

The two reports must not be merged into one generic export.

## QA cadence

Development may use multiple internal CI/build iterations.

Target-PC QA should be requested only after a coherent tranche is assembled. Avoid repeated user-facing mini-QA handoffs unless a narrow blocker genuinely requires target-machine evidence.
