using SolarOfThings.Core.Statistics;
using SolarOfThings.Core.SolarOfThings;

namespace SolarOfThings.Core.Utility;

public sealed class UtilityReconciliationService
{
    private readonly UtilityMeterRepository _repository;
    private readonly EnergyRangeStatisticsService _statistics;
    private readonly UtilityBillGapStatisticalCompletionService _billObserved;

    public UtilityReconciliationService(
        UtilityMeterRepository repository,
        EnergyRangeStatisticsService statistics,
        UtilityBillGapStatisticalCompletionService billObserved)
    {
        _repository = repository;
        _statistics = statistics;
        _billObserved = billObserved;
    }

    public IReadOnlyList<UtilityMeterReconciliation> GetMeterReconciliations(
        string deviceId)
    {
        var readings = _repository.GetReadings();
        if (readings.Count < 2)
            return Array.Empty<UtilityMeterReconciliation>();

        var result = new List<UtilityMeterReconciliation>();
        for (var index = 1; index < readings.Count; index++)
            result.Add(ReconcileReadings(deviceId, readings[index - 1], readings[index]));
        return result;
    }

    public UtilityMeterReconciliation ReconcileReadings(
        string deviceId,
        long fromReadingId,
        long toReadingId)
    {
        var from = _repository.GetReading(fromReadingId)
            ?? throw new InvalidOperationException("Start reading was not found.");
        var to = _repository.GetReading(toReadingId)
            ?? throw new InvalidOperationException("End reading was not found.");
        return ReconcileReadings(deviceId, from, to);
    }

    public UtilityMeterReconciliation ReconcileReadings(
        string deviceId,
        UtilityMeterReading previous,
        UtilityMeterReading current)
    {
        var timeBasis = TimeBasis(previous, current);

        if (current.ReadingAtUtc <= previous.ReadingAtUtc)
            return Invalid(previous, current, timeBasis, "INVALID_INTERVAL", "Reading timestamps are not strictly increasing.");

        var meterConsumption = current.ReadingKwh - previous.ReadingKwh;
        var grid = _statistics.GetMetric(
            deviceId,
            "grid_import_power_w",
            previous.ReadingAtUtc,
            current.ReadingAtUtc);

        if (meterConsumption < 0)
            return new UtilityMeterReconciliation(
                previous.ReadingId,
                current.ReadingId,
                previous.ReadingAtUtc,
                current.ReadingAtUtc,
                previous.ReadingKwh,
                current.ReadingKwh,
                null,
                grid.PositiveEnergyKwh,
                null,
                null,
                null,
                grid.CoveragePercent,
                timeBasis,
                "METER_RESET_OR_REPLACEMENT",
                "Cumulative meter value decreased; automatic consumption difference is not valid.");

        var difference = grid.PositiveEnergyKwh - meterConsumption;
        var absolute = Math.Abs(difference);
        var percent = meterConsumption > 0
            ? absolute / meterConsumption * 100.0
            : (double?)null;

        var quality = Quality(grid.CoveragePercent, timeBasis);
        var detail = QualityDetail(grid.CoveragePercent, timeBasis);

        var sensitivity = BuildSensitivity(
            deviceId,
            previous.ReadingAtUtc,
            current.ReadingAtUtc,
            grid,
            previous.TimePrecision == UtilityTimePrecision.DateOnly,
            current.TimePrecision == UtilityTimePrecision.DateOnly);

        return new UtilityMeterReconciliation(
            previous.ReadingId,
            current.ReadingId,
            previous.ReadingAtUtc,
            current.ReadingAtUtc,
            previous.ReadingKwh,
            current.ReadingKwh,
            meterConsumption,
            grid.PositiveEnergyKwh,
            difference,
            absolute,
            percent,
            grid.CoveragePercent,
            timeBasis,
            quality,
            detail)
        {
            Sensitivity = sensitivity
        };
    }

    public IReadOnlyList<UtilityBillReconciliation> GetBillReconciliations(
        string deviceId,
        string timeZoneId) =>
        _repository.GetBills()
            .Select(bill => ReconcileBill(deviceId, bill, timeZoneId))
            .ToArray();

    private UtilityBillReconciliation ReconcileBill(
        string deviceId,
        UtilityBillRecord bill,
        string timeZoneId)
    {
        var fromUtc = bill.PeriodStartUtc;
        var toUtc = bill.PeriodEndUtc;
        var timeBasis = bill.PeriodPrecision == UtilityTimePrecision.DateOnly
            ? "DATE_ONLY_ASSUMED"
            : "EXACT";
        var fromDateOnly =
            bill.PeriodPrecision == UtilityTimePrecision.DateOnly;
        var toDateOnly =
            bill.PeriodPrecision == UtilityTimePrecision.DateOnly;

        if (bill.FromReadingId.HasValue && bill.ToReadingId.HasValue)
        {
            var from = _repository.GetReading(bill.FromReadingId.Value);
            var to = _repository.GetReading(bill.ToReadingId.Value);
            if (from is not null && to is not null)
            {
                fromUtc = from.ReadingAtUtc;
                toUtc = to.ReadingAtUtc;
                timeBasis = TimeBasis(from, to);
                fromDateOnly =
                    from.TimePrecision == UtilityTimePrecision.DateOnly;
                toDateOnly =
                    to.TimePrecision == UtilityTimePrecision.DateOnly;
            }
        }

        double inverterKwh;
        double coveragePercent;
        UtilitySensitivityRange? sensitivity = null;

        if (bill.PeriodPrecision == UtilityTimePrecision.DateOnly)
        {
            var startLocalDate =
                SolarApiTime.GetLocalDate(
                    bill.PeriodStartUtc,
                    timeZoneId);
            var endLocalDateInclusive =
                SolarApiTime.GetLocalDate(
                    bill.PeriodEndUtc,
                    timeZoneId);

            var observed = _billObserved.AnalyzeObservedOnly(
                deviceId,
                startLocalDate,
                endLocalDateInclusive,
                timeZoneId);

            inverterKwh = observed.ObservedKwh;
            coveragePercent = observed.CoveragePercent;
            timeBasis = "DATE_ONLY_INCLUSIVE";
        }
        else
        {
            var grid = _statistics.GetMetric(
                deviceId,
                "grid_import_power_w",
                fromUtc,
                toUtc);

            inverterKwh = grid.PositiveEnergyKwh;
            coveragePercent = grid.CoveragePercent;
            sensitivity = BuildSensitivity(
                deviceId,
                fromUtc,
                toUtc,
                grid,
                fromDateOnly,
                toDateOnly);
        }

        double? signed = null;
        double? absolute = null;
        double? percent = null;

        if (bill.BilledConsumptionKwh.HasValue)
        {
            signed = inverterKwh - bill.BilledConsumptionKwh.Value;
            absolute = Math.Abs(signed.Value);
            if (bill.BilledConsumptionKwh.Value > 0)
                percent = absolute.Value / bill.BilledConsumptionKwh.Value * 100.0;
        }

        return new UtilityBillReconciliation(
            bill.BillId,
            fromUtc,
            toUtc,
            bill.BilledConsumptionKwh,
            inverterKwh,
            signed,
            absolute,
            percent,
            coveragePercent,
            timeBasis,
            Quality(coveragePercent, timeBasis),
            bill.PeriodPrecision == UtilityTimePrecision.DateOnly
                ? "Printed bill dates are treated as inclusive local calendar dates."
                : QualityDetail(coveragePercent, timeBasis))
        {
            Sensitivity = sensitivity
        };
    }

    private static UtilityMeterReconciliation Invalid(
        UtilityMeterReading previous,
        UtilityMeterReading current,
        string timeBasis,
        string quality,
        string detail) =>
        new(
            previous.ReadingId,
            current.ReadingId,
            previous.ReadingAtUtc,
            current.ReadingAtUtc,
            previous.ReadingKwh,
            current.ReadingKwh,
            null,
            0,
            null,
            null,
            null,
            0,
            timeBasis,
            quality,
            detail);

    private static string TimeBasis(
        UtilityMeterReading from,
        UtilityMeterReading to) =>
        from.TimePrecision == UtilityTimePrecision.Exact &&
        to.TimePrecision == UtilityTimePrecision.Exact
            ? "EXACT"
            : "DATE_ONLY_ASSUMED";

    private UtilitySensitivityRange BuildSensitivity(
        string deviceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        PowerMetricStatistics baseline,
        bool fromDateOnly,
        bool toDateOnly)
    {
        var baselineUpper = MissingDataUpper(
            baseline,
            fromUtc,
            toUtc);
        var lower = baseline.PositiveEnergyKwh;
        double? upper = baselineUpper;
        double? boundaryAlternative = null;
        var uncoveredHours = EffectiveUncoveredHours(
            baseline,
            fromUtc,
            toUtc);
        var observedMaximumKw = baseline.MaximumWatts.HasValue
            ? Math.Max(0, baseline.MaximumWatts.Value) / 1000.0
            : (double?)null;

        if (fromDateOnly || toDateOnly)
        {
            var alternateFrom =
                fromDateOnly ? fromUtc.AddMinutes(-1) : fromUtc;
            var alternateTo =
                toDateOnly ? toUtc.AddMinutes(-1) : toUtc;

            if (alternateTo > alternateFrom)
            {
                var alternate = _statistics.GetMetric(
                    deviceId,
                    "grid_import_power_w",
                    alternateFrom,
                    alternateTo);
                boundaryAlternative =
                    alternate.PositiveEnergyKwh;
                lower = Math.Min(
                    lower,
                    alternate.PositiveEnergyKwh);

                var alternateUpper = MissingDataUpper(
                    alternate,
                    alternateFrom,
                    alternateTo);
                upper = CombineUpper(
                    upper,
                    alternateUpper);

                uncoveredHours = Math.Max(
                    uncoveredHours,
                    EffectiveUncoveredHours(
                        alternate,
                        alternateFrom,
                        alternateTo));

                if (alternate.MaximumWatts.HasValue)
                {
                    observedMaximumKw = Math.Max(
                        observedMaximumKw ?? 0,
                        Math.Max(0, alternate.MaximumWatts.Value) / 1000.0);
                }
            }
        }

        var hasGapSensitivity = uncoveredHours > 0.000001;
        var hasBoundarySensitivity = fromDateOnly || toDateOnly;
        var basis = (hasGapSensitivity, hasBoundarySensitivity) switch
        {
            (true, true) =>
                "GAPS_OBSERVED_MAX_PLUS_ENEL_BOUNDARY",
            (true, false) =>
                "GAPS_OBSERVED_MAX",
            (false, true) =>
                "ENEL_ONE_MINUTE_BOUNDARY",
            _ =>
                "OBSERVED_INTERVAL_ONLY"
        };

        return new UtilitySensitivityRange(
            lower,
            upper,
            baseline.PositiveEnergyKwh,
            boundaryAlternative,
            uncoveredHours,
            observedMaximumKw,
            basis,
            IsFormalConfidenceInterval: false);
    }

    private static double EffectiveUncoveredHours(
        PowerMetricStatistics statistics,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc)
    {
        if (statistics.SampleCount == 0)
        {
            return Math.Max(
                0,
                (toUtc - fromUtc).TotalHours);
        }

        return Math.Max(
            0,
            statistics.UncoveredHours);
    }

    private static double? MissingDataUpper(
        PowerMetricStatistics statistics,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc)
    {
        var uncoveredHours = EffectiveUncoveredHours(
            statistics,
            fromUtc,
            toUtc);

        if (uncoveredHours <= 0.000001)
            return statistics.PositiveEnergyKwh;

        if (!statistics.MaximumWatts.HasValue)
            return null;

        var maximumImportKw =
            Math.Max(0, statistics.MaximumWatts.Value) / 1000.0;

        return statistics.PositiveEnergyKwh +
               uncoveredHours * maximumImportKw;
    }

    private static double? CombineUpper(
        double? left,
        double? right)
    {
        if (!left.HasValue || !right.HasValue)
            return null;

        return Math.Max(left.Value, right.Value);
    }

    private static string Quality(
        double coveragePercent,
        string timeBasis)
    {
        if (!string.Equals(timeBasis, "EXACT", StringComparison.Ordinal))
            return coveragePercent >= 80.0 ? "ASSUMED_TIME" : "LOW_COVERAGE";

        return coveragePercent switch
        {
            >= 98.0 => "GOOD",
            >= 80.0 => "PARTIAL",
            _ => "LOW_COVERAGE"
        };
    }

    private static string QualityDetail(
        double coveragePercent,
        string timeBasis)
    {
        if (!string.Equals(timeBasis, "EXACT", StringComparison.Ordinal))
            return "At least one utility reading has a date-only timestamp; the comparison uses the stored boundary assumption.";

        return coveragePercent switch
        {
            >= 98.0 => "Inverter coverage is high for this exact interval.",
            >= 80.0 => "Inverter comparison is usable but incomplete; interpret the difference with caution.",
            _ => "Inverter coverage is too low for a strong meter comparison."
        };
    }
}
