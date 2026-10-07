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
