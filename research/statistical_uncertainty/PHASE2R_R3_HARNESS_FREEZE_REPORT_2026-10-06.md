# Phase 2R — R3 future-validation harness freeze receipt

Date: 2026-10-06

Status: **HARNESS IMPLEMENTED AND FROZEN — NO OWNER FUTURE OUTCOMES SCORED**

Candidate:
`grid-import-boundary-residual-dayweighted-240m.candidate-v4`

Frozen protocol:
`PHASE2R_R3_FUTURE_VALIDATION_PROTOCOL_2026-09-30.md`

## Repository state

Pre-implementation canonical HEAD:
`917a37d198fbf3a8af8d3b62b86e1e61584f9bc6`

Harness implementation commit:
`0d7ebb7bf9ef648eae37bc4efd4a518ca42af759`

Harness file:
`research/statistical_uncertainty/phase2r_r3_future_validation.py`

Harness version:
`phase2r-r3-future-validation.v1`

Git blob SHA:
`8e50f134f35c83774edca47652ff22b8315acd1c`

File SHA-256:
`39012e73ac5638f8b37f765d370ea91b1f0d994c21127c1e9a5d17e696504e57`

## What is frozen

The harness encodes the frozen R3 protocol without using Enel or other utility evidence.

It provides three explicit stages:

1. `self-test`
   - deterministic checks for duration grouping;
   - boundary bridge;
   - weighted empirical quantile;
   - interval score;
   - deterministic seeding.

2. `count`
   - validates the ZIP SHA-256 and every declared `manifest.json` file length/hash;
   - reads only valid finite `grid_import_power_w` evidence for R3;
   - constructs future targets from local date 2026-09-28 onward;
   - checks boundary state and calibration-date eligibility;
   - evaluates the stopping rule without computing target truth, P5/P50/P95, coverage, error, interval score or Enel comparison;
   - identifies the **first** local date on which the frozen stopping rule becomes satisfied.

3. `score`
   - refuses to run unless the stopping rule is satisfied;
   - scores only through the first stopping date;
   - writes a score-attempt lock before outcome scoring;
   - uses direct equal-day weighted empirical residual quantiles for single gaps;
   - uses exactly 100,000 simulations for frozen multiple-gap validation;
   - produces per-case evidence, aggregate strata, day-cluster bootstrap coverage intervals, interval score, P50 bias/error metrics, multiple-gap results and gates;
   - compares raw C2 on the same eligible single-gap cases;
   - leaves gate 10 as `REQUIRES_REVIEW` rather than auto-declaring structural safety.

The equal-day weighted quantile is frozen as the inverse weighted empirical CDF.

Multiple-gap target selection is frozen as the lexicographically earliest non-overlapping combination available from the deterministic per-band single-gap targets.

A numerical coverage tolerance of `1e-9 kWh` is used only to prevent floating-point roundoff from creating false misses at an interval boundary.

## Verification before publication

Local verification against the exact published blob content, apart from non-executable docstring wording, completed successfully:

- Python compile: PASS.
- `--stage self-test`: PASS.
- Synthetic Research Exporter ZIP integrity/count path: PASS.
- Synthetic stopping-rule test: first READY date correctly identified as 2026-10-12 when every future date is eligible ACTIVE.
- Synthetic full score path: PASS mechanically.
- Constant synthetic multi-gap case: 100% coverage after applying the numerical boundary tolerance.

Synthetic tests are software/reproducibility checks only. They are **not** R3 validation evidence and do not strengthen any statistical claim.

## Real future evidence status

No newer owner Research Exporter ZIP was available in this continuation session.

Therefore:

- no owner future package was scored;
- no R3 future P5/P50/P95 were inspected;
- no coverage/error result was inspected;
- no future validation decision exists.

There is also a calendar lower bound independent of any outcome:

- future validation starts 2026-09-28;
- as of 2026-10-06, only 9 local dates can possibly have elapsed in that window;
- the stopping rule requires at least 15 distinct future local dates containing an eligible ACTIVE-start target;
- therefore the stopping rule **cannot possibly be satisfied before 2026-10-12**, even under perfect telemetry and target availability.

This does not guarantee readiness on 2026-10-12; the 100-case and per-duration-group requirements must also be satisfied.

## Next authorized operation

When a newer Research Exporter ZIP exists:

1. run the frozen harness in `count` mode only;
2. inspect only integrity/range/eligibility/stopping-rule outputs;
3. if status is `INSUFFICIENT`, do not score;
4. if and only if status is `READY`, run the frozen `score` stage exactly once for that frozen validation period;
5. review gate 10 structurally before any production decision.

Example count command:

```bash
python research/statistical_uncertainty/phase2r_r3_future_validation.py \
  --stage count \
  --package SolarOfThings-ResearchPackage-<timestamp>.zip \
  --output-dir phase2r_r3_output
```

If historical and future evidence are delivered as separate valid Research Exporter ZIPs, repeat `--package`; later package order wins only for duplicate timestamps after every package is independently integrity-validated.

Do not modify production C#, PDF wording, Simple Energy Report, source-attribution uncertainty, or create a normal Solar of Things build from this harness freeze.
