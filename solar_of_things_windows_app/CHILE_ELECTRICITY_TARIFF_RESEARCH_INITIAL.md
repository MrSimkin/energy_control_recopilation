# Initial Research Note — Chilean Residential Electricity Tariffs and Cost/Savings Modeling

Date: 2026-09-24

Status: INITIAL DESIGN RESEARCH — sufficient for product specification; exact user tariff remains configurable

## 1. Why a single CLP/kWh value is not enough

For regulated residential electricity supply in Chile, the bill is not simply:

`consumption_kWh × one universal price`

Official CNE and distributor material shows that regulated end-user tariffs combine multiple regulated components and vary by tariff option, distributor/network, commune, residential-equity tranche, protection/stabilization mechanisms and effective tariff period.

CNE describes the regulated final-user price conceptually as the combination of:
- generation / node price;
- distribution value added (VAD);
- transmission-system charges.

For common low-voltage residential BT1 supply, distributor material also identifies monthly bill components such as:
- fixed monthly charge;
- transmission-system-use charge;
- public-service charge;
- electricity/energy consumption charge;
- Fondo de Estabilización / protection-related charges where applicable.

Therefore the application must never hard-code one national residential electricity price.

## 2. Tariff options matter

CNE defines multiple low-voltage tariff options.

BT1 is the ordinary simple residential low-voltage option for energy metering with connected power/demand within the applicable residential threshold.

Other BT/AT options can include:
- contracted power;
- measured maximum demand;
- peak-period demand;
- time-sensitive demand rules.

The application should therefore store a **tariff-plan identifier** instead of assuming BT1 forever.

For this household, BT1 is a likely/default candidate but must be confirmed from the user's actual electricity bill before financial calculations are labeled authoritative.

## 3. Geography and network matter

Official distributor information states that tariff values can depend on factors including:
- commune;
- distribution network configuration;
- zonal transmission network;
- tariff classification.

Therefore tariff data must be versioned by:
- distributor;
- tariff plan;
- geographic/network applicability when relevant;
- effective-from date;
- effective-to date.

## 4. Residential Equity Mechanism (ETR)

For BT1, current official material uses a residential-equity classification based on the prior calendar year's average monthly energy consumption.

Current published bands include:
- T0: average ≤ 200 kWh/month;
- T1: >200 and ≤210;
- T2: >210 and ≤220;
- T3: >220 and ≤230;
- T4: >230 and ≤240;
- T5: >240.

CNE material for the mechanism describes progressive contribution percentages across these consumption bands.

This means even two residential customers in the same broad area can have different regulated values because their prior-year consumption classification differs.

## 5. Protection/stabilization tranches

Current distributor material also exposes a separate consumption-based protection classification (MPC), using moving-average consumption thresholds such as:
- lower band up to 350 kWh;
- middle band above 350 and up to 500 kWh;
- upper band above 500 kWh.

The exact regulatory mechanism/rates can change with legislation and tariff periods.

Therefore the app must store these classifications and rate schedules as data, not application constants.

## 6. Tariffs change over time — and can be retroactive

Distributor tariff archives publish multiple effective tariff documents during a year.

For example, Enel's 2026 tariff archive contains monthly supply-tariff publications and explicitly labels some July/August 2026 schedules as retroactive, followed by a September 2026 schedule.

CNE also publishes periodic price-setting decisions for regulated supply.

Implication:

A cost calculation covering many months must **split the selected period at every tariff-effective-date boundary**.

The application cannot safely apply today's tariff to historical consumption.

## 7. Proposed cost-model layers

Financial calculations should have three clearly different levels.

### Layer A — Energy avoided / grid energy economics

Purpose:
- estimate what imported grid energy would cost under the applicable variable tariff components;
- calculate avoided grid-energy cost attributable to solar/battery operation.

This is the most analytically useful and should be available even if full bill reconstruction is not configured.

### Layer B — Estimated regulated bill

Uses:
- imported grid kWh over the actual billing interval;
- tariff schedule valid during each subperiod;
- fixed monthly/period charges;
- consumption-linked regulated charges;
- applicable tariff classification.

This can estimate the expected regulated bill but must expose assumptions.

### Layer C — Actual bill reconciliation

Stores actual bill/meter observations and optional bill-line values.

Purpose:
- compare utility cumulative-meter consumption with inverter-derived grid import;
- compare estimated charges with what was actually billed;
- expose differences without assuming every difference is an energy error.

Possible actual-bill differences can include:
- user-specific benefits/subsidies;
- prior balances;
- credits/debits;
- retroactive adjustments;
- payment or collection items;
- rounding;
- tariff reclassification;
- non-energy services;
- other bill-specific adjustments.

## 8. Cost/savings metrics proposed

Optional financial metrics may include:

- Grid energy cost over selected period (CLP)
- Estimated regulated electricity cost (CLP)
- Actual billed amount entered by user (CLP)
- Estimated bill vs actual bill difference (CLP / %)
- Avoided grid-energy cost from solar (CLP)
- Avoided grid-energy cost associated with battery discharge (CLP), only where attribution is supportable
- Total estimated energy savings (CLP)
- Effective average grid-energy cost (CLP/kWh)
- Utility meter vs inverter grid-import difference (kWh / %)

The app must distinguish:
- measured values;
- tariff-derived estimates;
- user-entered actual bill values.

## 9. Data model implications

The SQL schema should include versioned tariff entities rather than a single settings field.

Conceptual tables:

- `utility_provider`
- `utility_service`
- `tariff_plan`
- `tariff_schedule`
- `tariff_component`
- `tariff_classification`
- `utility_meter_reading`
- `utility_bill`
- `utility_bill_line`

A tariff schedule should include:
- effective start/end;
- currency;
- distributor;
- plan;
- geographic/network classification where required;
- ETR/protection classification where required;
- component type;
- unit (CLP/month, CLP/kWh, CLP/kW, percentage, etc.);
- rate;
- official source/reference;
- whether the value is tax-inclusive or tax-exclusive when relevant.

## 10. Configuration philosophy

The user should not be forced to understand Chilean tariff regulation to use the energy-monitoring core.

Financial features should be:
- optional;
- disabled until a tariff is configured/identified;
- explained in plain language;
- transparent about assumptions.

Recommended setup flow:
1. user identifies distributor and tariff option from an electricity bill;
2. app records the relevant service/tariff classification;
3. tariff schedules can be entered/imported/updated;
4. calculations use the schedule effective on each date;
5. actual bills remain independent evidence for reconciliation.

## 11. Important product rule

Do **not** report a value simply as “money saved” unless the calculation basis is clear.

Prefer labels such as:
- **Estimated grid-energy cost avoided**
- **Estimated electricity cost**
- **Actual bill amount**
- **Difference from actual bill**

Tooltips and PDF glossary must explain these terms in basic language.

## 12. Current conclusion

Chilean tariff complexity does not make the optional financial feature infeasible.

It means the correct architecture is:

**versioned tariff engine + user bill/meter reconciliation**

rather than:

**one configurable CLP/kWh field**.

This feature should be implemented after the core telemetry, normalization, historical database and reporting pipeline are stable.


## 13. Automatic acquisition architecture

The financial module should not normally require the user to type tariff rates manually.

### Recommended acquisition strategy

Use provider-specific **official-source adapters**.

For the configured distributor/service, the adapter should:

1. discover the distributor's official tariff archive/publication page;
2. identify publications applicable to the user's tariff option, commune/network and date range;
3. download the official source document;
4. retain source metadata and a content hash;
5. parse the applicable tariff table;
6. normalize the tariff components into local SQL tables;
7. run structural/consistency validation;
8. store the parsed schedule as a new immutable version;
9. mark corrections/retroactive publications as superseding earlier versions without deleting the earlier evidence.

### Why distributor schedules should be preferred for calculation

Official distributors publish final supply tariffs for their service territories.

For example, Enel Distribución publishes a historical archive of 2026 supply-tariff PDFs by month, including retroactive July/August schedules and a September schedule. The tariff PDFs expose residential BT1 rows and rates by commune.

This means the application can often consume the already-composed official end-user tariff instead of independently reconstructing the final rate from every CNE upstream regulatory component.

CNE publications remain essential for:
- tariff-option rules;
- regulatory classifications;
- index values;
- effective dates;
- validation/cross-checking;
- cases where distributor schedules depend on a regulatory rule not self-contained in the distributor table.

### Parsing technology

Do not assume one retrieval format.

An adapter may use:
- official API/structured data where available;
- downloadable spreadsheets/CSV if an official source offers them;
- HTML tables;
- deterministic PDF table extraction;
- manual-import fallback.

Avoid OCR for born-digital tariff PDFs unless no structured/text extraction path exists.

### Parser safety

Because official PDF/table layouts can change, every adapter needs:
- expected headings/columns;
- row-count/range checks;
- numeric format validation;
- unit validation;
- effective-date validation;
- duplicate/supersession detection;
- failure-safe behavior.

A parser failure must stop that tariff version from becoming authoritative.

## 14. Additional bill charges

SEC guidance confirms that electricity bills can include charges for services associated with distribution that are not simply energy supply. Examples include meter rental, connection/disconnection, verification, late-payment-related services and third-party service costs; such items must be identified/desegregated according to the applicable rules.

Therefore full bill reconciliation requires a flexible actual-bill line model in addition to the tariff engine.

The tariff engine should calculate predictable tariff/rule components.

The actual-bill model should preserve any extra charge/credit exactly as it appears on the bill, with a free-text description and optional normalized category.

## 15. Authority and traceability rule

Each estimated bill should be reproducible from:
- local Solar of Things/grid-import data;
- utility meter observations where applicable;
- the exact locally cached tariff schedule versions;
- user service configuration;
- the calculation-engine version.

The report/detail view should expose the official tariff source used.

