using ScottPlot;

namespace SolarOfThings.Core.Reporting;

internal sealed class ReportChartRenderer
{
    public IReadOnlyList<string> RenderFamilyCharts(EnergyReportData report, string directory)
    {
        Directory.CreateDirectory(directory);

        var paths = new[]
        {
            Path.Combine(directory, "01-consumption-sources.png"),
            Path.Combine(directory, "02-energy-and-battery.png"),
            Path.Combine(directory, "03-house-and-sources.png")
        };

        RenderConsumptionSources(report, paths[0]);
        RenderEnergyAndBattery(report, paths[1]);
        RenderHouseAndSources(report, paths[2]);
        return paths;
    }

    private static void RenderConsumptionSources(EnergyReportData report, string path)
    {
        var rows = report.Attribution.Buckets.ToArray();
        var plot = new Plot();
        var colors = new[] { Colors.C0, Colors.C1, Colors.C2, Colors.C3 };

        for (var x = 0; x < rows.Length; x++)
        {
            var values = new[]
            {
                rows[x].SolarToHouseKwh,
                rows[x].BatteryToHouseKwh,
                rows[x].GridToHouseKwh,
                rows[x].UnattributedHouseKwh
            };

            var nextBase = 0.0;
            for (var series = 0; series < values.Length; series++)
            {
                if (values[series] <= 0) continue;

                plot.Add.Bar(new Bar
                {
                    Position = x,
                    ValueBase = nextBase,
                    Value = nextBase + values[series],
                    FillColor = colors[series]
                });
                nextBase += values[series];
            }
        }

        var labels = IsSpanish(report)
            ? new[] { "Solar → casa", "Batería → casa", "Enel → casa", "Sin atribuir" }
            : new[] { "Solar → home", "Battery → home", "Utility → home", "Unattributed" };

        for (var index = 0; index < labels.Length; index++)
        {
            plot.Legend.ManualItems.Add(new LegendItem
            {
                LabelText = labels[index],
                FillColor = colors[index]
            });
        }

        ApplyCommonAxes(
            plot,
            rows.Select(row => row.LocalLabel).ToArray(),
            IsSpanish(report) ? "Consumo de la casa por origen" : "Household consumption by source");

        plot.YLabel("kWh");
        plot.ShowLegend(Alignment.UpperRight);
        plot.Axes.Margins(bottom: 0, top: .20);
        plot.SavePng(path, 1100, 330);
    }

    private static void RenderEnergyAndBattery(EnergyReportData report, string path)
    {
        var rows = report.Attribution.Buckets.ToArray();
        var table = report.Table.Rows.ToDictionary(row => row.StartUtc);
        var xs = Enumerable.Range(0, rows.Length).Select(index => (double)index).ToArray();
        var solar = new double[rows.Length];
        var home = new double[rows.Length];
        var stored = new double[rows.Length];

        for (var index = 0; index < rows.Length; index++)
        {
            if (table.TryGetValue(rows[index].StartUtc, out var row))
            {
                solar[index] = row.PvEnergyDisplayKwh ?? double.NaN;
                home[index] = row.HouseEnergyDisplayKwh ?? double.NaN;
            }
            else
            {
                solar[index] = double.NaN;
                home[index] = double.NaN;
            }

            stored[index] = rows[index].BatteryStoredEndingKwh ?? double.NaN;
        }

        var plot = new Plot();
        var solarLine = plot.Add.Scatter(xs, solar);
        solarLine.LegendText = IsSpanish(report) ? "Solar producido" : "Solar produced";
        var homeLine = plot.Add.Scatter(xs, home);
        homeLine.LegendText = IsSpanish(report) ? "Consumo de la casa" : "Home consumption";
        var batteryLine = plot.Add.Scatter(xs, stored);
        batteryLine.LegendText = IsSpanish(report)
            ? "Energía guardada en batería (estimada)"
            : "Energy stored in battery (estimate)";

        ApplyCommonAxes(
            plot,
            rows.Select(row => row.LocalLabel).ToArray(),
            IsSpanish(report)
                ? "Solar, consumo y energía guardada en batería"
                : "Solar, consumption and energy stored in battery");

        plot.YLabel("kWh");
        plot.ShowLegend(Alignment.UpperRight);
        plot.SavePng(path, 1100, 330);
    }

    private static void RenderHouseAndSources(EnergyReportData report, string path)
    {
        var rows = report.Attribution.Buckets.ToArray();
        var xs = Enumerable.Range(0, rows.Length).Select(index => (double)index).ToArray();
        var plot = new Plot();

        var home = plot.Add.Scatter(
            xs,
            rows.Select(row => row.ObservedCoveragePercent > 0 ? row.ObservedHouseKwh : double.NaN).ToArray());
        home.LegendText = IsSpanish(report) ? "Consumo de la casa" : "Home consumption";

        var solar = plot.Add.Scatter(
            xs,
            rows.Select(row => row.AttributionCoverageOfObservedPercent > 0 ? row.SolarToHouseKwh : double.NaN).ToArray());
        solar.LegendText = IsSpanish(report) ? "Solar → casa" : "Solar → home";

        var battery = plot.Add.Scatter(
            xs,
            rows.Select(row => row.AttributionCoverageOfObservedPercent > 0 ? row.BatteryToHouseKwh : double.NaN).ToArray());
        battery.LegendText = IsSpanish(report) ? "Batería → casa" : "Battery → home";

        var grid = plot.Add.Scatter(
            xs,
            rows.Select(row => row.AttributionCoverageOfObservedPercent > 0 ? row.GridToHouseKwh : double.NaN).ToArray());
        grid.LegendText = IsSpanish(report) ? "Enel → casa" : "Utility → home";

        ApplyCommonAxes(
            plot,
            rows.Select(row => row.LocalLabel).ToArray(),
            IsSpanish(report)
                ? "Consumo de la casa y origen de la energía"
                : "Household consumption and energy sources");

        plot.YLabel("kWh");
        plot.ShowLegend(Alignment.UpperRight);
        plot.SavePng(path, 1100, 330);
    }

    private static void ApplyCommonAxes(Plot plot, IReadOnlyList<string> labels, string title)
    {
        plot.Title(title);

        var ticks = new TickGenerators.NumericManual();
        var step = Math.Max(1, (int)Math.Ceiling(labels.Count / 12.0));

        for (var index = 0; index < labels.Count; index += step)
            ticks.AddMajor(index, labels[index]);

        if (labels.Count > 1 && (labels.Count - 1) % step != 0)
            ticks.AddMajor(labels.Count - 1, labels[labels.Count - 1]);

        plot.Axes.Bottom.TickGenerator = ticks;
        plot.Axes.Margins(left: .03, right: .03, bottom: .08, top: .15);
    }

    private static bool IsSpanish(EnergyReportData report) =>
        report.Request.LanguageCode.StartsWith("es", StringComparison.OrdinalIgnoreCase);
}
