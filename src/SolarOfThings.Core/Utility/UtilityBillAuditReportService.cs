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
    private readonly UtilityGridImportStatisticalCompletionService _statisticalCompletion;
    private readonly UtilityBillTariffScenarioAnalysisService _tariffScenarioAnalysis;
    private static int _pdfFontsInitialized;

    public UtilityBillAuditReportService(
        UtilityMeterRepository repository,
        UtilityReconciliationService reconciliation,
        TariffBillRateVerificationService tariffVerification,
        UtilityGridImportStatisticalCompletionService statisticalCompletion,
        UtilityBillTariffScenarioAnalysisService tariffScenarioAnalysis)
    {
        _repository = repository;
        _reconciliation = reconciliation;
        _tariffVerification = tariffVerification;
        _statisticalCompletion = statisticalCompletion;
        _tariffScenarioAnalysis = tariffScenarioAnalysis;
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
        var statistical = _statisticalCompletion.Analyze(
            deviceId,
            from.ReadingAtUtc,
            to.ReadingAtUtc,
            timeZoneId);
        var tariffScenario = _tariffScenarioAnalysis.Analyze(
            billId,
            timeZoneId,
            statistical);

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

        AddHeading(
            section,
            L("Resumen ejecutivo", "Executive summary"),
            13);
        AddExecutiveEnergyComparison(
            section,
            bill,
            energy,
            statistical,
            spanish);
        AddExecutiveInterpretation(
            section,
            bill,
            statistical,
            spanish);

        AddHeading(
            section,
            L("Impacto económico según tarifa oficial", "Financial impact under official tariff"),
            13);
        AddFinancialScenarioComparison(
            section,
            tariffScenario,
            spanish);

        section.AddPageBreak();

        AddHeading(
            section,
            L("Conciliación de cargos reales", "Actual-charge reconciliation"),
            14);
        AddActualBillLines(
            section,
            bill,
            spanish);
        AddTariffComponentReconciliation(
            section,
            tariffScenario,
            spanish);

        section.AddPageBreak();

        AddHeading(
            section,
            L("Calidad, incertidumbre y evidencia", "Quality, uncertainty and evidence"),
            14);
        AddStatisticalEvidence(
            section,
            statistical,
            energy,
            spanish);
        AddTariffEvidence(
            section,
            tariffScenario,
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

    private static void AddExecutiveEnergyComparison(
        Section section,
        UtilityBillRecord bill,
        UtilityMeterReconciliation energy,
        UtilityGridImportStatisticalCompletion statistical,
        bool spanish)
    {
        string L(string es, string en) => spanish ? es : en;
        var enel = bill.BilledConsumptionKwh ??
                   energy.MeterConsumptionKwh;

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
            L("SOLAR OF THINGS · OBSERVADO", "SOLAR OF THINGS · OBSERVED"),
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
                "Rango predictivo Solar of Things",
                "Solar of Things predictive range"));
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
                        "5% de imputaciones quedan por debajo.",
                        "5% of imputations fall below."))
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
                        "Mediana de las 2.000 imputaciones.",
                        "Median of the 2,000 imputations."))
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
                        "95% de imputaciones quedan por debajo.",
                        "95% of imputations fall below."))
                : L("Sin evidencia suficiente", "Insufficient evidence"),
            Colors.AliceBlue,
            13);
    }

    private static string DifferenceContext(
        double value,
        double? enel,
        string evidence)
    {
        if (!enel.HasValue)
            return evidence;

        var difference = value - enel.Value;
        var percent = enel.Value > 0
            ? difference / enel.Value * 100.0
            : 0;

        return $"Δ Enel {difference:+0.000;-0.000;0.000} kWh " +
               $"({percent:+0.00;-0.00;0.00}%) · {evidence}";
    }

    private static void AddExecutiveInterpretation(
        Section section,
        UtilityBillRecord bill,
        UtilityGridImportStatisticalCompletion statistical,
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
                    "La comparación observada está disponible, pero todavía no hay evidencia estadística suficiente para ubicar el valor Enel dentro de un intervalo predictivo.",
                    "The observed comparison is available, but statistical evidence is not yet sufficient to place the utility value inside a predictive interval."),
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

        AddCallout(
            section,
            L("LECTURA DEL RESULTADO", "RESULT INTERPRETATION"),
            inside
                ? string.Format(
                    L(
                        "Los {0:N3} kWh de Enel caen dentro del intervalo predictivo Solar of Things P5–P95 ({1:N3}–{2:N3} kWh). La estimación central es {3:N3} kWh y Enel está a {4:N3} kWh de esa mediana. Esto no prueba equivalencia metrológica: indica compatibilidad con el patrón estadístico de la telemetría disponible.",
                        "The utility value of {0:N3} kWh falls inside the Solar of Things P5–P95 predictive interval ({1:N3}–{2:N3} kWh). The central estimate is {3:N3} kWh and the utility value is {4:N3} kWh from that median. This does not prove metrological equivalence; it indicates compatibility with the statistical pattern of available telemetry."),
                    enel.Value,
                    lower,
                    upper,
                    central,
                    distance)
                : string.Format(
                    L(
                        "Los {0:N3} kWh de Enel quedan fuera del intervalo predictivo Solar of Things P5–P95 ({1:N3}–{2:N3} kWh), a {3:N3} kWh del límite más cercano. Esto justifica revisión adicional de cobertura, límites temporales, medición y facturación.",
                        "The utility value of {0:N3} kWh falls outside the Solar of Things P5–P95 predictive interval ({1:N3}–{2:N3} kWh), {3:N3} kWh from the nearest bound. This supports additional review of coverage, time boundaries, metering and billing."),
                    enel.Value,
                    lower,
                    upper,
                    distance),
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
                    "Subtotal variable respaldado: {0:N3} CLP/kWh. Las mismas tasas oficiales se aplican a todos los escenarios para que la comparación sea equivalente.",
                    "Supported variable subtotal: {0:N3} CLP/kWh. The same official rates are applied to every scenario for an equivalent comparison."),
                analysis.SupportedVariableRateClpPerKwh.Value));
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
            L("SOLAR OBSERVADO", "SOLAR OBSERVED"),
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
            L("Escenario estadístico P5.", "P5 statistical scenario."),
            Colors.AliceBlue);

        AddAuditMoneyCard(
            row.Cells[1],
            L("P50 · COSTO CENTRAL", "P50 · CENTRAL COST"),
            analysis.Scenarios.FirstOrDefault(item =>
                item.Key == "SOLAR_CENTRAL"),
            enel,
            L("Escenario central P50.", "Central P50 scenario."),
            Colors.Honeydew);

        AddAuditMoneyCard(
            row.Cells[2],
            L("P95 · COSTO SUPERIOR", "P95 · UPPER COST"),
            analysis.Scenarios.FirstOrDefault(item =>
                item.Key == "SOLAR_UPPER"),
            enel,
            L("Escenario estadístico P95.", "P95 statistical scenario."),
            Colors.AliceBlue);

        AddCallout(
            section,
            L("ALCANCE DEL MONTO", "AMOUNT SCOPE"),
            L(
                "Son subtotales comparables de componentes tarifarios conciliados con evidencia oficial. Subsidios, cargos fijos, FET u otros ajustes sin regla reconstruida permanecen fuera del escenario.",
                "These are comparable subtotals for tariff components reconciled with official evidence. Subsidies, fixed charges, FET or other adjustments without a reconstructed rule remain outside the scenario."),
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
        table.AddColumn(Unit.FromCentimeter(4.2));
        table.AddColumn(Unit.FromCentimeter(2.7));
        table.AddColumn(Unit.FromCentimeter(2.8));
        table.AddColumn(Unit.FromCentimeter(2.8));
        table.AddColumn(Unit.FromCentimeter(2.2));
        table.AddColumn(Unit.FromCentimeter(2.0));

        var h = table.AddRow();
        h.Format.Font.Bold = true;
        h.Cells[0].AddParagraph(L("Línea", "Line"));
        h.Cells[1].AddParagraph(L("Tasa oficial", "Official rate"));
        h.Cells[2].AddParagraph(L("Monto real", "Actual"));
        h.Cells[3].AddParagraph(L("Reconstruido", "Reconstructed"));
        h.Cells[4].AddParagraph(L("Diferencia", "Difference"));
        h.Cells[5].AddParagraph(L("Evidencia", "Evidence"));

        foreach (var item in analysis.Components)
        {
            var r = table.AddRow();
            r.Cells[0].AddParagraph(item.BillLineDescription);
            r.Cells[1].AddParagraph(
                $"$ {item.RateClpPerKwh:N3}/kWh");
            r.Cells[2].AddParagraph(
                Money(item.ActualLineAmountClp));
            r.Cells[3].AddParagraph(
                Money(item.ReconstructedAmountClp));
            r.Cells[4].AddParagraph(
                MoneySigned(item.DifferenceClp));
            r.Cells[5].AddParagraph(
                item.EvidenceStatus.Contains(
                    "AMBIGUOUS",
                    StringComparison.Ordinal)
                    ? L("Tasa concilia; aplicabilidad no única", "Rate reconciles; applicability not unique")
                    : L("Conciliado con fuente oficial", "Reconciled to official source"));
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
    }

    private static void AddStatisticalEvidence(
        Section section,
        UtilityGridImportStatisticalCompletion statistical,
        UtilityMeterReconciliation energy,
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
            L("MUESTRAS DONANTES", "DONOR SAMPLES"),
            statistical.DonorSampleCount.ToString("N0"),
            L(
                "Telemetría comparable usada por el bootstrap.",
                "Comparable telemetry used by the bootstrap."),
            Colors.AliceBlue,
            13);

        var second = cards.AddRow();
        AddAuditMetricCard(
            second.Cells[0],
            L("SIMULACIONES", "SIMULATIONS"),
            statistical.SimulationCount.ToString("N0"),
            L("Construyen P5/P50/P95.", "Build P5/P50/P95."),
            Colors.WhiteSmoke,
            13);
        AddAuditMetricCard(
            second.Cells[1],
            L("DESVIACIÓN SIMULADA", "SIMULATED STD. DEV."),
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
                "Bootstrap empírico por hora local y tipo de día.",
                "Empirical bootstrap by local hour and day type."),
            Colors.Honeydew,
            12);

        AddCallout(
            section,
            L("CÓMO LEER LOS PERCENTILES", "HOW TO READ THE PERCENTILES"),
            L(
                "P5 es el margen inferior: sólo 5% de las imputaciones simuladas quedó por debajo. P50 es la mediana y la estimación central. P95 es el margen superior: 95% quedó por debajo. Enel no participa en la construcción del rango; se compara después como referencia independiente.",
                "P5 is the lower margin: only 5% of simulated imputations fell below it. P50 is the median and central estimate. P95 is the upper margin: 95% fell below it. The utility value does not participate in building the range; it is compared afterward as an independent reference."),
            Colors.Honeydew);

        if (energy.Sensitivity is not null)
        {
            AddCallout(
                section,
                L("LÍMITE DE INGENIERÍA (NO PROBABILÍSTICO)", "ENGINEERING BOUND (NOT PROBABILISTIC)"),
                L(
                    "El antiguo escenario de máxima potencia observada se conserva sólo como diagnóstico extremo y no se usa como rango probable.",
                    "The previous maximum-observed-power scenario is retained only as an extreme diagnostic and is not used as a probable range."),
                Colors.WhiteSmoke);
        }
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

        AddAuditMetricCard(
            row.Cells[0],
            L("VIGENCIA OFICIAL", "OFFICIAL EFFECTIVE PERIOD"),
            analysis.EffectiveFrom.HasValue
                ? $"{analysis.EffectiveFrom.Value:yyyy-MM}"
                : "—",
            analysis.IsRetroactive
                ? L("Versión retroactiva preferida.", "Preferred retroactive version.")
                : L("Versión vigente capturada.", "Captured effective version."),
            Colors.AliceBlue,
            12);

        AddAuditMetricCard(
            row.Cells[1],
            "RED / ETR",
            $"{analysis.NetworkType ?? "?"} / {analysis.EtrBand ?? "?"}",
            L(
                "Aplicabilidad inferida por conciliación de la boleta.",
                "Applicability inferred by bill reconciliation."),
            Colors.AliceBlue,
            12);

        AddAuditMetricCard(
            row.Cells[2],
            L("TASA VARIABLE MODELADA", "MODELED VARIABLE RATE"),
            analysis.SupportedVariableRateClpPerKwh.HasValue
                ? $"$ {analysis.SupportedVariableRateClpPerKwh.Value:N3}/kWh"
                : "—",
            L(
                "Suma de componentes que conciliaron con evidencia oficial.",
                "Sum of components reconciled with official evidence."),
            Colors.Honeydew,
            11.5);

        var source = section.AddParagraph(
            string.Format(
                L(
                    "Fuente: Enel Distribución Chile · tarifas de suministro eléctrico · {0}{1}.",
                    "Source: Enel Distribución Chile · electricity supply tariffs · {0}{1}."),
                analysis.EffectiveFrom.HasValue
                    ? analysis.EffectiveFrom.Value.ToString("MMMM yyyy")
                    : L("vigencia no identificada", "unidentified effective period"),
                analysis.IsRetroactive
                    ? L(" · publicación retroactiva", " · retroactive publication")
                    : string.Empty));
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
                spanish ? "Solar observado" : "Solar observed",
            "SOLAR_LOWER" =>
                spanish ? "Solar inferior P5" : "Solar lower P5",
            "SOLAR_CENTRAL" =>
                spanish ? "Solar central P50" : "Solar central P50",
            "SOLAR_UPPER" =>
                spanish ? "Solar superior P95" : "Solar upper P95",
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

    private static string MoneySigned(double? value) =>
        !value.HasValue
            ? "—"
            : value.Value > 0
                ? $"+$ {value.Value:N0}"
                : value.Value < 0
                    ? $"- $ {Math.Abs(value.Value):N0}"
                    : "$ 0";

    private static void EnsurePdfFonts()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "PDF export is supported by the Windows desktop application.");

        if (Interlocked.Exchange(ref _pdfFontsInitialized, 1) == 0)
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }
}
