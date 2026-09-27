using System.Globalization;
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
            Path.Combine(directory, "02-solar-vs-home.png"),
            Path.Combine(directory, "03-stored-battery.png")
        };

        RenderConsumptionSources(report, paths[0]);
        RenderSolarVsHome(report, paths[1]);
        RenderStoredBattery(report, paths[2]);
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
                if (values[series] <= 0)
                {
                    continue;
                }

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
            IsSpanish(report)
                ? "De dónde vino el consumo de la casa"
                : "Where household consumption came from");

        plot.YLabel("kWh");
        plot.ShowLegend(Alignment.UpperRight);
        plot.Axes.Margins(left: .03, right: .03, bottom: .08, top: .38);
        plot.SavePng(path, 1200, 520);
    }

    private static void RenderSolarVsHome(EnergyReportData report, string path)
    {
        var rows = report.Attribution.Buckets.ToArray();
        var table = report.Table.Rows.ToDictionary(row => row.StartUtc);
        var xs = Enumerable.Range(0, rows.Length)
            .Select(index => (double)index)
            .ToArray();
        var solar = new double[rows.Length];
        var home = new double[rows.Length];

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
        }

        var plot = new Plot();
        var solarLine = plot.Add.Scatter(xs, solar);
        solarLine.LegendText =
            IsSpanish(report) ? "Solar producido" : "Solar produced";
        var homeLine = plot.Add.Scatter(xs, home);
        homeLine.LegendText =
            IsSpanish(report) ? "Consumo de la casa" : "Home consumption";

        ApplyCommonAxes(
            plot,
            rows.Select(row => row.LocalLabel).ToArray(),
            IsSpanish(report)
                ? "Producción solar y consumo de la casa"
                : "Solar production and household consumption");

        plot.YLabel("kWh");
        plot.ShowLegend(Alignment.UpperRight);
        plot.SavePng(path, 1200, 520);
    }

    private static void RenderStoredBattery(EnergyReportData report, string path)
    {
        var rows = report.Attribution.Buckets.ToArray();
        var xs = Enumerable.Range(0, rows.Length)
            .Select(index => (double)index)
            .ToArray();
        var stored = rows
            .Select(row => row.BatteryStoredEndingKwh ?? double.NaN)
            .ToArray();

        var plot = new Plot();
        plot.Add.Scatter(xs, stored);

        ApplyCommonAxes(
            plot,
            rows.Select(row => row.LocalLabel).ToArray(),
            IsSpanish(report)
                ? "Energía estimada guardada en batería al cierre"
                : "Estimated battery energy stored at period end");

        plot.YLabel("kWh");
        plot.SavePng(path, 1200, 520);
    }

    private static void ApplyCommonAxes(
        Plot plot,
        IReadOnlyList<string> labels,
        string title)
    {
        plot.Title(title);

        var ticks = new ScottPlot.TickGenerators.NumericManual();
        var step = Math.Max(
            1,
            (int)Math.Ceiling(labels.Count / 8.0));

        for (var index = 0; index < labels.Count; index += step)
        {
            ticks.AddMajor(index, FormatAxisLabel(labels[index]));
        }

        if (labels.Count > 1 &&
            (labels.Count - 1) % step != 0)
        {
            ticks.AddMajor(
                labels.Count - 1,
                FormatAxisLabel(labels[^1]));
        }

        plot.Axes.Bottom.TickGenerator = ticks;
        plot.Axes.Margins(
            left: .03,
            right: .03,
            bottom: .08,
            top: .28);
    }

    private static string FormatAxisLabel(string label)
    {
        if (DateOnly.TryParseExact(
                label,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return date.ToString("dd-MM", CultureInfo.InvariantCulture);
        }

        return label.Length > 12
            ? label[..12]
            : label;
    }

    private static bool IsSpanish(EnergyReportData report) =>
        report.Request.LanguageCode.StartsWith(
            "es",
            StringComparison.OrdinalIgnoreCase);
}
