using ClosedXML.Excel;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Shapes.Charts;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using SolarOfThings.Core.Statistics;

namespace SolarOfThings.Core.Reporting;

public sealed class EnergyReportExportService
{
    private readonly EnergyRangeStatisticsService _statistics;
    private readonly EnergyAggregationTableService _aggregation;
    private static int _pdfFontsInitialized;

    public EnergyReportExportService(
        EnergyRangeStatisticsService statistics,
        EnergyAggregationTableService aggregation)
    {
        _statistics = statistics;
        _aggregation = aggregation;
    }

    public EnergyReportData Build(EnergyReportRequest request)
    {
        var summary = _statistics.Get(
            request.DeviceId,
            request.StartUtc,
            request.EndUtc);

        var table = _aggregation.Get(
            request.DeviceId,
            request.StartUtc,
            request.EndUtc,
            request.TimeZoneId,
            request.Aggregation);

        return new EnergyReportData(
            request,
            summary,
            table,
            DateTimeOffset.UtcNow);
    }

    public void ExportExcel(
        string path,
        EnergyReportData report)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var workbook = new XLWorkbook();
        AddSummarySheet(workbook, report);
        AddDetailSheet(workbook, report);
        AddQualitySheet(workbook, report);
        AddGlossarySheet(workbook, report);
        workbook.SaveAs(path);
    }

    public void ExportPdf(
        string path,
        EnergyReportData report)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        EnsurePdfFonts();

        var document = new Document();
        document.Info.Title = report.Request.Title;
        document.Info.Subject = "Solar Energy Monitor";

        var normal = document.Styles["Normal"]!;
        normal.Font.Name = "Arial";
        normal.Font.Size = 9;

        var section = document.AddSection();
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.3);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.3);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.3);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.3);

        var heading = section.AddParagraph(report.Request.Title);
        heading.Format.Font.Size = 18;
        heading.Format.Font.Bold = true;
        heading.Format.SpaceAfter = Unit.FromPoint(8);

        section.AddParagraph(string.Format(
            L(report, "Período: {0} — {1}", "Period: {0} — {1}"),
            report.Request.LocalStartDate.ToString("dd-MM-yyyy"),
            report.Request.LocalEndDate.ToString("dd-MM-yyyy")));
        section.AddParagraph(string.Format(
            L(report, "Agrupación: {0}", "Aggregation: {0}"),
            report.Request.Aggregation));
        section.AddParagraph(string.Format(
            L(report, "Generado: {0}", "Generated: {0}"),
            report.GeneratedUtc.ToLocalTime().ToString("dd-MM-yyyy HH:mm")));

        AddSummary(section, report);

        if (report.Request.Kind is ReportKind.SimpleEnergy or ReportKind.DetailedEnergy)
        {
            AddEnergyChart(section, report);
        }

        if (report.Request.Kind is ReportKind.SimpleEnergy or ReportKind.Battery or ReportKind.DetailedEnergy)
        {
            AddBatterySocChart(section, report);
        }

        if (report.Request.Kind == ReportKind.SimpleEnergy)
        {
            AddSimpleLimitations(section, report);
        }
        else if (report.Request.Kind == ReportKind.DetailedEnergy)
        {
            AddDetailedTable(section, report);
        }
        else if (report.Request.Kind == ReportKind.Battery)
        {
            AddBatteryTable(section, report);
        }

        AddQuality(section, report);
        AddGlossary(section, report);

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.AddText("Solar Energy Monitor · ");
        footer.AddPageField();
        footer.AddText("/");
        footer.AddNumPagesField();

        var renderer = new PdfDocumentRenderer
        {
            Document = document
        };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(path);
    }

    private static void AddSummary(
        Section section,
        EnergyReportData report)
    {
        var heading = section.AddParagraph(
            L(report, "Resumen", "Summary"));
        heading.Format.Font.Size = 13;
        heading.Format.Font.Bold = true;
        heading.Format.SpaceBefore = Unit.FromPoint(10);
        heading.Format.SpaceAfter = Unit.FromPoint(5);

        var table = section.AddTable();
        table.Borders.Width = 0.4;
        table.AddColumn(Unit.FromCentimeter(6.4));
        table.AddColumn(Unit.FromCentimeter(4.0));

        if (report.Request.Kind != ReportKind.Battery)
        {
            AddPdfValueRow(
                table,
                L(report, "Solar generado", "Solar produced"),
                EnergyValue(report.Summary.PvPower, report.Summary.PvEnergyKwh, report));
            AddPdfValueRow(
                table,
                L(report, "Energía usada por la casa", "Home energy used"),
                EnergyValue(report.Summary.HouseLoadPower, report.Summary.HouseEnergyKwh, report));
            AddPdfValueRow(
                table,
                L(report, "Energía tomada de la red", "Grid energy imported"),
                EnergyValue(report.Summary.GridImportPower, report.Summary.GridImportEnergyKwh, report));
        }

        AddPdfValueRow(
            table,
            L(report, "Batería entregada", "Battery supplied"),
            EnergyValue(
                report.Summary.BatteryPower,
                report.Summary.BatteryDischargedEnergyKwh,
                report));
        AddPdfValueRow(
            table,
            L(report, "Batería recibida", "Battery received"),
            EnergyValue(
                report.Summary.BatteryPower,
                report.Summary.BatteryChargedEnergyKwh,
                report));
    }

    private static void AddEnergyChart(
        Section section,
        EnergyReportData report)
    {
        var candidates = report.Table.Rows
            .Where(row =>
                row.PvCoveragePercent > 0 &&
                row.HouseCoveragePercent > 0 &&
                row.GridCoveragePercent > 0)
            .ToArray();

        var rows = SampleRows(candidates, 12);
        if (rows.Count < 2)
        {
            return;
        }

        var title = section.AddParagraph(
            L(report, "Energía por período", "Energy by period"));
        title.Format.Font.Size = 12;
        title.Format.Font.Bold = true;
        title.Format.SpaceBefore = Unit.FromPoint(10);

        var chart = section.AddChart(ChartType.Column2D);
        chart.Width = Unit.FromCentimeter(16);
        chart.Height = Unit.FromCentimeter(7);

        var solar = chart.SeriesCollection.AddSeries();
        solar.Name = L(report, "Solar", "Solar");
        solar.Add(rows.Select(row => row.PvEnergyKwh).ToArray());

        var home = chart.SeriesCollection.AddSeries();
        home.Name = L(report, "Casa", "Home");
        home.Add(rows.Select(row => row.HouseEnergyKwh).ToArray());

        var grid = chart.SeriesCollection.AddSeries();
        grid.Name = L(report, "Red", "Grid");
        grid.Add(rows.Select(row => row.GridImportEnergyKwh).ToArray());

        var xSeries = chart.XValues.AddXSeries();
        xSeries.Add(rows.Select(row => row.LocalLabel).ToArray());

        chart.YAxis.Title.Caption = "kWh";
        chart.YAxis.HasMajorGridlines = true;
        chart.FooterArea.AddLegend();

        var note = section.AddParagraph(
            L(
                report,
                "El gráfico incluye sólo períodos con mediciones para solar, casa y red; los huecos no se reemplazan por cero.",
                "The chart includes only periods with measurements for solar, home and grid; gaps are not replaced by zero."));
        note.Format.Font.Size = 8;
        note.Format.Font.Italic = true;
    }

    private static void AddBatterySocChart(
        Section section,
        EnergyReportData report)
    {
        var candidates = report.Table.Rows
            .Where(row =>
                row.SocCoveragePercent > 0 &&
                row.SocAveragePercent.HasValue)
            .ToArray();

        var rows = SampleRows(candidates, 14);
        if (rows.Count < 2)
        {
            return;
        }

        var title = section.AddParagraph(
            L(report, "Carga promedio de batería", "Average battery charge"));
        title.Format.Font.Size = 12;
        title.Format.Font.Bold = true;
        title.Format.SpaceBefore = Unit.FromPoint(10);

        var chart = section.AddChart(ChartType.Column2D);
        chart.Width = Unit.FromCentimeter(16);
        chart.Height = Unit.FromCentimeter(6);

        var soc = chart.SeriesCollection.AddSeries();
        soc.Name = "SOC";
        soc.Add(rows.Select(row => row.SocAveragePercent!.Value).ToArray());

        var xSeries = chart.XValues.AddXSeries();
        xSeries.Add(rows.Select(row => row.LocalLabel).ToArray());

        chart.YAxis.Title.Caption = "%";
        chart.YAxis.HasMajorGridlines = true;
        chart.FooterArea.AddLegend();

        var note = section.AddParagraph(
            L(
                report,
                "Se muestran sólo períodos con SOC medido; no se conectan ni inventan períodos desconocidos.",
                "Only periods with measured SOC are shown; unknown periods are not connected or invented."));
        note.Format.Font.Size = 8;
        note.Format.Font.Italic = true;
    }

    private static void AddSimpleLimitations(
        Section section,
        EnergyReportData report)
    {
        var heading = section.AddParagraph(
            L(report, "Qué aún no se calcula", "What is not calculated yet"));
        heading.Format.Font.Size = 12;
        heading.Format.Font.Bold = true;
        heading.Format.SpaceBefore = Unit.FromPoint(10);

        section.AddParagraph(
            L(
                report,
                "El porcentaje de consumo cubierto sin red no se muestra todavía porque requiere atribución de flujos validada. La comparación con la boleta/medidor tampoco se muestra hasta que existan lecturas del medidor de la compañía. La aplicación prefiere dejar esos valores como no disponibles antes que estimarlos sin evidencia.",
                "The percentage supplied without grid is not shown yet because it requires validated flow attribution. Utility-bill/meter comparison is also unavailable until utility meter readings exist. The application prefers to leave these values unavailable rather than estimate them without evidence."));
    }

    private static void AddDetailedTable(
        Section section,
        EnergyReportData report)
    {
        var heading = section.AddParagraph(
            L(report, "Detalle", "Detail"));
        heading.Format.Font.Size = 13;
        heading.Format.Font.Bold = true;
        heading.Format.SpaceBefore = Unit.FromPoint(10);
        heading.Format.SpaceAfter = Unit.FromPoint(4);

        var table = section.AddTable();
        table.Borders.Width = 0.3;
        table.AddColumn(Unit.FromCentimeter(3.0));
        table.AddColumn(Unit.FromCentimeter(2.5));
        table.AddColumn(Unit.FromCentimeter(2.5));
        table.AddColumn(Unit.FromCentimeter(2.5));
        table.AddColumn(Unit.FromCentimeter(2.0));

        var header = table.AddRow();
        header.Format.Font.Bold = true;
        header.Cells[0].AddParagraph(L(report, "Período", "Period"));
        header.Cells[1].AddParagraph("Solar kWh");
        header.Cells[2].AddParagraph(L(report, "Casa kWh", "Home kWh"));
        header.Cells[3].AddParagraph(L(report, "Red kWh", "Grid kWh"));
        header.Cells[4].AddParagraph(L(report, "Cobertura", "Coverage"));

        foreach (var row in report.Table.Rows.Take(80))
        {
            var pdfRow = table.AddRow();
            pdfRow.Cells[0].AddParagraph(row.LocalLabel);
            pdfRow.Cells[1].AddParagraph(NullableNumber(row.PvEnergyDisplayKwh));
            pdfRow.Cells[2].AddParagraph(NullableNumber(row.HouseEnergyDisplayKwh));
            pdfRow.Cells[3].AddParagraph(NullableNumber(row.GridImportEnergyDisplayKwh));
            pdfRow.Cells[4].AddParagraph($"{row.MinimumAvailableCoveragePercent:N1}%");
        }

        AddTruncationNote(section, report);
    }

    private static void AddBatteryTable(
        Section section,
        EnergyReportData report)
    {
        var heading = section.AddParagraph(
            L(report, "Detalle de batería", "Battery detail"));
        heading.Format.Font.Size = 13;
        heading.Format.Font.Bold = true;
        heading.Format.SpaceBefore = Unit.FromPoint(10);
        heading.Format.SpaceAfter = Unit.FromPoint(4);

        var table = section.AddTable();
        table.Borders.Width = 0.3;
        table.AddColumn(Unit.FromCentimeter(3.2));
        table.AddColumn(Unit.FromCentimeter(2.3));
        table.AddColumn(Unit.FromCentimeter(2.3));
        table.AddColumn(Unit.FromCentimeter(2.8));
        table.AddColumn(Unit.FromCentimeter(2.3));

        var header = table.AddRow();
        header.Format.Font.Bold = true;
        header.Cells[0].AddParagraph(L(report, "Período", "Period"));
        header.Cells[1].AddParagraph("SOC avg %");
        header.Cells[2].AddParagraph("SOC end %");
        header.Cells[3].AddParagraph(L(report, "Entregada kWh", "Supplied kWh"));
        header.Cells[4].AddParagraph(L(report, "Cobertura", "Coverage"));

        foreach (var row in report.Table.Rows.Take(80))
        {
            var pdfRow = table.AddRow();
            pdfRow.Cells[0].AddParagraph(row.LocalLabel);
            pdfRow.Cells[1].AddParagraph(NullableNumber(row.SocAveragePercent));
            pdfRow.Cells[2].AddParagraph(NullableNumber(row.SocEndingPercent));
            pdfRow.Cells[3].AddParagraph(NullableNumber(row.BatteryDischargedEnergyDisplayKwh));
            pdfRow.Cells[4].AddParagraph($"{row.SocCoveragePercent:N1}%");
        }

        AddTruncationNote(section, report);
    }

    private static void AddQuality(
        Section section,
        EnergyReportData report)
    {
        var heading = section.AddParagraph(
            L(report, "Calidad de datos", "Data quality"));
        heading.Format.Font.Size = 13;
        heading.Format.Font.Bold = true;
        heading.Format.SpaceBefore = Unit.FromPoint(10);
        heading.Format.SpaceAfter = Unit.FromPoint(4);

        section.AddParagraph(string.Format(
            L(
                report,
                "Cobertura — solar {0:N1}% · casa {1:N1}% · red {2:N1}% · batería {3:N1}%.",
                "Coverage — solar {0:N1}% · home {1:N1}% · grid {2:N1}% · battery {3:N1}%."),
            report.Summary.PvPower.CoveragePercent,
            report.Summary.HouseLoadPower.CoveragePercent,
            report.Summary.GridImportPower.CoveragePercent,
            report.Summary.BatteryPower.CoveragePercent));

        var note = section.AddParagraph(
            L(
                report,
                "Los períodos faltantes son desconocidos, no cero. La energía no se extrapola a través de huecos largos.",
                "Missing periods are unknown, not zero. Energy is not extrapolated across long gaps."));
        note.Format.Font.Italic = true;
    }

    private static void AddGlossary(
        Section section,
        EnergyReportData report)
    {
        var heading = section.AddParagraph(
            L(report, "Glosario", "Glossary"));
        heading.Format.Font.Size = 13;
        heading.Format.Font.Bold = true;
        heading.Format.SpaceBefore = Unit.FromPoint(10);

        var table = section.AddTable();
        table.Borders.Width = 0.25;
        table.AddColumn(Unit.FromCentimeter(4.0));
        table.AddColumn(Unit.FromCentimeter(9.0));

        AddPdfValueRow(
            table,
            L(report, "Generación solar", "Solar generation"),
            L(report, "Electricidad producida por los paneles solares.", "Electricity produced by the solar panels."));
        AddPdfValueRow(
            table,
            L(report, "Consumo de la casa", "Home consumption"),
            L(report, "Electricidad usada por los equipos y cargas de la vivienda.", "Electricity used by appliances and loads in the home."));
        AddPdfValueRow(
            table,
            L(report, "Importación de red", "Grid import"),
            L(report, "Electricidad tomada de la compañía eléctrica.", "Electricity taken from the utility grid."));
        AddPdfValueRow(
            table,
            "SOC",
            L(report, "Qué tan llena está la batería, expresado en porcentaje.", "How full the battery is, shown as a percentage."));
        AddPdfValueRow(
            table,
            L(report, "Cobertura", "Coverage"),
            L(report, "Qué parte del período tiene datos suficientes para el cálculo.", "How much of the period has enough data for the calculation."));
    }

    private static void AddSummarySheet(
        XLWorkbook workbook,
        EnergyReportData report)
    {
        var sheet = workbook.Worksheets.Add(
            L(report, "Resumen", "Summary"));
        sheet.Cell("A1").Value = report.Request.Title;
        sheet.Cell("A1").Style.Font.Bold = true;
        sheet.Cell("A1").Style.Font.FontSize = 16;

        sheet.Cell("A3").Value = L(report, "Desde", "From");
        sheet.Cell("B3").Value = report.Request.LocalStartDate.ToDateTime(TimeOnly.MinValue);
        sheet.Cell("A4").Value = L(report, "Hasta", "To");
        sheet.Cell("B4").Value = report.Request.LocalEndDate.ToDateTime(TimeOnly.MinValue);
        sheet.Cell("A5").Value = L(report, "Agrupación", "Aggregation");
        sheet.Cell("B5").Value = report.Request.Aggregation.ToString();
        sheet.Cell("A6").Value = L(report, "Tipo", "Type");
        sheet.Cell("B6").Value = report.Request.Kind.ToString();
        sheet.Cell("A7").Value = L(report, "Generado", "Generated");
        sheet.Cell("B7").Value = report.GeneratedUtc.LocalDateTime;
        sheet.Range("B3:B4").Style.DateFormat.Format = "dd-mm-yyyy";
        sheet.Cell("B7").Style.DateFormat.Format = "dd-mm-yyyy hh:mm";

        sheet.Cell("A9").Value = L(report, "Métrica", "Metric");
        sheet.Cell("B9").Value = L(report, "Energía (kWh)", "Energy (kWh)");
        sheet.Cell("C9").Value = L(report, "Cobertura (%)", "Coverage (%)");
        sheet.Range("A9:C9").Style.Font.Bold = true;

        var metrics = new[]
        {
            (L(report, "Solar generado", "Solar produced"), report.Summary.PvPower, report.Summary.PvEnergyKwh),
            (L(report, "Consumo de la casa", "Home use"), report.Summary.HouseLoadPower, report.Summary.HouseEnergyKwh),
            (L(report, "Importación de red", "Grid import"), report.Summary.GridImportPower, report.Summary.GridImportEnergyKwh),
            (L(report, "Batería entregada", "Battery supplied"), report.Summary.BatteryPower, report.Summary.BatteryDischargedEnergyKwh),
            (L(report, "Batería recibida", "Battery received"), report.Summary.BatteryPower, report.Summary.BatteryChargedEnergyKwh)
        };

        var row = 10;
        foreach (var item in metrics)
        {
            sheet.Cell(row, 1).Value = item.Item1;
            if (item.Item2.SampleCount >= 2) sheet.Cell(row, 2).Value = item.Item3;
            sheet.Cell(row, 3).Value = item.Item2.CoveragePercent;
            row++;
        }

        sheet.Cell(row + 1, 1).Value =
            L(
                report,
                "Los períodos faltantes son desconocidos, no cero; los huecos largos no se extrapolan.",
                "Missing periods are unknown, not zero; long gaps are not extrapolated.");
        sheet.Range(row + 1, 1, row + 1, 3).Merge();
        sheet.Cell(row + 1, 1).Style.Font.Italic = true;
        sheet.Columns().AdjustToContents();
    }

    private static void AddDetailSheet(
        XLWorkbook workbook,
        EnergyReportData report)
    {
        var sheet = workbook.Worksheets.Add(
            L(report, "Detalle", "Detail"));
        var headers = IsSpanish(report)
            ? new[]
            {
                "Período", "Inicio UTC", "Fin UTC",
                "Solar kWh", "Casa kWh", "Red kWh",
                "Batería entregada kWh", "Batería recibida kWh",
                "SOC prom %", "SOC mín %", "SOC máx %", "SOC fin %",
                "Cobertura solar %", "Cobertura casa %", "Cobertura red %",
                "Cobertura batería %", "Cobertura SOC %", "Cobertura mínima %"
            }
            : new[]
            {
                "Period", "Start UTC", "End UTC",
                "Solar kWh", "Home kWh", "Grid kWh",
                "Battery supplied kWh", "Battery received kWh",
                "SOC avg %", "SOC min %", "SOC max %", "SOC end %",
                "PV coverage %", "Home coverage %", "Grid coverage %",
                "Battery coverage %", "SOC coverage %", "Minimum coverage %"
            };

        for (var column = 0; column < headers.Length; column++)
            sheet.Cell(1, column + 1).Value = headers[column];
        sheet.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;

        var targetRow = 2;
        foreach (var item in report.Table.Rows)
        {
            sheet.Cell(targetRow, 1).Value = item.LocalLabel;
            sheet.Cell(targetRow, 2).Value = item.StartUtc.UtcDateTime;
            sheet.Cell(targetRow, 3).Value = item.EndUtcExclusive.UtcDateTime;
            if (item.PvEnergyDisplayKwh.HasValue) sheet.Cell(targetRow, 4).Value = item.PvEnergyDisplayKwh.Value;
            if (item.HouseEnergyDisplayKwh.HasValue) sheet.Cell(targetRow, 5).Value = item.HouseEnergyDisplayKwh.Value;
            if (item.GridImportEnergyDisplayKwh.HasValue) sheet.Cell(targetRow, 6).Value = item.GridImportEnergyDisplayKwh.Value;
            if (item.BatteryDischargedEnergyDisplayKwh.HasValue) sheet.Cell(targetRow, 7).Value = item.BatteryDischargedEnergyDisplayKwh.Value;
            if (item.BatteryChargedEnergyDisplayKwh.HasValue) sheet.Cell(targetRow, 8).Value = item.BatteryChargedEnergyDisplayKwh.Value;
            if (item.SocAveragePercent.HasValue) sheet.Cell(targetRow, 9).Value = item.SocAveragePercent.Value;
            if (item.SocMinimumPercent.HasValue) sheet.Cell(targetRow, 10).Value = item.SocMinimumPercent.Value;
            if (item.SocMaximumPercent.HasValue) sheet.Cell(targetRow, 11).Value = item.SocMaximumPercent.Value;
            if (item.SocEndingPercent.HasValue) sheet.Cell(targetRow, 12).Value = item.SocEndingPercent.Value;
            sheet.Cell(targetRow, 13).Value = item.PvCoveragePercent;
            sheet.Cell(targetRow, 14).Value = item.HouseCoveragePercent;
            sheet.Cell(targetRow, 15).Value = item.GridCoveragePercent;
            sheet.Cell(targetRow, 16).Value = item.BatteryCoveragePercent;
            sheet.Cell(targetRow, 17).Value = item.SocCoveragePercent;
            sheet.Cell(targetRow, 18).Value = item.MinimumAvailableCoveragePercent;
            targetRow++;
        }

        sheet.Columns(2, 3).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
        sheet.SheetView.FreezeRows(1);
        sheet.RangeUsed()?.SetAutoFilter();
        sheet.Columns().AdjustToContents();
    }

    private static void AddQualitySheet(
        XLWorkbook workbook,
        EnergyReportData report)
    {
        var sheet = workbook.Worksheets.Add(
            L(report, "Calidad", "Quality"));
        sheet.Cell("A1").Value =
            L(report, "Calidad de datos", "Data quality");
        sheet.Cell("A1").Style.Font.Bold = true;
        sheet.Cell("A1").Style.Font.FontSize = 14;

        sheet.Cell("A3").Value = L(report, "Métrica", "Metric");
        sheet.Cell("B3").Value = L(report, "Cobertura (%)", "Coverage (%)");
        sheet.Cell("C3").Value = L(report, "Muestras", "Samples");
        sheet.Range("A3:C3").Style.Font.Bold = true;

        var items = new[]
        {
            (L(report, "Solar", "Solar"), report.Summary.PvPower),
            (L(report, "Casa", "Home"), report.Summary.HouseLoadPower),
            (L(report, "Red", "Grid"), report.Summary.GridImportPower),
            (L(report, "Batería", "Battery"), report.Summary.BatteryPower)
        };

        var row = 4;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = item.Item1;
            sheet.Cell(row, 2).Value = item.Item2.CoveragePercent;
            sheet.Cell(row, 3).Value = item.Item2.SampleCount;
            row++;
        }

        sheet.Cell(row + 1, 1).Value =
            L(
                report,
                "Regla: faltante/desconocido nunca se interpreta como cero medido.",
                "Rule: missing/unknown is never interpreted as measured zero.");
        sheet.Range(row + 1, 1, row + 1, 3).Merge();
        sheet.Cell(row + 2, 1).Value =
            L(
                report,
                "La integración de energía excluye huecos largos en vez de extrapolarlos.",
                "Energy integration excludes long gaps instead of extrapolating them.");
        sheet.Range(row + 2, 1, row + 2, 3).Merge();
        sheet.Columns().AdjustToContents();
    }

    private static void AddGlossarySheet(
        XLWorkbook workbook,
        EnergyReportData report)
    {
        var sheet = workbook.Worksheets.Add(
            L(report, "Glosario", "Glossary"));
        sheet.Cell("A1").Value =
            L(report, "Término", "Term");
        sheet.Cell("B1").Value =
            L(report, "Explicación", "Explanation");
        sheet.Range("A1:B1").Style.Font.Bold = true;

        var rows = new[]
        {
            (
                L(report, "Generación solar", "Solar generation"),
                L(report, "Electricidad producida por los paneles solares.", "Electricity produced by the solar panels.")),
            (
                L(report, "Consumo de la casa", "Home consumption"),
                L(report, "Electricidad usada por los equipos y cargas de la vivienda.", "Electricity used by appliances and loads in the home.")),
            (
                L(report, "Importación de red", "Grid import"),
                L(report, "Electricidad tomada de la compañía eléctrica.", "Electricity taken from the utility grid.")),
            (
                "SOC",
                L(report, "Qué tan llena está la batería, expresado en porcentaje.", "How full the battery is, shown as a percentage.")),
            (
                L(report, "Cobertura", "Coverage"),
                L(report, "Qué parte del período tiene datos suficientes para el cálculo.", "How much of the period has enough data for the calculation."))
        };

        var row = 2;
        foreach (var item in rows)
        {
            sheet.Cell(row, 1).Value = item.Item1;
            sheet.Cell(row, 2).Value = item.Item2;
            row++;
        }
        sheet.Columns().AdjustToContents();
    }

    private static IReadOnlyList<EnergyAggregationRow> SampleRows(
        IReadOnlyList<EnergyAggregationRow> rows,
        int maxCount)
    {
        if (rows.Count <= maxCount)
        {
            return rows;
        }

        var result = new List<EnergyAggregationRow>(maxCount);
        var step = (rows.Count - 1.0) / (maxCount - 1.0);
        var used = new HashSet<int>();

        for (var index = 0; index < maxCount; index++)
        {
            var sourceIndex = (int)Math.Round(index * step);
            if (used.Add(sourceIndex))
            {
                result.Add(rows[sourceIndex]);
            }
        }

        return result;
    }

    private static void AddTruncationNote(
        Section section,
        EnergyReportData report)
    {
        if (report.Table.Rows.Count <= 80)
        {
            return;
        }

        section.AddParagraph(string.Format(
            L(
                report,
                "El PDF muestra las primeras 80 filas de detalle de {0}. Excel contiene todas las filas.",
                "The PDF shows the first 80 detail rows of {0}. Excel contains every row."),
            report.Table.Rows.Count));
    }

    private static string EnergyValue(
        PowerMetricStatistics metric,
        double energyKwh,
        EnergyReportData report) =>
        metric.SampleCount >= 2
            ? $"{energyKwh:N2} kWh"
            : L(report, "Sin datos", "No data");

    private static string NullableNumber(double? value) =>
        value.HasValue ? value.Value.ToString("N2") : "—";

    private static void AddPdfValueRow(
        Table table,
        string label,
        string value)
    {
        var row = table.AddRow();
        row.Cells[0].AddParagraph(label);
        row.Cells[1].AddParagraph(value);
    }

    private static bool IsSpanish(EnergyReportData report) =>
        report.Request.LanguageCode.StartsWith(
            "es",
            StringComparison.OrdinalIgnoreCase);

    private static string L(
        EnergyReportData report,
        string spanish,
        string english) =>
        IsSpanish(report) ? spanish : english;

    private static void EnsurePdfFonts()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "PDF export is currently supported by the Windows desktop application.");
        }

        if (Interlocked.Exchange(ref _pdfFontsInitialized, 1) == 0)
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        }
    }
}
