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
        {
            throw new InvalidOperationException(
                "The selected reading interval is invalid.");
        }

        var spanish = languageCode.StartsWith(
            "es",
            StringComparison.OrdinalIgnoreCase);
        string L(string es, string en) => spanish ? es : en;

        var document = new Document();
        document.Info.Title = L(
            "Comparación de lecturas - Medidor vs Solar of Things",
            "Reading comparison - Meter vs Solar of Things");

        var normal = document.Styles["Normal"];
        normal.Font.Name = "Arial";
        normal.Font.Size = 9;

        var section = document.AddSection();
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.35);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.45);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.5);

        AddTitle(section, document.Info.Title);
        AddSubtitle(
            section,
            L(
                "Comparación física de dos lecturas seleccionadas. No es una auditoría de boleta.",
                "Physical comparison of two selected readings. This is not a bill audit."));

        var localFrom = SolarApiTime.ConvertToLocalTime(
            result.FromUtc,
            timeZoneId);
        var localTo = SolarApiTime.ConvertToLocalTime(
            result.ToUtc,
            timeZoneId);

        var rangeBox = section.AddTable();
        rangeBox.Borders.Width = 0.35;
        rangeBox.Borders.Color = Colors.LightGray;
        rangeBox.Shading.Color = Colors.WhiteSmoke;
        rangeBox.AddColumn(Unit.FromCentimeter(16.7));
        var rangeCell = rangeBox.AddRow().Cells[0];
        var rangeTitle = rangeCell.AddParagraph(
            L("RANGO EVALUADO", "EVALUATED RANGE"));
        rangeTitle.Format.Font.Bold = true;
        rangeTitle.Format.Font.Size = 8;

        var rangeValue = rangeCell.AddParagraph(
            $"{localFrom:dd-MM-yyyy HH:mm:ss}  →  {localTo:dd-MM-yyyy HH:mm:ss}");
        rangeValue.Format.Font.Bold = true;
        rangeValue.Format.Font.Size = 12;

        var rangeContext = rangeCell.AddParagraph(
            $"{L("Zona horaria", "Time zone")}: {timeZoneId} · " +
            $"{L("Base temporal", "Time basis")}: {TimeBasisLabel(result.TimeBasis, spanish)}");
        rangeContext.Format.Font.Size = 8;
        rangeContext.Format.Font.Color = Colors.DimGray;

        var cardsTable = section.AddTable();
        cardsTable.Borders.Width = 0;
        cardsTable.AddColumn(Unit.FromCentimeter(4.05));
        cardsTable.AddColumn(Unit.FromCentimeter(4.05));
        cardsTable.AddColumn(Unit.FromCentimeter(4.05));
        cardsTable.AddColumn(Unit.FromCentimeter(4.05));
        var cards = cardsTable.AddRow();

        AddCard(
            cards.Cells[0],
            L("MEDIDOR ENEL", "UTILITY METER"),
            result.MeterConsumptionKwh.HasValue
                ? $"{result.MeterConsumptionKwh.Value:N3} kWh"
                : "—",
            L(
                "Diferencia entre lectura final e inicial",
                "Difference between final and initial readings"));
        AddCard(
            cards.Cells[1],
            "SOLAR OF THINGS",
            $"{result.InverterGridImportKwh:N3} kWh",
            L(
                "Importación total desde la red en el mismo intervalo",
                "Total grid import over the same interval"));
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
            QualityLabel(result.Quality, spanish));

        var interpretation = BuildInterpretation(
            result,
            spanish);
        var warning =
            result.CoveragePercent < 98.0 ||
            !string.Equals(
                result.TimeBasis,
                "EXACT",
                StringComparison.Ordinal);

        AddCallout(
            section,
            warning
                ? L("LECTURA DEL RESULTADO", "HOW TO READ THIS RESULT")
                : L("RESULTADO", "RESULT"),
            interpretation,
            warning
                ? Colors.LightYellow
                : Colors.Honeydew);

        if (warning)
        {
            var warningText = new List<string>();
            if (result.CoveragePercent < 98.0)
            {
                warningText.Add(
                    string.Format(
                        L(
                            "Solar of Things cubre {0:N1}% del intervalo; el faltante NO se extrapola.",
                            "Solar of Things covers {0:N1}% of the interval; missing data is NOT extrapolated."),
                        result.CoveragePercent));
            }

            if (!string.Equals(
                    result.TimeBasis,
                    "EXACT",
                    StringComparison.Ordinal))
            {
                warningText.Add(
                    L(
                        "Al menos una lectura de Enel sólo trae fecha. Se interpreta como límite de período; el inicio de la fecha X equivale operacionalmente al cierre de X−1. Enel no informó una hora exacta.",
                        "At least one utility reading provides only a date. It is interpreted as a period boundary; the start of date X is operationally equivalent to the end of X−1. The utility did not supply an exact time."));
            }

            AddCallout(
                section,
                L("CAUTELAS IMPORTANTES", "IMPORTANT CAVEATS"),
                string.Join(" ", warningText),
                Colors.LemonChiffon);
        }

        var compareHeading = section.AddParagraph(
            L("Qué se está comparando", "What is being compared"));
        compareHeading.Format.Font.Size = 12;
        compareHeading.Format.Font.Bold = true;
        compareHeading.Format.SpaceBefore = Unit.FromPoint(10);
        compareHeading.Format.SpaceAfter = Unit.FromPoint(4);

        var compare = section.AddTable();
        compare.Borders.Width = 0.25;
        compare.AddColumn(Unit.FromCentimeter(4.1));
        compare.AddColumn(Unit.FromCentimeter(12.6));

        AddDefinitionRow(
            compare,
            L("Medidor Enel", "Utility meter"),
            L(
                "Consumo calculado por diferencia entre dos lecturas acumuladas del medidor.",
                "Consumption calculated from the difference between two cumulative meter readings."));
        AddDefinitionRow(
            compare,
            "Solar of Things",
            L(
                "Importación total desde Enel integrada por la app para exactamente el mismo intervalo. No es la métrica Enel → Casa.",
                "Total import from the utility integrated by the app over exactly the same interval. It is not the Utility → Home metric."));
        AddDefinitionRow(
            compare,
            L("Diferencia", "Difference"),
            L(
                "Solar of Things − Medidor Enel. Negativo = Solar of Things registró menos energía que el medidor; positivo = registró más.",
                "Solar of Things − utility meter. Negative = Solar of Things recorded less energy than the meter; positive = it recorded more."));

        var page2 = section.AddParagraph(
            L(
                "Evidencia, trazabilidad y metodología",
                "Evidence, traceability and methodology"));
        page2.Format.PageBreakBefore = true;
        page2.Format.Font.Size = 17;
        page2.Format.Font.Bold = true;
        page2.Format.SpaceAfter = Unit.FromPoint(8);

        var generated = section.AddParagraph();
        generated.AddFormattedText(
            L("Generado: ", "Generated: "),
            TextFormat.Bold);
        generated.AddText(
            SolarApiTime.ConvertToLocalTime(
                DateTimeOffset.UtcNow,
                timeZoneId)
            .ToString("dd-MM-yyyy HH:mm:ss"));

        AddReadingEvidence(
            section,
            from,
            to,
            timeZoneId,
            spanish);

        var qualityHeading = section.AddParagraph(
            L("Calidad de la comparación", "Comparison quality"));
        qualityHeading.Format.Font.Size = 12;
        qualityHeading.Format.Font.Bold = true;
        qualityHeading.Format.SpaceBefore = Unit.FromPoint(10);
        qualityHeading.Format.SpaceAfter = Unit.FromPoint(4);

        var qualityTable = section.AddTable();
        qualityTable.Borders.Width = 0.25;
        qualityTable.AddColumn(Unit.FromCentimeter(5.1));
        qualityTable.AddColumn(Unit.FromCentimeter(11.6));
        AddDefinitionRow(
            qualityTable,
            L("Cobertura Solar of Things", "Solar of Things coverage"),
            $"{result.CoveragePercent:N1}%");
        AddDefinitionRow(
            qualityTable,
            L("Base horaria", "Time basis"),
            TimeBasisLabel(result.TimeBasis, spanish));
        AddDefinitionRow(
            qualityTable,
            L("Evaluación", "Assessment"),
            QualityLabel(result.Quality, spanish));

        var methodHeading = section.AddParagraph(
            L("Metodología y límites", "Methodology and limits"));
        methodHeading.Format.Font.Size = 12;
        methodHeading.Format.Font.Bold = true;
        methodHeading.Format.SpaceBefore = Unit.FromPoint(10);
        methodHeading.Format.SpaceAfter = Unit.FromPoint(4);

        var bullets = new[]
        {
            L(
                "El consumo del medidor es lectura final − lectura inicial.",
                "Meter consumption is final reading − initial reading."),
            L(
                "Solar of Things usa importación total de red; no usa la atribución analítica Enel → Casa.",
                "Solar of Things uses total grid import; it does not use the analytical Utility → Home attribution."),
            L(
                "Los períodos faltantes de telemetría no se rellenan ni extrapolan; se informa la cobertura observada.",
                "Missing telemetry intervals are not filled or extrapolated; observed coverage is reported."),
            L(
                "Cuando Enel informa sólo una fecha, la app la trata como límite de período. No presenta una hora exacta como evidencia oficial.",
                "When the utility supplies only a date, the app treats it as a period boundary. It does not present an exact clock time as official evidence."),
            L(
                "Una discrepancia no demuestra por sí sola un error de facturación: antes deben revisarse cobertura, límites horarios, tarifa aplicada y la boleta vinculada.",
                "A discrepancy alone does not prove a billing error: coverage, time boundaries, tariff application and the linked bill must be reviewed first."),
            L(
                "La telemetría del inversor es evidencia técnica independiente de contraste y no sustituye al medidor certificado de la distribuidora.",
                "Inverter telemetry is independent technical comparison evidence and does not replace the distributor's certified meter.")
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

    private static void AddTitle(
        Section section,
        string title)
    {
        var paragraph = section.AddParagraph(title);
        paragraph.Format.Font.Size = 18;
        paragraph.Format.Font.Bold = true;
        paragraph.Format.SpaceAfter = Unit.FromPoint(3);
    }

    private static void AddSubtitle(
        Section section,
        string text)
    {
        var paragraph = section.AddParagraph(text);
        paragraph.Format.Font.Size = 9;
        paragraph.Format.Font.Color = Colors.DimGray;
        paragraph.Format.SpaceAfter = Unit.FromPoint(9);
    }

    private static void AddCallout(
        Section section,
        string title,
        string body,
        Color background)
    {
        var table = section.AddTable();
        table.Borders.Width = 0.35;
        table.Borders.Color = Colors.LightGray;
        table.AddColumn(Unit.FromCentimeter(16.7));
        var cell = table.AddRow().Cells[0];
        cell.Shading.Color = background;

        var heading = cell.AddParagraph(title);
        heading.Format.Font.Size = 8;
        heading.Format.Font.Bold = true;
        heading.Format.SpaceBefore = Unit.FromPoint(3);

        var text = cell.AddParagraph(body);
        text.Format.Font.Size = 9;
        text.Format.SpaceAfter = Unit.FromPoint(4);
    }

    private static string BuildInterpretation(
        UtilityMeterReconciliation result,
        bool spanish)
    {
        if (!result.MeterConsumptionKwh.HasValue ||
            !result.SignedDifferenceKwh.HasValue)
        {
            return spanish
                ? "No hay evidencia suficiente para calcular una diferencia de consumo válida."
                : "There is not enough evidence to calculate a valid consumption difference.";
        }

        var difference = result.SignedDifferenceKwh.Value;
        var magnitude = Math.Abs(difference);
        var percent = result.DifferencePercent;

        if (Math.Abs(difference) < 0.005)
        {
            return spanish
                ? "Ambas mediciones coinciden prácticamente en este intervalo."
                : "Both measurements are practically equal over this interval.";
        }

        if (difference < 0)
        {
            return spanish
                ? $"Solar of Things registró {magnitude:N3} kWh menos que el medidor Enel" +
                  (percent.HasValue ? $" ({percent.Value:N2}%)." : ".")
                : $"Solar of Things recorded {magnitude:N3} kWh less than the utility meter" +
                  (percent.HasValue ? $" ({percent.Value:N2}%)." : ".");
        }

        return spanish
            ? $"Solar of Things registró {magnitude:N3} kWh más que el medidor Enel" +
              (percent.HasValue ? $" ({percent.Value:N2}%)." : ".")
            : $"Solar of Things recorded {magnitude:N3} kWh more than the utility meter" +
              (percent.HasValue ? $" ({percent.Value:N2}%)." : ".");
    }

    private static void AddReadingEvidence(
        Section section,
        UtilityMeterReading from,
        UtilityMeterReading to,
        string timeZoneId,
        bool spanish)
    {
        var heading = section.AddParagraph(
            spanish
                ? "Lecturas utilizadas"
                : "Readings used");
        heading.Format.Font.Size = 12;
        heading.Format.Font.Bold = true;
        heading.Format.SpaceBefore = Unit.FromPoint(7);
        heading.Format.SpaceAfter = Unit.FromPoint(4);

        var table = section.AddTable();
        table.Borders.Width = 0.25;
        table.AddColumn(Unit.FromCentimeter(1.7));
        table.AddColumn(Unit.FromCentimeter(3.0));
        table.AddColumn(Unit.FromCentimeter(3.7));
        table.AddColumn(Unit.FromCentimeter(2.3));
        table.AddColumn(Unit.FromCentimeter(2.4));
        table.AddColumn(Unit.FromCentimeter(3.6));

        var header = table.AddRow();
        header.Format.Font.Bold = true;
        header.Cells[0].AddParagraph(spanish ? "Lectura" : "Reading");
        header.Cells[1].AddParagraph(spanish ? "Fecha" : "Date");
        header.Cells[2].AddParagraph(spanish ? "Hora / supuesto" : "Time / assumption");
        header.Cells[3].AddParagraph("kWh");
        header.Cells[4].AddParagraph(spanish ? "Origen" : "Source");
        header.Cells[5].AddParagraph(spanish ? "Referencia" : "Reference");

        AddReadingRow(
            table,
            spanish ? "Inicial" : "Start",
            from,
            timeZoneId,
            spanish);
        AddReadingRow(
            table,
            spanish ? "Final" : "End",
            to,
            timeZoneId,
            spanish);
    }

    private void AddBillEvidence(
        Section section,
        UtilityBillRecord bill,
        bool spanish)
    {
        string L(string es, string en) => spanish ? es : en;

        var heading = section.AddParagraph(
            L("Boleta vinculada", "Linked bill"));
        heading.Format.Font.Size = 12;
        heading.Format.Font.Bold = true;
        heading.Format.SpaceBefore = Unit.FromPoint(10);
        heading.Format.SpaceAfter = Unit.FromPoint(4);

        var summary = section.AddTable();
        summary.Borders.Width = 0.25;
        summary.AddColumn(Unit.FromCentimeter(5.1));
        summary.AddColumn(Unit.FromCentimeter(11.6));

        AddDefinitionRow(
            summary,
            L("Consumo facturado", "Billed consumption"),
            bill.BilledConsumptionKwh.HasValue
                ? $"{bill.BilledConsumptionKwh.Value:N3} kWh"
                : "—");
        AddDefinitionRow(
            summary,
            L("Tarifa informada", "Reported tariff"),
            string.IsNullOrWhiteSpace(bill.TariffPlan)
                ? "—"
                : bill.TariffPlan);
        AddDefinitionRow(
            summary,
            L("Monto afecto / IVA / exento", "Taxable / VAT / exempt"),
            $"{Money(bill.TaxableAmountClp)} / {Money(bill.IvaClp)} / {Money(bill.ExemptAmountClp)}");
        AddDefinitionRow(
            summary,
            L(
                "Total boleta / otros cargos / total a pagar",
                "Gross bill / other charges / total due"),
            $"{Money(bill.GrossBillAmountClp)} / {MoneySigned(bill.OtherChargesClp)} / {Money(bill.TotalDueClp ?? bill.AmountClp)}");

        var lines = _repository.GetBillLines(bill.BillId);
        if (lines.Count == 0)
        {
            return;
        }

        var linesHeading = section.AddParagraph(
            L("Detalle guardado de cargos y créditos", "Stored charge and credit detail"));
        linesHeading.Format.Font.Size = 10;
        linesHeading.Format.Font.Bold = true;
        linesHeading.Format.SpaceBefore = Unit.FromPoint(7);
        linesHeading.Format.SpaceAfter = Unit.FromPoint(3);

        var table = section.AddTable();
        table.Borders.Width = 0.25;
        table.AddColumn(Unit.FromCentimeter(2.8));
        table.AddColumn(Unit.FromCentimeter(7.4));
        table.AddColumn(Unit.FromCentimeter(2.3));
        table.AddColumn(Unit.FromCentimeter(2.1));
        table.AddColumn(Unit.FromCentimeter(2.1));

        var header = table.AddRow();
        header.Format.Font.Bold = true;
        header.Cells[0].AddParagraph(L("Sección", "Section"));
        header.Cells[1].AddParagraph(L("Descripción", "Description"));
        header.Cells[2].AddParagraph(L("Cantidad", "Quantity"));
        header.Cells[3].AddParagraph(L("Precio", "Rate"));
        header.Cells[4].AddParagraph(L("Monto", "Amount"));

        foreach (var line in lines)
        {
            var row = table.AddRow();
            row.Cells[0].AddParagraph(BillSectionLabel(line.SectionKey, spanish));
            row.Cells[1].AddParagraph(line.Description);
            row.Cells[2].AddParagraph(
                line.Quantity.HasValue
                    ? $"{line.Quantity.Value:N3} {line.Unit}".Trim()
                    : "—");
            row.Cells[3].AddParagraph(
                line.UnitRateClp.HasValue
                    ? $"$ {line.UnitRateClp.Value:N3}"
                    : "—");
            row.Cells[4].AddParagraph(
                $"$ {line.AmountClp:+0;-0;0}");
        }
    }

    private static void AddReadingRow(
        Table table,
        string label,
        UtilityMeterReading reading,
        string timeZoneId,
        bool spanish)
    {
        var local = SolarApiTime.ConvertToLocalTime(
            reading.ReadingAtUtc,
            timeZoneId);
        var row = table.AddRow();
        row.Cells[0].AddParagraph(label);
        row.Cells[1].AddParagraph($"{local:dd-MM-yyyy}");
        row.Cells[2].AddParagraph(
            reading.TimePrecision == UtilityTimePrecision.DateOnly
                ? (spanish
                    ? "Límite de fecha Enel; hora exacta no informada"
                    : "Enel date boundary; exact time not supplied")
                : $"{local:HH:mm:ss}");
        row.Cells[3].AddParagraph($"{reading.ReadingKwh:N3}");
        row.Cells[4].AddParagraph(
            reading.SourceKind == UtilityReadingSourceKind.UtilityOfficial
                ? (spanish ? "Enel oficial" : "Utility official")
                : reading.SourceKind == UtilityReadingSourceKind.Personal
                    ? (spanish ? "Personal" : "Personal")
                    : reading.SourceKind);
        row.Cells[5].AddParagraph(
            string.IsNullOrWhiteSpace(reading.Reference)
                ? "—"
                : reading.Reference);
    }

    private static string BillSectionLabel(
        string sectionKey,
        bool spanish) =>
        sectionKey switch
        {
            "SERVICIO_ELECTRICO" =>
                spanish ? "Servicio eléctrico" : "Electric service",
            "OTROS_CARGOS" =>
                spanish ? "Otros cargos" : "Other charges",
            "ACUMULADO" =>
                spanish ? "Acumulado" : "Totals",
            _ =>
                spanish ? "Otro" : "Other"
        };

    private static string TimeBasisLabel(
        string timeBasis,
        bool spanish) =>
        timeBasis switch
        {
            "EXACT" =>
                spanish ? "Hora exacta en ambas lecturas" : "Exact time for both readings",
            "DATE_ONLY_ASSUMED" =>
                spanish
                    ? "Límite de fecha Enel en al menos un extremo"
                    : "Enel date boundary at one or both endpoints",
            _ => timeBasis
        };

    private static string QualityLabel(
        string quality,
        bool spanish) =>
        quality switch
        {
            "GOOD" => spanish ? "Buena" : "Good",
            "PARTIAL" => spanish ? "Parcial" : "Partial",
            "LOW_COVERAGE" => spanish ? "Cobertura baja" : "Low coverage",
            "ASSUMED_TIME" => spanish ? "Hora asumida" : "Assumed time",
            "METER_RESET_OR_REPLACEMENT" =>
                spanish ? "Revisar medidor" : "Review meter",
            "INVALID_INTERVAL" =>
                spanish ? "Intervalo inválido" : "Invalid interval",
            _ => quality
        };

    private static void AddDefinitionRow(
        Table table,
        string label,
        string value)
    {
        var row = table.AddRow();
        row.Cells[0].AddParagraph(label);
        row.Cells[0].Format.Font.Bold = true;
        row.Cells[1].AddParagraph(value);
    }

    private static void AddCard(
        Cell cell,
        string label,
        string value,
        string context)
    {
        cell.Borders.Width = 0.35;
        cell.Borders.Color = Colors.LightGray;
        cell.Shading.Color = Colors.AliceBlue;
        cell.Format.Alignment = ParagraphAlignment.Center;
        cell.VerticalAlignment = VerticalAlignment.Center;

        var p1 = cell.AddParagraph(label);
        p1.Format.Font.Bold = true;
        p1.Format.Font.Size = 7.5;
        p1.Format.SpaceBefore = Unit.FromPoint(5);

        var p2 = cell.AddParagraph(value);
        p2.Format.Font.Bold = true;
        p2.Format.Font.Size = 14;
        p2.Format.SpaceBefore = Unit.FromPoint(1);
        p2.Format.SpaceAfter = Unit.FromPoint(2);

        var p3 = cell.AddParagraph(context);
        p3.Format.Font.Size = 7;
        p3.Format.SpaceAfter = Unit.FromPoint(5);
    }

    private static string Money(double? value) =>
        value.HasValue ? $"$ {value.Value:N0}" : "—";

    private static string MoneySigned(double? value) =>
        value.HasValue ? $"$ {value.Value:+0;-0;0}" : "—";

    private static void EnsurePdfFonts()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "PDF export is supported by the Windows desktop application.");
        }

        if (Interlocked.Exchange(ref _pdfFontsInitialized, 1) == 0)
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        }
    }
}
