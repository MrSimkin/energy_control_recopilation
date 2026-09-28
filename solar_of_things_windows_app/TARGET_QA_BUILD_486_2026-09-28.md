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
