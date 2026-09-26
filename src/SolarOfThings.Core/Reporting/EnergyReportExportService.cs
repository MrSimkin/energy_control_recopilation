using ClosedXML.Excel;
using MigraDoc.DocumentObjectModel;
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
        document.Info.Subject = "Solar Energy Monitor report";

        var normal = document.Styles["Normal"];
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

        section.AddParagraph(
            $"Period: {report.Request.LocalStartDate:dd-MM-yyyy} — {report.Request.LocalEndDate:dd-MM-yyyy}");
        section.AddParagraph(
            $"Aggregation: {report.Request.Aggregation}");
        section.AddParagraph(
            $"Generated: {report.GeneratedUtc.ToLocalTime():dd-MM-yyyy HH:mm}");

        var summaryHeading = section.AddParagraph("Summary");
        summaryHeading.Format.Font.Size = 13;
        summaryHeading.Format.Font.Bold = true;
        summaryHeading.Format.SpaceBefore = Unit.FromPoint(10);
        summaryHeading.Format.SpaceAfter = Unit.FromPoint(5);

        var summaryTable = section.AddTable();
        summaryTable.Borders.Width = 0.4;
        summaryTable.AddColumn(Unit.FromCentimeter(6.4));
        summaryTable.AddColumn(Unit.FromCentimeter(4.0));
        AddPdfValueRow(summaryTable, "Solar produced", report.Summary.PvPower.SampleCount >= 2 ? $"{report.Summary.PvEnergyKwh:N2} kWh" : "No data");
        AddPdfValueRow(summaryTable, "Home use", report.Summary.HouseLoadPower.SampleCount >= 2 ? $"{report.Summary.HouseEnergyKwh:N2} kWh" : "No data");
        AddPdfValueRow(summaryTable, "Grid import", report.Summary.GridImportPower.SampleCount >= 2 ? $"{report.Summary.GridImportEnergyKwh:N2} kWh" : "No data");
        AddPdfValueRow(summaryTable, "Battery supplied", report.Summary.BatteryPower.SampleCount >= 2 ? $"{report.Summary.BatteryDischargedEnergyKwh:N2} kWh" : "No data");
        AddPdfValueRow(summaryTable, "Battery received", report.Summary.BatteryPower.SampleCount >= 2 ? $"{report.Summary.BatteryChargedEnergyKwh:N2} kWh" : "No data");

        var qualityHeading = section.AddParagraph("Data quality");
        qualityHeading.Format.Font.Size = 13;
        qualityHeading.Format.Font.Bold = true;
        qualityHeading.Format.SpaceBefore = Unit.FromPoint(10);
        qualityHeading.Format.SpaceAfter = Unit.FromPoint(4);

        section.AddParagraph(
            $"Coverage — solar {report.Summary.PvPower.CoveragePercent:N1}% · home {report.Summary.HouseLoadPower.CoveragePercent:N1}% · grid {report.Summary.GridImportPower.CoveragePercent:N1}% · battery {report.Summary.BatteryPower.CoveragePercent:N1}%.");
        var qualityNote = section.AddParagraph(
            "Missing periods are unknown, not zero. Energy is never extrapolated across long gaps.");
        qualityNote.Format.Font.Italic = true;

        var detailHeading = section.AddParagraph("Detail");
        detailHeading.Format.Font.Size = 13;
        detailHeading.Format.Font.Bold = true;
        detailHeading.Format.SpaceBefore = Unit.FromPoint(10);
        detailHeading.Format.SpaceAfter = Unit.FromPoint(4);

        var detail = section.AddTable();
        detail.Borders.Width = 0.3;
        detail.AddColumn(Unit.FromCentimeter(3.0));
        detail.AddColumn(Unit.FromCentimeter(2.5));
        detail.AddColumn(Unit.FromCentimeter(2.5));
        detail.AddColumn(Unit.FromCentimeter(2.5));
        detail.AddColumn(Unit.FromCentimeter(2.0));

        var header = detail.AddRow();
        header.Format.Font.Bold = true;
        header.Cells[0].AddParagraph("Period");
        header.Cells[1].AddParagraph("Solar kWh");
        header.Cells[2].AddParagraph("Home kWh");
        header.Cells[3].AddParagraph("Grid kWh");
        header.Cells[4].AddParagraph("Coverage");

        foreach (var row in report.Table.Rows.Take(80))
        {
            var pdfRow = detail.AddRow();
            pdfRow.Cells[0].AddParagraph(row.LocalLabel);
            pdfRow.Cells[1].AddParagraph(
                row.PvEnergyDisplayKwh.HasValue
                    ? row.PvEnergyDisplayKwh.Value.ToString("N2")
                    : "—");
            pdfRow.Cells[2].AddParagraph(
                row.HouseEnergyDisplayKwh.HasValue
                    ? row.HouseEnergyDisplayKwh.Value.ToString("N2")
                    : "—");
            pdfRow.Cells[3].AddParagraph(
                row.GridImportEnergyDisplayKwh.HasValue
                    ? row.GridImportEnergyDisplayKwh.Value.ToString("N2")
                    : "—");
            pdfRow.Cells[4].AddParagraph(
                $"{row.MinimumAvailableCoveragePercent:N1}%");
        }

        if (report.Table.Rows.Count > 80)
        {
            section.AddParagraph(
                $"PDF shows the first 80 detail rows of {report.Table.Rows.Count}. The Excel export contains every row.");
        }

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

    private static void AddSummarySheet(
        XLWorkbook workbook,
        EnergyReportData report)
    {
        var sheet = workbook.Worksheets.Add("Summary");
        sheet.Cell("A1").Value = report.Request.Title;
        sheet.Cell("A1").Style.Font.Bold = true;
        sheet.Cell("A1").Style.Font.FontSize = 16;

        sheet.Cell("A3").Value = "From";
        sheet.Cell("B3").Value = report.Request.LocalStartDate.ToDateTime(TimeOnly.MinValue);
        sheet.Cell("A4").Value = "To";
        sheet.Cell("B4").Value = report.Request.LocalEndDate.ToDateTime(TimeOnly.MinValue);
        sheet.Cell("A5").Value = "Aggregation";
        sheet.Cell("B5").Value = report.Request.Aggregation.ToString();
        sheet.Cell("A6").Value = "Generated";
        sheet.Cell("B6").Value = report.GeneratedUtc.LocalDateTime;
        sheet.Range("B3:B4").Style.DateFormat.Format = "dd-mm-yyyy";
        sheet.Cell("B6").Style.DateFormat.Format = "dd-mm-yyyy hh:mm";

        sheet.Cell("A8").Value = "Metric";
        sheet.Cell("B8").Value = "Energy (kWh)";
        sheet.Cell("C8").Value = "Coverage (%)";
        sheet.Range("A8:C8").Style.Font.Bold = true;

        var metrics = new[]
        {
            ("Solar produced", report.Summary.PvPower, report.Summary.PvEnergyKwh),
            ("Home use", report.Summary.HouseLoadPower, report.Summary.HouseEnergyKwh),
            ("Grid import", report.Summary.GridImportPower, report.Summary.GridImportEnergyKwh),
            ("Battery supplied", report.Summary.BatteryPower, report.Summary.BatteryDischargedEnergyKwh),
            ("Battery received", report.Summary.BatteryPower, report.Summary.BatteryChargedEnergyKwh)
        };

        var row = 9;
        foreach (var item in metrics)
        {
            sheet.Cell(row, 1).Value = item.Item1;
            if (item.Item2.SampleCount >= 2)
            {
                sheet.Cell(row, 2).Value = item.Item3;
            }
            sheet.Cell(row, 3).Value = item.Item2.CoveragePercent;
            row++;
        }

        sheet.Cell(row + 1, 1).Value =
            "Missing periods are unknown, not zero; long gaps are not extrapolated.";
        sheet.Range(row + 1, 1, row + 1, 3).Merge();
        sheet.Cell(row + 1, 1).Style.Font.Italic = true;

        sheet.Columns().AdjustToContents();
    }

    private static void AddDetailSheet(
        XLWorkbook workbook,
        EnergyReportData report)
    {
        var sheet = workbook.Worksheets.Add("Detail");
        var headers = new[]
        {
            "Period", "Start UTC", "End UTC",
            "Solar kWh", "Home kWh", "Grid kWh",
            "Battery supplied kWh", "Battery received kWh",
            "SOC avg %", "SOC min %", "SOC max %", "SOC end %",
            "PV coverage %", "Home coverage %", "Grid coverage %",
            "Battery coverage %", "SOC coverage %", "Minimum coverage %"
        };

        for (var column = 0; column < headers.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = headers[column];
        }
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
        var sheet = workbook.Worksheets.Add("Quality");
        sheet.Cell("A1").Value = "Data quality";
        sheet.Cell("A1").Style.Font.Bold = true;
        sheet.Cell("A1").Style.Font.FontSize = 14;

        sheet.Cell("A3").Value = "Metric";
        sheet.Cell("B3").Value = "Coverage (%)";
        sheet.Cell("C3").Value = "Samples";
        sheet.Range("A3:C3").Style.Font.Bold = true;

        var items = new[]
        {
            ("Solar", report.Summary.PvPower),
            ("Home", report.Summary.HouseLoadPower),
            ("Grid", report.Summary.GridImportPower),
            ("Battery", report.Summary.BatteryPower)
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
            "Rule: missing/unknown is never interpreted as measured zero.";
        sheet.Range(row + 1, 1, row + 1, 3).Merge();
        sheet.Cell(row + 2, 1).Value =
            "Energy integration excludes long gaps rather than extrapolating them.";
        sheet.Range(row + 2, 1, row + 2, 3).Merge();
        sheet.Columns().AdjustToContents();
    }

    private static void AddPdfValueRow(
        Table table,
        string label,
        string value)
    {
        var row = table.AddRow();
        row.Cells[0].AddParagraph(label);
        row.Cells[1].AddParagraph(value);
    }

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
