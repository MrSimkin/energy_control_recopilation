# TARGET-PC QA — Build 441 — 2026-09-28

Status: **READY FOR ONE BUNDLED TARGET-PC QA PASS**

Windows Build:
- build: **441**
- code commit: `e4ae3b4700d3a6f4be2401f57aec92158998f6ac`
- workflow run: `36490027783`
- CI: **GREEN**
- artifact: `SolarEnergyMonitor-win-x64-dev`
- artifact ID: `11001317265`
- SHA-256: `3b957efe709f82b955c5dc9ef087ba4db0fd1b714c211584da85fd7743e7cba3`

This QA intentionally bundles the UX + Enel/tariff/audit tranche. Do not split it into multiple mini-QA rounds.

## 1. Data reuse / install path

Preferred test because guided import is part of this tranche:

1. Extract Build 441 into a **new clean folder**.
2. Keep the previous working build/Data folder unchanged as backup.
3. Launch Build 441 without manually overwriting its program files with the old build.
4. In **Datos -> Importar datos**, select the previous installation or its `Data` folder.
5. Confirm:
   - visible import progress/activity;
   - app does not look frozen;
   - automatic restart/application of imported data;
   - existing history/profile/bills/readings remain available after restart.

Fallback:
- if guided import cannot be exercised, copy the existing portable `Data\` folder into the new Build 441 folder as in previous QA cycles and report that guided import was skipped.

Do not run a new full historical backfill solely for this QA.

## 2. Startup / foreground / visible work

After data is present:

- close the app completely and reopen it;
- confirm the splash/startup status visibly changes while work occurs;
- confirm the main window opens in a normal visible state and comes to foreground rather than appearing minimized/behind other windows;
- confirm visible footer/activity feedback appears for any startup data preparation that takes noticeable time.

Report only if startup still appears frozen, minimized/hidden, or silent during a noticeable long task.

## 3. Visual/navigation pass

Review these pages at normal/maximized width:
- Dashboard;
- Analysis;
- Battery;
- Grid & Utility / Red y compañía eléctrica;
- Data;
- Reports.

Confirm:
- selected sidebar page is visually obvious;
- complex pages use task tabs instead of one very long mixed page;
- buttons have consistent primary/secondary/destructive hierarchy;
- tables have coherent headers/selection/row styling;
- layout feels materially cleaner than Build 350.

Then make the window narrower (without going below its allowed minimum) and confirm summary-card rows stack rather than becoming unreadably compressed.

Evidence requested:
- **one screenshot at normal/maximized width** showing a representative complex page;
- **one screenshot at narrower width** showing the responsive card/tab behavior.

No pixel-perfect approval is expected yet; report clipping, unreadable density, broken tab headers or obviously unattractive/inconsistent areas.

## 4. Official Enel reading boundary UX

In **Red y compañía eléctrica -> Lecturas**:

- choose official Enel reading source;
- confirm there is **no editable time field**;
- confirm saved/displayed official readings are presented as an Enel date boundary, not as if Enel supplied an exact `00:00` measurement;
- personal readings may still use exact time.

If convenient, add or inspect two official readings that match one existing bill period.

## 5. Bill-to-reading automatic proposal

In **Boletas Enel**:

- add/select a bill whose dates correspond to saved official Enel readings;
- confirm the app proposes the matching start/end readings automatically;
- confirm the proposal remains user-correctable;
- confirm the bill/reading interval does not make you manually reason about an apparent one-day shift.

No need to recreate every previously entered bill.

## 6. Official tariff acquisition — main functional gate

Open **Tarifas oficiales**:

1. select year **2026**;
2. run **Descargar / actualizar año** once;
3. keep the app open until it finishes.

Confirm during the operation:
- visible global activity;
- local status text changes while catalog/PDFs/normalization are processed;
- UI remains responsive enough to show that work is continuing.

As of 2026-09-28 the official Enel archive exposes 12 regulated-supply publications for 2026, including retroactive July/August publications and multiple effective-month versions. The exact archive can change, so the hard gate is **non-zero discovery/capture** and internally coherent statuses rather than a permanently hard-coded count.

After completion verify:
- publications are listed, not `0/0`;
- captured PDFs show capture success or an explicit failure;
- normalization reports extracted candidates or an explicit normalization failure;
- July/August retroactive evidence is distinguishable;
- ambiguous/superseded/preferred version status is visible rather than silently overwritten;
- re-running 2026 does not destroy prior evidence.

Evidence requested:
- **one screenshot of the tariff-results table/status after completion**;
- copy/paste the final capture-status sentence if any failures are reported.

## 7. Bill audit preview

Open **Auditoría de boleta** and select one existing representative bill.

Confirm:
- audit starts from the bill, not an arbitrary reading pair;
- the table shows each stored bill line with a verification state;
- sources are human-readable (effective month + normal/retroactive + publication title), not only database IDs;
- unprovable lines remain “sólo evidencia real” / pending rather than receiving invented expected values;
- where a printed rate is found in official candidates, it is identified as verified;
- a rate match does **not** falsely claim that commune/RED/ETR has been uniquely established when it has not;
- editing/adding/deleting a bill line refreshes the audit preview immediately.

Important expected conservative states are acceptable:
- tariff version ambiguous;
- applicability ambiguous;
- actual-only evidence;
- missing component/rate source.

Those are not defects if the underlying evidence really is insufficient.

## 8. Two separate PDFs

### A. Reading comparison PDF

From **Comparar lecturas**:
- choose any valid reading pair;
- export the comparison PDF;
- confirm visible export activity;
- confirm the PDF describes a physical meter-vs-Solar-of-Things comparison and does **not** present itself as a bill audit.

### B. Enel bill audit PDF

From **Auditoría de boleta**:
- export the selected bill audit PDF;
- confirm visible export activity;
- confirm it includes:
  - bill identity/actual charges;
  - official reading boundaries;
  - Solar of Things comparison;
  - coverage/sensitivity caveats;
  - tariff evidence/verification table;
  - human-readable source publication traceability;
  - no fabricated expected values for unsupported components.

Evidence requested:
- return the **bill-audit PDF**;
- return the reading-comparison PDF only if it looks wrong or confusing.

## 9. What does NOT need to be repeated

For this QA do **not** repeat:
- full historical API archaeology;
- exhaustive diagnostics bundle unless a new error requires it;
- previously accepted family XLSX/PDF quantitative validation;
- a full Update Data/backfill solely for this test;
- Help/manual completeness review (intentionally deferred until near project end).

## 10. Return package

Please return only:

1. one normal-width screenshot;
2. one narrow-width screenshot;
3. one tariff-capture result screenshot;
4. the Enel bill-audit PDF;
5. short notes for any failed/confusing item, especially:
   - startup/window behavior;
   - tariff capture result/failures;
   - retroactive/version statuses;
   - audit-line statuses that look wrong;
   - clipping/unreadable/visually poor areas.

If everything else is good, no additional diagnostic bundle is required.

## Acceptance intent

This QA is intended to answer four questions in one pass:

1. Did the application become materially clearer and more attractive?
2. Does every noticeable process visibly communicate that work is occurring?
3. Does official Enel year capture finally work against the real site and preserve retroactive/version evidence?
4. Is the new bill-first audit understandable and conservative enough to continue toward full tariff applicability/reconstruction?

## Target-PC results received — Build 441 — 2026-09-28 (partial)

Immediate user-reported results recorded before further diagnosis/fixes:

### Item 6 — Official tariff acquisition: **FAIL**
- Selecting/running official tariff acquisition leaves the tariff area blank.
- No usable official tariff publications are downloaded/listed on the target PC.
- Therefore the real-site acquisition gate is **not accepted** despite internal fixture/smoke coverage.
- Treat this as a live-source integration defect, not a user configuration problem.
- Do not mark Phase 9 acquisition complete.

### Item 7 — Bill audit preview: **NOT TESTABLE / BLOCKED BY ITEM 6**
- The tariff-dependent audit cannot be meaningfully tested while no official tariff evidence is captured.
- Do not infer pass/fail for tariff verification or line reconstruction from this run.

### Item 8 — Two reports: **PARTIAL**
- The user successfully generated and returned the **reading-comparison PDF**.
- The bill-audit PDF was not produced/tested because the tariff/audit path is blocked by item 6/7.
- Returned comparison PDF evidence:
  - title explicitly identifies it as “Comparación de lecturas - Medidor vs Solar of Things”;
  - it explicitly states that it is **not** a bill audit;
  - interval: 27-08-2026 boundary -> 27-09-2026 17:56;
  - meter delta: 97.400 kWh;
  - Solar of Things total grid import: 84.080 kWh;
  - signed difference: -13.320 kWh / 13.68%;
  - coverage: 99.3%;
  - sensitivity range: 84.080–98.032 kWh;
  - report preserves the Enel date-boundary convention and warns that discrepancy alone does not prove billing error.
- This supports the separation between arbitrary reading comparison and bill audit.

### Service-location clue for later applicability research
- User reports the service is in **Ñuñoa, Villa Olímpica, Santiago** and in an apartment building.
- This may be used only as a research clue for official territorial/service applicability.
- Do **not** infer RED, ETR, commune-specific tariff column, meter topology or building/common-service treatment from location alone.

Immediate priority after recording:
1. diagnose/fix live Enel tariff acquisition;
2. revalidate capture against the real official source internally where possible;
3. only then request the blocked tariff/audit subset of QA;
4. do not make the user repeat unrelated Build 441 checks.

