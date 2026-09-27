using SolarOfThings.Core.Statistics;

namespace SolarOfThings.Core.Utility;

public sealed class UtilityReconciliationService
{
    private readonly UtilityMeterRepository _repository;
    private readonly EnergyRangeStatisticsService _statistics;

    public UtilityReconciliationService(
        UtilityMeterRepository repository,
        EnergyRangeStatisticsService statistics)
    {
        _repository = repository;
        _statistics = statistics;
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
            detail);
    }

    public IReadOnlyList<UtilityBillReconciliation> GetBillReconciliations(
        string deviceId) =>
        _repository.GetBills()
            .Select(bill => ReconcileBill(deviceId, bill))
            .ToArray();

    private UtilityBillReconciliation ReconcileBill(
        string deviceId,
        UtilityBillRecord bill)
    {
        var fromUtc = bill.PeriodStartUtc;
        var toUtc = bill.PeriodEndUtc;
        var timeBasis = bill.PeriodPrecision == UtilityTimePrecision.DateOnly
            ? "DATE_ONLY_ASSUMED"
            : "EXACT";

        if (bill.FromReadingId.HasValue && bill.ToReadingId.HasValue)
        {
            var from = _repository.GetReading(bill.FromReadingId.Value);
            var to = _repository.GetReading(bill.ToReadingId.Value);
            if (from is not null && to is not null)
            {
                fromUtc = from.ReadingAtUtc;
                toUtc = to.ReadingAtUtc;
                timeBasis = TimeBasis(from, to);
            }
        }

        var grid = _statistics.GetMetric(
            deviceId,
            "grid_import_power_w",
            fromUtc,
            toUtc);

        double? signed = null;
        double? absolute = null;
        double? percent = null;

        if (bill.BilledConsumptionKwh.HasValue)
        {
            signed = grid.PositiveEnergyKwh - bill.BilledConsumptionKwh.Value;
            absolute = Math.Abs(signed.Value);
            if (bill.BilledConsumptionKwh.Value > 0)
                percent = absolute.Value / bill.BilledConsumptionKwh.Value * 100.0;
        }

        return new UtilityBillReconciliation(
            bill.BillId,
            fromUtc,
            toUtc,
            bill.BilledConsumptionKwh,
            grid.PositiveEnergyKwh,
            signed,
            absolute,
            percent,
            grid.CoveragePercent,
            timeBasis,
            Quality(grid.CoveragePercent, timeBasis),
            QualityDetail(grid.CoveragePercent, timeBasis));
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
