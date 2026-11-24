# Native internal engineering checkpoint — 2026-11-24

R3/R4/R5 candidate matrix and isolated PDF appendix implemented in C#; no production method replaced. Use opt-in environment variable SOLAR_ENERGY_MONITOR_EXPERIMENTAL_R345=1 only in isolated QA session. Default export unchanged. The experimental PDF title and footer mark it as internal, and it adds per-gap quantiles, support, applicability and partial aggregate sensitivity under independent/common-rank assumptions. No calibrated period total is asserted. No Enel inputs enter the candidate engine. Existing UI export progress and user-configurable destination remain in use.

Port intentionally labeled pre-parity: C# time precision and RNG differ from Python, R5 natural-gap mapping uses nearest 5-minute count, and R5 C# empirical quantiles use inverse CDF (Python simulation quantiles interpolate). Cross-language fixture parity is pending; do not treat this as a modification to the frozen research protocols or use native outputs for their prospective score. R5 both-zero endpoint predictions are inspection-only and excluded from aggregate. Mixed-model selection/total not authorized. Native report reflects existing bill gap detector (threshold based on actual cadence), not a new gap-detection policy. The report shows a partial missing-energy subtotal if not all detected gaps are estimable.

Reference SHA256 from local original ZIPs:
- R4/r4_frozen.py: 98d76656446efb7aca860b6c98e7f3c59a7b1645d9ee4de2e2ca5f8941d237b9
- R5/r5_frozen.py: 33a4adbfabfad11874d6856587f75c04d09442b339e245d26b0ffe3497f32f0a
- R5/r4_reference.py: identical to R4/r4_frozen.py
- R5/r5_bridge.py: bea74492ef93cd7d2594816b1ed8510d26d9b913464f6934f3f68baee621bec5

No owner data uploaded. CI must compile and run the added synthetic smoke tests; CI not yet verified at this checkpoint. QA on copied Data only. Original Python source consolidation remains pending byte-identical upload; protocols/receipts already committed. No merge/release.
