# TARGET-PC QA — Build 486 — 2026-09-28

Status: **READY — SCOPED FOLLOW-UP FOR BUILD 441 BLOCKED ITEMS ONLY**

## Build identity

- Windows Build: **486**
- code commit: `788171056ccd438f19e9836acce856466e76a49e`
- workflow run: `36498902491`
- CI: **GREEN**
- deterministic smoke: PASS
- live official CNE probe/capture: PASS
- portable publish/upload: PASS
- artifact: `SolarEnergyMonitor-win-x64-dev`
- artifact ID: `11004089059`
- SHA-256: `bc921d563059beb59db93cd5c7887d9dbd9a43ef95a20a862567ba6721140623`

## Why this build exists

Build 441 target QA found:
- item 6 tariff acquisition FAIL;
- item 7 audit NOT TESTABLE;
- item 8 bill-audit export blocked.

Research/live CI proved the root cause:
- Enel's website returns an Imperva/Reese JavaScript/cookie challenge to raw HttpClient clients;
- the browser-visible archive is valid, but unattended raw HTTP is not a reliable acquisition channel;
- CNE's official regulatory source is machine-accessible and is now captured automatically;
- Enel final tariff-table PDFs are accepted through controlled browser/manual import.

The application must never silently show a blank tariff surface when Enel is blocked.

## Internal live evidence before handoff

Build 486 live CNE run:
- HTTP 200 official CNE catalog;
- 14 candidate PDF links reviewed;
- 12 VAD-index documents captured;
- 2 corrections captured;
- 0 failures;
- January correction N°819 preserved;
- August correction N°380 preserved as effective 2026-08;
- original August N°368 preserved separately;
- later September N°440 and October N°506 also preserved.

This validates the automatic source path against the live official website, not only fixtures.

## Data reuse

Reuse the existing Build 441 `Data\` folder.

Preferred:
- copy the existing portable `Data\` folder into the new Build 486 folder before launch.

No guided data-import QA is required again.

## QA 6 — Official tariff evidence update

Open:
**Red / Grid & Utility -> Tarifas oficiales**

1. Select **2026**.
2. Run **Actualizar / descargar evidencia oficial**.
3. Let the operation finish.

Expected:
- visible busy/progress feedback while work occurs;
- tariff table must **not remain blank**;
- CNE rows must appear;
- final status should report CNE evidence captured and corrections;
- the August CNE correction should be visible separately from the original August source;
- automatic Enel acquisition may state that it is unavailable because of web protection;
- that Enel block is an expected source constraint, not this QA's failure, provided the UI explains it and offers the browser/import path.

Return:
- one screenshot of the final tariff table/status;
- copy the final status sentence only if counts/failures differ materially from the expected result.

## QA 7 — Import official Enel tariff tables + bill audit

Because Enel requires browser JavaScript/cookies:

1. In **Tarifas oficiales**, use **Abrir página oficial Enel**.
2. In the normal browser, download the official supply-tariff PDF(s) relevant to one stored bill.
3. Back in the app choose **Importar PDFs oficiales Enel…**.
4. Multi-select the downloaded PDFs in one operation.

For the existing July/August 2026 bill interval, use the official July/August publication set so retroactive precedence can be exercised:
- July 2026 24T;
- July 2026 8T Retroactivo;
- August 2026 24T;
- August 2026 8T Retroactivo.

Expected after import:
- each imported Enel PDF appears as authority **Enel**;
- PDF/hash/page evidence is retained;
- normalization produces tariff candidates or an explicit failure;
- original and retroactive versions remain separate;
- version state makes supersession/retroactivity visible.

Then open **Auditoría de boleta** and choose that bill.

Expected:
- audit starts from the bill;
- official reading pair is used;
- bill lines show conservative verification states;
- source names are human-readable;
- rates may be verified while RED/ETR applicability remains explicitly ambiguous;
- unsupported/common/account-specific charges remain actual-only evidence;
- no invented expected values.

Return:
- screenshot only if the audit preview looks wrong/confusing.

## QA 8B — Bill-audit PDF only

Export the PDF from **Auditoría de boleta**.

Expected:
- visible export activity;
- bill identity and actual lines;
- linked official reading boundaries;
- Solar of Things comparison;
- sensitivity/coverage caveats;
- CNE/Enel source traceability as available;
- tariff verification states;
- no unsupported claim of Enel billing error;
- no fabricated charge for evidence the engine cannot reconstruct.

Return:
- the **bill-audit PDF**.

## Do NOT repeat

Do not repeat:
- Build 441 visual/responsive review;
- startup QA;
- guided Data import;
- arbitrary reading-comparison PDF;
- full Update Data/backfill;
- diagnostic bundle;
- family XLSX/PDF;
- Help/manual review.

This pass exists only to close the tariff -> audit -> audit-PDF chain that Build 441 could not test.

## Target-PC result — Build 486 — 2026-09-28

### QA 6 — Official tariff evidence update: **FAIL AGAIN**
- User reports: “sigue sin pasar nada”.
- Target behavior remains effectively inert from the user's point of view: no useful visible result / tariff population is produced.
- This means the Build 486 handoff does **not** close the Build 441 tariff-source blocker despite live CNE CI success.
- Treat this as a target-PC application integration / UX execution defect, not as a CNE source-availability defect.
- QA 7 and QA 8B remain blocked.
- Do not ask the user to repeat unrelated QA.


## Correction from target-PC video evidence — 2026-09-28

The previously recorded statement that Build 486 itself had failed tariff QA is **not supported**.

User-supplied screen recording `Grabación 2026-09-28 205723.mp4` shows:
- footer identity: **v0.10.0 · Build 441 · e4ae3b47**;
- therefore the executable under test was Build 441, not Build 486;
- tariff year selector shown in the recording: **2025**;
- UI shown is the older Build 441 Enel-only tariff surface:
  - heading `Tarifas oficiales Enel`;
  - action `Descargar / actualizar año`;
  - no CNE evidence controls / no Enel browser+PDF-import fallback introduced later;
- pressing the button completes with the old `0/0` result, consistent with the already-known Build 441 Imperva failure.

QA status correction:
- **Build 441 item 6 remains FAIL** as previously established;
- **Build 486 item 6 is NOT YET TESTED on the target PC**;
- Build 486 items 7/8B remain pending, not failed;
- do not perform further code fixes based on the mistaken premise that Build 486 showed the same target behavior.

Likely operational cause:
- an older extracted folder/executable or shortcut was launched instead of the Build 486 executable.

Next target action:
- launch a clean Build 486 folder and verify the footer says `Build 486 · 78817105` before running tariff QA.


## Target-PC result — Build 486 — QA 6 PASS — 2026-09-28

User-supplied screenshot confirms Build 486 is actually running:
- footer: `v0.10.0 · Build 486 · 78817105`;
- selected year: 2026.

Official tariff evidence update result:
- **CNE 12 VAD documents captured**;
- **2 corrections**;
- **0 failures**;
- Enel automatic acquisition explicitly reports unavailable due to web protection and directs the user to:
  - `Abrir página oficial Enel`;
  - `Importar PDFs oficiales Enel...`.

Visible version/correction handling:
- 2026-08 Resolution 380 appears as **Corrección vigente**;
- 2026-08 Resolution 368 appears as **Rectificada**;
- 2026-09 Resolution 440 appears as unique/current;
- 2026-10 Resolution 506 appears as unique/current.

UI result:
- tariff/evidence table is populated;
- source, effective date, correction state, capture state, version state, pages, hash and official publication title are visible;
- the previous blank/0-0 behavior is resolved for the automatic official-evidence path.

QA status:
- Build 486 item 6: **PASS**.
- Proceed to item 7: browser-assisted official Enel PDF import + bill audit.


## Target-PC result — Build 486 — QA 7 partial evidence — 2026-09-28

User screenshot confirms:
- footer identity: `v0.10.0 · Build 486 · 78817105`;
- **Auditoría de boleta** tab opens;
- bill selector is populated with multiple stored bill intervals;
- selected example: `29-07-2026 00:00:00 -> 27-08-2026 23:59:00 · sin referencia`;
- audit verification grid is visible but contains **no rows** for the selected bill;
- no visible tariff-verification outcome is therefore available yet.

QA interpretation at this point:
- bill-audit surface/navigation: functionally reachable;
- QA 7 is **PARTIAL / BLOCKED** until the empty verification grid is explained;
- do not infer tariff verification PASS or FAIL yet;
- investigate whether the selected bill lacks stored bill lines, lacks imported Enel tariff-table evidence, has an interval/source applicability issue, or the preview refresh path is defective.

Do not ask the user to repeat prior QA while diagnosing this empty-grid condition.

