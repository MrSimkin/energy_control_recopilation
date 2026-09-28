using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using SolarOfThings.Core.SolarOfThings;

namespace SolarOfThings.Core.Utility;

public sealed class UtilityReconciliationReportService
{
    private readonly UtilityMeterRepository _repository;
    private readonly UtilityReconciliationService _reconciliation;
    private static int _pdfFontsInitialized;

    public UtilityReconciliationReportService(
        UtilityMeterRepository repository,
        UtilityReconciliationService reconciliation)
    {
        _repository = repository;
        _reconciliation = reconciliation;
    }

    public void ExportPdf(
        string path,
        string deviceId,
        long fromReadingId,
        long toReadingId,
        string timeZoneId,
        string languageCode)
    {
        EnsurePdfFonts();

        var from = _repository.GetReading(fromReadingId)
            ?? throw new InvalidOperationException("Start reading was not found.");
        var to = _repository.GetReading(toReadingId)
            ?? throw new InvalidOperationException("End reading was not found.");
        var result = _reconciliation.ReconcileReadings(
            deviceId,
            from,
            to);

        if (result.ToUtc <= result.FromUtc)
            throw new InvalidOperationException("The selected reading interval is invalid.");

        var spanish = languageCode.StartsWith("es", StringComparison.OrdinalIgnoreCase);
        string L(string es, string en) => spanish ? es : en;

        var document = new Document();
        document.Info.Title = L(
            "Informe de conciliación de consumo eléctrico",
            "Electricity consumption reconciliation report");

        var normal = document.Styles["Normal"];
        normal.Font.Name = "Arial";
        normal.Font.Size = 9;

        var section = document.AddSection();
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.6);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.6);

        var title = section.AddParagraph(document.Info.Title);
        title.Format.Font.Size = 18;
        title.Format.Font.Bold = true;
        title.Format.SpaceAfter = Unit.FromPoint(4);

        var subtitle = section.AddParagraph(
            L(
                "Comparación entre medidor Enel y la importación total registrada por Solar of Things",
                "Comparison between the utility meter and total utility import recorded by Solar of Things"));
        subtitle.Format.Font.Size = 10;
        subtitle.Format.Font.Color = Colors.DimGray;
        subtitle.Format.SpaceAfter = Unit.FromPoint(10);

        AddKeyValue(
            section,
            L("Generado", "Generated"),
            SolarApiTime.ConvertToLocalTime(DateTimeOffset.UtcNow, timeZoneId)
                .ToString("dd-MM-yyyy HH:mm:ss"));
        AddKeyValue(section, L("Zona horaria", "Time zone"), timeZoneId);
        AddKeyValue(
            section,
            L("Intervalo comparado", "Compared interval"),
            $"{FormatReadingInstant(from, timeZoneId, spanish)} → {FormatReadingInstant(to, timeZoneId, spanish)}");
        AddKeyValue(
            section,
            L("Base temporal", "Time basis"),
            result.TimeBasis == "EXACT"
                ? L("Hora exacta en ambas lecturas", "Exact time for both readings")
                : L(
                    "Al menos una lectura tiene sólo fecha; 00:00 se usa como supuesto explícito",
                    "At least one reading has date only; 00:00 is used as an explicit assumption"));

        var evidenceHeading = section.AddParagraph(
            L("Evidencia de medidor", "Meter evidence"));
        evidenceHeading.Format.Font.Size = 13;
        evidenceHeading.Format.Font.Bold = true;
        evidenceHeading.Format.SpaceBefore = Unit.FromPoint(12);
        evidenceHeading.Format.SpaceAfter = Unit.FromPoint(5);

        var readings = section.AddTable();
        readings.Borders.Width = 0.35;
        readings.AddColumn(Unit.FromCentimeter(2.5));
        readings.AddColumn(Unit.FromCentimeter(5.3));
        readings.AddColumn(Unit.FromCentimeter(2.7));
        readings.AddColumn(Unit.FromCentimeter(5.3));

        var rh = readings.AddRow();
        rh.Format.Font.Bold = true;
        rh.Cells[0].AddParagraph(L("Lectura", "Reading"));
        rh.Cells[1].AddParagraph(L("Fecha/hora usada", "Date/time used"));
        rh.Cells[2].AddParagraph("kWh");
        rh.Cells[3].AddParagraph(L("Origen", "Source"));

        AddReadingRow(readings, L("Inicial", "Start"), from, timeZoneId, spanish);
        AddReadingRow(readings, L("Final", "End"), to, timeZoneId, spanish);

        var summaryHeading = section.AddParagraph(
            L("Resultado de conciliación", "Reconciliation result"));
        summaryHeading.Format.Font.Size = 13;
        summaryHeading.Format.Font.Bold = true;
        summaryHeading.Format.SpaceBefore = Unit.FromPoint(12);
        summaryHeading.Format.SpaceAfter = Unit.FromPoint(5);

        var summary = section.AddTable();
        summary.Borders.Width = 0;
        summary.AddColumn(Unit.FromCentimeter(4.0));
        summary.AddColumn(Unit.FromCentimeter(4.0));
        summary.AddColumn(Unit.FromCentimeter(4.0));
        summary.AddColumn(Unit.FromCentimeter(4.0));
        var cards = summary.AddRow();

        AddCard(
            cards.Cells[0],
            L("MEDIDOR ENEL", "UTILITY METER"),
            result.MeterConsumptionKwh.HasValue
                ? $"{result.MeterConsumptionKwh.Value:N3} kWh"
                : "—",
            L("Diferencia de lecturas acumuladas", "Difference between cumulative readings"));
        AddCard(
            cards.Cells[1],
            "SOLAR OF THINGS",
            $"{result.InverterGridImportKwh:N3} kWh",
            L("Importación total desde Enel", "Total utility import"));
        AddCard(
            cards.Cells[2],
            L("DIFERENCIA", "DIFFERENCE"),
            result.SignedDifferenceKwh.HasValue
                ? $"{result.SignedDifferenceKwh.Value:+0.000;-0.000;0.000} kWh"
                : "—",
            result.DifferencePercent.HasValue
                ? $"{result.DifferencePercent.Value:N2}%"
                : "—");
        AddCard(
            cards.Cells[3],
            L("COBERTURA", "COVERAGE"),
            $"{result.CoveragePercent:N1}%",
            result.Quality);

        var formula = section.AddParagraph(
            L(
                "Convención: diferencia = Solar of Things − medidor Enel. Un valor negativo significa que Solar of Things registró menos importación que el medidor.",
                "Convention: difference = Solar of Things − utility meter. A negative value means Solar of Things recorded less import than the utility meter."));
        formula.Format.Font.Size = 8;
        formula.Format.Font.Italic = true;
        formula.Format.SpaceBefore = Unit.FromPoint(6);

        var bill = _repository.GetBills()
            .FirstOrDefault(item =>
                item.FromReadingId == fromReadingId &&
                item.ToReadingId == toReadingId);

        if (bill is not null)
            AddBillEvidence(section, bill, spanish);

        var limitations = section.AddParagraph(
            L("Alcance y limitaciones", "Scope and limitations"));
        limitations.Format.Font.Size = 13;
        limitations.Format.Font.Bold = true;
        limitations.Format.SpaceBefore = Unit.FromPoint(12);
        limitations.Format.SpaceAfter = Unit.FromPoint(4);

        var bullets = new[]
        {
            L(
                "El valor Solar of Things usa importación total de red, no la métrica analítica Enel → Casa.",
                "Solar of Things uses total grid import, not the analytical Utility → Home metric."),
            L(
                "No se extrapolan los períodos faltantes: la cobertura se muestra explícitamente.",
                "Missing intervals are not extrapolated: coverage is shown explicitly."),
            L(
                "Las lecturas marcadas como “sólo fecha” no traen una hora oficial; el informe conserva ese hecho y muestra el supuesto usado.",
                "Readings marked as date-only do not provide an official time; the report preserves that fact and shows the assumption used."),
            L(
                "La telemetría del inversor no reemplaza al medidor certificado de la compañía; este informe sirve como evidencia técnica de contraste.",
                "Inverter telemetry does not replace the utility's certified meter; this report is technical comparison evidence.")
        };

        foreach (var item in bullets)
        {
            var p = section.AddParagraph("• " + item);
            p.Format.SpaceAfter = Unit.FromPoint(2);
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

    private void AddBillEvidence(
        Section section,
        UtilityBillRecord bill,
        bool spanish)
    {
        string L(string es, string en) => spanish ? es : en;

        var heading = section.AddParagraph(
            L("Boleta Enel vinculada", "Linked utility bill"));
        heading.Format.Font.Size = 13;
        heading.Format.Font.Bold = true;
        heading.Format.SpaceBefore = Unit.FromPoint(12);
        heading.Format.SpaceAfter = Unit.FromPoint(5);

        AddKeyValue(
            section,
            L("Consumo facturado", "Billed consumption"),
            bill.BilledConsumptionKwh.HasValue
                ? $"{bill.BilledConsumptionKwh.Value:N3} kWh"
                : "—");
        AddKeyValue(
            section,
            L("Tarifa informada", "Reported tariff"),
            string.IsNullOrWhiteSpace(bill.TariffPlan) ? "—" : bill.TariffPlan);
        AddKeyValue(
            section,
            L("Monto afecto / IVA / exento", "Taxable / VAT / exempt"),
            $"{Money(bill.TaxableAmountClp)} / {Money(bill.IvaClp)} / {Money(bill.ExemptAmountClp)}");
        AddKeyValue(
            section,
            L(
                "Total boleta / otros cargos / total a pagar",
                "Gross bill / other charges / total due"),
            $"{Money(bill.GrossBillAmountClp)} / {MoneySigned(bill.OtherChargesClp)} / {Money(bill.TotalDueClp ?? bill.AmountClp)}");

        var lines = _repository.GetBillLines(bill.BillId);
        if (lines.Count == 0)
            return;

        var table = section.AddTable();
        table.Borders.Width = 0.25;
        table.AddColumn(Unit.FromCentimeter(3.0));
        table.AddColumn(Unit.FromCentimeter(7.5));
        table.AddColumn(Unit.FromCentimeter(2.3));
        table.AddColumn(Unit.FromCentimeter(3.2));

        var header = table.AddRow();
        header.Format.Font.Bold = true;
        header.Cells[0].AddParagraph(L("Sección", "Section"));
        header.Cells[1].AddParagraph(L("Descripción", "Description"));
        header.Cells[2].AddParagraph(L("Cantidad", "Quantity"));
        header.Cells[3].AddParagraph(L("Monto", "Amount"));

        foreach (var line in lines)
        {
            var row = table.AddRow();
            row.Cells[0].AddParagraph(line.SectionKey);
            row.Cells[1].AddParagraph(line.Description);
            row.Cells[2].AddParagraph(
                line.Quantity.HasValue
                    ? $"{line.Quantity.Value:N3} {line.Unit}".Trim()
                    : string.Empty);
            row.Cells[3].AddParagraph($"$ {line.AmountClp:+0;-0;0}");
        }
    }

    private static void AddReadingRow(
        Table table,
        string label,
        UtilityMeterReading reading,
        string timeZoneId,
        bool spanish)
    {
        var row = table.AddRow();
        row.Cells[0].AddParagraph(label);
        row.Cells[1].AddParagraph(
            FormatReadingInstant(reading, timeZoneId, spanish));
        row.Cells[2].AddParagraph($"{reading.ReadingKwh:N3}");
        row.Cells[3].AddParagraph(
            reading.SourceKind == UtilityReadingSourceKind.UtilityOfficial
                ? (spanish ? "Enel oficial" : "Utility official")
                : reading.SourceKind == UtilityReadingSourceKind.Personal
                    ? (spanish ? "Lectura personal" : "Personal reading")
                    : reading.SourceKind);
    }

    private static string FormatReadingInstant(
        UtilityMeterReading reading,
        string timeZoneId,
        bool spanish)
    {
        var local = SolarApiTime.ConvertToLocalTime(
            reading.ReadingAtUtc,
            timeZoneId);

        if (reading.TimePrecision == UtilityTimePrecision.DateOnly)
        {
            return spanish
                ? $"{local:dd-MM-yyyy} · hora oficial no informada · 00:00 asumida"
                : $"{local:dd-MM-yyyy} · official time not provided · 00:00 assumed";
        }

        return local.ToString("dd-MM-yyyy HH:mm:ss");
    }

    private static void AddKeyValue(
        Section section,
        string key,
        string value)
    {
        var p = section.AddParagraph();
        p.AddFormattedText(key + ": ", TextFormat.Bold);
        p.AddText(value);
        p.Format.SpaceAfter = Unit.FromPoint(2);
    }

    private static void AddCard(
        Cell cell,
        string label,
        string value,
        string context)
    {
        cell.Borders.Width = 0.4;
        cell.Borders.Color = Colors.LightGray;
        cell.Shading.Color = Colors.AliceBlue;
        cell.Format.Alignment = ParagraphAlignment.Center;
        cell.VerticalAlignment = VerticalAlignment.Center;

        var p1 = cell.AddParagraph(label);
        p1.Format.Font.Bold = true;
        p1.Format.Font.Size = 8;
        p1.Format.SpaceBefore = Unit.FromPoint(4);

        var p2 = cell.AddParagraph(value);
        p2.Format.Font.Bold = true;
        p2.Format.Font.Size = 14;
        p2.Format.SpaceAfter = Unit.FromPoint(3);

        var p3 = cell.AddParagraph(context);
        p3.Format.Font.Size = 7;
        p3.Format.SpaceAfter = Unit.FromPoint(4);
    }

    private static string Money(double? value) =>
        value.HasValue ? $"$ {value.Value:N0}" : "—";

    private static string MoneySigned(double? value) =>
        value.HasValue ? $"$ {value.Value:+0;-0;0}" : "—";

    private static void EnsurePdfFonts()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "PDF export is supported by the Windows desktop application.");

        if (Interlocked.Exchange(ref _pdfFontsInitialized, 1) == 0)
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }
}
