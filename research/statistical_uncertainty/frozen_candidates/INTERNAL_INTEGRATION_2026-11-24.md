# Internal R3/R4/R5 integration — 2026-11-24

Owner authorizes an internal experimental integration/build before prospective acceptance. This supersedes the earlier timing restriction on **internal engineering only**, not the prospective statistical gates, public reporting claims, or the prohibition on using Enel for model fitting/selection.

Base: Build 866 code, main affd957135d19fc47090329d79cfd5d3c163b499. Existing owner QA of 866 is not declared passed. Branch is public because repository is public: no owner telemetry, individual validation cases, raw bills, credentials or private ZIPs are committed. No merge/release implied.

R3 remains exactly frozen, last count reconstructed locally 13/15 active dates, 137/100 active cases, groups 54/73/10. Run original canonical harness count before scoring. R4-v3 and R5 two-state protocols and historical receipts are recorded in this directory; source snapshots still require byte-for-byte verification and commit. R4/R5 prospective start 2026-11-25. Recommended next owner package 2026-12-08, not guaranteed sufficient. Preserve independent stop rules and score-attempt locks.

Internal application contract:
- Keep existing report method and existing accepted output intact. Candidate outputs explicitly labeled experimental/prospective validation pending; no external-facing claims.
- Distinguish observed kWh from modeled missing kWh; record gap bounds, elapsed time, eligibility, candidate version, support and P5/P50/P95. Never treat insufficient as zero or silently fall back to another method.
- R3 bounded >15 to <=240 real minutes; R4 same local date and six-hour band; R5 >240 to <=480 within same local date and 12-hour band. R5 both-zero endpoints require structural caution. Candidate comparisons are separate from automatic selection; do not tune on Enel.
- Do not sum marginal quantiles. If any gap ineligible or dependence unvalidated, no calibrated period total. An experimental Monte Carlo aggregate may be shown separately with independence assumption and limitations; no replacement of official report totals.
- Native/sidecar implementation requires parity tests against frozen references, synthetic smoke and Windows CI. Existing PDF cards, tariff arithmetic, provenance and local export paths preserved. No owner database access or schema migration for this work.
- Scope QA on isolated copy of data, not live installation. No artifact handoff until CI success; no assertion of prospective PASS or release until actual validation.

This commit is a documentation checkpoint, not a build or a completed integration.
