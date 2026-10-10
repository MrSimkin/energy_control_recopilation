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
    private readonly TariffBillRateVerificationService _tariffVerification;
    private readonly UtilityBillGapStatisticalCompletionService _billGapCompletion;
    private readonly UtilityBillTariffScenarioAnalysisService _tariffScenarioAnalysis;
    private static int _pdfFontsInitialized;

    public UtilityBillAuditReportService(
        UtilityMeterRepository repository,
        TariffBillRateVerificationService tariffVerification,
        UtilityBillGapStatisticalCompletionService billGapCompletion,
        UtilityBillTariffScenarioAnalysisService tariffScenarioAnalysis)
    {
        _repository = repository;
        _tariffVerification = tariffVerification;
        _billGapCompletion = billGapCompletion;
        _tariffScenarioAnalysis = tariffScenarioAnalysis;
    }

    public void ExportPdf(
        string path,
        string deviceId,
        long billId,
        string timeZoneId,
        string languageCode,
        string? producerIdentity = null)
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

        var billStartLocalDate =
            SolarApiTime.GetLocalDate(
                bill.PeriodStartUtc,
                timeZoneId);
        var billEndLocalDate =
            SolarApiTime.GetLocalDate(
                bill.PeriodEndUtc,
                timeZoneId);

        var billGapAnalysis =
            _billGapCompletion.Analyze(
                deviceId,
                billStartLocalDate,
                billEndLocalDate,
                timeZoneId);
        var statistical =
            billGapAnalysis.Completion;
        var tariffScenario =
            _tariffScenarioAnalysis.Analyze(
                billId,
                timeZoneId,
                statistical);

        var spanish = languageCode.StartsWith(
            "es",
            StringComparison.OrdinalIgnoreCase);
        string L(string es, string en) => spanish ? es : en;

        var document = new Document();
        document.Info.Title = L(
            "Informe técnico de revisión de consumo eléctrico facturado",
            "Technical review of billed electricity consumption");

        var normal = document.Styles["Normal"];
        normal.Font.Name = "Arial";
        normal.Font.Size = 9;

        var section = document.AddSection();
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.35);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.45);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.5);

        var fromLocal =
            SolarApiTime.ConvertToLocalTime(
                from.ReadingAtUtc,
                timeZoneId);
        var toLocal =
            SolarApiTime.ConvertToLocalTime(
                to.ReadingAtUtc,
                timeZoneId);
        var verifications =
            _tariffVerification.VerifyBill(
                billId,
                timeZoneId);

        // PAGE 1 — executive energy summary.
        AddHeading(section, document.Info.Title, 18);
        var subtitle = section.AddParagraph(
            L(
                "Documento técnico: evidencia de boleta, lecturas oficiales y contraste independiente con registros del inversor.",
                "Technical document: bill evidence, official readings and independent inverter records."));
        subtitle.Format.Font.Color = Colors.DimGray;
        subtitle.Format.SpaceAfter = Unit.FromPoint(8);

        var identity = section.AddTable();
        identity.Borders.Width = 0.3;
        identity.AddColumn(Unit.FromCentimeter(5.2));
        identity.AddColumn(Unit.FromCentimeter(11.5));
        AddDefinitionRow(
            identity,
            L("Referencia", "Reference"),
            string.IsNullOrWhiteSpace(bill.InvoiceReference)
                ? "—"
                : bill.InvoiceReference);
        AddDefinitionRow(
            identity,
            L("Período auditado", "Audited period"),
            $"{billGapAnalysis.StartLocalDate:dd-MM-yyyy} → {billGapAnalysis.EndLocalDateInclusive:dd-MM-yyyy}");
        AddDefinitionRow(
            identity,
            L("Tarifa impresa", "Printed tariff"),
            string.IsNullOrWhiteSpace(bill.TariffPlan)
                ? L("No capturada en el registro local", "Not captured in local record")
                : bill.TariffPlan);
        AddDefinitionRow(
            identity,
            L("Consumo facturado", "Billed consumption"),
            bill.BilledConsumptionKwh.HasValue
                ? $"{bill.BilledConsumptionKwh.Value:N3} kWh"
                : "—");
        AddDefinitionRow(
            identity,
            L("Total a pagar", "Total due"),
            Money(bill.TotalDueClp ?? bill.AmountClp));

        AddHeading(
            section,
            L("1. Resumen ejecutivo de la discrepancia", "1. Executive discrepancy summary"),
            13);
        AddExecutiveEnergyComparison(
            section,
            bill,
            from,
            to,
            statistical,
            spanish);
        AddExecutiveInterpretation(
            section,
            bill,
            statistical,
            billGapAnalysis,
            spanish);

        // PAGE 2 — financial comparison.
        section.AddPageBreak();
        AddHeading(
            section,
            L("2. Efecto económico de la diferencia de consumo", "2. Financial effect of the consumption difference"),
            16);
        AddFinancialScenarioComparison(
            section,
            tariffScenario,
            spanish);
        AddComparableTotalDueTable(
            section,
            bill,
            tariffScenario,
            spanish);

        // PAGE 3 — full economic decomposition.
        section.AddPageBreak();
        AddHeading(
            section,
            L("3. Descomposición económica y reconstrucción", "3. Economic decomposition and reconstruction"),
            16);
        AddActualBillLines(
            section,
            bill,
            spanish);
        AddNonVariableChargeTreatment(
            section,
            bill,
            tariffScenario,
            spanish);
        AddTariffComponentReconciliation(
            section,
            tariffScenario,
            spanish);
        AddHeading(
            section,
            L("Verificación contra fuentes oficiales", "Verification against official sources"),
            12);
        if (tariffScenario.HasTariffModel)
        {
            AddResolvedTariffEvidence(
                section,
                tariffScenario,
                spanish);
        }
        else
        {
            AddTariffVerification(
                section,
                verifications,
                spanish);
        }
        AddEconomicConclusion(
            section,
            tariffScenario,
            spanish);

        // PAGE 4 — energy evidence and early gap defense.
        section.AddPageBreak();
        AddHeading(
            section,
            L("4. Evidencia energética y tratamiento de discontinuidades", "4. Energy evidence and treatment of discontinuities"),
            16);
        AddEnergyEvidencePage(
            section,
            bill,
            statistical,
            billGapAnalysis,
            spanish);

        // PAGE 5 — hard numerical quality evidence.
        section.AddPageBreak();
        AddHeading(
            section,
            L("5. Calidad, cobertura y trazabilidad cuantitativa", "5. Numerical quality, coverage and traceability"),
            16);
        AddQualityEvidencePage(
            section,
            statistical,
            billGapAnalysis,
            spanish);

        // PAGE 6 — findings.
        section.AddPageBreak();
        AddHeading(
            section,
            L("6. Hallazgos e inconsistencias", "6. Findings and inconsistencies"),
            16);
        AddFindingsPage(
            section,
            bill,
            statistical,
            billGapAnalysis,
            tariffScenario,
            spanish);

        // PAGE 7 — sources and traceability.
        section.AddPageBreak();
        AddHeading(
            section,
            L("7. Fuentes y trazabilidad", "7. Sources and traceability"),
            16);
        AddSourcesAndTraceabilityPage(
            section,
            bill,
            from,
            to,
            fromLocal,
            toLocal,
            statistical,
            tariffScenario,
            spanish);

        // PAGE 8 — technical methodology.
        section.AddPageBreak();
        AddHeading(
            section,
            L("8. Metodología técnica y limitaciones", "8. Technical methodology and limitations"),
            16);
        AddTechnicalMethodologyPage(
            section,
            statistical,
            billGapAnalysis,
            tariffScenario,
            spanish);

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.AddText(
            L("Informe técnico", "Technical report"));
        if (!string.IsNullOrWhiteSpace(producerIdentity))
        {
            footer.AddText($" · {producerIdentity}");
        }
        footer.AddText(" · ");
        footer.AddPageField();
        footer.AddText("/");
        footer.AddNumPagesField();

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(path);
    }

    private static void AddExecutiveEnergyComparison(
        Section section,
        UtilityBillRecord bill,
        UtilityMeterReading from,
        UtilityMeterReading to,
        UtilityGridImportStatisticalCompletion statistical,
        bool spanish)
    {
        string L(string es, string en) => spanish ? es : en;
        var meterDifference =
            to.ReadingKwh >= from.ReadingKwh
                ? to.ReadingKwh - from.ReadingKwh
                : (double?)null;
        var enel =
            bill.BilledConsumptionKwh ??
            meterDifference;

        var primary = section.AddTable();
        primary.Borders.Width = 0;
        primary.AddColumn(Unit.FromCentimeter(8.15));
        primary.AddColumn(Unit.FromCentimeter(8.15));
        var primaryRow = primary.AddRow();

        AddAuditMetricCard(
            primaryRow.Cells[0],
            L("ENEL / MEDIDOR", "UTILITY / METER"),
            enel.HasValue ? $"{enel.Value:N3} kWh" : "—",
            L(
                "Valor medido/facturado por Enel · referencia externa.",
                "Measured/billed by utility · external reference."),
            Colors.LemonChiffon,
            16);

        AddAuditMetricCard(
            primaryRow.Cells[1],
            L("INVERSOR · OBSERVADO", "INVERTER · OBSERVED"),
            $"{statistical.ObservedKwh:N3} kWh",
            DifferenceContext(
                statistical.ObservedKwh,
                enel,
                L(
                    "Integración directa; sin rellenar huecos.",
                    "Direct integration; gaps unfilled.")),
            Colors.AliceBlue,
            16);

        var rangeHeading = section.AddParagraph(
            L(
                "Rango estadístico del inversor",
                "Inverter statistical range"));
        rangeHeading.Format.Font.Size = 10.5;
        rangeHeading.Format.Font.Bold = true;
        rangeHeading.Format.SpaceBefore = Unit.FromPoint(6);
        rangeHeading.Format.SpaceAfter = Unit.FromPoint(3);

        var predictive = section.AddTable();
        predictive.Borders.Width = 0;
        predictive.AddColumn(Unit.FromCentimeter(5.43));
        predictive.AddColumn(Unit.FromCentimeter(5.43));
        predictive.AddColumn(Unit.FromCentimeter(5.43));
        var predictiveRow = predictive.AddRow();

        AddAuditMetricCard(
            predictiveRow.Cells[0],
            L("P5 · MARGEN INFERIOR", "P5 · LOWER MARGIN"),
            statistical.LowerKwh.HasValue
                ? $"{statistical.LowerKwh.Value:N3} kWh"
                : "—",
            statistical.LowerKwh.HasValue
                ? DifferenceContext(
                    statistical.LowerKwh.Value,
                    enel,
                    L(
                        "Valor hacia el extremo inferior: aproximadamente 5% de los resultados del método queda por debajo.",
                        "Lower-side value: approximately 5% of method results fall below it."))
                : L("Sin evidencia suficiente", "Insufficient evidence"),
            Colors.AliceBlue,
            13);

        AddAuditMetricCard(
            predictiveRow.Cells[1],
            L("P50 · ESTIMACIÓN CENTRAL", "P50 · CENTRAL ESTIMATE"),
            statistical.MedianKwh.HasValue
                ? $"{statistical.MedianKwh.Value:N3} kWh"
                : "—",
            statistical.MedianKwh.HasValue
                ? DifferenceContext(
                    statistical.MedianKwh.Value,
                    enel,
                    L(
                        "Mediana y estimación central: aproximadamente la mitad de los resultados queda por debajo y la mitad por encima.",
                        "Median and central estimate: approximately half the results fall below and half above."))
                : L("Sin evidencia suficiente", "Insufficient evidence"),
            Colors.Honeydew,
            13);

        AddAuditMetricCard(
            predictiveRow.Cells[2],
            L("P95 · MARGEN SUPERIOR", "P95 · UPPER MARGIN"),
            statistical.UpperKwh.HasValue
                ? $"{statistical.UpperKwh.Value:N3} kWh"
                : "—",
            statistical.UpperKwh.HasValue
                ? DifferenceContext(
                    statistical.UpperKwh.Value,
                    enel,
                    L(
                        "Valor hacia el extremo superior: aproximadamente 95% de los resultados del método queda por debajo.",
                        "Upper-side value: approximately 95% of method results fall below it."))
                : L("Sin evidencia suficiente", "Insufficient evidence"),
            Colors.AliceBlue,
            13);

        AddCallout(
            section,
            L("¿QUÉ SIGNIFICAN P5, P50 Y P95?", "WHAT DO P5, P50 AND P95 MEAN?"),
            L(
                "Como existen períodos sin telemetría, no sería correcto inventar un único valor exacto. P5 es un valor hacia el extremo inferior; P50 es la mediana y estimación central; P95 es un valor hacia el extremo superior. El intervalo P5–P95 contiene el 90% central de los resultados producidos por el método estadístico aplicado. En este período P5 y P50 coinciden porque la mediana empírica del aporte de cada gap interno es 0 kWh.",
                "Because some periods lack telemetry, it would not be correct to invent one exact value. P5 is a lower-side value; P50 is the median and central estimate; P95 is an upper-side value. The P5–P95 interval contains the central 90% of the results produced by the statistical method. In this period P5 and P50 coincide because the empirical median contribution of each internal gap is 0 kWh."),
            Colors.WhiteSmoke);

        if (enel.HasValue &&
            statistical.LowerKwh.HasValue &&
            statistical.MedianKwh.HasValue &&
            statistical.UpperKwh.HasValue)
        {
            var differences = section.AddTable();
            differences.Borders.Width = 0.25;
            differences.AddColumn(Unit.FromCentimeter(4.8));
            differences.AddColumn(Unit.FromCentimeter(5.6));
            differences.AddColumn(Unit.FromCentimeter(5.9));

            var header = differences.AddRow();
            header.Format.Font.Bold = true;
            header.Cells[0].AddParagraph(L("Comparación", "Comparison"));
            header.Cells[1].AddParagraph(L("Diferencia", "Difference"));
            header.Cells[2].AddParagraph(L("Diferencia %", "Difference %"));

            AddExecutiveDifferenceRow(
                differences,
                "Enel − P5",
                enel.Value,
                statistical.LowerKwh.Value);
            AddExecutiveDifferenceRow(
                differences,
                "Enel − P50",
                enel.Value,
                statistical.MedianKwh.Value);
            AddExecutiveDifferenceRow(
                differences,
                "Enel − P95",
                enel.Value,
                statistical.UpperKwh.Value);
        }
    }

    private static void AddExecutiveDifferenceRow(
        Table table,
        string label,
        double enelKwh,
        double inverterKwh)
    {
        var difference =
            enelKwh - inverterKwh;
        var percent =
            enelKwh > 0
                ? difference / enelKwh * 100.0
                : 0;

        var row = table.AddRow();
        row.Cells[0].AddParagraph(label);
        row.Cells[0].Format.Font.Bold = true;
        row.Cells[1].AddParagraph(
            $"{difference:+0.000;-0.000;0.000} kWh");
        row.Cells[2].AddParagraph(
            $"{percent:+0.00;-0.00;0.00}%");
    }

    private static string DifferenceContext(
        double value,
        double? enel,
        string evidence)
    {
        if (!enel.HasValue)
            return evidence;

        var difference = enel.Value - value;
        var percent = enel.Value > 0
            ? difference / enel.Value * 100.0
            : 0;

        return $"Enel − escenario {difference:+0.000;-0.000;0.000} kWh " +
               $"({percent:+0.00;-0.00;0.00}%) · {evidence}";
    }

    private static void AddExecutiveInterpretation(
        Section section,
        UtilityBillRecord bill,
        UtilityGridImportStatisticalCompletion statistical,
        UtilityBillGapStatisticalAnalysis analysis,
        bool spanish)
    {
        string L(string es, string en) => spanish ? es : en;
        var enel = bill.BilledConsumptionKwh;

        if (!enel.HasValue ||
            !statistical.HasPredictiveInterval)
        {
            AddCallout(
                section,
                L("LECTURA DEL RESULTADO", "RESULT INTERPRETATION"),
                L(
                    "La comparación observada está disponible, pero todavía no hay evidencia estadística suficiente para ubicar el valor Enel dentro de un rango P5–P95.",
                    "The observed comparison is available, but statistical evidence is not yet sufficient to place the utility value inside a P5–P95 range."),
                Colors.LemonChiffon);
            return;
        }

        var lower = statistical.LowerKwh!.Value;
        var upper = statistical.UpperKwh!.Value;
        var central = statistical.MedianKwh!.Value;
        var inside =
            enel.Value >= lower &&
            enel.Value <= upper;

        var distance = inside
            ? Math.Abs(enel.Value - central)
            : enel.Value < lower
                ? lower - enel.Value
                : enel.Value - upper;

        var maxEmpirical =
            analysis.MaximumKwh;

        AddCallout(
            section,
            L("LECTURA DEL RESULTADO", "RESULT INTERPRETATION"),
            inside
                ? string.Format(
                    L(
                        "Los {0:N3} kWh de Enel caen dentro del rango P5–P95 provisional ({1:N3}–{2:N3} kWh). La estimación central es {3:N3} kWh y Enel está a {4:N3} kWh de esa mediana. Esto no prueba equivalencia metrológica: indica compatibilidad con el patrón estadístico de la telemetría disponible.",
                        "The utility value of {0:N3} kWh falls inside the provisional P5–P95 range ({1:N3}–{2:N3} kWh). The central estimate is {3:N3} kWh and the utility value is {4:N3} kWh from that median. This does not prove metrological equivalence; it indicates compatibility with the statistical pattern of available telemetry."),
                    enel.Value,
                    lower,
                    upper,
                    central,
                    distance)
                : string.Format(
                    L(
                        "Bajo la agregación provisional de gaps, Enel queda {3:N3} kWh por encima de P95 ({2:N3} kWh). La separación es pequeña. El soporte empírico completo alcanza {4}, por lo que 97 kWh no se presenta como imposible ni como estadísticamente excluido; sí se mantiene una diferencia material frente a P50 ({1:N3} kWh).",
                        "Under the provisional gap aggregation, the utility value lies {3:N3} kWh above P95 ({2:N3} kWh). The separation is small. Full empirical support reaches {4}, so 97 kWh is not presented as impossible or statistically excluded; a material difference from P50 ({1:N3} kWh) remains."),
                    enel.Value,
                    central,
                    upper,
                    distance,
                    maxEmpirical.HasValue
                        ? $"{maxEmpirical.Value:N3} kWh"
                        : "—"),
            inside
                ? Colors.Honeydew
                : Colors.LemonChiffon);
    }

    private static void AddFinancialScenarioComparison(
        Section section,
        UtilityBillTariffScenarioAnalysis analysis,
        bool spanish)
    {
        string L(string es, string en) => spanish ? es : en;

        if (!analysis.HasTariffModel ||
            !analysis.SupportedVariableRateClpPerKwh.HasValue)
        {
            AddCallout(
                section,
                L("TARIFA NO RESUELTA", "TARIFF NOT RESOLVED"),
                L(
                    "Todavía no existe evidencia suficiente para construir un escenario monetario oficial sin inventar una tasa.",
                    "There is not yet enough evidence to build an official monetary scenario without inventing a rate."),
                Colors.LemonChiffon);
            return;
        }

        var intro = section.AddParagraph(
            string.Format(
                L(
                    "Componentes respaldados: tasa variable {0:N3} CLP/kWh; cargo fijo reconstruido {1}. La misma base oficial se aplica a todos los escenarios para que la comparación sea equivalente.",
                    "Supported components: variable rate {0:N3} CLP/kWh; reconstructed fixed charge {1}. The same official basis is applied to every scenario for an equivalent comparison."),
                analysis.SupportedVariableRateClpPerKwh.Value,
                Money(
                    analysis.SupportedFixedAmountClp ??
                    0)));
        intro.Format.Font.Size = 8.5;
        intro.Format.Font.Color = Colors.DimGray;
        intro.Format.SpaceAfter = Unit.FromPoint(5);

        var enel = analysis.Scenarios.FirstOrDefault(item =>
            item.Key == "ENEL_BILLED");
        var observed = analysis.Scenarios.FirstOrDefault(item =>
            item.Key == "SOLAR_OBSERVED");

        var primary = section.AddTable();
        primary.Borders.Width = 0;
        primary.AddColumn(Unit.FromCentimeter(8.15));
        primary.AddColumn(Unit.FromCentimeter(8.15));
        var primaryRow = primary.AddRow();

        AddAuditMoneyCard(
            primaryRow.Cells[0],
            L("ENEL · SUBTOTAL MODELADO", "UTILITY · MODELED SUBTOTAL"),
            enel,
            enel,
            L(
                "Misma base tarifaria que concilia las líneas modeladas.",
                "Same tariff basis that reconciles the modeled lines."),
            Colors.LemonChiffon);

        AddAuditMoneyCard(
            primaryRow.Cells[1],
            L("INVERSOR OBSERVADO", "INVERTER OBSERVED"),
            observed,
            enel,
            L(
                "Costo contrafactual sobre la energía observada.",
                "Counterfactual cost on observed energy."),
            Colors.AliceBlue);

        var predictive = section.AddTable();
        predictive.Borders.Width = 0;
        predictive.AddColumn(Unit.FromCentimeter(5.43));
        predictive.AddColumn(Unit.FromCentimeter(5.43));
        predictive.AddColumn(Unit.FromCentimeter(5.43));
        var row = predictive.AddRow();

        AddAuditMoneyCard(
            row.Cells[0],
            L("P5 · COSTO INFERIOR", "P5 · LOWER COST"),
            analysis.Scenarios.FirstOrDefault(item =>
                item.Key == "SOLAR_LOWER"),
            enel,
            L("Margen inferior del rango central 90%.", "Lower edge of the central 90% range."),
            Colors.AliceBlue);

        AddAuditMoneyCard(
            row.Cells[1],
            L("P50 · COSTO CENTRAL", "P50 · CENTRAL COST"),
            analysis.Scenarios.FirstOrDefault(item =>
                item.Key == "SOLAR_CENTRAL"),
            enel,
            L("Mediana y estimación central.", "Median and central estimate."),
            Colors.Honeydew);

        AddAuditMoneyCard(
            row.Cells[2],
            L("P95 · COSTO SUPERIOR", "P95 · UPPER COST"),
            analysis.Scenarios.FirstOrDefault(item =>
                item.Key == "SOLAR_UPPER"),
            enel,
            L("Margen superior del rango central 90%.", "Upper edge of the central 90% range."),
            Colors.AliceBlue);

        AddCallout(
            section,
            L("ALCANCE DEL MONTO", "AMOUNT SCOPE"),
            L(
                "Son subtotales comparables de componentes conciliados con evidencia oficial. Incluyen el cargo fijo cuando éste puede reconstruirse independientemente. Subsidios, servicios asociados, FET no determinable u otros ajustes permanecen preservados como monto real y no se presentan como reconstruidos.",
                "These are comparable subtotals for components reconciled with official evidence. They include the fixed charge when it can be independently reconstructed. Subsidies, associated services, indeterminate FET, and other adjustments remain preserved as actual amounts and are not presented as reconstructed."),
            Colors.WhiteSmoke);
    }

    private static void AddAuditMoneyCard(
        Cell cell,
        string label,
        UtilityTariffScenario? scenario,
        UtilityTariffScenario? enelScenario,
        string explanation,
        Color fill)
    {
        if (scenario is null)
        {
            AddAuditMetricCard(
                cell,
                label,
                "—",
                explanation,
                fill,
                13);
            return;
        }

        var delta = enelScenario is null
            ? string.Empty
            : $" · Δ Enel {MoneySigned(scenario.SupportedTariffSubtotalClp - enelScenario.SupportedTariffSubtotalClp)}";

        AddAuditMetricCard(
            cell,
            label,
            Money(scenario.SupportedTariffSubtotalClp),
            $"{scenario.EnergyKwh:N3} kWh{delta} · {explanation}",
            fill,
            13);
    }

    private static void AddTariffComponentReconciliation(
        Section section,
        UtilityBillTariffScenarioAnalysis analysis,
        bool spanish)
    {
        string L(string es, string en) => spanish ? es : en;

        if (!analysis.HasTariffModel ||
            analysis.Components.Count == 0)
        {
            return;
        }

        AddHeading(
            section,
            L("Componentes reconstruidos", "Reconstructed components"),
            12);

        var table = section.AddTable();
        table.Borders.Width = 0.25;
        table.AddColumn(Unit.FromCentimeter(5.0));
        table.AddColumn(Unit.FromCentimeter(3.2));
        table.AddColumn(Unit.FromCentimeter(3.0));
        table.AddColumn(Unit.FromCentimeter(3.0));
        table.AddColumn(Unit.FromCentimeter(2.5));

        var h = table.AddRow();
        h.Format.Font.Bold = true;
        h.Shading.Color = Colors.AliceBlue;
        h.Cells[0].AddParagraph(L("Línea", "Line"));
        h.Cells[1].AddParagraph(L("Tasa oficial", "Official rate"));
        h.Cells[2].AddParagraph(L("Monto real", "Actual"));
        h.Cells[3].AddParagraph(L("Reconstruido", "Reconstructed"));
        h.Cells[4].AddParagraph(L("Diferencia", "Difference"));

        foreach (var item in analysis.Components)
        {
            var r = table.AddRow();
            r.Cells[0].AddParagraph(item.BillLineDescription);
            r.Cells[1].AddParagraph(
                item.FixedAmountClp > 0.0001 &&
                Math.Abs(item.RateClpPerKwh) <= 0.000001
                    ? $"$ {item.FixedAmountClp:N0} fijo"
                    : item.FixedAmountClp > 0.0001
                        ? $"$ {item.FixedAmountClp:N0} fijo + $ {item.RateClpPerKwh:N3}/kWh"
                        : $"$ {item.RateClpPerKwh:N3}/kWh");
            r.Cells[2].AddParagraph(
                Money(item.ActualLineAmountClp));
            r.Cells[3].AddParagraph(
                Money(item.ReconstructedAmountClp));
            r.Cells[4].AddParagraph(
                MoneySigned(item.DifferenceClp));
        }

        if (analysis.ReconstructedVsActualDifferenceClp.HasValue)
        {
            var p = section.AddParagraph(
                string.Format(
                    L(
                        "Subtotal real de líneas modeladas: {0}. Subtotal reconstruido: {1}. Residual: {2}.",
                        "Actual subtotal of modeled lines: {0}. Reconstructed subtotal: {1}. Residual: {2}."),
                    Money(analysis.ActualSupportedLinesClp),
                    Money(analysis.ReconstructedBilledSupportedClp),
                    MoneySigned(
                        analysis.ReconstructedVsActualDifferenceClp)));
            p.Format.Font.Size = 8.5;
            p.Format.SpaceBefore = Unit.FromPoint(4);
        }

        var ambiguous = analysis.Components.Any(item =>
            item.EvidenceStatus.Contains(
                "AMBIGUOUS",
                StringComparison.Ordinal));

        AddCallout(
            section,
            L("ESTADO DE LA CONCILIACIÓN", "RECONCILIATION STATUS"),
            ambiguous
                ? L(
                    "Los montos reconstruidos concilian con las tasas oficiales. En Electricidad consumida la tasa queda demostrada por el monto de la boleta, pero la combinación RED/ETR no queda identificada de forma única sólo con esa coincidencia; se presenta como inferida, no como dato impreso.",
                    "Reconstructed amounts reconcile with official rates. For Electricity consumed, the rate is demonstrated by the bill amount, but the RED/ETR combination is not uniquely identified by that match alone; it is presented as inferred, not printed evidence.")
                : L(
                    "Los componentes modelados concilian con las tasas oficiales dentro de la tolerancia de auditoría.",
                    "Modeled components reconcile with official rates within audit tolerance."),
            ambiguous
                ? Colors.LemonChiffon
                : Colors.Honeydew);
    }

    private static void AddStatisticalEvidence(
        Section section,
        UtilityGridImportStatisticalCompletion statistical,
        UtilityBillGapStatisticalAnalysis billGapAnalysis,
        bool spanish)
    {
        string L(string es, string en) => spanish ? es : en;

        var cards = section.AddTable();
        cards.Borders.Width = 0;
        cards.AddColumn(Unit.FromCentimeter(5.43));
        cards.AddColumn(Unit.FromCentimeter(5.43));
        cards.AddColumn(Unit.FromCentimeter(5.43));

        var first = cards.AddRow();
        AddAuditMetricCard(
            first.Cells[0],
            L("COBERTURA TEMPORAL RED", "GRID TIME COVERAGE"),
            $"{statistical.CoveragePercent:N1}%",
            L(
                "Continuidad suficiente de grid_import_power_w.",
                "Sufficient continuity of grid_import_power_w."),
            Colors.AliceBlue,
            13);
        AddAuditMetricCard(
            first.Cells[1],
            L("HORAS SIN COBERTURA", "UNCOVERED HOURS"),
            $"{statistical.MissingHours:N2} h",
            L(
                "Se imputan estadísticamente; no se convierten en cero.",
                "Statistically imputed; not converted to zero."),
            Colors.AliceBlue,
            13);
        AddAuditMetricCard(
            first.Cells[2],
            L("VENTANAS DE CALIBRACIÓN", "CALIBRATION WINDOWS"),
            statistical.DonorSampleCount.ToString("N0"),
            L(
                "Ventanas históricas completas comparables utilizadas en los gaps.",
                "Comparable complete historical windows used for the gaps."),
            Colors.AliceBlue,
            13);

        var second = cards.AddRow();
        AddAuditMetricCard(
            second.Cells[0],
            L("COMBINACIONES EXACTAS", "EXACT COMBINATIONS"),
            statistical.SimulationCount.ToString("N0"),
            L("Combinaciones históricas usadas para P5/P50/P95.", "Historical combinations used for P5/P50/P95."),
            Colors.WhiteSmoke,
            13);
        AddAuditMetricCard(
            second.Cells[1],
            L("DESVIACIÓN DE RESULTADOS", "RESULT STD. DEV."),
            statistical.StandardDeviationKwh.HasValue
                ? $"{statistical.StandardDeviationKwh.Value:N3} kWh"
                : "—",
            L(
                "Dispersión de los totales completados.",
                "Spread of completed totals."),
            Colors.WhiteSmoke,
            13);
        AddAuditMetricCard(
            second.Cells[2],
            L("MÉTODO", "METHOD"),
            "P5 / P50 / P95",
            L(
                "Ventanas históricas completas por horario, duración y tipo de día.",
                "Complete historical windows by clock time, duration and day type."),
            Colors.Honeydew,
            12);

        AddCallout(
            section,
            L("CÓMO LEER LOS PERCENTILES", "HOW TO READ THE PERCENTILES"),
            L(
                "P5 y P95 delimitan el 90% central de los resultados producidos por el método. P5 es el margen inferior, P50 es la mediana y estimación central, y P95 es el margen superior. Enel no participa en la construcción de este rango; se compara después como referencia independiente.",
                "P5 and P95 bound the central 90% of results produced by the method. P5 is the lower margin, P50 is the median and central estimate, and P95 is the upper margin. The utility value does not participate in building this range; it is compared afterward as an independent reference."),
            Colors.Honeydew);

        AddCallout(
            section,
            L("ESTADO DEL MÉTODO", "METHOD STATUS"),
            string.Format(
                L(
                    "Método {0}. Estado: PROVISIONAL / RESEARCH ONLY. Se usan ventanas históricas completas del mismo horario, duración y tipo de día. Enel no participa en la construcción ni selección del rango.",
                    "Method {0}. Status: PROVISIONAL / RESEARCH ONLY. Complete historical windows with the same clock time, duration and day type are used. The utility value does not participate in constructing or selecting the range."),
                statistical.MethodVersion),
            Colors.WhiteSmoke);
    }

    private static void AddTariffEvidence(
        Section section,
        UtilityBillTariffScenarioAnalysis analysis,
        bool spanish)
    {
        string L(string es, string en) => spanish ? es : en;

        AddHeading(
            section,
            L("Evidencia tarifaria", "Tariff evidence"),
            12);

        if (!analysis.HasTariffModel)
        {
            AddCallout(
                section,
                L("MODELO TARIFARIO NO CONCILIADO", "TARIFF MODEL NOT RECONCILED"),
                L(
                    "No se logró conciliar un modelo tarifario oficial para esta boleta.",
                    "An official tariff model could not be reconciled for this bill."),
                Colors.LemonChiffon);
            return;
        }

        var summary = section.AddTable();
        summary.Borders.Width = 0;
        summary.AddColumn(Unit.FromCentimeter(5.43));
        summary.AddColumn(Unit.FromCentimeter(5.43));
        summary.AddColumn(Unit.FromCentimeter(5.43));
        var row = summary.AddRow();

        var effectiveLabel =
            analysis.PublicationPeriods.Count > 1
                ? string.Join(
                    " + ",
                    analysis.PublicationPeriods.Select(
                        item =>
                            item.EffectiveFrom.HasValue
                                ? $"{item.EffectiveFrom.Value:yyyy-MM}" +
                                  (item.IsRetroactive
                                      ? L(" R", " R")
                                      : string.Empty)
                                : "—"))
                : analysis.EffectiveFrom.HasValue
                    ? $"{analysis.EffectiveFrom.Value:yyyy-MM}"
                    : "—";

        var effectiveDetail =
            analysis.PublicationPeriods.Count > 1
                ? string.Join(
                    " · ",
                    analysis.PublicationPeriods.Select(
                        item =>
                            $"{item.Days} d / {item.Weight * 100.0:N2}%"))
                : analysis.IsRetroactive
                    ? L("Versión retroactiva preferida.", "Preferred retroactive version.")
                    : L("Versión vigente capturada.", "Captured effective version.");

        AddAuditMetricCard(
            row.Cells[0],
            L("VIGENCIAS OFICIALES", "OFFICIAL EFFECTIVE PERIODS"),
            effectiveLabel,
            effectiveDetail,
            Colors.AliceBlue,
            11.5);

        AddAuditMetricCard(
            row.Cells[1],
            L("RED / ETR INFERIDO", "INFERRED NETWORK / ETR"),
            $"{analysis.NetworkType ?? "?"} / {analysis.EtrBand ?? "?"}",
            L(
                "No está impreso en la boleta; se infiere de candidatos que reproducen los cargos.",
                "Not printed on the bill; inferred from candidates that reproduce the charges."),
            Colors.AliceBlue,
            12);

        AddAuditMetricCard(
            row.Cells[2],
            L("BASE TARIFARIA MODELADA", "MODELED TARIFF BASIS"),
            analysis.SupportedVariableRateClpPerKwh.HasValue
                ? $"$ {analysis.SupportedVariableRateClpPerKwh.Value:N3}/kWh + fijo {Money(analysis.SupportedFixedAmountClp ?? 0)}"
                : "—",
            L(
                "Suma de componentes variables y fijos que conciliaron con evidencia oficial.",
                "Sum of variable and fixed components reconciled with official evidence."),
            Colors.Honeydew,
            11.5);

        var sourceSummary =
            analysis.PublicationPeriods.Count > 0
                ? string.Join(
                    " | ",
                    analysis.PublicationPeriods.Select(
                        item =>
                            item.EffectiveFrom.HasValue
                                ? $"{item.EffectiveFrom.Value:yyyy-MM}" +
                                  (item.IsRetroactive
                                      ? L(" retroactiva", " retroactive")
                                      : string.Empty)
                                : item.PublicationTitle))
                : analysis.EffectiveFrom.HasValue
                    ? analysis.EffectiveFrom.Value.ToString("yyyy-MM")
                    : L("vigencia no identificada", "unidentified effective period");

        var source = section.AddParagraph(
            string.Format(
                L(
                    "Fuentes: Enel Distribución Chile · publicaciones oficiales aplicadas: {0}.",
                    "Sources: Enel Distribución Chile · official publications applied: {0}."),
                sourceSummary));
        source.Format.Font.Size = 8;
        source.Format.Font.Color = Colors.DimGray;
        source.Format.SpaceBefore = Unit.FromPoint(4);
        source.Format.SpaceAfter = Unit.FromPoint(4);

        if (analysis.Components.Count == 0)
            return;

        var components = section.AddTable();
        components.Borders.Width = 0;
        components.AddColumn(Unit.FromCentimeter(8.15));
        components.AddColumn(Unit.FromCentimeter(8.15));
        var componentRow = components.AddRow();

        for (var index = 0; index < analysis.Components.Count && index < 2; index++)
        {
            var component = analysis.Components[index];
            var humanName = component.ComponentKey switch
            {
                "ELECTRICITY_CONSUMED" =>
                    L("Electricidad consumida", "Electricity consumed"),
                "ELECTRICITY_TRANSPORT_PLUS_PUBLIC_SERVICE" =>
                    L("Transporte + servicio público", "Transport + public service"),
                "ELECTRICITY_TRANSPORT" =>
                    L("Transporte de electricidad", "Electricity transport"),
                _ => component.BillLineDescription
            };

            var status = component.EvidenceStatus.Contains(
                    "AMBIGUOUS",
                    StringComparison.Ordinal)
                ? L(
                    "La tasa reproduce la línea; RED/ETR no queda demostrada de forma única por esa coincidencia.",
                    "The rate reproduces the line; RED/ETR is not uniquely proven by that match.")
                : L(
                    "La tasa oficial reproduce la línea real dentro de la tolerancia de auditoría.",
                    "The official rate reproduces the actual line within audit tolerance.");

            AddAuditMetricCard(
                componentRow.Cells[index],
                humanName.ToUpperInvariant(),
                $"$ {component.RateClpPerKwh:N3}/kWh",
                string.Format(
                    L(
                        "Real {0} · reconstruido {1} · diferencia {2}. {3}",
                        "Actual {0} · reconstructed {1} · difference {2}. {3}"),
                    Money(component.ActualLineAmountClp),
                    Money(component.ReconstructedAmountClp),
                    MoneySigned(component.DifferenceClp),
                    status),
                index == 0
                    ? Colors.AliceBlue
                    : Colors.Honeydew,
                12);
        }
    }

    private static void AddEvidenceRow(
        Table table,
        string label,
        string value,
        string meaning)
    {
        var r = table.AddRow();
        r.Cells[0].AddParagraph(label);
        r.Cells[0].Format.Font.Bold = true;
        r.Cells[1].AddParagraph(value);
        r.Cells[2].AddParagraph(meaning);
    }

    private static string ScenarioLabel(
        string key,
        bool spanish) =>
        key switch
        {
            "ENEL_BILLED" =>
                spanish ? "Enel facturado" : "Utility billed",
            "SOLAR_OBSERVED" =>
                spanish ? "Inversor observado" : "Inverter observed",
            "SOLAR_LOWER" =>
                spanish ? "Inversor P5" : "Inverter P5",
            "SOLAR_CENTRAL" =>
                spanish ? "Inversor P50" : "Inverter P50",
            "SOLAR_UPPER" =>
                spanish ? "Inversor P95" : "Inverter P95",
            _ => key
        };

    private static string ScenarioEvidenceLabel(
        string key,
        bool spanish) =>
        key switch
        {
            "ENEL_BILLED" =>
                spanish ? "Medido/facturado Enel" : "Utility measured/billed",
            "SOLAR_OBSERVED" =>
                spanish ? "Observado directamente" : "Directly observed",
            "SOLAR_LOWER" or
            "SOLAR_CENTRAL" or
            "SOLAR_UPPER" =>
                spanish ? "Completado estadísticamente" : "Statistically completed",
            _ => key
        };

    private static void AddResolvedTariffEvidence(
        Section section,
        UtilityBillTariffScenarioAnalysis analysis,
        bool spanish)
    {
        string L(string es, string en) => spanish ? es : en;

        var intro = section.AddParagraph(
            L(
                "La reconstrucción monetaria usa únicamente publicaciones oficiales normalizadas y mantiene la misma identidad tarifaria entre los períodos efectivos. CandidateIndex no se trata como identidad estable entre PDFs.",
                "The monetary reconstruction uses only normalized official publications and preserves the same tariff identity across effective periods. CandidateIndex is not treated as a stable identity across PDFs."));
        intro.Format.Font.Size = 8;
        intro.Format.Font.Color = Colors.DimGray;
        intro.Format.SpaceAfter = Unit.FromPoint(4);

        var table = section.AddTable();
        table.Borders.Width = 0.25;
        table.AddColumn(Unit.FromCentimeter(3.0));
        table.AddColumn(Unit.FromCentimeter(2.0));
        table.AddColumn(Unit.FromCentimeter(2.2));
        table.AddColumn(Unit.FromCentimeter(3.0));
        table.AddColumn(Unit.FromCentimeter(6.1));

        var h = table.AddRow();
        h.Format.Font.Bold = true;
        h.Cells[0].AddParagraph(L("Período", "Period"));
        h.Cells[1].AddParagraph(L("Días", "Days"));
        h.Cells[2].AddParagraph(L("Peso", "Weight"));
        h.Cells[3].AddParagraph(L("Versión", "Version"));
        h.Cells[4].AddParagraph(L("Fuente oficial", "Official source"));

        foreach (var period in analysis.PublicationPeriods)
        {
            var row = table.AddRow();
            row.Cells[0].AddParagraph(
                $"{period.AppliedFrom:dd-MM-yyyy} → {period.AppliedTo:dd-MM-yyyy}");
            row.Cells[1].AddParagraph(
                period.Days.ToString());
            row.Cells[2].AddParagraph(
                $"{period.Weight * 100.0:N2}%");
            row.Cells[3].AddParagraph(
                period.IsRetroactive
                    ? L("Retroactiva", "Retroactive")
                    : L("Normal", "Standard"));
            row.Cells[4].AddParagraph(
                period.EffectiveFrom.HasValue
                    ? $"Enel · {period.EffectiveFrom.Value:yyyy-MM}"
                    : "Enel");
        }

        AddCallout(
            section,
            L("ESTADO DE EVIDENCIA", "EVIDENCE STATUS"),
            string.Format(
                L(
                    "Modelo tarifario conciliado: {0}. RED {1}; ETR {2}; columna {3}. Los títulos completos, tasas y bases de cálculo quedan en Fuentes y en el anexo.",
                    "Tariff model reconciled: {0}. RED {1}; ETR {2}; column {3}. Full titles, rates and calculation bases remain in Sources and in the annex."),
                analysis.Status,
                analysis.NetworkType ?? "—",
                analysis.EtrBand ?? "—",
                analysis.Column ?? "—"),
            Colors.Honeydew);
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
        table.AddColumn(Unit.FromCentimeter(3.2));
        table.AddColumn(Unit.FromCentimeter(1.8));
        table.AddColumn(Unit.FromCentimeter(2.4));
        table.AddColumn(Unit.FromCentimeter(2.7));
        table.AddColumn(Unit.FromCentimeter(2.2));
        table.AddColumn(Unit.FromCentimeter(2.1));
        table.AddColumn(Unit.FromCentimeter(2.3));

        var header = table.AddRow();
        header.Format.Font.Bold = true;
        header.Cells[0].AddParagraph(L("Línea", "Line"));
        header.Cells[1].AddParagraph(L("Tasa boleta", "Bill rate"));
        header.Cells[2].AddParagraph(L("Base cálculo", "Calc. basis"));
        header.Cells[3].AddParagraph(L("Verificación", "Verification"));
        header.Cells[4].AddParagraph(L("Fuente", "Source"));
        header.Cells[5].AddParagraph(L("Reconstruido", "Reconstructed"));
        header.Cells[6].AddParagraph(L("Dif. monto", "Amount diff."));

        foreach (var item in verifications)
        {
            var row = table.AddRow();
            row.Cells[0].AddParagraph(item.Description);
            row.Cells[1].AddParagraph(
                item.PrintedUnitRateClp.HasValue
                    ? $"$ {item.PrintedUnitRateClp.Value:N3}"
                    : "—");
            row.Cells[2].AddParagraph(
                FormatCalculationBasis(
                    item,
                    spanish));
            row.Cells[3].AddParagraph(
                VerificationStatusLabel(
                    item.Status,
                    spanish));
            row.Cells[4].AddParagraph(
                item.Publications.Count == 0
                    ? "—"
                    : string.Join(
                        " · ",
                        item.Publications.Select(
                            publication =>
                                FormatTariffPublicationEvidence(
                                    publication,
                                    spanish))));
            row.Cells[5].AddParagraph(
                item.ReconstructedAmountClp.HasValue
                    ? $"$ {item.ReconstructedAmountClp.Value:N0}"
                    : "—");
            row.Cells[6].AddParagraph(
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

    private static string FormatCalculationBasis(
        BillLineTariffVerification item,
        bool spanish)
    {
        if (!item.CalculationQuantity.HasValue)
            return "—";

        var unit = string.IsNullOrWhiteSpace(item.CalculationUnit)
            ? string.Empty
            : $" {item.CalculationUnit}";

        var source = item.CalculationQuantitySource switch
        {
            "BILL_BILLED_KWH" =>
                spanish ? "consumo boleta" : "bill consumption",
            "BILL_LINE" =>
                spanish ? "línea boleta" : "bill line",
            _ =>
                spanish ? "derivada" : "derived"
        };

        return $"{item.CalculationQuantity.Value:N3}{unit} · {source}";
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
                spanish ? "Sin tasa en boleta" : "No rate on bill",
            "OFFICIAL_RATE_DERIVATION_PENDING" =>
                spanish ? "Fuente oficial disponible · cálculo pendiente" : "Official source available · calculation pending",
            "OFFICIAL_RATE_DERIVATION_MULTI_PERIOD" =>
                spanish ? "Fuente oficial disponible · dividir período tarifario" : "Official source available · split tariff period",
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

    private void AddNonVariableChargeTreatment(
        Section section,
        UtilityBillRecord bill,
        UtilityBillTariffScenarioAnalysis analysis,
        bool spanish)
    {
        string L(string es, string en) => spanish ? es : en;

        var rows = _repository
            .GetBillLines(bill.BillId)
            .Select(line =>
            {
                var normalized =
                    (line.Description ?? string.Empty)
                        .ToUpperInvariant();

                if (normalized.Contains("ADMINISTR"))
                {
                    if (analysis.Components.Any(item =>
                            item.ComponentKey ==
                                UtilityBillLineCategory.ServiceAdministration))
                    {
                        return null;
                    }

                    return new
                    {
                        Line = line,
                        Class = L("FIJO · NO RECONSTRUIDO", "FIXED · NOT RECONSTRUCTED"),
                        Treatment = L(
                            "Se preserva porque la evidencia tarifaria no permitió reconstruir un cargo fijo único.",
                            "Preserved because tariff evidence did not establish one unique fixed charge.")
                    };
                }

                if (normalized.Contains("ARRIENDO") &&
                    normalized.Contains("MEDIDOR"))
                {
                    return new
                    {
                        Line = line,
                        Class = L("FIJO", "FIXED"),
                        Treatment = L(
                            "Se preserva sin cambio entre escenarios.",
                            "Preserved unchanged across scenarios.")
                    };
                }

                if (normalized.Contains("SERVICIO COM") ||
                    normalized.Contains("SERVICIO COMÚN"))
                {
                    return new
                    {
                        Line = line,
                        Class = L(
                            "PRESERVADO REAL",
                            "PRESERVED ACTUAL"),
                        Treatment = L(
                            "No se recalcula desde el kWh individual discutido.",
                            "Not recalculated from the disputed individual kWh.")
                    };
                }

                if (normalized.Contains("SUBSIDIO"))
                {
                    return new
                    {
                        Line = line,
                        Class = L(
                            "CONDICIONAL / REGULADO",
                            "CONDITIONAL / REGULATED"),
                        Treatment = L(
                            "Se conserva el crédito real de esta cuota; no se deriva del kWh contrafactual.",
                            "The actual credit installment is preserved; it is not derived from counterfactual kWh.")
                    };
                }

                return null;
            })
            .Where(item => item is not null)
            .ToArray();

        if (rows.Length == 0)
            return;

        AddHeading(
            section,
            L(
                "Tratamiento de cargos no reconstruidos",
                "Treatment of non-reconstructed charges"),
            11.5);

        var table = section.AddTable();
        table.Borders.Width = 0.25;
        table.Format.Font.Size = 7.5;
        table.AddColumn(Unit.FromCentimeter(4.6));
        table.AddColumn(Unit.FromCentimeter(3.5));
        table.AddColumn(Unit.FromCentimeter(6.2));
        table.AddColumn(Unit.FromCentimeter(2.0));

        var h = table.AddRow();
        h.Format.Font.Bold = true;
        h.Cells[0].AddParagraph(L("Concepto", "Concept"));
        h.Cells[1].AddParagraph(L("Clasificación", "Classification"));
        h.Cells[2].AddParagraph(L("Tratamiento", "Treatment"));
        h.Cells[3].AddParagraph(L("Monto", "Amount"));

        foreach (var item in rows)
        {
            var row = table.AddRow();
            row.Cells[0].AddParagraph(item!.Line.Description);
            row.Cells[1].AddParagraph(item.Class);
            row.Cells[2].AddParagraph(item.Treatment);
            row.Cells[3].AddParagraph(
                MoneySigned(
                    item.Line.AmountClp));
        }
    }

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

        double? derivedOtherCharges = null;
        if (bill.GrossBillAmountClp.HasValue &&
            (bill.TotalDueClp ?? bill.AmountClp).HasValue)
        {
            derivedOtherCharges =
                (bill.TotalDueClp ?? bill.AmountClp)!.Value -
                bill.GrossBillAmountClp.Value;
        }

        AddDefinitionRow(
            summary,
            L("Otros cargos/abonos netos", "Net other charges/credits"),
            MoneySigned(
                derivedOtherCharges ??
                bill.OtherChargesClp));
        AddDefinitionRow(summary, L("Total a pagar", "Total due"), Money(bill.TotalDueClp ?? bill.AmountClp));

        if (derivedOtherCharges.HasValue &&
            bill.OtherChargesClp.HasValue &&
            Math.Abs(
                derivedOtherCharges.Value -
                bill.OtherChargesClp.Value) >
            0.5)
        {
            var reconciliationNote = section.AddParagraph(
                string.Format(
                    L(
                        "Nota de conciliación: el neto mostrado ({0}) se deriva de Total a pagar − Total boleta. El valor agregado capturado manualmente ({1}) no concilia con esos totales y no se usa como autoridad del informe.",
                        "Reconciliation note: the displayed net amount ({0}) is derived from Total due − Gross bill. The manually captured aggregate value ({1}) does not reconcile with those totals and is not used as report authority."),
                    MoneySigned(
                        derivedOtherCharges.Value),
                    MoneySigned(
                        bill.OtherChargesClp.Value)));
            reconciliationNote.Format.Font.Size = 7.5;
            reconciliationNote.Format.Font.Color =
                Colors.DimGray;
            reconciliationNote.Format.SpaceAfter =
                Unit.FromPoint(3);
        }

        if (lines.Count == 0)
        {
            var p = section.AddParagraph(
                L(
                    "No hay líneas de cargo guardadas para esta boleta.",
                    "No charge lines are stored for this bill."));
            p.Format.Font.Color = Colors.DimGray;
            return;
        }

        var evidenceNote = section.AddParagraph(
            L(
                "Un guion en Cantidad o Precio significa que ese dato no fue capturado junto a la línea. No debe completarse por suposición. Para componentes reconocidos en $/kWh, la auditoría puede reutilizar el consumo facturado de la boleta como base de cálculo y lo identifica como dato derivado de la cabecera, no como evidencia impresa en la línea.",
                "A dash in Quantity or Rate means that value was not captured beside the line. It must not be filled by assumption. For recognized $/kWh components, the audit may reuse the bill-level billed consumption as the calculation basis and identifies it as derived from the bill header, not as line-printed evidence."));
        evidenceNote.Format.Font.Size = 8;
        evidenceNote.Format.Font.Color = Colors.DimGray;
        evidenceNote.Format.SpaceAfter = Unit.FromPoint(4);

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
            r.Cells[0].AddParagraph(
                line.SectionKey switch
                {
                    "SERVICIO_ELECTRICO" =>
                        L("Servicio eléctrico", "Electric service"),
                    "OTROS_CARGOS" or "OTRO" =>
                        L("Otros", "Other"),
                    _ => line.SectionKey.Replace('_', ' ')
                });
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

    private static void AddComparableTotalDueTable(
        Section section,
        UtilityBillRecord bill,
        UtilityBillTariffScenarioAnalysis analysis,
        bool spanish)
    {
        string L(string es, string en) => spanish ? es : en;

        var printedTotal =
            bill.TotalDueClp ??
            bill.AmountClp;
        var enelScenario =
            analysis.Scenarios.FirstOrDefault(
                item => item.Key == "ENEL_BILLED");

        if (!printedTotal.HasValue ||
            enelScenario is null ||
            !analysis.HasTariffModel)
        {
            AddCallout(
                section,
                L("TOTAL COMPARABLE NO DISPONIBLE", "COMPARABLE TOTAL UNAVAILABLE"),
                L(
                    "La boleta conserva sus montos reales, pero todavía no existe una base tarifaria suficiente para extender el cálculo al total comparable.",
                    "The bill keeps its actual amounts, but there is not yet enough tariff evidence to extend the calculation to a comparable total."),
                Colors.LemonChiffon);
            return;
        }

        var preservedNonVariable =
            printedTotal.Value -
            enelScenario.SupportedTariffSubtotalClp;

        var intro = section.AddParagraph(
            L(
                "Para comparar escenarios se calcula el subtotal de componentes variables sustentados en $/kWh más el cargo fijo reconstruido, si existe. Los demás importes de la boleta se conservan sin cambios.",
                "For comparable scenarios, the subtotal includes supported variable $/kWh components plus any reconstructed fixed charge. All other bill amounts are preserved unchanged."));
        intro.Format.Font.Size = 8.5;
        intro.Format.Font.Color = Colors.DimGray;
        intro.Format.SpaceAfter = Unit.FromPoint(5);

        var summary = section.AddTable();
        summary.Borders.Width = 0;
        summary.AddColumn(Unit.FromCentimeter(8.15));
        summary.AddColumn(Unit.FromCentimeter(8.15));
        var summaryRow = summary.AddRow();

        AddAuditMetricCard(
            summaryRow.Cells[0],
            L("MONTO NO VARIABLE PRESERVADO", "PRESERVED NON-VARIABLE AMOUNT"),
            MoneySigned(preservedNonVariable),
            L(
                "Incluye cargos reales no recalculados y cualquier redondeo/residual de presentación.",
                "Includes actual charges not recalculated and any display rounding/residual."),
            Colors.WhiteSmoke,
            12);

        AddAuditMetricCard(
            summaryRow.Cells[1],
            L("TOTAL REAL ENEL", "ACTUAL UTILITY TOTAL"),
            Money(printedTotal),
            L(
                "Referencia impresa de la boleta.",
                "Printed bill reference."),
            Colors.LemonChiffon,
            12);

        var table = section.AddTable();
        table.Borders.Width = 0.25;
        table.AddColumn(Unit.FromCentimeter(3.8));
        table.AddColumn(Unit.FromCentimeter(2.4));
        table.AddColumn(Unit.FromCentimeter(3.3));
        table.AddColumn(Unit.FromCentimeter(3.4));
        table.AddColumn(Unit.FromCentimeter(3.4));

        var h = table.AddRow();
        h.Format.Font.Bold = true;
        h.Cells[0].AddParagraph(L("Escenario", "Scenario"));
        h.Cells[1].AddParagraph("kWh");
        h.Cells[2].AddParagraph(L("Subtotal modelado", "Modeled subtotal"));
        h.Cells[3].AddParagraph(L("Total comparable", "Comparable total"));
        h.Cells[4].AddParagraph(L("Dif. vs Enel", "Diff. vs utility"));

        foreach (var scenario in analysis.Scenarios)
        {
            var total =
                preservedNonVariable +
                scenario.SupportedTariffSubtotalClp;
            var difference =
                total -
                printedTotal.Value;

            var row = table.AddRow();
            row.Cells[0].AddParagraph(
                ScenarioLabel(
                    scenario.Key,
                    spanish));
            row.Cells[1].AddParagraph(
                $"{scenario.EnergyKwh:N3}");
            row.Cells[2].AddParagraph(
                Money(
                    scenario.SupportedTariffSubtotalClp));
            row.Cells[3].AddParagraph(
                Money(total));
            row.Cells[4].AddParagraph(
                MoneySigned(difference));
        }
    }

    private static void AddEconomicConclusion(
        Section section,
        UtilityBillTariffScenarioAnalysis analysis,
        bool spanish)
    {
        string L(string es, string en) => spanish ? es : en;

        if (!analysis.HasTariffModel)
        {
            AddCallout(
                section,
                L("CONCLUSIÓN ECONÓMICA", "ECONOMIC CONCLUSION"),
                L(
                    "La estructura tarifaria aún no puede reconstruirse con evidencia suficiente.",
                    "The tariff structure cannot yet be reconstructed with sufficient evidence."),
                Colors.LemonChiffon);
            return;
        }

        AddCallout(
            section,
            L("CONCLUSIÓN ECONÓMICA", "ECONOMIC CONCLUSION"),
            L(
                "Los componentes tarifarios reconstruibles son coherentes con las fuentes oficiales y con la boleta. La evidencia actual no sustenta un error tarifario material; la controversia técnica se concentra en la cantidad de energía facturada.",
                "The reconstructible tariff components are consistent with official sources and the bill. Current evidence does not support a material tariff error; the technical dispute is concentrated on the billed energy quantity."),
            Colors.Honeydew);
    }

    private static void AddEnergyEvidencePage(
        Section section,
        UtilityBillRecord bill,
        UtilityGridImportStatisticalCompletion statistical,
        UtilityBillGapStatisticalAnalysis analysis,
        bool spanish)
    {
        string L(string es, string en) => spanish ? es : en;

        var explanation = section.AddParagraph(
            L(
                "El inversor registra periódicamente potencia importada desde la red y cada registro conserva su marca temporal real. La energía observada se obtiene integrando esa potencia a lo largo del tiempo. Los intervalos demasiado largos para considerarlos continuidad observada se marcan como gaps: no se tratan como consumo cero y no se fabrican frames inexistentes.",
                "The inverter periodically records power imported from the grid and each record retains its real timestamp. Observed energy is obtained by integrating that power through time. Intervals too long to be treated as observed continuity are marked as gaps: they are not treated as zero consumption and no nonexistent frames are fabricated."));
        explanation.Format.SpaceAfter = Unit.FromPoint(6);

        AddCallout(
            section,
            L("PRINCIPIO DE INTEGRACIÓN", "INTEGRATION PRINCIPLE"),
            "Ei ≈ ((Pi + Pi+1) / 2) × Δti",
            Colors.WhiteSmoke);

        var cards = section.AddTable();
        cards.Borders.Width = 0;
        cards.AddColumn(Unit.FromCentimeter(5.43));
        cards.AddColumn(Unit.FromCentimeter(5.43));
        cards.AddColumn(Unit.FromCentimeter(5.43));
        var first = cards.AddRow();

        AddAuditMetricCard(
            first.Cells[0],
            L("ENERGÍA OBSERVADA", "OBSERVED ENERGY"),
            $"{statistical.ObservedKwh:N3} kWh",
            L(
                "Integrada sólo donde existe continuidad suficiente.",
                "Integrated only where sufficient continuity exists."),
            Colors.AliceBlue,
            13);
        AddAuditMetricCard(
            first.Cells[1],
            L("TIEMPO NO CUBIERTO", "UNCOVERED TIME"),
            $"{analysis.UncoveredHours:N3} h",
            L(
                "Se identifica y se completa estadísticamente.",
                "Identified and statistically completed."),
            Colors.WhiteSmoke,
            13);
        AddAuditMetricCard(
            first.Cells[2],
            L("COBERTURA TEMPORAL", "TIME COVERAGE"),
            $"{statistical.CoveragePercent:N2}%",
            L(
                "Cobertura de importación desde red; no confundir con atribución de fuentes.",
                "Grid-import coverage; not source-attribution coverage."),
            Colors.Honeydew,
            13);

        if (!statistical.HasPredictiveInterval)
        {
            AddCallout(
                section,
                L("COMPLETACIÓN NO DISPONIBLE", "COMPLETION UNAVAILABLE"),
                L(
                    "No existe evidencia estadística suficiente para completar todos los gaps de este período.",
                    "There is not enough statistical evidence to complete all gaps in this period."),
                Colors.LemonChiffon);
            return;
        }

        var components = section.AddTable();
        components.Borders.Width = 0.25;
        components.AddColumn(Unit.FromCentimeter(6.8));
        components.AddColumn(Unit.FromCentimeter(4.7));
        components.AddColumn(Unit.FromCentimeter(4.8));
        var h = components.AddRow();
        h.Format.Font.Bold = true;
        h.Cells[0].AddParagraph(L("Escenario", "Scenario"));
        h.Cells[1].AddParagraph(L("Aporte estimado gaps", "Estimated gap contribution"));
        h.Cells[2].AddParagraph(L("Total período", "Period total"));

        AddGapContributionRow(
            components,
            "P5",
            statistical.ObservedKwh,
            statistical.LowerKwh!.Value);
        AddGapContributionRow(
            components,
            "P50",
            statistical.ObservedKwh,
            statistical.MedianKwh!.Value);
        AddGapContributionRow(
            components,
            "P95",
            statistical.ObservedKwh,
            statistical.UpperKwh!.Value);

        if (bill.BilledConsumptionKwh.HasValue)
        {
            var enel =
                bill.BilledConsumptionKwh.Value;
            var difference =
                enel -
                statistical.UpperKwh.Value;
            var percent =
                enel > 0
                    ? difference / enel * 100.0
                    : 0;

            AddCallout(
                section,
                L("¿PUEDE LA DIFERENCIA EXPLICARSE SÓLO POR LOS DATOS FALTANTES?", "CAN MISSING DATA ALONE EXPLAIN THE DIFFERENCE?"),
                string.Format(
                    L(
                        "Enel: {0:N3} kWh · P95 provisional: {1:N3} kWh · Enel − P95: {2:N3} kWh ({3:N2}%). El máximo empírico conjunto alcanza {4}; por ello el exceso sobre P95 se trata como una diferencia menor dependiente del supuesto de agregación, no como exclusión estadística.",
                        "Utility: {0:N3} kWh · provisional P95: {1:N3} kWh · utility − P95: {2:N3} kWh ({3:N2}%). Full empirical support reaches {4}; therefore the excess above P95 is treated as a minor difference dependent on the aggregation assumption, not as statistical exclusion."),
                    enel,
                    statistical.UpperKwh.Value,
                    difference,
                    percent,
                    analysis.MaximumKwh.HasValue
                        ? $"{analysis.MaximumKwh.Value:N3} kWh"
                        : "—"),
                difference > 0
                    ? Colors.LemonChiffon
                    : Colors.Honeydew);
        }
    }

    private static void AddGapContributionRow(
        Table table,
        string label,
        double observedKwh,
        double totalKwh)
    {
        var row = table.AddRow();
        row.Cells[0].AddParagraph(label);
        row.Cells[0].Format.Font.Bold = true;
        row.Cells[1].AddParagraph(
            $"{Math.Max(0, totalKwh - observedKwh):N3} kWh");
        row.Cells[2].AddParagraph(
            $"{totalKwh:N3} kWh");
    }

    private static void AddQualityEvidencePage(
        Section section,
        UtilityGridImportStatisticalCompletion statistical,
        UtilityBillGapStatisticalAnalysis analysis,
        bool spanish)
    {
        string L(string es, string en) => spanish ? es : en;

        var cards = section.AddTable();
        cards.Borders.Width = 0;
        cards.AddColumn(Unit.FromCentimeter(5.43));
        cards.AddColumn(Unit.FromCentimeter(5.43));
        cards.AddColumn(Unit.FromCentimeter(5.43));

        var r1 = cards.AddRow();
        AddAuditMetricCard(
            r1.Cells[0],
            L("FRAMES VÁLIDOS", "VALID FRAMES"),
            analysis.IntervalSampleCount.ToString("N0"),
            L("Muestras finitas y resolubles del período.", "Finite resolvable samples in the period."),
            Colors.AliceBlue,
            13);
        AddAuditMetricCard(
            r1.Cells[1],
            L("CADENCIA MEDIANA", "MEDIAN CADENCE"),
            $"{analysis.MedianCadenceMinutes:N3} min",
            L("Separación típica real entre frames.", "Typical real separation between frames."),
            Colors.AliceBlue,
            13);
        AddAuditMetricCard(
            r1.Cells[2],
            L("UMBRAL CONTINUIDAD", "CONTINUITY THRESHOLD"),
            $"{analysis.ContinuityThresholdMinutes:N3} min",
            L("Sobre este valor el intervalo se trata como gap.", "Above this value the interval is treated as a gap."),
            Colors.WhiteSmoke,
            13);

        var r2 = cards.AddRow();
        AddAuditMetricCard(
            r2.Cells[0],
            L("TIEMPO CUBIERTO", "COVERED TIME"),
            $"{analysis.CoveredHours:N3} h",
            L("Integración directamente observada.", "Directly observed integration."),
            Colors.Honeydew,
            13);
        AddAuditMetricCard(
            r2.Cells[1],
            L("TIEMPO FALTANTE", "MISSING TIME"),
            $"{analysis.UncoveredHours:N3} h",
            L("No se convierte en cero.", "Not converted to zero."),
            Colors.LemonChiffon,
            13);
        AddAuditMetricCard(
            r2.Cells[2],
            L("GAPS IDENTIFICADOS", "IDENTIFIED GAPS"),
            analysis.Gaps.Count.ToString("N0"),
            L("Incluye gaps internos y bordes cortos.", "Includes internal gaps and short boundaries."),
            Colors.WhiteSmoke,
            13);

        AddHeading(
            section,
            L("Detalle de discontinuidades", "Discontinuity detail"),
            11.5);

        var gaps = section.AddTable();
        gaps.Borders.Width = 0.25;
        gaps.AddColumn(Unit.FromCentimeter(1.0));
        gaps.AddColumn(Unit.FromCentimeter(2.7));
        gaps.AddColumn(Unit.FromCentimeter(4.0));
        gaps.AddColumn(Unit.FromCentimeter(2.1));
        gaps.AddColumn(Unit.FromCentimeter(2.8));
        gaps.AddColumn(Unit.FromCentimeter(3.7));
        var gh = gaps.AddRow();
        gh.Format.Font.Bold = true;
        gh.Cells[0].AddParagraph("#");
        gh.Cells[1].AddParagraph(L("Tipo", "Type"));
        gh.Cells[2].AddParagraph(L("Inicio local", "Local start"));
        gh.Cells[3].AddParagraph(L("Duración", "Duration"));
        gh.Cells[4].AddParagraph(L("Bordes", "Boundaries"));
        gh.Cells[5].AddParagraph(L("Calibración / backtest", "Calibration / backtest"));

        foreach (var gap in analysis.Gaps)
        {
            var row = gaps.AddRow();
            row.Cells[0].AddParagraph(gap.GapIndex.ToString());
            row.Cells[1].AddParagraph(
                GapKindLabel(
                    gap.Kind,
                    spanish));
            row.Cells[2].AddParagraph(
                $"{gap.StartLocal:dd-MM-yyyy HH:mm:ss}");
            row.Cells[3].AddParagraph(
                $"{gap.DurationMinutes:N2} min");
            row.Cells[4].AddParagraph(
                $"{FormatWatts(gap.TargetStartWatts)} → {FormatWatts(gap.TargetEndWatts)}");
            row.Cells[5].AddParagraph(
                gap.CalibrationDays > 0
                    ? string.Format(
                        L(
                            "{0} días · {1} casos · cobertura {2}",
                            "{0} days · {1} cases · coverage {2}"),
                        gap.CalibrationDays,
                        gap.BacktestCases,
                        gap.BacktestCoverage.HasValue
                            ? $"{gap.BacktestCoverage.Value * 100.0:N1}%"
                            : "—")
                    : L("Borde determinístico", "Deterministic boundary"));
        }

        AddHeading(
            section,
            L("Cuantiles por gap interno", "Internal-gap quantiles"),
            11.5);

        var quantiles = section.AddTable();
        quantiles.Borders.Width = 0.25;
        quantiles.AddColumn(Unit.FromCentimeter(2.5));
        quantiles.AddColumn(Unit.FromCentimeter(2.5));
        quantiles.AddColumn(Unit.FromCentimeter(2.5));
        quantiles.AddColumn(Unit.FromCentimeter(2.5));
        quantiles.AddColumn(Unit.FromCentimeter(6.2));
        var qh = quantiles.AddRow();
        qh.Format.Font.Bold = true;
        qh.Cells[0].AddParagraph(L("Gap", "Gap"));
        qh.Cells[1].AddParagraph("P5");
        qh.Cells[2].AddParagraph("P50");
        qh.Cells[3].AddParagraph("P95");
        qh.Cells[4].AddParagraph(L("Validación histórica", "Historical validation"));

        foreach (var gap in analysis.Gaps.Where(item =>
                     item.Q50Kwh.HasValue))
        {
            var row = quantiles.AddRow();
            row.Cells[0].AddParagraph($"#{gap.GapIndex} · {gap.DayType}");
            row.Cells[1].AddParagraph($"{gap.Q05Kwh!.Value:N3} kWh");
            row.Cells[2].AddParagraph($"{gap.Q50Kwh!.Value:N3} kWh");
            row.Cells[3].AddParagraph($"{gap.Q95Kwh!.Value:N3} kWh");
            row.Cells[4].AddParagraph(
                string.Format(
                    L(
                        "{0} casos · cobertura P5–P95 {1} · sesgo P50 {2}",
                        "{0} cases · P5–P95 coverage {1} · P50 bias {2}"),
                    gap.BacktestCases,
                    gap.BacktestCoverage.HasValue
                        ? $"{gap.BacktestCoverage.Value * 100.0:N1}%"
                        : "—",
                    gap.BacktestP50BiasKwh.HasValue
                        ? $"{gap.BacktestP50BiasKwh.Value:+0.000;-0.000;0.000} kWh"
                        : "—"));
        }

        AddCallout(
            section,
            L("CONTROLES METODOLÓGICOS", "METHODOLOGICAL CONTROLS"),
            L(
                "Timestamps reales · ausencia ≠ cero · sin grilla artificial de 5 minutos · sin observaciones fabricadas · Enel excluido de la calibración · método versionado · datos fuente disponibles para anexo.",
                "Real timestamps · missing ≠ zero · no artificial 5-minute grid · no fabricated observations · utility value excluded from calibration · versioned method · source data available for annex."),
            Colors.Honeydew);

        section.AddPageBreak();
        AddHeading(
            section,
            L("5.1 Cobertura diaria del período", "5.1 Daily period coverage"),
            14);

        var daily = section.AddTable();
        daily.Borders.Width = 0.25;
        daily.AddColumn(Unit.FromCentimeter(2.8));
        daily.AddColumn(Unit.FromCentimeter(2.8));
        daily.AddColumn(Unit.FromCentimeter(3.2));
        daily.AddColumn(Unit.FromCentimeter(3.2));
        daily.AddColumn(Unit.FromCentimeter(3.0));
        daily.AddColumn(Unit.FromCentimeter(2.5));

        var dh = daily.AddRow();
        dh.Format.Font.Bold = true;
        dh.HeadingFormat = true;
        dh.Cells[0].AddParagraph(L("Fecha", "Date"));
        dh.Cells[1].AddParagraph(L("Frames", "Frames"));
        dh.Cells[2].AddParagraph(L("Cubierto", "Covered"));
        dh.Cells[3].AddParagraph(L("Faltante", "Missing"));
        dh.Cells[4].AddParagraph(L("Cobertura", "Coverage"));
        dh.Cells[5].AddParagraph(L("kWh obs.", "Obs. kWh"));

        foreach (var day in analysis.DailyEvidence)
        {
            var row = daily.AddRow();
            row.Cells[0].AddParagraph(
                $"{day.LocalDate:dd-MM-yyyy}");
            row.Cells[1].AddParagraph(
                day.ValidSamples.ToString("N0"));
            row.Cells[2].AddParagraph(
                $"{day.CoveredHours:N3} h");
            row.Cells[3].AddParagraph(
                $"{day.UncoveredHours:N3} h");
            row.Cells[4].AddParagraph(
                $"{day.CoveragePercent:N2}%");
            row.Cells[5].AddParagraph(
                $"{day.ObservedPositiveKwh:N3}");
        }

    }

    private static string GapKindLabel(
        string kind,
        bool spanish) =>
        kind switch
        {
            "BOUNDARY_START" =>
                spanish ? "BORDE INICIO" : "START EDGE",
            "BOUNDARY_END" =>
                spanish ? "BORDE FIN" : "END EDGE",
            "INTERNAL" =>
                spanish ? "INTERNO" : "INTERNAL",
            _ => kind
        };

    private static string FormatWatts(
        double? watts) =>
        watts.HasValue
            ? $"{watts.Value:N0} W"
            : "—";

    private static void AddFindingsPage(
        Section section,
        UtilityBillRecord bill,
        UtilityGridImportStatisticalCompletion statistical,
        UtilityBillGapStatisticalAnalysis analysis,
        UtilityBillTariffScenarioAnalysis tariff,
        bool spanish)
    {
        string L(string es, string en) => spanish ? es : en;

        var table = section.AddTable();
        table.Borders.Width = 0.25;
        table.AddColumn(Unit.FromCentimeter(2.0));
        table.AddColumn(Unit.FromCentimeter(10.7));
        table.AddColumn(Unit.FromCentimeter(3.6));
        var h = table.AddRow();
        h.Format.Font.Bold = true;
        h.Cells[0].AddParagraph(L("Hallazgo", "Finding"));
        h.Cells[1].AddParagraph(L("Evidencia", "Evidence"));
        h.Cells[2].AddParagraph(L("Estado", "Status"));

        if (bill.BilledConsumptionKwh.HasValue &&
            statistical.MedianKwh.HasValue)
        {
            var enel = bill.BilledConsumptionKwh.Value;
            var diff = enel - statistical.MedianKwh.Value;
            var pct = enel > 0 ? diff / enel * 100.0 : 0;
            AddFindingRow(
                table,
                "H01",
                string.Format(
                    L(
                        "Enel factura {0:N3} kWh; P50 del inversor {1:N3} kWh. Diferencia Enel − P50: {2:N3} kWh ({3:N2}%).",
                        "Utility bills {0:N3} kWh; inverter P50 {1:N3} kWh. Utility − P50 difference: {2:N3} kWh ({3:N2}%)."),
                    enel,
                    statistical.MedianKwh.Value,
                    diff,
                    pct),
                L("REQUIERE REVISIÓN", "REVIEW REQUIRED"));
        }

        if (bill.BilledConsumptionKwh.HasValue &&
            statistical.UpperKwh.HasValue)
        {
            var enel = bill.BilledConsumptionKwh.Value;
            var diff = enel - statistical.UpperKwh.Value;
            var pct = enel > 0 ? diff / enel * 100.0 : 0;
            AddFindingRow(
                table,
                "H02",
                string.Format(
                    L(
                        "Comparación de extremo alto: Enel − P95 = {0:N3} kWh ({1:N2}%). La separación es pequeña y el máximo empírico conjunto alcanza {2}; por ello P95 no se usa como prueba de exclusión estadística.",
                        "High-side comparison: utility − P95 = {0:N3} kWh ({1:N2}%). The separation is small and full empirical support reaches {2}; therefore P95 is not used as proof of statistical exclusion."),
                    diff,
                    pct,
                    analysis.MaximumKwh.HasValue
                        ? $"{analysis.MaximumKwh.Value:N3} kWh"
                        : "—"),
                diff > 0
                    ? L("DIFERENCIA MENOR", "MINOR DIFFERENCE")
                    : L("COINCIDE", "CONSISTENT"));
        }

        var printedTotal =
            bill.TotalDueClp ??
            bill.AmountClp;
        var enelScenario =
            tariff.Scenarios.FirstOrDefault(
                item => item.Key == "ENEL_BILLED");
        var centralScenario =
            tariff.Scenarios.FirstOrDefault(
                item => item.Key == "SOLAR_CENTRAL");

        if (printedTotal.HasValue &&
            enelScenario is not null &&
            centralScenario is not null)
        {
            var fixedNet =
                printedTotal.Value -
                enelScenario.SupportedTariffSubtotalClp;
            var centralTotal =
                fixedNet +
                centralScenario.SupportedTariffSubtotalClp;
            var diff =
                printedTotal.Value -
                centralTotal;

            AddFindingRow(
                table,
                "H03",
                string.Format(
                    L(
                        "Con los mismos cargos no variables preservados, el escenario P50 produce {0} frente a {1} impresos. Enel − P50 = {2}.",
                        "With the same non-variable charges preserved, the P50 scenario produces {0} versus printed {1}. Utility − P50 = {2}."),
                    Money(centralTotal),
                    Money(printedTotal),
                    MoneySigned(diff)),
                L("DIFERENCIA ECONÓMICA", "ECONOMIC DIFFERENCE"));
        }

        AddFindingRow(
            table,
            "H04",
            tariff.HasTariffModel
                ? L(
                    "Las tasas y componentes reconstruibles concilian con fuentes oficiales; no se identifica un error tarifario material.",
                    "Reconstructible rates and components reconcile with official sources; no material tariff error is identified.")
                : L(
                    "La evidencia tarifaria no está completa.",
                    "Tariff evidence is incomplete."),
            tariff.HasTariffModel
                ? L("COINCIDE", "CONSISTENT")
                : L("NO VERIFICABLE", "NOT VERIFIABLE"));

        AddFindingRow(
            table,
            "H05",
            L(
                "El HPVINV02 no expone un contador independiente utilizable de energía comprada: buyElectricityQuantity es placeholder y dayPurchaseElectricityConsumption no está poblado.",
                "The HPVINV02 does not expose a usable independent purchased-energy counter: buyElectricityQuantity is a placeholder and dayPurchaseElectricityConsumption is not populated."),
            L("NO DISPONIBLE", "UNAVAILABLE"));

        AddCallout(
            section,
            L("LECTURA GLOBAL", "OVERALL READING"),
            L(
                "La evidencia no sustenta afirmar un error tarifario. Sí muestra una diferencia material respecto de la estimación central independiente de energía, mientras el extremo P95 queda muy próximo a Enel. El informe conserva ambas conclusiones sin exagerar ninguna.",
                "The evidence does not support claiming a tariff error. It does show a material difference from the independent central energy estimate, while the P95 high side lies very close to the utility value. The report preserves both conclusions without overstating either."),
            Colors.WhiteSmoke);
    }

    private static void AddFindingRow(
        Table table,
        string id,
        string evidence,
        string status)
    {
        var row = table.AddRow();
        row.Cells[0].AddParagraph(id);
        row.Cells[0].Format.Font.Bold = true;
        row.Cells[1].AddParagraph(evidence);
        row.Cells[2].AddParagraph(status);
        row.Cells[2].Format.Font.Bold = true;
    }

    private static void AddSourcesAndTraceabilityPage(
        Section section,
        UtilityBillRecord bill,
        UtilityMeterReading from,
        UtilityMeterReading to,
        DateTimeOffset fromLocal,
        DateTimeOffset toLocal,
        UtilityGridImportStatisticalCompletion statistical,
        UtilityBillTariffScenarioAnalysis tariff,
        bool spanish)
    {
        string L(string es, string en) => spanish ? es : en;

        var sources = section.AddTable();
        sources.Borders.Width = 0.25;
        sources.AddColumn(Unit.FromCentimeter(1.2));
        sources.AddColumn(Unit.FromCentimeter(5.2));
        sources.AddColumn(Unit.FromCentimeter(9.9));
        var h = sources.AddRow();
        h.Format.Font.Bold = true;
        h.Cells[0].AddParagraph("ID");
        h.Cells[1].AddParagraph(L("Fuente", "Source"));
        h.Cells[2].AddParagraph(L("Uso / estado", "Use / status"));

        AddSourceRow(
            sources,
            "S1",
            L("Boleta Enel", "Utility bill"),
            L(
                "Período, lecturas, 97 kWh facturados, tarifa impresa, cargos, impuestos y total.",
                "Period, readings, 97 billed kWh, printed tariff, charges, taxes and total."));
        AddSourceRow(
            sources,
            "S2",
            L("Telemetría del inversor", "Inverter telemetry"),
            L(
                "mainsPower → grid_import_power_w; integración temporal directa de importación desde red.",
                "mainsPower → grid_import_power_w; direct time integration of grid import."));
        AddSourceRow(
            sources,
            "S3",
            L("Método estadístico", "Statistical method"),
            $"{statistical.MethodVersion} · {statistical.Status}");
        AddSourceRow(
            sources,
            "S4",
            L("Tarifas oficiales Enel", "Official utility tariffs"),
            tariff.PublicationPeriods.Count > 0
                ? string.Join(
                    " | ",
                    tariff.PublicationPeriods.Select(
                        item =>
                            $"{item.AppliedFrom:yyyy-MM-dd}..{item.AppliedTo:yyyy-MM-dd} · {item.Days} d · {item.PublicationTitle}" +
                            (string.IsNullOrWhiteSpace(item.ContentSha256)
                                ? string.Empty
                                : $" · SHA256 {item.ContentSha256[..Math.Min(12, item.ContentSha256.Length)]}…")))
                : L("Sin publicación resuelta.", "No publication resolved."));
        AddSourceRow(
            sources,
            "S5",
            L("Reglas regulatorias", "Regulatory rules"),
            L(
                "Prorrateo por días entre meses tarifarios, servicio público, FET y subsidio aplicable.",
                "Day allocation across tariff months, public-service charge, FET and applicable subsidy."));
        AddSourceRow(
            sources,
            "S6",
            L("Prueba de contador HPVINV02", "HPVINV02 counter probe"),
            L(
                "buyElectricityQuantity: placeholder; dayPurchaseElectricityConsumption: no poblado. No utilizable.",
                "buyElectricityQuantity: placeholder; dayPurchaseElectricityConsumption: not populated. Not usable."));
        AddSourceRow(
            sources,
            "S7",
            L("Motor económico", "Economic engine"),
            L(
                "Mismas tarifas oficiales aplicadas a Enel, observado, P5, P50 y P95; cargos no variables preservados.",
                "Same official rates applied to utility, observed, P5, P50 and P95; non-variable charges preserved."));

        AddHeading(
            section,
            L("Trazabilidad de lecturas oficiales", "Official-reading traceability"),
            11.5);

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
        AddReadingRow(
            readings,
            L("Inicial", "Start"),
            from,
            fromLocal,
            spanish);
        AddReadingRow(
            readings,
            L("Final", "End"),
            to,
            toLocal,
            spanish);

        AddTariffEvidence(
            section,
            tariff,
            spanish);
    }

    private static void AddSourceRow(
        Table table,
        string id,
        string source,
        string use)
    {
        var row = table.AddRow();
        row.Cells[0].AddParagraph(id);
        row.Cells[0].Format.Font.Bold = true;
        row.Cells[1].AddParagraph(source);
        row.Cells[2].AddParagraph(use);
    }

    private static void AddTechnicalMethodologyPage(
        Section section,
        UtilityGridImportStatisticalCompletion statistical,
        UtilityBillGapStatisticalAnalysis analysis,
        UtilityBillTariffScenarioAnalysis tariff,
        bool spanish)
    {
        string L(string es, string en) => spanish ? es : en;

        AddMethodBlock(
            section,
            L("Intervalo temporal", "Time interval"),
            string.Format(
                L(
                    "Se interpreta la boleta como días locales completos: [{0:dd-MM-yyyy} 00:00, {1:dd-MM-yyyy} 00:00), donde el segundo límite es el día posterior a la fecha 'hasta'.",
                    "The bill is interpreted as complete local days: [{0:dd-MM-yyyy} 00:00, {1:dd-MM-yyyy} 00:00), where the second bound is the day after the printed end date."),
                analysis.StartLocalDate.ToDateTime(TimeOnly.MinValue),
                analysis.EndLocalDateInclusive.AddDays(1).ToDateTime(TimeOnly.MinValue)));

        AddMethodBlock(
            section,
            L("Conversión potencia → energía", "Power → energy conversion"),
            L(
                "La potencia importada se integra usando timestamps reales y regla trapezoidal: Ei ≈ ((Pi + Pi+1) / 2) × Δti. Enlaces por sobre el umbral de continuidad se excluyen de la integración observada.",
                "Imported power is integrated using real timestamps and the trapezoidal rule: Ei ≈ ((Pi + Pi+1) / 2) × Δti. Links above the continuity threshold are excluded from observed integration."));

        AddMethodBlock(
            section,
            L("Definición de gap", "Gap definition"),
            string.Format(
                L(
                    "Cadencia mediana {0:N3} min; umbral de continuidad {1:N3} min. Ausencia no equivale a cero.",
                    "Median cadence {0:N3} min; continuity threshold {1:N3} min. Missing does not equal zero."),
                analysis.MedianCadenceMinutes,
                analysis.ContinuityThresholdMinutes));

        AddMethodBlock(
            section,
            L("Completación estadística", "Statistical completion"),
            L(
                "Para cada gap interno se usan ventanas históricas completas con el mismo horario local, la duración real del gap y el mismo tipo de día (laboral/fin de semana), siempre anteriores al objetivo. Se toman los 15 días elegibles más recientes, con mínimo 10. P5/P50/P95 se obtienen con cuantiles empíricos inversos. Los gaps distintos se agregan mediante combinaciones empíricas exactas.",
                "For each internal gap, complete historical windows with the same local clock time, actual gap duration and day type (weekday/weekend) are used, always prior to the target. The latest 15 eligible dates are used, minimum 10. P5/P50/P95 use inverse empirical quantiles. Distinct gaps are aggregated using exact empirical combinations."));

        AddMethodBlock(
            section,
            L("Percentiles", "Percentiles"),
            L(
                "P5 es un valor hacia el extremo inferior; P50 es la mediana y estimación central; P95 es un valor hacia el extremo superior. P5–P95 contiene el 90% central de los resultados producidos por este método. No es una tolerancia metrológica ni un intervalo de confianza certificado.",
                "P5 is a lower-side value; P50 is the median and central estimate; P95 is an upper-side value. P5–P95 contains the central 90% of results produced by this method. It is not a metrological tolerance or a certified confidence interval."));

        AddMethodBlock(
            section,
            L("Cuantiles empíricos discretos", "Discrete empirical quantiles"),
            L(
                "Cada gap interno usa 15 ventanas históricas. Con cuantiles empíricos inversos, q05 coincide con el mínimo y q95 con el máximo de esas 15 ventanas. El P95 total no es la suma de esos máximos: es el percentil 95 de las 3.375 combinaciones exactas de los tres gaps.",
                "Each internal gap uses 15 historical windows. With inverse empirical quantiles, q05 equals the minimum and q95 the maximum of those 15 windows. Total P95 is not the sum of those maxima: it is the 95th percentile of the 3,375 exact combinations of the three gaps."));

        AddMethodBlock(
            section,
            L("Dependencia entre gaps", "Dependence across gaps"),
            string.Format(
                L(
                    "La agregación P5/P50/P95 supone independencia empírica entre gaps separados. Ese supuesto no está validado como probabilidad metrológica. El máximo empírico conjunto de las combinaciones disponibles es {0}; por eso un valor Enel por encima del P95 provisional no se interpreta como imposibilidad ni exclusión estadística.",
                    "P5/P50/P95 aggregation assumes empirical independence across separated gaps. That assumption is not validated as a metrological probability model. The maximum joint empirical combination is {0}; therefore a utility value above provisional P95 is not interpreted as impossibility or statistical exclusion."),
                analysis.MaximumKwh.HasValue
                    ? $"{analysis.MaximumKwh.Value:N3} kWh"
                    : "—"));

        AddMethodBlock(
            section,
            L("Independencia respecto de Enel", "Independence from utility value"),
            L(
                "Los 97 kWh facturados no se usan para construir, calibrar, seleccionar ni puntuar la distribución del inversor. Enel se incorpora sólo después como referencia de comparación.",
                "The billed 97 kWh are not used to construct, calibrate, select or score the inverter distribution. The utility value is introduced only afterward as a comparison reference."));

        AddMethodBlock(
            section,
            L("Estado de validación", "Validation status"),
            string.Format(
                L(
                    "Método {0}: PROVISIONAL / RESEARCH ONLY. R3 permanece como investigación separada y no se presenta como validado para esta boleta.",
                    "Method {0}: PROVISIONAL / RESEARCH ONLY. R3 remains a separate research stream and is not presented as validated for this bill."),
                statistical.MethodVersion));

        AddMethodBlock(
            section,
            L("Contador energético alternativo", "Alternative energy counter"),
            L(
                "En este HPVINV02, buyElectricityQuantity es placeholder (isRealValue=false) y dayPurchaseElectricityConsumption no está poblado. Por ello no existe un segundo contador utilizable para corroborar la integración de mainsPower.",
                "On this HPVINV02, buyElectricityQuantity is a placeholder (isRealValue=false) and dayPurchaseElectricityConsumption is not populated. Therefore no second usable counter exists to corroborate mainsPower integration."));

        AddMethodBlock(
            section,
            L("Tarifas", "Tariffs"),
            tariff.PublicationPeriods.Count > 1
                ? L(
                    "La boleta cruza más de un mes tarifario. La energía se distribuye por días calendario entre los períodos oficiales aplicables y la misma regla se usa en todos los escenarios.",
                    "The bill crosses more than one tariff month. Energy is allocated by calendar days across the applicable official periods and the same rule is used for every scenario.")
                : L(
                    "Se aplica la publicación tarifaria oficial correspondiente al período.",
                    "The official tariff publication applicable to the period is used."));

        AddCallout(
            section,
            L("LIMITACIÓN PRINCIPAL", "MAIN LIMITATION"),
            L(
                "Los registros del inversor constituyen evidencia técnica independiente, pero no se presentan como sustituto metrológicamente certificado del medidor de facturación. El objetivo del informe es hacer transparente, reproducible y auditable la discrepancia observada.",
                "Inverter records are independent technical evidence, but are not presented as a metrologically certified substitute for the billing meter. The report's purpose is to make the observed discrepancy transparent, reproducible and auditable."),
            Colors.WhiteSmoke);
    }

    private static void AddMethodBlock(
        Section section,
        string title,
        string text)
    {
        var p = section.AddParagraph();
        p.Format.SpaceAfter = Unit.FromPoint(5);
        var bold = p.AddFormattedText(
            title + ": ",
            TextFormat.Bold);
        bold.Font.Size = 9.5;
        p.AddText(text);
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

    private static void AddAuditMetricCard(
        Cell cell,
        string label,
        string value,
        string context,
        Color fillColor,
        double valueFontSize)
    {
        cell.Borders.Width = 0.4;
        cell.Borders.Color = Colors.LightGray;
        cell.Shading.Color = fillColor;
        cell.VerticalAlignment = VerticalAlignment.Center;
        cell.Format.Alignment = ParagraphAlignment.Center;

        var labelParagraph = cell.AddParagraph(label);
        labelParagraph.Format.Font.Bold = true;
        labelParagraph.Format.Font.Size = 8.2;
        labelParagraph.Format.SpaceBefore = Unit.FromPoint(4);
        labelParagraph.Format.SpaceAfter = Unit.FromPoint(3);

        var valueParagraph = cell.AddParagraph(value);
        valueParagraph.Format.Font.Bold = true;
        valueParagraph.Format.Font.Size = valueFontSize;
        valueParagraph.Format.SpaceAfter = Unit.FromPoint(3);

        var contextParagraph = cell.AddParagraph(context);
        contextParagraph.Format.Font.Size = 7.4;
        contextParagraph.Format.SpaceAfter = Unit.FromPoint(5);
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

    private static string MoneySigned(double? value)
    {
        if (!value.HasValue)
            return "—";

        if (Math.Abs(value.Value) < 0.5)
            return "$ 0";

        return value.Value > 0
            ? $"+$ {value.Value:N0}"
            : $"- $ {Math.Abs(value.Value):N0}";
    }

    private static void EnsurePdfFonts()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "PDF export is supported by the Windows desktop application.");

        if (Interlocked.Exchange(ref _pdfFontsInitialized, 1) == 0)
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }
}
