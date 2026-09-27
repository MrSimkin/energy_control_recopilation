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
        {
            return Array.Empty<UtilityMeterReconciliation>();
        }

        var result = new List<UtilityMeterReconciliation>();
        for (var index = 1; index < readings.Count; index++)
        {
            var previous = readings[index - 1];
            var current = readings[index];
            result.Add(ReconcileReadings(deviceId, previous, current));
        }

        return result;
    }

    public IReadOnlyList<UtilityBillReconciliation> GetBillReconciliations(
        string deviceId)
    {
        return _repository.GetBills()
            .Select(bill => ReconcileBill(deviceId, bill))
            .ToArray();
    }

    private UtilityMeterReconciliation ReconcileReadings(
        string deviceId,
        UtilityMeterReading previous,
        UtilityMeterReading current)
    {
        if (current.ReadingAtUtc <= previous.ReadingAtUtc)
        {
            return new UtilityMeterReconciliation(
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
                "INVALID_INTERVAL",
                "Reading timestamps are not strictly increasing.");
        }

        var meterConsumption = current.ReadingKwh - previous.ReadingKwh;
        if (meterConsumption < 0)
        {
            var stats = _statistics.GetMetric(
                deviceId,
                "grid_import_power_w",
                previous.ReadingAtUtc,
                current.ReadingAtUtc);
            return new UtilityMeterReconciliation(
                previous.ReadingId,
                current.ReadingId,
                previous.ReadingAtUtc,
                current.ReadingAtUtc,
                previous.ReadingKwh,
                current.ReadingKwh,
                null,
                stats.PositiveEnergyKwh,
                null,
                null,
                null,
                stats.CoveragePercent,
                "METER_RESET_OR_REPLACEMENT",
                "Cumulative meter value decreased; automatic consumption difference is not valid.");
        }

        var grid = _statistics.GetMetric(
            deviceId,
            "grid_import_power_w",
            previous.ReadingAtUtc,
            current.ReadingAtUtc);

        var difference = grid.PositiveEnergyKwh - meterConsumption;
        var absolute = Math.Abs(difference);
        var percent = meterConsumption > 0
            ? absolute / meterConsumption * 100.0
            : (double?)null;

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
            Quality(grid.CoveragePercent),
            QualityDetail(grid.CoveragePercent));
    }

    private UtilityBillReconciliation ReconcileBill(
        string deviceId,
        UtilityBillRecord bill)
    {
        var grid = _statistics.GetMetric(
            deviceId,
            "grid_import_power_w",
            bill.PeriodStartUtc,
            bill.PeriodEndUtc);

        double? signed = null;
        double? absolute = null;
        double? percent = null;

        if (bill.BilledConsumptionKwh.HasValue)
        {
            signed = grid.PositiveEnergyKwh - bill.BilledConsumptionKwh.Value;
            absolute = Math.Abs(signed.Value);
            if (bill.BilledConsumptionKwh.Value > 0)
            {
                percent =
                    absolute.Value /
                    bill.BilledConsumptionKwh.Value *
                    100.0;
            }
        }

        return new UtilityBillReconciliation(
            bill.BillId,
            bill.PeriodStartUtc,
            bill.PeriodEndUtc,
            bill.BilledConsumptionKwh,
            grid.PositiveEnergyKwh,
            signed,
            absolute,
            percent,
            grid.CoveragePercent,
            Quality(grid.CoveragePercent),
            QualityDetail(grid.CoveragePercent));
    }

    private static string Quality(double coveragePercent) =>
        coveragePercent switch
        {
            >= 98.0 => "GOOD",
            >= 80.0 => "PARTIAL",
            _ => "LOW_COVERAGE"
        };

    private static string QualityDetail(double coveragePercent) =>
        coveragePercent switch
        {
            >= 98.0 =>
                "Inverter coverage is high for this exact interval.",
            >= 80.0 =>
                "Inverter comparison is usable but incomplete; interpret the difference with caution.",
            _ =>
                "Inverter coverage is too low for a strong meter comparison."
        };
}
