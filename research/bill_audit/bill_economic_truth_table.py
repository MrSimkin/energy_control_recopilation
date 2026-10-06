#!/usr/bin/env python3
import argparse, csv, json
from dataclasses import dataclass
from datetime import datetime, timedelta
from pathlib import Path

VERSION = "bill-economic-truth-table.v1"

@dataclass(frozen=True)
class MonthRate:
    month: str
    electricity_iva_clp_per_kwh: float
    transport_iva_clp_per_kwh: float
    public_service_exempt_clp_per_kwh: float

def parse_date(value):
    return datetime.strptime(value, "%Y-%m-%d").date()

def month_key(day):
    return f"{day.year:04d}-{day.month:02d}"

def day_weights(start, end_inclusive):
    counts = {}
    day = start
    while day <= end_inclusive:
        key = month_key(day)
        counts[key] = counts.get(key, 0) + 1
        day += timedelta(days=1)
    total = sum(counts.values())
    return counts, {key: count / total for key, count in counts.items()}, total

def analyze(config):
    start = parse_date(config["period"]["start_local_date"])
    end = parse_date(config["period"]["end_local_date_inclusive"])
    counts, weights, total_days = day_weights(start, end)

    rates = {
        item["month"]: MonthRate(**item)
        for item in config["monthly_rates"]
    }
    missing = [key for key in counts if key not in rates]
    if missing:
        raise ValueError(f"missing monthly rates: {missing}")

    # Regulatory billing rule: when the billing interval contains fractions of
    # two calendar months, allocate energy in proportion to calendar days.
    # This deliberately remains day-based across the Chile DST transition.
    blended_electricity = sum(
        weights[key] * rates[key].electricity_iva_clp_per_kwh
        for key in counts
    )
    blended_transport = sum(
        weights[key] * rates[key].transport_iva_clp_per_kwh
        for key in counts
    )
    blended_public = sum(
        weights[key] * rates[key].public_service_exempt_clp_per_kwh
        for key in counts
    )

    fixed = config["fixed_and_actual_components"]
    admin = float(fixed["administration_official_iva_clp"])
    meter_rent = float(fixed["meter_rent_actual_clp"])
    common_service = float(fixed["common_service_actual_clp"])
    subsidy = float(fixed["subsidy_actual_clp"])
    fet_limit = float(config["fet_no_surcharge_up_to_kwh"])

    scenarios = []
    for key, value in config["energy_scenarios_kwh"].items():
        kwh = float(value)
        if kwh > fet_limit:
            raise ValueError(
                "scenario crosses FET threshold; explicit FET schedule required"
            )

        electricity = kwh * blended_electricity
        transport = kwh * blended_transport
        public_service = kwh * blended_public
        fet = 0.0

        variable = electricity + transport + public_service + fet
        bill_subtotal = variable + admin + meter_rent

        # Common service is independent of the apartment's disputed individual
        # meter kWh. Ley 21.667 subsidy is a fixed installment; all current
        # scenarios remain above its amount, so the installment is preserved.
        total_due = bill_subtotal + common_service + subsidy

        scenarios.append({
            "scenario": key,
            "energy_kwh": kwh,
            "electricity_consumed_clp": electricity,
            "transport_component_clp": transport,
            "public_service_component_clp": public_service,
            "fet_clp": fet,
            "variable_supported_subtotal_clp": variable,
            "administration_clp": admin,
            "meter_rent_clp": meter_rent,
            "regulated_bill_subtotal_clp": bill_subtotal,
            "common_service_clp": common_service,
            "subsidy_clp": subsidy,
            "total_due_counterfactual_clp": total_due,
        })

    billed_kwh = float(config["energy_scenarios_kwh"]["ENEL_BILLED"])
    printed = config["printed_bill"]
    for row in scenarios:
        row["difference_vs_printed_total_clp"] = (
            row["total_due_counterfactual_clp"] -
            float(printed["total_due_clp"])
        )
        row["difference_vs_billed_energy_kwh"] = (
            row["energy_kwh"] - billed_kwh
        )
        row["difference_vs_billed_energy_pct"] = (
            row["difference_vs_billed_energy_kwh"] /
            billed_kwh * 100.0
        )

    billed = next(
        row for row in scenarios
        if row["scenario"] == "ENEL_BILLED"
    )

    return {
        "version": VERSION,
        "period": {
            "start_local_date": start.isoformat(),
            "end_local_date_inclusive": end.isoformat(),
            "calendar_days": total_days,
            "month_day_counts": counts,
            "month_weights": weights,
            "allocation_rule": "CALENDAR_DAY_PROPORTION_BY_MONTH",
        },
        "applicability": config["applicability"],
        "blended_published_rates": {
            "electricity_iva_clp_per_kwh": blended_electricity,
            "transport_iva_clp_per_kwh": blended_transport,
            "public_service_exempt_clp_per_kwh": blended_public,
            "supported_variable_total_clp_per_kwh":
                blended_electricity + blended_transport + blended_public,
        },
        "printed_bill": printed,
        "scenarios": scenarios,
        "billed_reconstruction": {
            "reconstructed_total_bill_clp":
                billed["regulated_bill_subtotal_clp"],
            "printed_total_bill_clp":
                float(printed["total_bill_clp"]),
            "difference_clp":
                billed["regulated_bill_subtotal_clp"] -
                float(printed["total_bill_clp"]),
            "reconstructed_total_due_clp":
                billed["total_due_counterfactual_clp"],
            "printed_total_due_clp":
                float(printed["total_due_clp"]),
            "total_due_difference_clp":
                billed["total_due_counterfactual_clp"] -
                float(printed["total_due_clp"]),
        },
        "source_semantics": config["source_semantics"],
    }

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--config", required=True, type=Path)
    parser.add_argument("--output-dir", type=Path)
    args = parser.parse_args()

    config = json.loads(args.config.read_text(encoding="utf-8"))
    result = analyze(config)

    if args.output_dir:
        args.output_dir.mkdir(parents=True, exist_ok=True)
        (args.output_dir / "BILL_ECONOMIC_TRUTH_TABLE.json").write_text(
            json.dumps(result, indent=2, ensure_ascii=False),
            encoding="utf-8",
        )
        rows = result["scenarios"]
        with (args.output_dir / "BILL_ECONOMIC_SCENARIOS.csv").open(
            "w", newline="", encoding="utf-8"
        ) as stream:
            writer = csv.DictWriter(stream, fieldnames=list(rows[0].keys()))
            writer.writeheader()
            writer.writerows(rows)

    print(json.dumps(result, indent=2, ensure_ascii=False))

if __name__ == "__main__":
    main()
