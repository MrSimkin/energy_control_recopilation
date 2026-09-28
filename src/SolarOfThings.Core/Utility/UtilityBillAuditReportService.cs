using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using SolarOfThings.Core.SolarOfThings;

namespace SolarOfThings.Core.Utility;

/// <summary>
/// Bill-first audit report. This intentionally remains separate from the
/// arbitrary reading-pair comparison report.
/// </summary>
public sealed class UtilityBillAuditReportService
{
    private readonly UtilityMeterRepository _repository;
    private readonly UtilityReconciliationService _reconciliation;
    private readonly TariffBillRateVerificationService _tariffVerification;
    private static int _pdfFontsInitialized;

    public UtilityBillAuditReportService(
        UtilityMeterRepository repository,
        UtilityReconciliationService reconciliation,
        TariffBillRateVerificationService tariffVerification)
    {
        _repository = repository;
        _reconciliation = reconciliation;
        _tariffVerification = tariffVerification;
    }

    public void ExportPdf(
        string path,
        string deviceId,
        long billId,
        string timeZoneId,
        string languageCode)
    {
        EnsurePdfFonts();

        var bill = _repository.GetBills()
            .SingleOrDefault(item => item.BillId == billId)
            ?? throw new InvalidOperationException("Bill was not found.");

        if (!bill.FromReadingId.HasValue || !bill.ToReadingId.HasValue)
        {
            throw new InvalidOperationException(
                "The bill must be linked to its official start/end readings before audit export.");
        }

        var from = _repository.GetReading(bill.FromReadingId.Value)
            ?? throw new InvalidOperationException("Bill start reading was not found.");
        var to = _repository.GetReading(bill.ToReadingId.Value)
            ?? throw new InvalidOperationException("Bill end reading was not found.");

        var energy = _reconciliation.ReconcileReadings(
            deviceId,
            from,
            to);

        var spanish = languageCode.StartsWith(
            "es",
            StringComparison.OrdinalIgnoreCase);
        string L(string es, string en) => spanish ? es : en;

        var document = new Document();
        document.Info.Title = L(
            "Auditoría de boleta Enel",
            "Enel bill audit");

        var normal = document.Styles["Normal"];
        normal.Font.Name = "Arial";
        normal.Font.Size = 9;

        var section = document.AddSection();
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.35);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.45);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.5);

        AddHeading(section, document.Info.Title, 18);
        var subtitle = section.AddParagraph(
            L(
                "Documento técnico: evidencia de boleta, lecturas oficiales y contraste independiente con Solar of Things.",
                "Technical document: bill evidence, official readings and independent Solar of Things comparison."));
        subtitle.Format.Font.Color = Colors.DimGray;
        subtitle.Format.SpaceAfter = Unit.FromPoint(10);

        var identity = section.AddTable();
        identity.Borders.Width = 0.3;
        identity.AddColumn(Unit.FromCentimeter(5.2));
        identity.AddColumn(Unit.FromCentimeter(11.5));
        AddDefinitionRow(identity, L("Referencia", "Reference"),
            string.IsNullOrWhiteSpace(bill.InvoiceReference) ? "—" : bill.InvoiceReference);
        AddDefinitionRow(identity, L("Tarifa impresa", "Printed tariff"),
            string.IsNullOrWhiteSpace(bill.TariffPlan) ? "—" : bill.TariffPlan);
        AddDefinitionRow(identity, L("Consumo facturado", "Billed consumption"),
            bill.BilledConsumptionKwh.HasValue ? $"{bill.BilledConsumptionKwh.Value:N3} kWh" : "—");
        AddDefinitionRow(identity, L("Total a pagar", "Total due"),
            Money(bill.TotalDueClp ?? bill.AmountClp));

        AddHeading(section, L("Intervalo oficial", "Official interval"), 13);

        var fromLocal = SolarApiTime.ConvertToLocalTime(from.ReadingAtUtc, timeZoneId);
        var toLocal = SolarApiTime.ConvertToLocalTime(to.ReadingAtUtc, timeZoneId);

        var boundaryText = section.AddParagraph(
            $"{ReadingBoundary(from, fromLocal, spanish)}  →  {ReadingBoundary(to, toLocal, spanish)}");
        boundaryText.Format.Font.Size = 12;
        boundaryText.Format.Font.Bold = true;

        var convention = section.AddParagraph(
            L(
                "Convención Enel: una lectura informada sólo con fecha se trata como límite de período. El inicio de la fecha X equivale operacionalmente al cierre del día X−1; no se presenta 00:00 como una hora medida por Enel.",
                "Enel convention: a date-only utility reading is treated as a period boundary. The start of date X is operationally equivalent to the end of day X−1; 00:00 is not presented as a measured utility time."));
        convention.Format.Font.Size = 8.5;
        convention.Format.Font.Color = Colors.DimGray;
        convention.Format.SpaceAfter = Unit.FromPoint(8);

        var cards = section.AddTable();
        cards.Borders.Width = 0;
        for (var i = 0; i < 4; i++)
            cards.AddColumn(Unit.FromCentimeter(4.05));
        var row = cards.AddRow();
        AddCard(row.Cells[0], L("BOLETA", "BILL"),
            bill.BilledConsumptionKwh.HasValue ? $"{bill.BilledConsumptionKwh.Value:N3} kWh" : "—",
            L("Consumo impreso", "Printed consumption"));
        AddCard(row.Cells[1], L("MEDIDOR", "METER"),
            energy.MeterConsumptionKwh.HasValue ? $"{energy.MeterConsumptionKwh.Value:N3} kWh" : "—",
            L("Diferencia de lecturas", "Reading delta"));
        AddCard(row.Cells[2], "SOLAR OF THINGS",
            $"{energy.InverterGridImportKwh:N3} kWh",
            L("Importación de red", "Grid import"));
        AddCard(row.Cells[3], L("COBERTURA", "COVERAGE"),
            $"{energy.CoveragePercent:N1}%",
            L("No es confianza metrológica", "Not metrological confidence"));

        var differenceText = section.AddParagraph();
        differenceText.Format.SpaceBefore = Unit.FromPoint(8);
        differenceText.AddFormattedText(
            L("Contraste físico: ", "Physical comparison: "),
            TextFormat.Bold);
        differenceText.AddText(
            energy.SignedDifferenceKwh.HasValue
                ? $"{energy.SignedDifferenceKwh.Value:+0.000;-0.000;0.000} kWh"
                : "—");
        if (energy.DifferencePercent.HasValue)
            differenceText.AddText($" ({energy.DifferencePercent.Value:N2}%)");

        if (energy.Sensitivity is not null)
        {
            AddCallout(
                section,
                L("SENSIBILIDAD DE DATOS", "DATA SENSITIVITY"),
                SensitivityText(
                    energy.Sensitivity,
                    spanish),
                Colors.AliceBlue);
        }

        AddCallout(
            section,
            L("INTERPRETACIÓN", "INTERPRETATION"),
            L(
                "Una diferencia entre el medidor certificado y la telemetría del inversor no demuestra por sí sola un error de facturación. Deben considerarse cobertura, límites temporales, sensibilidad de integración, incertidumbre sustentable y la tarifa oficial aplicable.",
                "A difference between the certified meter and inverter telemetry does not by itself prove a billing error. Coverage, time boundaries, integration sensitivity, defensible uncertainty and the applicable official tariff must be considered."),
            Colors.LemonChiffon);

        AddHeading(section, L("Cargos reales de la boleta", "Actual bill charges"), 13);
        AddActualBillLines(section, bill, spanish);

        AddHeading(section, L("Reconstrucción tarifaria", "Tariff reconstruction"), 13);
        var tariffVerifications =
            _tariffVerification.VerifyBill(
                bill.BillId,
                timeZoneId);
        AddTariffVerification(
            section,
            tariffVerifications,
            spanish);

        AddHeading(section, L("Trazabilidad de lecturas", "Reading traceability"), 13);
        var readings = section.AddTable();
        readings.Borders.Width = 0.25;
        readings.AddColumn(Unit.FromCentimeter(2.4));
        readings.AddColumn(Unit.FromCentimeter(4.2));
        readings.AddColumn(Unit.FromCentimeter(3.0));
        readings.AddColumn(Unit.FromCentimeter(7.1));
        var rh = readings.AddRow();
        rh.Format.Font.Bold = true;
        rh.Cells[0].AddParagraph(L("Extremo", "Boundary"));
        rh.Cells[1].AddParagraph(L("Fecha", "Date"));
        rh.Cells[2].AddParagraph("kWh");
        rh.Cells[3].AddParagraph(L("Evidencia", "Evidence"));
        AddReadingRow(readings, L("Inicial", "Start"), from, fromLocal, spanish);
        AddReadingRow(readings, L("Final", "End"), to, toLocal, spanish);

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.AddText("Solar Energy Monitor · ");
        footer.AddPageField();
        footer.AddText("/");
        footer.AddNumPagesField();

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(path);
    }

    private static void AddTariffVerification(
        Section section,
        IReadOnlyList<BillLineTariffVerification> verifications,
        bool spanish)
    {
        string L(string es, string en) => spanish ? es : en;

        if (verifications.Count == 0)
        {
            AddCallout(
                section,
                L("ESTADO DE EVIDENCIA", "EVIDENCE STATUS"),
                L(
                    "No hay líneas de boleta guardadas para contrastar con tasas oficiales.",
                    "No stored bill lines are available for official-rate verification."),
                Colors.WhiteSmoke);
            return;
        }

        var table = section.AddTable();
        table.Borders.Width = 0.25;
        table.AddColumn(Unit.FromCentimeter(4.4));
        table.AddColumn(Unit.FromCentimeter(2.0));
        table.AddColumn(Unit.FromCentimeter(3.8));
        table.AddColumn(Unit.FromCentimeter(2.0));
        table.AddColumn(Unit.FromCentimeter(2.2));
        table.AddColumn(Unit.FromCentimeter(2.3));

        var header = table.AddRow();
        header.Format.Font.Bold = true;
        header.Cells[0].AddParagraph(L("Línea", "Line"));
        header.Cells[1].AddParagraph(L("Tasa impresa", "Printed rate"));
        header.Cells[2].AddParagraph(L("Verificación", "Verification"));
        header.Cells[3].AddParagraph(L("Fuente", "Source"));
        header.Cells[4].AddParagraph(L("Reconstruido", "Reconstructed"));
        header.Cells[5].AddParagraph(L("Dif. monto", "Amount diff."));

        foreach (var item in verifications)
        {
            var row = table.AddRow();
            row.Cells[0].AddParagraph(item.Description);
            row.Cells[1].AddParagraph(
                item.PrintedUnitRateClp.HasValue
                    ? $"$ {item.PrintedUnitRateClp.Value:N3}"
                    : "—");
            row.Cells[2].AddParagraph(
                VerificationStatusLabel(
                    item.Status,
                    spanish));
            row.Cells[3].AddParagraph(
                item.Publications.Count == 0
                    ? "—"
                    : string.Join(
                        " · ",
                        item.Publications.Select(
                            publication =>
                                FormatTariffPublicationEvidence(
                                    publication,
                                    spanish))));
            row.Cells[4].AddParagraph(
                item.ReconstructedAmountClp.HasValue
                    ? $"$ {item.ReconstructedAmountClp.Value:N0}"
                    : "—");
            row.Cells[5].AddParagraph(
                item.AmountDifferenceClp.HasValue
                    ? $"$ {item.AmountDifferenceClp.Value:+0;-0;0}"
                    : "—");
        }

        var verified = verifications.Count(item =>
            item.Status.StartsWith(
                "VERIFIED_",
                StringComparison.Ordinal) ||
            item.Status.StartsWith(
                "RATE_VERIFIED_",
                StringComparison.Ordinal));
        var pending = verifications.Count - verified;

        AddCallout(
            section,
            L("ESTADO DE EVIDENCIA", "EVIDENCE STATUS"),
            string.Format(
                L(
                    "{0} línea(s) con tasa encontrada en fuente oficial; {1} línea(s) permanecen pendientes, ambiguas o sólo como evidencia real. Una coincidencia de tasa no identifica por sí sola comuna/RED/ETR.",
                    "{0} line(s) have a rate found in official source evidence; {1} line(s) remain pending, ambiguous or actual-only. A rate match alone does not identify commune/RED/ETR."),
                verified,
                pending),
            verified > 0
                ? Colors.Honeydew
                : Colors.AliceBlue);

        foreach (var item in verifications.Where(item =>
                     !string.IsNullOrWhiteSpace(item.Detail)))
        {
            var detail = section.AddParagraph(
                $"• {item.Description}: {item.Detail}");
            detail.Format.Font.Size = 7.5;
            detail.Format.Font.Color = Colors.DimGray;
            detail.Format.SpaceAfter = Unit.FromPoint(1);
        }
    }

    private static string FormatTariffPublicationEvidence(
        BillTariffPublicationEvidence publication,
        bool spanish)
    {
        var effective = publication.EffectiveFrom.HasValue
            ? publication.EffectiveFrom.Value.ToString("yyyy-MM")
            : (spanish ? "sin fecha" : "no date");
        var revision = publication.IsRetroactive
            ? (spanish ? "retroactiva" : "retroactive")
            : (spanish ? "normal" : "standard");

        return $"{effective} · {revision} · {publication.Title}";
    }

    private static string VerificationStatusLabel(
        string status,
        bool spanish) =>
        status switch
        {
            "VERIFIED_RECONSTRUCTED_SOURCE_UNIQUE" =>
                spanish ? "Tasa verificada · reconstruida" : "Rate verified · reconstructed",
            "VERIFIED_RECONSTRUCTED_APPLICABILITY_AMBIGUOUS" =>
                spanish ? "Tasa verificada · aplicabilidad ambigua" : "Rate verified · applicability ambiguous",
            "RATE_VERIFIED_SOURCE_UNIQUE" =>
                spanish ? "Tasa verificada" : "Rate verified",
            "RATE_VERIFIED_APPLICABILITY_AMBIGUOUS" =>
                spanish ? "Tasa verificada · aplicabilidad ambigua" : "Rate verified · applicability ambiguous",
            "ACTUAL_ONLY_UNMAPPED" =>
                spanish ? "Sólo evidencia real" : "Actual-only evidence",
            "ACTUAL_ONLY_NO_UNIT_RATE" =>
                spanish ? "Sin tasa impresa" : "No printed rate",
            "MISSING_TARIFF_SOURCE" =>
                spanish ? "Falta fuente tarifaria" : "Missing tariff source",
            "TARIFF_VERSION_AMBIGUOUS" =>
                spanish ? "Versión tarifaria ambigua" : "Ambiguous tariff version",
            "TARIFF_NOT_NORMALIZED" =>
                spanish ? "Fuente aún no normalizada" : "Source not normalized",
            "MISSING_COMPONENT_SOURCE" =>
                spanish ? "Componente no extraído" : "Component not extracted",
            "RATE_NOT_FOUND" =>
                spanish ? "Tasa no encontrada" : "Rate not found",
            "TARIFF_INTERVAL_INVALID" =>
                spanish ? "Intervalo inválido" : "Invalid interval",
            _ => status
        };

    private void AddActualBillLines(
        Section section,
        UtilityBillRecord bill,
        bool spanish)
    {
        string L(string es, string en) => spanish ? es : en;
        var lines = _repository.GetBillLines(bill.BillId);

        var summary = section.AddTable();
        summary.Borders.Width = 0.25;
        summary.AddColumn(Unit.FromCentimeter(5.2));
        summary.AddColumn(Unit.FromCentimeter(11.5));
        AddDefinitionRow(summary, L("Afecto informado", "Reported taxable"), Money(bill.TaxableAmountClp));
        AddDefinitionRow(summary, "IVA", Money(bill.IvaClp));
        AddDefinitionRow(summary, L("Exento informado", "Reported exempt"), Money(bill.ExemptAmountClp));
        AddDefinitionRow(summary, L("Total boleta", "Gross bill"), Money(bill.GrossBillAmountClp));
        AddDefinitionRow(summary, L("Otros cargos/ajustes", "Other charges/adjustments"), MoneySigned(bill.OtherChargesClp));
        AddDefinitionRow(summary, L("Total a pagar", "Total due"), Money(bill.TotalDueClp ?? bill.AmountClp));

        if (lines.Count == 0)
        {
            var p = section.AddParagraph(
                L(
                    "No hay líneas de cargo guardadas para esta boleta.",
                    "No charge lines are stored for this bill."));
            p.Format.Font.Color = Colors.DimGray;
            return;
        }

        var table = section.AddTable();
        table.Borders.Width = 0.25;
        table.AddColumn(Unit.FromCentimeter(2.8));
        table.AddColumn(Unit.FromCentimeter(7.2));
        table.AddColumn(Unit.FromCentimeter(2.1));
        table.AddColumn(Unit.FromCentimeter(2.1));
        table.AddColumn(Unit.FromCentimeter(2.5));

        var h = table.AddRow();
        h.Format.Font.Bold = true;
        h.Cells[0].AddParagraph(L("Sección", "Section"));
        h.Cells[1].AddParagraph(L("Descripción", "Description"));
        h.Cells[2].AddParagraph(L("Cantidad", "Quantity"));
        h.Cells[3].AddParagraph(L("Precio", "Rate"));
        h.Cells[4].AddParagraph(L("Monto real", "Actual"));

        foreach (var line in lines)
        {
            var r = table.AddRow();
            r.Cells[0].AddParagraph(line.SectionKey);
            r.Cells[1].AddParagraph(line.Description);
            r.Cells[2].AddParagraph(
                line.Quantity.HasValue
                    ? $"{line.Quantity.Value:N3} {line.Unit}".Trim()
                    : "—");
            r.Cells[3].AddParagraph(
                line.UnitRateClp.HasValue
                    ? $"$ {line.UnitRateClp.Value:N3}"
                    : "—");
            r.Cells[4].AddParagraph(MoneySigned(line.AmountClp));
        }
    }

    private static void AddReadingRow(
        Table table,
        string label,
        UtilityMeterReading reading,
        DateTimeOffset local,
        bool spanish)
    {
        var r = table.AddRow();
        r.Cells[0].AddParagraph(label);
        r.Cells[1].AddParagraph(
            reading.TimePrecision == UtilityTimePrecision.DateOnly
                ? $"{local:dd-MM-yyyy} · {(spanish ? "límite Enel" : "Enel boundary")}"
                : $"{local:dd-MM-yyyy HH:mm:ss}");
        r.Cells[2].AddParagraph($"{reading.ReadingKwh:N3}");
        r.Cells[3].AddParagraph(
            reading.TimePrecision == UtilityTimePrecision.DateOnly
                ? (spanish
                    ? "Fecha oficial; hora exacta no informada"
                    : "Official date; exact time not supplied")
                : (spanish ? "Lectura con hora exacta" : "Exact-time reading"));
    }

    private static string ReadingBoundary(
        UtilityMeterReading reading,
        DateTimeOffset local,
        bool spanish) =>
        reading.TimePrecision == UtilityTimePrecision.DateOnly
            ? $"{local:dd-MM-yyyy} · {(spanish ? "límite de fecha Enel" : "Enel date boundary")}"
            : $"{local:dd-MM-yyyy HH:mm:ss}";

    private static string SensitivityText(
        UtilitySensitivityRange sensitivity,
        bool spanish)
    {
        var range = sensitivity.UpperKwh.HasValue
            ? $"{sensitivity.LowerKwh:N3}–{sensitivity.UpperKwh.Value:N3} kWh"
            : $"≥ {sensitivity.LowerKwh:N3} kWh; " +
              (spanish
                  ? "límite superior no cuantificable"
                  : "upper limit cannot be quantified");

        var method = sensitivity.Basis switch
        {
            "GAPS_OBSERVED_MAX_PLUS_ENEL_BOUNDARY" =>
                spanish
                    ? "Se prueba el efecto de horas sin telemetría usando como escenario la máxima importación observada y, además, el límite Enel alternativo de un minuto."
                    : "The effect of uncovered telemetry hours is tested using observed maximum import as the scenario, plus the one-minute Enel boundary alternative.",
            "GAPS_OBSERVED_MAX" =>
                spanish
                    ? "Se prueba el efecto de horas sin telemetría usando como escenario la máxima importación observada."
                    : "The effect of uncovered telemetry hours is tested using observed maximum import as the scenario.",
            "ENEL_ONE_MINUTE_BOUNDARY" =>
                spanish
                    ? "Se prueba el límite Enel alternativo de un minuto (inicio de X versus cierre de X−1)."
                    : "The one-minute Enel boundary alternative is tested (start of X versus end of X−1).",
            _ =>
                spanish
                    ? "El intervalo observado no agrega sensibilidad por gaps o límite temporal."
                    : "The observed interval adds no gap or boundary sensitivity."
        };

        return spanish
            ? $"{range}. {method} No es un intervalo de confianza; la incertidumbre de sensor/calibración no se cuantifica sin evidencia metrológica."
            : $"{range}. {method} This is not a confidence interval; sensor/calibration uncertainty is not quantified without metrological evidence.";
    }

    private static void AddHeading(Section section, string text, double size)
    {
        var p = section.AddParagraph(text);
        p.Format.Font.Size = size;
        p.Format.Font.Bold = true;
        p.Format.SpaceBefore = Unit.FromPoint(9);
        p.Format.SpaceAfter = Unit.FromPoint(5);
    }

    private static void AddDefinitionRow(Table table, string label, string value)
    {
        var r = table.AddRow();
        r.Cells[0].AddParagraph(label);
        r.Cells[0].Format.Font.Bold = true;
        r.Cells[1].AddParagraph(value);
    }

    private static void AddCard(Cell cell, string label, string value, string context)
    {
        cell.Borders.Width = 0.35;
        cell.Borders.Color = Colors.LightGray;
        cell.Shading.Color = Colors.AliceBlue;
        cell.Format.Alignment = ParagraphAlignment.Center;
        var a = cell.AddParagraph(label);
        a.Format.Font.Bold = true;
        a.Format.Font.Size = 7.5;
        var b = cell.AddParagraph(value);
        b.Format.Font.Bold = true;
        b.Format.Font.Size = 13;
        var c = cell.AddParagraph(context);
        c.Format.Font.Size = 7;
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
        var h = cell.AddParagraph(title);
        h.Format.Font.Bold = true;
        h.Format.Font.Size = 8;
        var p = cell.AddParagraph(body);
        p.Format.Font.Size = 9;
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
