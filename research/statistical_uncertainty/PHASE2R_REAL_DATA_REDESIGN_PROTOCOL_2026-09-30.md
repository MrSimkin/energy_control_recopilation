# Phase 2R — Real-data redesign after Phase 3 rejection

Date: 2026-09-30

Status: **ACTIVE — candidate rules frozen before candidate outcome inspection**

## 1. Trigger

Phase 3 rejected C2-120 as a general production interval.

Key real-data failure:
- overall C2 coverage looked near nominal through 240 min;
- when the already-observed gap-start grid state was active (>100 W), coverage was only about 67–78% in the principal durations;
- negative P50 bias increased with gap length;
- 480-minute overall coverage also fell to 83.3%;
- hard active/inactive donor filtering did not rescue calibration because it reduced effective independent donor days too aggressively.

C2 remains a research baseline, not a production candidate.

## 2. Anti-overfitting split

New redesign candidates may use only the development target dates for discovery:

**Development dates**
- 2026-07-19 through 2026-09-11;
- only dates/windows satisfying the frozen Phase 3 truth-quality rules.

**Locked real holdout**
- 2026-09-13 through 2026-09-26 inclusive;
- these 14 good local dates are not inspected for the new candidate until its rules are frozen.

The baseline C2 result has already been viewed across the full package; therefore this is not claimed to be a pristine study-wide holdout. It is an untouched holdout for the *new redesign candidate outputs*.

No Enel/utility value is permitted.

## 3. R1 candidate — boundary-state whole-gap day bootstrap

Research label:
`grid-import-boundary-state-whole-gap-day-bootstrap.candidate-v2`

Purpose:
- preserve dependence across the **entire missing interval**, avoiding independent 120-minute stitching;
- condition on information actually observable at both ends of an internal telemetry gap;
- treat donor **days**, rather than many overlapping five-minute starts, as the primary independent resampling units.

### 3.1 Observable boundary state

Grid-active threshold:
- <=100 W = inactive;
- >100 W = active.

Target transition class is one of:
- inactive -> inactive;
- inactive -> active;
- active -> inactive;
- active -> active.

The 100 W threshold is not tuned here; it is inherited from existing application context semantics.

### 3.2 Whole-gap donor block

For target nominal duration D:
- donor block has the same number of nominal 5-minute intervals as the target;
- every donor link must satisfy the same 4.5–5.5 minute regular-cadence quality gate;
- donor block must lie strictly before target start and inside the rolling 90-day history;
- donor block start/end active-state class must exactly match the target transition class;
- donor energy is calculated with actual donor timestamps/trapezoidal integration;
- donor energy is scaled by target actual duration / donor actual duration.

No block stitching occurs for the tested 20–480 minute durations.

### 3.3 One donor block per local donor date

For each prior local donor date:
- find eligible blocks matching the boundary transition;
- prefer same weekday/weekend type **only if at least 20 distinct donor dates remain**;
- primary clock-position neighborhood: target start time +/-120 minutes;
- if fewer than 20 distinct donor dates, widen to +/-240 minutes;
- if still fewer than 20, allow all clock positions;
- from each eligible donor date retain only the block whose start clock time is closest to target start time;
- ties resolve deterministically by earliest timestamp.

This prevents pseudo-replication from treating many overlapping five-minute starts within one day as independent episodes.

### 3.4 Evidence floor

At least 20 distinct donor dates are required.

If fewer than 20 state-matched donor dates exist even after deterministic widening:
- candidate status = INSUFFICIENT_STATE_MATCHED_DONOR_DAYS;
- do not fabricate a predictive interval.

Evidence insufficiency is a valid result.

### 3.5 Predictive distribution

The retained donor-day block energies are the empirical predictive support.

For initial R1 discovery:
- calculate P5/P50/P95 directly from the empirical donor-day energy distribution;
- no Monte Carlo noise is introduced;
- report donor-date count and clock-window fallback.

This is intentionally simple and auditable.

## 4. Discovery metrics

Use the same Phase 3 target construction on development dates.

Report:
- P5-P95 coverage;
- cluster-by-day 95% bootstrap interval;
- P50 bias/MAE/RMSE;
- interval width;
- alpha=0.10 interval score;
- insufficient-evidence rate;
- coverage by nominal duration;
- coverage by observable start state;
- coverage by boundary transition class;
- distinct donor-day distribution.

Compare against frozen C2 only on the same development targets.

## 5. Development gate

R1 may proceed to the locked real holdout only if:
- it materially improves the active-start failure relative to C2;
- it does not create a new severe inactive-start failure;
- its proper interval score is not materially worse overall;
- insufficiency remains transparent and operationally manageable;
- no duration exhibits an obvious structural collapse.

If R1 fails development:
- do not inspect its locked-holdout outcomes;
- redesign again.

## 6. Holdout gate

If R1 passes development:
- freeze all rules above;
- execute exactly once on 2026-09-13 through 2026-09-26;
- use cluster/day-aware descriptive uncertainty where sample size permits;
- do not tune after seeing holdout.

R1 advances back to Phase 3 only if holdout does not reproduce material conditional undercoverage.

## 7. Methodological basis

The redesign remains in the dependent/local block-bootstrap family:
- contiguous blocks preserve serial dependence;
- local matching addresses time-varying structure;
- whole-gap blocks avoid the known independence break introduced by stitching 120-minute blocks;
- one block per donor day makes effective independent evidence explicit.

Relevant background includes:
- Kreiss & Paparoditis (2011), bootstrap methods for dependent data;
- Paparoditis & Politis (2002), local block bootstrap, DOI 10.1016/S1631-073X(02)02578-5;
- Gneiting & Raftery (2007), proper scoring rules, DOI 10.1198/016214506000001437.

These references motivate principles; the installation-specific decision remains evidence-driven.
