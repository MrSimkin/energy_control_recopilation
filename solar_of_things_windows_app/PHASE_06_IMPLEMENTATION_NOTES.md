# Phase 6 — Core Dashboard and Interactive Analysis UI — Implementation Notes

Date started: 2026-09-25

Status: **IN PROGRESS — FIRST INTERACTIVE ANALYSIS CHECKPOINT**

## Canonical UI/data rule

Every chart and table must consume the same validated local statistics layer.

Charts must not:
- query raw telemetry through a separate calculation path;
- alter physical calculations based on household settings;
- bridge long missing-data intervals;
- hide low coverage;
- fabricate current/live status from stale local readings.

Household-specific settings are allowed only as contextual explanation/overlays (for example battery 10/20/50% reference lines) and do not modify SOC measurements.

## Implemented normal household UI

### Home

- latest locally available PV, house/load, battery charge and grid values;
- explicit timestamp/freshness warning;
- plain-language explanation of the latest observed household state;
- stored/unverified/verified Solar of Things connection-state distinction;
- installation date and next automatic historical-download date kept distinct;
- Update Data with safe Stop and progress.

### Battery

- charge percentage;
- estimated stored energy;
- estimated ordinary-use energy above the configured normal reserve;
- remaining emergency outage reserve;
- protected-floor explanation;
- charging / supplying house / resting;
- parent-friendly language and tooltips;
- collapsed secondary “Información técnica” panel:
  - measured battery voltage;
  - measured charging current;
  - measured discharge current;
  - derived battery power;
- target normalization rule advanced to hpvinv02.v3 to preserve charge/discharge current measurements independently of household context.

### Data & Updates

- actual first/last saved reading;
- reviewed days;
- empty days;
- retryable/problem days;
- terminal unavailable days;
- raw reading count;
- normalized reading count;
- next automatic historical date;
- read-only current-configuration health summary.

### History & Charts

- exact From/To dates;
- transparent quick period presets that populate the visible dates;
- manual date edit switches to custom range;
- contextual time summaries with uncovered time kept unknown;
- physical whole-period energy totals with coverage;
- hour/day/week/month/year grouping;
- detailed auditable aggregation table;
- minimum coverage per bucket;
- visible warning when any displayed bucket is below 80% coverage;
- interactive ScottPlot energy chart:
  - solar;
  - house;
  - grid;
  - pan/zoom;
  - reset view;
  - source-selection checkboxes;
  - readable hover/tooltip point inspection showing period values and coverage;
- separate interactive battery-charge chart on a 0–100% axis:
  - average SOC;
  - ending SOC;
  - 10% protected-floor contextual reference;
  - 20% normal grid-transfer contextual reference;
  - 50% normal return contextual reference;
  - readable hover/tooltip inspection for average/end/min/max SOC and coverage;
- energy and battery charts share horizontal/time-axis movement while keeping independent kWh and % Y axes.

## Accessibility / older family users

Normal UI follows the product requirement for people 75+ with no technical background:

- Spanish default;
- everyday household wording;
- larger readable headings/values;
- technical terms moved to tooltips/Technical Help;
- no reliance on color alone for important state;
- explicit stale-data warning;
- exact table retained below charts for auditability;
- reset-view button so users do not need to know chart gestures.

## Chart dependency

ScottPlot.WPF 5.1.59:
- centrally version-managed;
- MIT license;
- WPF-specific;
- interactive pan/zoom;
- chart data comes from EnergyAggregationTable rows, the same rows displayed in the detailed table.

## Validation already green

- first energy chart: run 232 — SUCCESS;
- separate battery chart rendering: run 236 — SUCCESS;
- low-coverage chart warning: run 239 — SUCCESS;
- chart reset control: run 243 — SUCCESS;
- battery 10/20/50 contextual overlay: run 249 — SUCCESS;
- energy-chart metric picker: run 253 — SUCCESS;
- readable chart hover inspection: run 257 — SUCCESS;
- synchronized energy/battery horizontal axes: run 258 — SUCCESS.

The battery technical-metrics build is the current intended next combined checkpoint.

## Deliberately not invented

These remain deferred until evidence-backed flow attribution is validated:

- PV contribution-to-load percentage;
- battery contribution percentage;
- percentage supplied without grid;
- solar self-consumption;
- export energy/revenue.

Zero-export remains household context; negative/export-like evidence is not converted into an ordinary export feature.

## Next combined target-PC validation

Use the existing real Data/energy.db.

Validate in one session:

1. schema migration and normal startup;
2. stale/latest-data wording;
3. Battery page reserve semantics;
4. Data & Updates coverage/configuration context;
5. History quick ranges;
6. hour/day/week/month/year aggregation;
7. physical energy totals and coverage;
8. detailed table;
9. energy chart pan/zoom/reset/source selection;
10. battery chart and 10/20/50 reference lines;
11. no loss of existing raw/history data;
12. Update Data resume frontier remains correct.

Do not require a full April→current backfill for this checkpoint.

## Partial real-PC combined validation — 2026-09-25

Artifact `10892690906` was tested on the real Windows 11 x64 target PC with the recovered real `Data\energy.db`.

Passed/observed:
- normal startup and recovered database open;
- stale-safe Home wording and exact saved timestamp;
- latest-saved-day summary with visible 74.0% low coverage;
- Battery family view and reserve estimates;
- Battery technical panel with real voltage / measured charge current / measured discharge current / derived power;
- Data & Updates first/last dates, reviewed-day counts, raw/normalized counts, and distinct installation vs next-download dates;
- History whole-period summaries, coverage warning, charts and detailed table render against the real corpus.

Real-PC issues to fix after completing the pending sync validation:

1. **Current settings vs family-policy thresholds**
   - Battery reserve UI currently takes 20/10/50 from `InstallationContextPolicyService`.
   - Several localization strings also hard-code 20/10/50.
   - The application already captures current inverter-setting evidence in the current-state snapshot and `InstallationHealthService`.
   - Prefer validated current device settings for claims about what the inverter is currently configured to do; retain the family manual as expected configuration/fallback/context and expose drift.

2. **Optional truly-current Home snapshot**
   - While authenticated, Home should be able to refresh a recent Solar of Things latest-state snapshot for PV, house/load, battery SOC (plus estimated kWh), and grid use.
   - Only label it current/live when freshness supports that claim.
   - Preserve the already-correct stale/local fallback.
   - Avoid aggressive polling.

3. **Missing-data chart correctness bug**
   - Battery chart currently builds scatter arrays by removing missing SOC buckets, then connects remaining points. This bridges long unknown intervals and is visually false.
   - Energy buckets with 0% coverage must not look like measured 0 kWh.
   - Rendering must preserve discontinuities and the canonical rule: missing/unknown is not zero.

4. **Chart interaction/readability**
   - Full-history sparse ranges are difficult to interpret.
   - Mouse-wheel interaction currently competes with page scrolling, making chart zoom/navigation awkward.
   - Improve wheel event handling, readable time-axis labeling, and long sparse-range UX.

The final combined-validation block remains pending:
- short `Actualizar datos`;
- current-state/auth refresh;
- sensible resume frontier;
- progress indication;
- deliberate Stop/Detener;
- committed-data preservation;
- persisted next resume frontier.

Do not require a full April→current backfill for this validation.

