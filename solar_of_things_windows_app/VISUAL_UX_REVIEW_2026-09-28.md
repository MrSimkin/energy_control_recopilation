# Solar Energy Monitor — visual polish / attractiveness review

Date: 2026-09-28

Status: **ACTIVE UX REVIEW**

Scope:
- review of the current WPF/XAML structure and product-experience implementation;
- informed by target-PC feedback;
- this is not a screenshot-based pixel audit, because the repository currently contains no target-PC screenshots.

## Overall assessment

The application currently reads as a **competent engineering/dashboard prototype with a coherent base style, but not yet as a fully polished consumer-grade desktop product**.

Indicative current level:

- visual consistency: **7/10**;
- readability on simple screens: **7/10**;
- readability on dense screens: **4/10**;
- navigation clarity: **5/10**;
- responsiveness/adaptation to width: **4/10**;
- perceived polish/attractiveness: **6/10**;
- state/feedback clarity: **6/10**, with important gaps in long-running operations.

Target for the next product UX tranche:
- maintain the professional dashboard character;
- reach approximately **8/10 perceived polish**;
- improve clarity more than decoration.

The goal is not decorative animation or visual novelty. The goal is a calm, modern, trustworthy Windows application that makes dense energy/audit information easy to understand.

## What already works visually

### 1. Coherent shell

The dark left sidebar + light content surface provides a stable application frame.

Strengths:
- consistent primary navigation;
- clear separation between navigation and content;
- restrained professional palette;
- familiar desktop-dashboard pattern.

### 2. Metric cards

Dashboard cards already provide:
- visual hierarchy;
- large primary values;
- secondary explanatory text;
- accent colors;
- predictable card structure.

This is one of the strongest existing visual patterns and should become the reference for summary surfaces elsewhere.

### 3. Typography baseline

Segoe UI, restrained font sizes and neutral colors are appropriate for Windows.

There is already a reasonable hierarchy between:
- page title;
- section heading;
- primary metric;
- helper text.

### 4. White-card-on-neutral-background language

The current `#F4F6F9` page background and white cards create sufficient contrast without becoming visually harsh.

## Main visual weaknesses

### 1. Dense screens become form dumps

The largest problem is not color. It is **information architecture and density**.

Grid & Utility currently stacks:
- reading entry;
- reading table;
- reconciliation controls;
- reconciliation table;
- bill form;
- bill summary;
- bill lines;
- tariff capture;
- tariff table;

inside one long page.

This produces:
- poor scanability;
- weak sense of “what am I doing now?”;
- too many simultaneous controls;
- difficult navigation;
- visually exhausting pages.

Fix:
- task tabs;
- master/detail;
- progressive disclosure;
- one primary action per task surface.

### 2. Fixed widths fight available space

Many controls and tables use explicit widths.

At smaller usable widths this creates:
- awkward wrapping;
- horizontal pressure;
- uneven whitespace;
- apparent “responsive” behavior that is technically wrapping but visually uncontrolled.

Fix:
- adaptive grid layouts;
- minimum readable widths;
- breakpoint-like WPF layout behavior;
- vertical stacking when space is insufficient.

### 3. Card styling is functional but dated

Current cards:
- 4 px radius;
- thin gray border;
- little depth;
- almost identical visual weight everywhere.

This is safe but visually flat.

Recommended evolution:
- slightly larger corner radius (6–8 px);
- more intentional internal spacing;
- softer neutral border;
- subtle shadow only on high-level cards, not every container;
- clearer header/body division where needed.

Avoid excessive shadows.

### 4. Navigation lacks strong selected-state affordance

Hover is clear, but the current base style does not provide a sufficiently prominent persistent active-page state.

Recommended:
- active navigation background/accent;
- small accent strip/icon state;
- stronger text contrast;
- consistent selected behavior for both sidebar and internal tabs.

### 5. Primary/secondary/destructive actions are visually inconsistent

Several buttons use local one-off styling while many others inherit plain WPF appearance.

Recommended shared styles:
- Primary;
- Secondary;
- Quiet/link;
- Destructive;
- Icon/compact;
- Disabled/loading.

A user should recognize action importance without reading every label.

### 6. Tables dominate too much of the visual hierarchy

Large DataGrids often appear immediately under dense forms.

Recommended:
- concise columns by default;
- detail panel for secondary fields;
- clear empty state;
- row selection highlight;
- contextual actions near the selected record;
- avoid forcing every property into a column.

### 7. Status text is easy to miss

Several operations communicate through small gray text.

For important state use:
- status chip/banner;
- progress strip;
- success/warning/error icon + text;
- persistent completion message when relevant.

### 8. Global busy feedback needs to become a product pattern

Build 350 introduced a footer busy indicator, which is directionally correct.

It should become transversal:
- local progress near the action;
- global footer state for operations that affect the application;
- clear operation name;
- determinate percentage/count when available;
- no silent long task.

### 9. Empty states need design attention

Screens with no data should explain:
- what is missing;
- why the page is empty;
- what action to take next.

A blank DataGrid is not an adequate onboarding state.

### 10. Visual complexity should follow task complexity

Audit screens need density, but that density should be structured.

Good target:
- summary first;
- expandable evidence;
- tabs for subdomains;
- source/provenance available without dominating the primary view.

## Proposed visual system evolution

### Semantic palette

Retain the current professional neutral base.

Use semantic accents consistently:
- informational / solar;
- success / normal;
- warning / partial evidence;
- error / failed;
- neutral / unavailable.

Do not attach meaning to color alone.

### Spacing

Adopt a consistent spacing rhythm:
- 4: micro;
- 8: related controls;
- 12/16: normal group spacing;
- 20/24: section spacing.

Current ad-hoc margins should gradually converge on this rhythm.

### Rounded surfaces

Recommended:
- cards: 8 px;
- buttons/inputs: visually compatible radius;
- status chips: pill or compact rounded rectangle.

### Page pattern

Every complex page should follow:

1. title + short purpose;
2. optional summary/status row;
3. task tabs;
4. one primary content task;
5. contextual status/progress;
6. details/evidence below or beside the primary task.

### Tabs

Use tabs across all materially complex application areas, not just Grid & Utility.

Candidate pages for a tab pass:
- Analysis;
- Battery, if operational/detail/configuration continue growing;
- Grid & Utility;
- Reports;
- Data;
- Diagnostics;
- Settings if connection/preferences/support become more complex.

Simple Help/About pages do not require artificial tabs unless content grows enough to justify them.

## Near-term implementation priorities

Priority 1:
- task tabs for Grid & Utility;
- global operation feedback coverage;
- selected navigation/tab states;
- responsive layout corrections;
- primary/secondary button styles.

Priority 2:
- Reports/Data/Analysis task separation where current surfaces are long/dense;
- master/detail bill/readings layouts;
- improved empty states;
- status banners/chips.

Priority 3:
- final Help/manual content;
- final typography/spacing refinement;
- iconography and visual QA on target PC;
- screenshot-based final polish pass.

## Acceptance principle

A visually improved build should not merely “look prettier”.

It should make it easier for the user to answer:
- where am I?
- what can I do here?
- what is currently happening?
- what succeeded or failed?
- what evidence am I looking at?
- what should I do next?

If the redesign improves decoration but not those answers, it has failed.
