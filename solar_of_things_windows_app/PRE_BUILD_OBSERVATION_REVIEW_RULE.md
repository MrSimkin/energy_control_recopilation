# Pre-build observation review rule

Status: **CANONICAL OPERATIONAL RULE**

Date: 2026-09-28

This rule applies before every new build, fix, refactor, redesign tranche, migration, export change or similar code modification.

## Mandatory gate

Before editing code, the development assistant must review the prior repository observations that materially intersect the intended change.

At minimum, inspect as relevant:

- `CONTINUITY_STATUS.md`, especially the newest target-PC QA feedback;
- the active phase implementation notes;
- `GRID_UTILITY_UX_REDESIGN_PROPOSAL_2026-09-28.md` when Grid/Utility, Enel, readings, bills, tariffs or reconciliation are involved;
- tariff/bill research when financial or Enel behavior is involved;
- `BUILD_HANDOFF_RULE.md` before any user-facing build handoff;
- any unresolved defect or explicit deferment touching the feature being changed.

## Required reasoning

Before implementation, verify:

1. What prior user observation caused or constrains this change?
2. What requirements must not regress?
3. Is the change local, or does it have transversal UX/data/report consequences?
4. Is visible operation feedback required?
5. Is the screen complex enough to require task tabs/separation?
6. Does the change affect report semantics, evidence provenance, uncertainty or audit wording?
7. Is there deferred work that must stay deferred?
8. Can this change be validated internally first, avoiding unnecessary target-PC mini-QA?

## Transversal UX gates

### Visible work feedback

Every materially non-instant operation must visibly indicate that the application is working.

Use:
- determinate progress when a meaningful percentage/count exists;
- otherwise an indeterminate progress indicator;
- clear operation text;
- visible completion/failure state.

Silent work that makes the app appear frozen is a defect.

### Task separation

For materially complex screens/windows:
- prefer tabs or equivalent task-level navigation;
- one dominant user task per tab;
- do not grow one long vertical page indefinitely;
- use responsive stacking/master-detail rather than compressing dense controls.

### User-facing QA cadence

Internal development may use multiple commits/builds/CI runs.

Do not ask the user to perform every intermediate validation.

Request target-PC QA only when:
- a coherent tranche is ready;
- CI/internal checks are green;
- the manual checklist can be bundled into a meaningful pass.

## Evidence of compliance

The commit/build sequence should make it possible to identify which prior observations were addressed.

A code change that contradicts a prior relevant observation must be stopped or explicitly reconciled in repository documentation before continuing.
