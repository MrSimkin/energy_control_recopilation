# Tariff precedence and acquisition tranche — 2026-10-06

Status: **IMPLEMENTED / OWNER TARGET QA PENDING**

Accepted Build 538 Enel evidence package remains frozen and unchanged.
This tranche is separate from the already accepted current-bill evidence package.

## 1. Objective

Improve two tariff-system weaknesses:

1. choose the authoritative tariff publication when multiple versions/duplicates exist;
2. reduce manual Enel tariff acquisition while preserving provenance and avoiding unsupported anti-bot bypass behavior.

## 2. Version-precedence policy

Never choose authority by:
- SQLite row id;
- local capture/update timestamp;
- parser-local CandidateIndex;
- filename recency alone.

Precedence now is:

1. explicit official correction/supersession relation;
2. byte-identical duplicate evidence (same preserved SHA-256);
3. unique later official publication date only when the document is explicitly corrective/retroactive;
4. unique explicitly retroactive publication;
5. unique single publication;
6. otherwise remain ambiguous.

Semantic duplicate tariff-rate rows collapse only when identity + rate content agree.
Conflicting semantic duplicates remain unresolved rather than selecting one silently.

## 3. Schema 14

Migration 14 adds to `tariff_publication`:
- `official_document_number`;
- `official_publication_date`;
- `corrects_official_document_number`;
- `regulatory_metadata_source`.

New table:
- `tariff_publication_relation`.

It preserves:
- source publication;
- relation type;
- target provider/category;
- target official document identifier;
- resolved target publication id when available;
- evidence source URL/text.

In-place migration from schema v13 to v14 is covered by smoke test and passed in Build 558.

## 4. CNE regulatory graph

`CneTariffEvidenceCaptureService` now extracts stable official identities such as:
- `REX-368-2026`;
- `REX-380-2026`.

It captures official issue dates and explicit `CORRECTS` relations.

Live CNE validation proved:
- source HTTP available;
- 12 VAD documents captured for 2026;
- 2 correction documents;
- 0 source/capture failures in the validated run;
- Resolution 368: official date 2026-07-17;
- Resolution 380: official date 2026-07-24;
- relation:
  `REX-380-2026 CORRECTS REX-368-2026`;
- resolver selects 380 as current for the August effective period and supersedes 368.

The same mechanism also captures the January 819 -> 816 correction relationship.

Live CNE source validation is therefore **PASS**.

## 5. Production resolver

`TariffPublicationVersionResolver` now accepts publication relations and exposes authoritative statuses including:
- `VERSION_PREFERRED_OFFICIAL_CORRECTION`;
- `VERSION_SUPERSEDED_BY_OFFICIAL_CORRECTION`;
- `VERSION_PREFERRED_OFFICIAL_DATE`;
- `VERSION_SUPERSEDED_BY_LATER_OFFICIAL_DATE`;
- `VERSION_PREFERRED_EQUIVALENT_FILE`;
- `VERSION_EQUIVALENT_DUPLICATE_FILE`.

Both production paths use this resolver:
- `TariffBillRateVerificationService`;
- `UtilityBillTariffScenarioAnalysisService`.

The old unique-retroactive logic remains only as a lower-priority fallback when stronger official evidence is unavailable.

## 6. Tariff UI

The Tariffs grid now resolves using the same relation graph as production instead of a legacy resolver call.

New user-facing labels include:
- Corrección oficial vigente;
- Rectificada por corrección oficial;
- Versión oficial posterior;
- Reemplazada por versión oficial posterior;
- Fuente vigente · duplicados idénticos;
- Duplicado idéntico.

Thus UI and bill-audit selection no longer disagree about which version is current.

## 7. Enel acquisition architecture

Existing capabilities retained:
- official Enel catalog parsing;
- official `content/dam` PDF download;
- PDF SHA-256/page/source preservation;
- normalization;
- manual official-PDF import.

New resilience layers:
1. try live official Enel catalog;
2. persist/reuse only a **valid** catalog cache that actually contains official tariff links;
3. never overwrite a good cache with an Imperva/challenge page returning HTTP 200;
4. migrate the old `catalog-{year}-last.html` cache only if it parses as a valid official catalog;
5. derive official filename families from already-known/manual-imported Enel PDFs;
6. opportunistically probe missing/recent `content/dam` assets;
7. require PDF magic bytes before accepting a direct candidate;
8. on full capture, validate the PDF-declared effective month against the discovered month;
9. browser/manual official PDF import remains the final fallback.

Manual browser imports retain the canonical official filename, so they seed future filename-family probing.

## 8. Live Enel limitation

Live validation from GitHub Actions showed:
- catalog request can return HTTP 200 with an Imperva/Reese challenge and 0 PDF links;
- direct `content/dam` PDF request can also return the same HTML challenge rather than PDF bytes.

Therefore:
- direct static-asset probing is useful/opportunistic;
- it is **not** a guaranteed bypass and must not be described as one;
- no unsupported anti-bot circumvention is implemented;
- target/residential networks may behave differently and require owner QA;
- when all automatic Enel routes are denied, browser download + official PDF import remains the supported fallback.

CNE remains automatically accessible and independent of this Enel limitation.

Official Enel public archive nevertheless currently lists both original and retroactive 2026 tariff PDFs side by side, so preservation/version resolution remains necessary even when acquisition is manual.

## 9. CI / validation checkpoints

Key green checkpoints:
- Build 546: schema14 + synthetic official correction graph PASS;
- Build 558: in-place schema v13 -> v14 migration PASS;
- Build 561: CNE live source/correction graph PASS;
- Build 562: workflow allows CNE live probe to run independently from Enel live failure;
- Build 566: current target-QA code Build + SQLite smoke + portable PASS.

Expected/diagnostic red checkpoints:
- Build 548: legacy live Enel catalog-only test failed on Imperva; test design corrected;
- Build 555/563: Enel live direct request also blocked by Imperva; this is classified as external source protection, not a compile/schema failure.
- CNE still ran independently in Build 563 and passed.

## 10. Build 566 owner QA handoff

Workflow run:
- `37555998081`.

Code HEAD:
- `6f8128db814edfa0832eeb3cac991fdc6f041878`.

Artifact:
- `SolarEnergyMonitor-win-x64-dev`;
- artifact ID: `11455095513`;
- artifact digest:
  `sha256:885789f95e02759436b8d18e85d2b4ad4655b173dea095e94023d49bf3c3fe23`.

CI:
- Build PASS;
- SQLite smoke PASS;
- portable publish/upload PASS.

## 11. Owner QA requested

Use a COPY of the existing accepted Build-538-era `Data\` folder.

On Build 566:
1. start the app; successful startup proves target DB migration v13 -> v14;
2. go to `Red eléctrica -> Tarifas oficiales`;
3. select year 2026;
4. click `Descargar / actualizar año`;
5. do NOT manually import Enel PDFs yet, even if automatic Enel reports protection failure;
6. return:
   - one screenshot showing the full tariff status message;
   - one screenshot of the tariff grid around the August 2026 CNE rows, ideally showing 368/380 version labels;
   - mention whether the app remained responsive.

Expected:
- CNE should update automatically;
- 380 should display as current official correction and 368 as superseded/corrected;
- Enel may succeed via local/cache/direct route or may report Imperva protection; either outcome is evidence for target-network classification.

## 12. Remaining after owner QA

If target QA passes:
- freeze this tariff precedence/acquisition tranche;
- no need to regenerate the already accepted Build 538 current-bill report solely because schema/versioning infrastructure improved.

Potential later enhancement, only if justified:
- parse/persist explicit Enel regulatory-basis references (decree / CNE basis) to resolve future cases with multiple distinct Enel retroactive publications.


## 13. Build 575 supersedes Build 566

Build 575 is now the preferred owner QA handoff because it contains the
browser-assisted Enel fallback in addition to the schema/preference work
already present in Build 566.

Workflow run:
- `37556413195`.

Code HEAD:
- `1bda3f73a647c9cc6b5d02f73cd5d968f19570fb`.

Artifact:
- `SolarEnergyMonitor-win-x64-dev`;
- artifact ID `11454832323`;
- digest:
  `sha256:fca91116ea603e786dee7aead5a1fab3c6107d66c5a273dcf85931cdcda26aa4`.

CI:
- Build PASS;
- SQLite smoke PASS;
- portable publish/upload PASS.

Browser-assisted Enel fallback:
- uses Microsoft Edge WebView2;
- opens the official Enel tariff archive in a normal browser session;
- does not attempt to bypass or automate around Imperva;
- intercepts only downloads whose filenames look like official
  `Tarifas Suministro Eléctrico` PDFs;
- saves the browser download to a temporary file;
- routes it through the existing controlled Enel PDF import pipeline;
- the canonical imported copy remains under `Data/Tariffs`;
- import preserves/normalizes the PDF and refreshes the tariff grid;
- manual PDF import remains available if WebView2 cannot start or the
  browser-assisted path cannot complete.

The browser-assisted action is mutually exclusive with year refresh/manual
import while those operations are active.

### Revised owner QA

Use a COPY of the accepted Build-538-era `Data\`.

1. Launch Build 575.
   - Successful startup is the target v13 -> v14 migration gate.
2. Open `Red eléctrica -> Tarifas oficiales`.
3. Select 2026.
4. Click `Descargar / actualizar año`.
5. Return a screenshot of:
   - the complete status text;
   - the August CNE 368/380 rows/version labels if visible.
6. Confirm whether the app remained responsive.
7. If automatic Enel succeeds, stop there.
8. If automatic Enel reports web protection:
   - click `Capturar Enel en navegador…`;
   - allow the official Enel page to load normally;
   - download ONE already-known official tariff PDF (September 2026 is sufficient);
   - the app should automatically import/normalize it;
   - return a screenshot of the browser-assisted status/result.
9. Do not use manual `Importar PDFs oficiales Enel…` during this QA unless
   explicitly requested after reviewing the browser-assisted result.

This QA does not require regenerating the accepted Build 538 bill-audit PDF
or technical annex.


## 17. Build 575 target QA result and Build 577 browser-window fix

Owner returned target-PC screenshots from Build 575.

### Build 575 target results

PASS:
- app started successfully with a copied Build-538-era `Data\` folder;
- target v13 -> v14 migration therefore passed the real-PC startup gate;
- `Descargar / actualizar año` remained responsive;
- CNE 2026 capture status showed:
  - 12 VAD documents captured;
  - 2 corrections;
  - 0 failures;
- tariff grid showed:
  - REX 380 / August 2026 as `Corrección oficial vigente`;
  - REX 368 / August 2026 as `Rectificada por corrección`;
- Enel automated HTTP was correctly classified as blocked by web protection;
- browser-assisted WebView2 could navigate through the official Enel archive to the official September 2026 tariff PDF.

Observed defect:
- clicking Enel's ordinary `DESCARGAR` action opened the official PDF in an **external Edge window**, not inside the integrated WebView2;
- owner then used the external Edge PDF viewer download/save action;
- the file downloaded successfully, but the Solar app did not react/import because the download belonged to external Edge;
- therefore the Build-575 `CoreWebView2.DownloadStarting` interception was never reached.

This is a browser-window ownership defect, not an Enel protection failure and not a tariff parser/importer failure.

Decision:
- do NOT bypass Imperva/Reese;
- do NOT add anti-bot circumvention;
- keep official Enel navigation within the normal WebView2 session whenever Enel opens a tariff link with `target=_blank` / a new-window request.

### Fix

`EnelTariffBrowserWindow` now:
- subscribes to `CoreWebView2.NewWindowRequested`;
- for official `enel.cl` HTTP/HTTPS URLs:
  - marks the new-window request handled;
  - navigates the same integrated WebView2 to that official URL;
- detects when the resulting URL is an official `Tarifas Suministro Eléctrico` PDF;
- keeps a clear instruction visible:
  `Usa el icono Descargar del visor PDF; la app interceptará esa descarga y la importará automáticamente.`;
- adds an `Atrás` navigation action for returning from the PDF to the archive.

Security/operational boundary:
- only official Enel navigation is retained internally;
- unrelated external navigation is not forcibly captured;
- no web-protection bypass is attempted.

Code commits:
- `5f754bd2d00551085db2524e93824a5ba4bbbef9`
  — keep Enel tariff PDF new windows inside integrated browser;
- `0d66e6147c641ca42578cf0ba17ae0df626ee4e3`
  — keep PDF import guidance visible after integrated navigation.

### Build 577

Workflow run:
- `37558525946`.

Code HEAD:
- `0d66e6147c641ca42578cf0ba17ae0df626ee4e3`.

Artifact:
- `SolarEnergyMonitor-win-x64-dev`;
- artifact ID:
  `11455624411`;
- digest:
  `sha256:07f08bc4416187fae4d36d1ad5c90309161ae523cc1283e2a1f006045cd43d99`.

CI:
- Build PASS;
- SQLite smoke PASS;
- portable publish/upload PASS;
- live-source probes intentionally skipped because this commit did not request live-source revalidation; prior CNE live PASS / Enel HTTP-protected classification remain authoritative.

Build 577 **supersedes Build 575** for the remaining browser-assisted Enel QA.

## 18. Owner QA requested for Build 577

Use the same copied `Data\` QA folder; no need to repeat the CNE/migration checks already passed on Build 575 unless the app fails to start.

Steps:

1. Launch Build 577 with the copied QA `Data\`.
2. Open:
   `Red eléctrica -> Tarifas oficiales -> Capturar Enel en navegador…`.
3. Navigate to the same September 2026 tariff publication and press Enel's ordinary `DESCARGAR` button.
4. Gate A:
   - expected: the PDF remains **inside the integrated Solar app browser window**;
   - unexpected: a separate external Edge window opens.
5. If Gate A passes, use the PDF viewer's download icon once.
6. Gate B:
   - expected status inside the integrated window:
     downloaded/imported official PDF and normalized candidates;
   - after import, tariff grid refreshes;
   - app remains responsive.
7. Return:
   - screenshot showing the PDF inside the integrated browser before the viewer-download click;
   - screenshot of the integrated-window status after import;
   - screenshot of tariff grid after import if it changes;
   - exact text of any error.

Because September 2026 already exists in the QA database, a successful re-import may be idempotent and the visible grid row/hash may remain unchanged. The browser-window import status is therefore the primary Gate-B evidence.

Do not regenerate the accepted Build 538 bill audit/annex.
Do not use anti-bot circumvention.


## 19. Build 578 — deterministic evidence for remaining Gate B

Source inspection after Build 577 found that the browser-assisted temporary path used
`GUID-<official filename>`. Since `EnelTariffPdfImportService` derives the canonical
title from `Path.GetFileName(sourcePath)`, that construction could contaminate provenance
with a temporary GUID prefix if the import actually ran.

Build 578 corrects the handoff:
- temporary uniqueness is now a GUID directory;
- the PDF filename passed to the importer remains the exact downloaded official filename;
- temporary cleanup remains best-effort after the canonical `Data/Tariffs` copy exists.

The import result now includes a per-file receipt with:
- official filename;
- publication id;
- SHA-256;
- page count;
- normalized-candidate count;
- idempotency/update outcome;
- UTC completion timestamp.

`EnelTariffBrowserWindow` keeps a dedicated visible receipt independent from navigation
status. It records any `CoreWebView2.DownloadStarting` event and, on success, explicitly
shows the path:
`CoreWebView2.DownloadStarting → EnelTariffPdfImportService: COMPLETADO`.

Build 578:
- code commit: `53a7fa184c7d90e8242ec32b762060f2a9f1c108`;
- workflow run: `37561379685`;
- Build PASS;
- SQLite smoke PASS;
- portable PASS;
- artifact: `SolarEnergyMonitor-win-x64-dev`;
- artifact ID: `11457196560`;
- digest:
  `sha256:db76026303a07256a034e5ed5aa9dce415ba9ab3e7ecd4f6c8354bd602fce676`.

Only remaining owner action:
- use the same QA Data copy;
- Build 578;
- open the same September 2026 PDF in the integrated browser;
- click the viewer download icon once;
- return one screenshot of the persistent receipt.

Do not repeat any already-passed migration/CNE/precedence/Imperva/navigation gate.
Do not infer Gate B from an unchanged grid row.


## 20. Final owner acceptance — Build 578 Gate B PASS

Owner QA on Build 578 conclusively closed the remaining browser-assisted import gate.

The integrated receipt displayed:

`CoreWebView2.DownloadStarting → EnelTariffPdfImportService: COMPLETADO`

Validated receipt:
- official filename preserved:
  `Enel Distribución Chile SA._Tarifas Suministro Eléctrico 8T_ VAD 5T Septiembre de 2026.pdf`;
- outcome:
  `existente · reimportación byte-idéntica`;
- SHA-256:
  `27b65928d47c34d46da4afa25c094734432be860750dac31e1deddb6a4d3ab17`;
- publication ID: `29`;
- page count: `18`;
- normalized candidates: `7786`;
- completed UTC: `2026-10-07 02:42:31`.

The transient imported filename carried the browser duplicate suffix `(1)`, but the
canonical receipt and importer identity correctly removed that suffix and preserved the
official publication filename.

Final tranche gates:
1. schema v13 -> v14 migration — PASS;
2. CNE corrections — PASS;
3. tariff precedence — PASS;
4. browser-assisted navigation to official Enel PDF — PASS;
5. integrated viewer download — PASS;
6. PDF routed through `EnelTariffPdfImportService` — PASS;
7. SHA/provenance preserved — PASS;
8. candidates normalized — PASS;
9. application remained responsive — PASS.

**Tariff precedence + acquisition tranche is CLOSED / FROZEN.**

Accepted Build-538 bill evidence remains frozen; no regeneration is required.
