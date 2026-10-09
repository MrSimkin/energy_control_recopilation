using ClosedXML.Excel;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Shapes.Charts;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using SolarOfThings.Core.SolarOfThings;
using SolarOfThings.Core.Statistics;
using SolarOfThings.Core.Utility;

namespace SolarOfThings.Core.Reporting;

public sealed class EnergyReportExportService
{
    private readonly EnergyRangeStatisticsService _statistics;
    private readonly EnergyAggregationTableService _aggregation;
    private readonly FamilyReportAnalysisService _familyAnalysis;
    private readonly SourceAttributionService _sourceAttribution;
    private readonly UtilityGridImportStatisticalCompletionService _gridImportStatistical;
    private static int _pdfFontsInitialized;

    public EnergyReportExportService(
        EnergyRangeStatisticsService statistics,
        EnergyAggregationTableService aggregation,
        FamilyReportAnalysisService familyAnalysis,
        SourceAttributionService sourceAttribution,
        UtilityGridImportStatisticalCompletionService gridImportStatistical)
    {
        _statistics = statistics;
        _aggregation = aggregation;
        _familyAnalysis = familyAnalysis;
        _sourceAttribution = sourceAttribution;
        _gridImportStatistical = gridImportStatistical;
    }

    public EnergyReportData Build(
        EnergyReportRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var summary = _statistics.Get(
            request.DeviceId,
            request.StartUtc,
            request.EndUtc,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var table = _aggregation.Get(
            request.DeviceId,
            request.StartUtc,
            request.EndUtc,
            request.TimeZoneId,
            request.Aggregation,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        var family = _familyAnalysis.Analyze(request);
        cancellationToken.ThrowIfCancellationRequested();
        var attribution = _sourceAttribution.Get(
            request.DeviceId,
            request.StartUtc,
            request.EndUtc,
            request.TimeZoneId,
            request.Aggregation);
        cancellationToken.ThrowIfCancellationRequested();
        var dailyAttribution =
            request.Aggregation == AggregationPeriod.Day
                ? attribution
                : _sourceAttribution.Get(
                    request.DeviceId,
                    request.StartUtc,
                    request.EndUtc,
                    request.TimeZoneId,
                    AggregationPeriod.Day);

        cancellationToken.ThrowIfCancellationRequested();
        var gridImportStatistical =
            _gridImportStatistical.Analyze(
                request.DeviceId,
                request.StartUtc,
                request.EndUtc,
                request.TimeZoneId);

        cancellationToken.ThrowIfCancellationRequested();
        return new EnergyReportData(
            request,
            summary,
            table,
            family,
            attribution,
            dailyAttribution,
            gridImportStatistical,
            DateTimeOffset.UtcNow);
    }

    public void ExportExcel(
        string path,
        EnergyReportData report,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        string? chartDirectory = null;
        IReadOnlyList<string>? familyCharts = null;

        try
        {
            if (report.Request.Kind == ReportKind.SimpleEnergy)
            {
                chartDirectory = CreateChartTempDirectory();
                familyCharts = new ReportChartRenderer()
                    .RenderFamilyCharts(report, chartDirectory);
            }

            cancellationToken.ThrowIfCancellationRequested();
            using var workbook = new XLWorkbook();
            AddSummarySheet(workbook, report, familyCharts);
            cancellationToken.ThrowIfCancellationRequested();
            if (report.Request.Kind == ReportKind.SimpleEnergy)
            {
                AddPatternsSheet(workbook, report);
                cancellationToken.ThrowIfCancellationRequested();
                AddGridUseSheet(workbook, report);
                cancellationToken.ThrowIfCancellationRequested();
                AddEventsSheet(workbook, report);
                cancellationToken.ThrowIfCancellationRequested();
                AddEvolutionSheet(workbook, report);
                cancellationToken.ThrowIfCancellationRequested();
            }
            AddDetailSheet(workbook, report);
            cancellationToken.ThrowIfCancellationRequested();
            AddQualitySheet(workbook, report);
            cancellationToken.ThrowIfCancellationRequested();
            AddGlossarySheet(workbook, report);
            cancellationToken.ThrowIfCancellationRequested();
            ApplyWorkbookTypography(workbook);
            cancellationToken.ThrowIfCancellationRequested();
            workbook.SaveAs(path);
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            DeleteChartTempDirectory(chartDirectory);
        }
    }

    public void ExportPdf(
        string path,
        EnergyReportData report,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();
        EnsurePdfFonts();

        string? chartDirectory = null;
        IReadOnlyList<string>? familyCharts = null;

        if (report.Request.Kind == ReportKind.SimpleEnergy)
        {
            chartDirectory = CreateChartTempDirectory();
            familyCharts = new ReportChartRenderer()
                .RenderFamilyCharts(report, chartDirectory);
        }

        try
        {
        cancellationToken.ThrowIfCancellationRequested();
        var document = new Document();
        document.Info.Title = DisplayReportTitle(report);
        document.Info.Subject = "Solar Energy Monitor";

        var normal = document.Styles["Normal"]!;
        normal.Font.Name = PreferredPdfTextFont();
        normal.Font.Size = 9;

        var section = document.AddSection();
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.3);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.3);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.3);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.3);

        var heading = section.AddParagraph(DisplayReportTitle(report));
        heading.Format.Font.Size = 18;
        heading.Format.Font.Bold = true;
        heading.Format.SpaceAfter = Unit.FromPoint(8);

        section.AddParagraph(string.Format(
            L(report, "Período seleccionado: {0} — {1}", "Selected period: {0} — {1}"),
            report.Request.LocalStartDate.ToString("dd-MM-yyyy"),
            report.Request.LocalEndDate.ToString("dd-MM-yyyy")));
        section.AddParagraph(
            L(report, "Ventana exacta: ", "Exact window: ") +
            ExactLocalWindowLabel(report));
        section.AddParagraph(
            L(
                report,
                $"Hora local de la estación ({report.Request.TimeZoneId}); el día final se incluye completo.",
                $"Station local time ({report.Request.TimeZoneId}); the final day is included in full."));
        section.AddParagraph(string.Format(
            L(report, "Agrupación: {0}", "Aggregation: {0}"),
            AggregationLabel(report)));
        section.AddParagraph(string.Format(
            L(report, "Generado: {0}", "Generated: {0}"),
            report.GeneratedUtc.ToLocalTime().ToString("dd-MM-yyyy HH:mm")));

        if (report.Request.Kind == ReportKind.SimpleEnergy)
        {
            AddFamilySimplePdf(section, report, familyCharts);
        }
        else
        {
            AddSummary(section, report);

            if (report.Request.Kind == ReportKind.DetailedEnergy)
            {
                AddEnergyChart(section, report);
            }

            if (report.Request.Kind is ReportKind.Battery or ReportKind.DetailedEnergy)
            {
                AddBatterySocChart(section, report);
            }

            if (report.Request.Kind == ReportKind.DetailedEnergy)
            {
                AddDetailedTable(section, report);
            }
            else if (report.Request.Kind == ReportKind.Battery)
            {
                AddBatteryTable(section, report);
            }

            AddQuality(section, report);
            AddGlossary(section, report);
        }

        cancellationToken.ThrowIfCancellationRequested();
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
        cancellationToken.ThrowIfCancellationRequested();
        renderer.PdfDocument.Save(path);
        cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            DeleteChartTempDirectory(chartDirectory);
        }
    }

    private static void AddFamilySimplePdf(
        Section section,
        EnergyReportData report,
        IReadOnlyList<string>? familyCharts)
    {
        AddFamilyPageOne(section, report);

        var pageTwo = section.AddParagraph(
            L(report, "Página 2 — Qué pasó con toda la energía", "Page 2 — What happened to all the energy"));
        pageTwo.Format.PageBreakBefore = true;
        pageTwo.Format.Font.Size = 15;
        pageTwo.Format.Font.Bold = true;
        pageTwo.Format.SpaceAfter = Unit.FromPoint(6);

        var partial = FamilyCoveragePercent(report) < 80.0;
        var cards = section.AddTable();
        cards.Borders.Width = 0;
        cards.AddColumn(Unit.FromCentimeter(4.35));
        cards.AddColumn(Unit.FromCentimeter(4.35));
        cards.AddColumn(Unit.FromCentimeter(4.35));

        var firstCardRow = cards.AddRow();
        AddPdfMetricCard(
            firstCardRow.Cells[0],
            partial
                ? L(report, "Solar producido registrado", "Recorded solar production")
                : L(report, "Solar producido", "Solar production"),
            EnergyValue(report.Summary.PvPower, report.Summary.PvEnergyKwh, report),
            string.Format(L(report, "Cobertura {0:N1}%", "Coverage {0:N1}%"), report.Summary.PvPower.CoveragePercent));
        AddPdfMetricCard(
            firstCardRow.Cells[1],
            partial
                ? L(report, "Consumo de casa registrado", "Recorded home consumption")
                : L(report, "Consumo de la casa", "Home consumption"),
            EnergyValue(report.Summary.HouseLoadPower, report.Summary.HouseEnergyKwh, report),
            string.Format(L(report, "Cobertura {0:N1}%", "Coverage {0:N1}%"), report.Summary.HouseLoadPower.CoveragePercent));
        AddPdfMetricCard(
            firstCardRow.Cells[2],
            partial
                ? L(report, "IMPORTACIÓN TOTAL DE ENEL REGISTRADA", "RECORDED TOTAL UTILITY IMPORT")
                : L(report, "IMPORTACIÓN TOTAL DESDE ENEL", "TOTAL IMPORT FROM UTILITY"),
            EnergyValue(report.Summary.GridImportPower, report.Summary.GridImportEnergyKwh, report),
            string.Format(
                L(
                    report,
                    "Cobertura {0:N1}% · COMPARAR CON MEDIDOR/BOLETA. Es toda la energía que entró desde Enel al sistema, no sólo la que fue a la casa.",
                    "Coverage {0:N1}% · COMPARE WITH UTILITY METER/BILL. This is all energy entering the system from the utility, not only the part that supplied the home."),
                report.Summary.GridImportPower.CoveragePercent));

        var secondCardRow = cards.AddRow();
        AddPdfMetricCard(
            secondCardRow.Cells[0],
            L(report, "Batería: energía entregada", "Battery: energy discharged"),
            EnergyValue(report.Summary.BatteryPower, report.Summary.BatteryDischargedEnergyKwh, report),
            L(report, "Movimiento total de salida", "Total movement out"));
        AddPdfMetricCard(
            secondCardRow.Cells[1],
            L(report, "Batería: energía recibida", "Battery: energy charged"),
            EnergyValue(report.Summary.BatteryPower, report.Summary.BatteryChargedEnergyKwh, report),
            L(report, "Movimiento total de entrada", "Total movement in"));
        AddPdfMetricCard(
            secondCardRow.Cells[2],
            L(report, "Atribución de origen", "Source attribution"),
            $"{report.Attribution.AttributionCoverageOfObservedPercent:N1}%",
            string.Format(
                L(report, "Sin atribuir {0:N2} kWh", "Unattributed {0:N2} kWh"),
                report.Attribution.UnattributedHouseKwh));

        AddGridImportPredictiveCardsPdf(
            section,
            report,
            compact: true);

        var unavailable = section.AddParagraph(
            L(
                report,
                "Energía solar que no pudimos aprovechar: todavía no puede calcularse con suficiente confianza. No se estima como un residuo.",
                "Solar energy we could not use: it cannot yet be calculated with enough confidence. It is not estimated as a residual."));
        unavailable.Format.SpaceBefore = Unit.FromPoint(6);
        unavailable.Format.Font.Italic = true;

        AddFamilyReportChartsPdf(section, report, familyCharts);

        var gridUsePage = section.AddParagraph(
            L(report, "Página 4 — Uso de Enel / red", "Page 4 — Utility / grid use"));
        gridUsePage.Format.PageBreakBefore = true;
        gridUsePage.Format.Font.Size = 15;
        gridUsePage.Format.Font.Bold = true;
        gridUsePage.Format.SpaceAfter = Unit.FromPoint(6);

        AddGridUsePdf(section, report);

        var patternsPage = section.AddParagraph(
            L(report, "Página 5 — Patrones y episodios nocturnos", "Page 5 — Patterns and nighttime episodes"));
        patternsPage.Format.PageBreakBefore = true;
        patternsPage.Format.Font.Size = 15;
        patternsPage.Format.Font.Bold = true;
        patternsPage.Format.SpaceAfter = Unit.FromPoint(6);

        AddFamilyPatternsPdf(section, report);
        AddFamilyEventsPdf(section, report);

        var annex = section.AddParagraph(
            L(report, "Página 6 — Anexo técnico: calidad y glosario", "Page 6 — Technical annex: quality and glossary"));
        annex.Format.PageBreakBefore = true;
        annex.Format.Font.Size = 15;
        annex.Format.Font.Bold = true;
        annex.Format.SpaceAfter = Unit.FromPoint(6);

        AddQuality(section, report);
        AddGlossary(section, report);
    }

    private static void AddFamilyPageOne(
        Section section,
        EnergyReportData report)
    {
        var familyCoverage = FamilyCoveragePercent(report);
        if (familyCoverage < 80.0)
        {
            var warning = section.AddParagraph(string.Format(
                L(
                    report,
                    "RESUMEN PARCIAL: sólo hay datos suficientes para aproximadamente {0:N1}% del período. Las cifras muestran lo medido y no proyectan los huecos.",
                    "PARTIAL SUMMARY: enough data is available for only approximately {0:N1}% of the period. Figures show measured data and do not project across gaps."),
                familyCoverage));
            warning.Format.Font.Bold = true;
            warning.Format.SpaceBefore = Unit.FromPoint(8);
            warning.Format.SpaceAfter = Unit.FromPoint(8);
        }

        var overviewHeading = section.AddParagraph(
            L(
                report,
                "Resumen de la casa",
                "Household summary"));
        overviewHeading.Format.Font.Size = 13;
        overviewHeading.Format.Font.Bold = true;
        overviewHeading.Format.SpaceBefore = Unit.FromPoint(8);
        overviewHeading.Format.SpaceAfter = Unit.FromPoint(5);

        var overview = section.AddTable();
        overview.Borders.Width = 0;
        overview.AddColumn(Unit.FromCentimeter(8.15));
        overview.AddColumn(Unit.FromCentimeter(8.15));
        var overviewRow = overview.AddRow();

        AddPdfFamilyCard(
            overviewRow.Cells[0],
            L(report, "CONSUMO TOTAL DE LA CASA", "TOTAL HOUSEHOLD CONSUMPTION"),
            report.Attribution.ObservedHouseKwh > 0
                ? $"{report.Attribution.ObservedHouseKwh:N2} kWh"
                : L(report, "Sin datos", "No data"),
            L(
                report,
                "Todo lo usado por la vivienda en la parte observable del período.",
                "Everything used by the home in the observable part of the period."),
            Colors.AliceBlue,
            17);

        AddPdfFamilyCard(
            overviewRow.Cells[1],
            L(report, "ORIGEN IDENTIFICADO", "SOURCE IDENTIFIED"),
            $"{report.Attribution.AttributionCoverageOfObservedPercent:N1}%",
            string.Format(
                L(
                    report,
                    "{0:N2} kWh permanecen sin atribuir; no se reparten artificialmente.",
                    "{0:N2} kWh remain unattributed; they are not artificially distributed."),
                report.Attribution.UnattributedHouseKwh),
            Colors.AliceBlue,
            17);

        var sourceHeading = section.AddParagraph(
            L(
                report,
                "¿De dónde vino la energía que usó la casa?",
                "Where did the household's energy come from?"));
        sourceHeading.Format.Font.Size = 13;
        sourceHeading.Format.Font.Bold = true;
        sourceHeading.Format.SpaceBefore = Unit.FromPoint(9);
        sourceHeading.Format.SpaceAfter = Unit.FromPoint(5);

        var sources = section.AddTable();
        sources.Borders.Width = 0;
        sources.AddColumn(Unit.FromCentimeter(5.4));
        sources.AddColumn(Unit.FromCentimeter(5.4));
        sources.AddColumn(Unit.FromCentimeter(5.4));
        var sourceRow = sources.AddRow();

        AddPdfFamilyCard(
            sourceRow.Cells[0],
            L(
                report,
                "1. ENEL → CASA (NO ES LA BOLETA)",
                "1. UTILITY → HOME (NOT THE BILL)"),
            report.Attribution.ObservedHouseKwh > 0
                ? $"{report.Attribution.GridToHouseKwh:N2} kWh"
                : L(report, "Sin datos", "No data"),
            SourceShareContext(
                report,
                report.Attribution.GridToHouseKwh,
                L(
                    report,
                    "del consumo observado · Sólo Enel → Casa; NO comparar con boleta/medidor",
                    "of observed consumption · Utility → Home only; DO NOT compare with bill/meter")),
            Colors.AliceBlue,
            15);

        AddPdfFamilyCard(
            sourceRow.Cells[1],
            L(report, "2. DIRECTAMENTE DEL SOL", "2. DIRECTLY FROM SOLAR"),
            report.Attribution.ObservedHouseKwh > 0
                ? $"{report.Attribution.SolarToHouseKwh:N2} kWh"
                : L(report, "Sin datos", "No data"),
            SourceShareContext(
                report,
                report.Attribution.SolarToHouseKwh,
                L(report, "del consumo observado", "of observed consumption")),
            Colors.AliceBlue,
            15);

        AddPdfFamilyCard(
            sourceRow.Cells[2],
            L(report, "3. DESDE LA BATERÍA", "3. FROM THE BATTERY"),
            report.Attribution.ObservedHouseKwh > 0
                ? $"{report.Attribution.BatteryToHouseKwh:N2} kWh"
                : L(report, "Sin datos", "No data"),
            SourceShareContext(
                report,
                report.Attribution.BatteryToHouseKwh,
                L(report, "del consumo observado", "of observed consumption")),
            Colors.AliceBlue,
            15);

        var billingImport = section.AddTable();
        billingImport.Borders.Width = 0;
        billingImport.AddColumn(Unit.FromCentimeter(16.3));
        var billingRow = billingImport.AddRow();

        AddPdfFamilyCard(
            billingRow.Cells[0],
            L(
                report,
                "IMPORTACIÓN TOTAL DESDE ENEL — COMPARAR CON MEDIDOR / BOLETA",
                "TOTAL IMPORT FROM UTILITY — COMPARE WITH METER / BILL"),
            report.Summary.GridImportPower.SampleCount >= 2
                ? $"{report.Summary.GridImportEnergyKwh:N2} kWh"
                : L(report, "Sin datos suficientes", "Insufficient data"),
            string.Format(
                L(
                    report,
                    "OBSERVADO directamente · cobertura temporal {0:N1}%. Los huecos no se consideran cero; abajo se muestra el rango estadístico P5/P50/P95.",
                    "DIRECTLY observed · temporal coverage {0:N1}%. Gaps are not treated as zero; the P5/P50/P95 statistical range is shown below."),
                report.Summary.GridImportPower.CoveragePercent),
            Colors.Honeydew,
            15);

        AddGridImportPredictiveCardsPdf(
            section,
            report,
            compact: false);

        var completeNights = Math.Max(
            0,
            report.Request.LocalEndDate.DayNumber -
            report.Request.LocalStartDate.DayNumber);
        var problemFreeNights = Math.Max(
            0,
            report.Family.ObservableNightCount -
            report.Family.NightsWithReserveGridUse);
        var unknownNights = Math.Max(
            0,
            completeNights -
            report.Family.ObservableNightCount);

        var nightHeading = section.AddParagraph(
            L(
                report,
                "¿La batería alcanzó para cubrir la noche?",
                "Did the battery cover the night?"));
        nightHeading.Format.Font.Size = 13;
        nightHeading.Format.Font.Bold = true;
        nightHeading.Format.SpaceBefore = Unit.FromPoint(9);
        nightHeading.Format.SpaceAfter = Unit.FromPoint(5);

        var nights = section.AddTable();
        nights.Borders.Width = 0;
        nights.AddColumn(Unit.FromCentimeter(5.4));
        nights.AddColumn(Unit.FromCentimeter(5.4));
        nights.AddColumn(Unit.FromCentimeter(5.4));
        var nightRow = nights.AddRow();

        AddPdfFamilyCard(
            nightRow.Cells[0],
            L(report, "SIN PROBLEMAS DE ALIMENTACIÓN", "NO SUPPLY PROBLEMS"),
            problemFreeNights.ToString("N0"),
            string.Format(
                L(
                    report,
                    "de {0} noches observables",
                    "of {0} observable nights"),
                report.Family.ObservableNightCount),
            Colors.Honeydew,
            17);

        AddPdfFamilyCard(
            nightRow.Cells[1],
            L(report, "QUEDAMOS CORTOS", "BATTERY RAN SHORT"),
            report.Family.NightsWithReserveGridUse.ToString("N0"),
            string.Format(
                L(
                    report,
                    "{0} episodios · {1:N2} h acumuladas",
                    "{0} episodes · {1:N2} accumulated h"),
                report.Family.ReserveGridEpisodeCount,
                report.Family.ReserveGridTotalMinutes / 60.0),
            Colors.MistyRose,
            17);

        AddPdfFamilyCard(
            nightRow.Cells[2],
            L(report, "SIN DATOS SUFICIENTES", "INSUFFICIENT DATA"),
            unknownNights.ToString("N0"),
            L(
                report,
                "No se cuentan como noches sin problemas.",
                "They are not counted as problem-free nights."),
            Colors.WhiteSmoke,
            17);

        if (report.Family.TypicalReserveTime.HasValue)
        {
            var typical = section.AddParagraph(
                string.Format(
                    L(
                        report,
                        "Hora típica de llegada a la reserva: {0}.",
                        "Typical reserve-arrival time: {0}."),
                    report.Family.TypicalReserveTime.Value.ToString("HH:mm")));
            typical.Format.Font.Bold = true;
            typical.Format.SpaceBefore = Unit.FromPoint(6);
            typical.Format.SpaceAfter = Unit.FromPoint(2);
        }

        var distinction = section.AddParagraph(
            L(
                report,
                "Importante: estos episodios nocturnos NO son el conteo de todos los días en que la casa usó Enel. El uso total de Enel y los días con aporte mixto se resumen en la página 4.",
                "Important: these nighttime episodes are NOT the count of every day when the home used utility power. Total utility use and mixed-source days are summarized on page 4."));
        distinction.Format.SpaceBefore = Unit.FromPoint(5);
        distinction.Format.Font.Bold = true;
        distinction.Format.Font.Size = 8;

        var note = section.AddParagraph(
            L(
                report,
                "Una noche queda como “quedamos cortos” sólo cuando la batería llega a su umbral normal de transferencia, la casa necesita red y el solar es ausente o insuficiente. Los huecos de datos no se convierten en noches sin problemas.",
                "A night is counted as “battery ran short” only when the battery reaches its normal transfer threshold, the home needs grid power, and solar is absent or insufficient. Data gaps are not converted into problem-free nights."));
        note.Format.SpaceBefore = Unit.FromPoint(5);
        note.Format.Font.Size = 8;
        note.Format.Font.Italic = true;
    }

    private static void AddFamilyPatternsPdf(
        Section section,
        EnergyReportData report)
    {
        if (report.Family.HighestHouseConsumptionWindow is null &&
            report.Family.HighestSolarGenerationWindow is null &&
            report.Family.HighestGridUseWindow is null)
        {
            var insufficient = section.AddParagraph(
                L(
                    report,
                    "No hay suficientes días observables para afirmar patrones horarios habituales con confianza en este período.",
                    "There are not enough observable days to state typical hourly patterns confidently for this period."));
            insufficient.Format.Font.Italic = true;
            insufficient.Format.SpaceAfter = Unit.FromPoint(6);
        }

        var patternTable = section.AddTable();
        patternTable.Borders.Width = 0.3;
        patternTable.AddColumn(Unit.FromCentimeter(6.6));
        patternTable.AddColumn(Unit.FromCentimeter(6.4));

        AddPatternPdfRow(
            patternTable,
            report,
            L(report, "Mayor consumo habitual", "Highest typical consumption"),
            report.Family.HighestHouseConsumptionWindow);

        AddPatternPdfRow(
            patternTable,
            report,
            L(report, "Mayor producción solar habitual", "Highest typical solar production"),
            report.Family.HighestSolarGenerationWindow);

        AddPatternPdfRow(
            patternTable,
            report,
            L(report, "Mayor uso habitual de red", "Highest typical grid use"),
            report.Family.HighestGridUseWindow);

        if (report.Family.TypicalReserveTime.HasValue)
        {
            AddPdfValueRow(
                patternTable,
                L(report, "Hora típica de llegada a reserva", "Typical reserve-arrival time"),
                report.Family.TypicalReserveTime.Value.ToString("HH:mm") + " · " +
                string.Format(
                    L(report, "{0} episodios observados", "{0} observed episodes"),
                    report.Family.ReserveGridEpisodeCount));
        }

        if (report.Family.Highlights.Count > 0)
        {
            var heading = section.AddParagraph(
                L(report, "Días destacados", "Period highlights"));
            heading.Format.Font.Bold = true;
            heading.Format.SpaceBefore = Unit.FromPoint(8);

            foreach (var item in report.Family.Highlights)
            {
                section.AddParagraph(
                    $"{HighlightLabel(report, item.MetricKey)}: " +
                    $"{item.LocalLabel} · {item.Value:N2} {item.Unit} " +
                    $"({item.CoveragePercent:N0}% " +
                    L(report, "cobertura", "coverage") + ")");
            }
        }
    }

    private static void AddPatternPdfRow(
        Table table,
        EnergyReportData report,
        string label,
        FamilyHourlyPattern? pattern)
    {
        if (pattern is null)
        {
            return;
        }

        AddPdfValueRow(
            table,
            label,
            $"{pattern.StartHour:00}:00–{pattern.EndHourExclusive:00}:00 · " +
            $"{pattern.TypicalWatts / 1000.0:N2} kW · " +
            string.Format(
                L(report, "{0} días válidos; {1}/{2} con datos", "{0} valid days; {1}/{2} with data"),
                pattern.ObservedDays,
                pattern.OpportunityDays,
                SelectedDayCount(report)));
    }

    private static void AddGridUsePdf(
        Section section,
        EnergyReportData report)
    {
        var days = BuildGridUseDays(report);
        var mixedDays = days.Count(item => item.Kind == GridUseDayKind.Mixed);
        var nearExclusiveDays = days.Count(item => item.Kind == GridUseDayKind.NearExclusive);
        var reserveDays = days.Count(item => item.HasReserveGridEpisode);

        var cards = section.AddTable();
        cards.Borders.Width = 0;
        cards.AddColumn(Unit.FromCentimeter(4.05));
        cards.AddColumn(Unit.FromCentimeter(4.05));
        cards.AddColumn(Unit.FromCentimeter(4.05));
        cards.AddColumn(Unit.FromCentimeter(4.05));
        var row = cards.AddRow();

        AddPdfFamilyCard(
            row.Cells[0],
            L(report, "DÍAS CON ENEL → CASA", "DAYS WITH UTILITY → HOME"),
            days.Count.ToString("N0"),
            string.Format(
                L(report, "de {0} días del período", "of {0} days in the period"),
                SelectedDayCount(report)),
            Colors.AliceBlue,
            16);
        AddPdfFamilyCard(
            row.Cells[1],
            L(report, "APORTE MIXTO EN EL DÍA", "MIXED SOURCES IN THE DAY"),
            mixedDays.ToString("N0"),
            L(
                report,
                "Varias fuentes aportaron dentro del día; no implica simultaneidad.",
                "Several sources contributed within the day; this does not imply simultaneity."),
            Colors.AliceBlue,
            16);
        AddPdfFamilyCard(
            row.Cells[2],
            L(report, "CASI SÓLO ENEL", "NEARLY ALL UTILITY"),
            nearExclusiveDays.ToString("N0"),
            L(
                report,
                "Enel cubrió al menos 99% del consumo observado del día.",
                "Utility supplied at least 99% of observed household consumption that day."),
            Colors.AliceBlue,
            16);
        AddPdfFamilyCard(
            row.Cells[3],
            L(report, "CON RESERVA + RED NOCTURNA", "WITH NIGHT RESERVE + GRID"),
            reserveDays.ToString("N0"),
            L(
                report,
                "Subconjunto de los días con Enel.",
                "Subset of days with utility contribution."),
            Colors.AliceBlue,
            16);

        var explanation = section.AddParagraph(
            L(
                report,
                "Esta página cuenta días en los que Enel aportó efectivamente al consumo de la casa (Enel → Casa). Es una pregunta distinta del detector “quedamos cortos”, que sólo busca episodios nocturnos de batería en reserva.",
                "This page counts days when utility power actually contributed to household consumption (Utility → Home). This is a different question from the “battery ran short” detector, which only looks for nighttime battery-reserve episodes."));
        explanation.Format.SpaceBefore = Unit.FromPoint(7);
        explanation.Format.SpaceAfter = Unit.FromPoint(6);
        explanation.Format.Font.Size = 8;
        explanation.Format.Font.Italic = true;

        if (days.Count == 0)
        {
            section.AddParagraph(
                L(
                    report,
                    "No se detectaron días con aporte de Enel → Casa en el período observable.",
                    "No days with Utility → Home contribution were detected in the observable period."));
            return;
        }

        var notable = days
            .Where(item => item.Kind == GridUseDayKind.NearExclusive)
            .OrderByDescending(item => item.GridSharePercent)
            .FirstOrDefault();
        if (notable is not null)
        {
            var callout = section.AddParagraph(
                string.Format(
                    L(
                        report,
                        "Caso especial: {0} — Enel → Casa {1:N2} kWh de {2:N2} kWh observados ({3:N1}%).",
                        "Special case: {0} — Utility → Home {1:N2} kWh out of {2:N2} observed kWh ({3:N1}%)."),
                    notable.LocalLabel,
                    notable.GridToHouseKwh,
                    notable.ObservedHouseKwh,
                    notable.GridSharePercent));
            callout.Format.Font.Bold = true;
            callout.Format.SpaceAfter = Unit.FromPoint(6);
        }

        var table = section.AddTable();
        table.Borders.Width = 0.25;
        table.AddColumn(Unit.FromCentimeter(2.7));
        table.AddColumn(Unit.FromCentimeter(3.0));
        table.AddColumn(Unit.FromCentimeter(2.3));
        table.AddColumn(Unit.FromCentimeter(5.0));
        table.AddColumn(Unit.FromCentimeter(3.0));

        var header = table.AddRow();
        header.Format.Font.Bold = true;
        header.Cells[0].AddParagraph(L(report, "Fecha", "Date"));
        header.Cells[1].AddParagraph(L(report, "Enel → Casa", "Utility → Home"));
        header.Cells[2].AddParagraph(L(report, "% casa", "% home"));
        header.Cells[3].AddParagraph(L(report, "Tipo de día", "Day type"));
        header.Cells[4].AddParagraph(L(report, "Reserva+red", "Reserve+grid"));

        foreach (var item in days)
        {
            var dataRow = table.AddRow();
            dataRow.Cells[0].AddParagraph(item.LocalLabel);
            dataRow.Cells[1].AddParagraph($"{item.GridToHouseKwh:N2} kWh");
            dataRow.Cells[2].AddParagraph($"{item.GridSharePercent:N1}%");
            dataRow.Cells[3].AddParagraph(GridUseKindLabel(report, item.Kind));
            dataRow.Cells[4].AddParagraph(
                item.HasReserveGridEpisode
                    ? L(report, "Sí", "Yes")
                    : L(report, "No", "No"));
        }
    }

    private static void AddFamilyEventsPdf(
        Section section,
        EnergyReportData report)
    {
        var heading = section.AddParagraph(
            L(
                report,
                "Episodios nocturnos de batería en reserva + red",
                "Nighttime battery-reserve + grid episodes"));
        heading.Format.Font.Bold = true;
        heading.Format.SpaceBefore = Unit.FromPoint(10);
        heading.Format.SpaceAfter = Unit.FromPoint(4);

        if (report.Family.Events.Count == 0)
        {
            section.AddParagraph(
                L(
                    report,
                    "No se detectaron episodios con evidencia suficiente en la parte observable del período.",
                    "No sufficiently supported episodes were detected in the observable part of the period."));
            return;
        }

        var table = section.AddTable();
        table.Borders.Width = 0.25;
        table.AddColumn(Unit.FromCentimeter(3.7));
        table.AddColumn(Unit.FromCentimeter(3.7));
        table.AddColumn(Unit.FromCentimeter(2.3));
        table.AddColumn(Unit.FromCentimeter(2.3));

        var header = table.AddRow();
        header.Format.Font.Bold = true;
        header.Cells[0].AddParagraph(L(report, "Inicio", "Start"));
        header.Cells[1].AddParagraph(L(report, "Fin", "End"));
        header.Cells[2].AddParagraph(L(report, "Duración", "Duration"));
        header.Cells[3].AddParagraph(L(report, "SOC mín.", "Min SOC"));

        foreach (var item in report.Family.Events)
        {
            var row = table.AddRow();
            row.Cells[0].AddParagraph(item.StartLocal.ToString("dd-MM-yyyy HH:mm"));
            row.Cells[1].AddParagraph(item.EndLocal.ToString("dd-MM-yyyy HH:mm"));
            row.Cells[2].AddParagraph($"{item.DurationMinutes:N0} min");
            row.Cells[3].AddParagraph($"{item.MinimumSocPercent:N1}%");
        }
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

        var table = section.AddTable();
        table.Borders.Width = 0.25;
        table.AddColumn(Unit.FromCentimeter(8.0));
        table.AddColumn(Unit.FromCentimeter(5.0));

        AddPdfValueRow(table, L(report, "Cobertura solar", "Solar coverage"), $"{report.Summary.PvPower.CoveragePercent:N1}%");
        AddPdfValueRow(table, L(report, "Cobertura casa", "Home coverage"), $"{report.Summary.HouseLoadPower.CoveragePercent:N1}%");
        AddPdfValueRow(table, L(report, "Cobertura red", "Grid coverage"), $"{report.Summary.GridImportPower.CoveragePercent:N1}%");
        AddPdfValueRow(table, L(report, "Cobertura batería", "Battery coverage"), $"{report.Summary.BatteryPower.CoveragePercent:N1}%");
        AddPdfValueRow(table, L(report, "Cobertura de atribución", "Attribution coverage"), $"{report.Attribution.AttributionCoverageOfObservedPercent:N1}%");
        AddPdfValueRow(table, L(report, "Casa sin atribuir", "Unattributed home energy"), $"{report.Attribution.UnattributedHouseKwh:N2} kWh");
        AddPdfValueRow(
            table,
            L(report, "Noches observables", "Observable nights"),
            $"{report.Family.ObservableNightCount}/{Math.Max(0, report.Request.LocalEndDate.DayNumber - report.Request.LocalStartDate.DayNumber)}");

        var note = section.AddParagraph(
            L(
                report,
                "Faltante/desconocido nunca se interpreta como cero. La energía no se extrapola a través de huecos largos y los residuales de balance se conservan como diagnóstico en vez de repartirse artificialmente entre fuentes.",
                "Missing/unknown is never interpreted as zero. Energy is not extrapolated across long gaps, and balance residuals remain diagnostic instead of being artificially distributed among sources."));
        note.Format.Font.Italic = true;
        note.Format.SpaceBefore = Unit.FromPoint(5);
    }

    private static void AddGlossary(
        Section section,
        EnergyReportData report)
    {
        var heading = section.AddParagraph(
            L(report, "Glosario esencial", "Essential glossary"));
        heading.Format.Font.Size = 13;
        heading.Format.Font.Bold = true;
        heading.Format.SpaceBefore = Unit.FromPoint(10);

        var table = section.AddTable();
        table.Borders.Width = 0.25;
        table.AddColumn(Unit.FromCentimeter(4.0));
        table.AddColumn(Unit.FromCentimeter(9.0));

        var items = new[]
        {
            (L(report, "Solar → Casa", "Solar → Home"), L(report, "Parte del consumo atribuida directamente al solar; no es igual a toda la generación.", "Part of consumption attributed directly to solar; not the same as all generation.")),
            (L(report, "Batería → Casa", "Battery → Home"), L(report, "Parte del consumo atribuida a energía descargada de batería hacia la casa.", "Part of consumption attributed to battery energy supplied to the home.")),
            (L(report, "Enel → Casa", "Utility → Home"), L(report, "Parte del consumo de la casa atribuida a Enel. NO es la importación total y NO se compara directamente con la boleta/medidor.", "Part of household consumption attributed to the utility. This is NOT total import and should NOT be compared directly with the bill/meter.")),
            (L(report, "Importación total desde Enel", "Total import from utility"), L(report, "Toda la energía que entró desde la red al sistema. ESTA SÍ es la referencia del reporte para comparar con el medidor/boleta, considerando período y cobertura.", "All energy entering the system from the grid. THIS is the report value to compare with the utility meter/bill, accounting for matching period and coverage.")),
            (L(report, "Sin atribuir", "Unattributed"), L(report, "Consumo observado cuyo origen no pudo demostrarse; no se asigna por residuo.", "Observed consumption whose source could not be demonstrated; it is not assigned by residual.")),
            ("SOC", L(report, "Porcentaje de carga reportado por el BMS.", "Battery state-of-charge percentage reported by the BMS.")),
            (L(report, "Energía guardada (estimada)", "Stored energy (estimate)"), L(report, "Capacidad útil configurada × SOC, expresada en kWh.", "Configured usable capacity × SOC, expressed in kWh.")),
            (L(report, "Cobertura", "Coverage"), L(report, "Parte del período con continuidad suficiente; faltante no significa cero.", "Part of the period with sufficient continuity; missing does not mean zero.")),
            (L(report, "Noche observable", "Observable night"), L(report, "Noche con evidencia suficiente para evaluar la alimentación.", "Night with enough evidence to evaluate supply.")),
            (L(report, "Nivel mínimo protegido", "Protected minimum level"), L(report, "Límite inferior que el sistema intenta no cruzar para proteger la batería.", "Lower level the system tries not to cross to protect the battery.")),
            ("kWh", L(report, "Cantidad de energía acumulada durante un período.", "Amount of energy accumulated over a period.")),
            (L(report, "Residual de balance", "Balance residual"), L(report, "Diferencia diagnóstica entre entradas y salidas medidas; no se fuerza a cero.", "Diagnostic difference between measured inputs and outputs; it is not forced to zero."))
        };

        foreach (var item in items)
        {
            var row = table.AddRow();
            row.Cells[0].AddParagraph(item.Item1);
            row.Cells[1].AddParagraph(item.Item2);
        }

        var fullGlossary = section.AddParagraph(
            L(
                report,
                "El Excel contiene un glosario ampliado que también explica todas las columnas de Detalle y los límites de interpretación.",
                "The Excel workbook contains an expanded glossary that also explains all Detail columns and interpretation limits."));
        fullGlossary.Format.Font.Italic = true;
        fullGlossary.Format.SpaceBefore = Unit.FromPoint(5);
    }

    private static void AddSummarySheet(
        XLWorkbook workbook,
        EnergyReportData report,
        IReadOnlyList<string>? familyCharts = null)
    {
        var sheet = workbook.Worksheets.Add(
            L(report, "Resumen", "Summary"));

        if (report.Request.Kind == ReportKind.SimpleEnergy)
        {
            sheet.Range("A1:I1").Merge();
            sheet.Cell("A1").Value = DisplayReportTitle(report);
            sheet.Cell("A1").Style.Font.Bold = true;
            sheet.Cell("A1").Style.Font.FontSize = 18;
            sheet.Cell("A1").Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            sheet.Cell("A3").Value = L(report, "Período", "Period");
            sheet.Range("B3:D3").Merge();
            sheet.Cell("B3").Value =
                $"{report.Request.LocalStartDate:dd-MM-yyyy} — {report.Request.LocalEndDate:dd-MM-yyyy}";
            sheet.Cell("E3").Value = L(report, "Generado", "Generated");
            sheet.Range("F3:I3").Merge();
            sheet.Cell("F3").Value = report.GeneratedUtc.LocalDateTime;
            sheet.Cell("F3").Style.DateFormat.Format = "dd-mm-yyyy hh:mm";

            sheet.Range("A4:I4").Merge();
            sheet.Cell("A4").Value =
                L(report, "VENTANA EXACTA: ", "EXACT WINDOW: ") +
                ExactLocalWindowLabel(report) +
                L(
                    report,
                    $" · hora local ({report.Request.TimeZoneId}) · incluye completo el día final",
                    $" · local time ({report.Request.TimeZoneId}) · includes the full final day");
            sheet.Cell("A4").Style.Font.Italic = true;
            sheet.Cell("A4").Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;
            sheet.Cell("A4").Style.Alignment.WrapText = true;
            sheet.Row(4).Height = 30;

            AddFamilySummarySheet(sheet, report);
            AddFamilyChartsExcel(sheet, report, familyCharts);

            for (var column = 1; column <= 9; column++)
            {
                sheet.Column(column).Width = 14;
            }

            sheet.RangeUsed()?.Style.Alignment.WrapText = true;
            sheet.SheetView.FreezeRows(3);
        }
        else
        {
            sheet.Cell("A1").Value = DisplayReportTitle(report);
            sheet.Cell("A1").Style.Font.Bold = true;
            sheet.Cell("A1").Style.Font.FontSize = 16;

            sheet.Cell("A3").Value =
                L(report, "Ventana comparada", "Compared window");
            sheet.Cell("B3").Value = ExactLocalWindowLabel(report) +
                L(
                    report,
                    $" · hora local ({report.Request.TimeZoneId})",
                    $" · local time ({report.Request.TimeZoneId})");
            sheet.Cell("A4").Value = L(report, "Agrupación", "Aggregation");
            sheet.Cell("B4").Value = AggregationLabel(report);
            sheet.Cell("A5").Value = L(report, "Tipo", "Type");
            sheet.Cell("B5").Value = ReportKindLabel(report);
            sheet.Cell("A6").Value = L(report, "Generado", "Generated");
            sheet.Cell("B6").Value = report.GeneratedUtc.LocalDateTime;
            sheet.Cell("B6").Style.DateFormat.Format = "dd-mm-yyyy hh:mm";

            AddTechnicalSummarySheet(sheet, report);

            sheet.Columns().AdjustToContents();
            sheet.Column(1).Width = Math.Min(sheet.Column(1).Width, 42);
            sheet.Column(2).Width = Math.Min(Math.Max(sheet.Column(2).Width, 20), 64);
            sheet.Column(3).Width = Math.Min(Math.Max(sheet.Column(3).Width, 14), 28);
            sheet.RangeUsed()?.Style.Alignment.WrapText = true;
        }
    }

    private static void AddFamilySummarySheet(
        IXLWorksheet sheet,
        EnergyReportData report)
    {
        var family = report.Family;
        var familyCoverage = FamilyCoveragePercent(report);
        var partial = familyCoverage < 80.0;
        var selectedNights = Math.Max(
            0,
            report.Request.LocalEndDate.DayNumber -
            report.Request.LocalStartDate.DayNumber);
        var nightsWithoutReserveGrid = Math.Max(
            0,
            family.ObservableNightCount -
            family.NightsWithReserveGridUse);
        var unknownNights = Math.Max(
            0,
            selectedNights -
            family.ObservableNightCount);

        StyleFamilySection(
            sheet,
            "A5:I5",
            L(report, "PÁGINA 1 — ¿CÓMO NOS FUE?", "PAGE 1 — HOW DID WE DO?"));

        var coverageMessage = partial
            ? string.Format(
                L(
                    report,
                    "RESUMEN PARCIAL — hay datos suficientes para aproximadamente {0:N1}% del período. Las cifras muestran sólo lo realmente medido; el resto NO se estima.",
                    "PARTIAL SUMMARY — enough data is available for approximately {0:N1}% of the period. Figures show only what was actually measured; the rest is NOT estimated."),
                familyCoverage)
            : string.Format(
                L(
                    report,
                    "Datos suficientes para aproximadamente {0:N1}% del período.",
                    "Enough data is available for approximately {0:N1}% of the period."),
                familyCoverage);
        sheet.Range("A7:I8").Merge();
        sheet.Cell("A7").Value = coverageMessage;
        sheet.Cell("A7").Style.Font.Bold = true;
        sheet.Cell("A7").Style.Alignment.Horizontal =
            XLAlignmentHorizontalValues.Center;
        sheet.Cell("A7").Style.Alignment.Vertical =
            XLAlignmentVerticalValues.Center;
        sheet.Cell("A7").Style.Fill.BackgroundColor =
            partial ? XLColor.FromHtml("#FFF2CC") : XLColor.FromHtml("#E2F0D9");
        sheet.Range("A7:I8").Style.Border.OutsideBorder =
            XLBorderStyleValues.Thin;

        AddFamilyCard(
            sheet,
            "A10:C15",
            L(report, "1. ENEL → CASA (NO ES LA BOLETA)", "1. UTILITY → HOME (NOT THE BILL)"),
            report.Attribution.ObservedHouseKwh > 0
                ? $"{report.Attribution.GridToHouseKwh:N2} kWh"
                : L(report, "Sin datos suficientes", "Insufficient data"),
            AttributionCoverageLabel(report),
            L(
                report,
                "Sólo la parte del consumo de la vivienda atribuida a Enel. NO se compara con la boleta/medidor porque no incluye toda la energía importada por el sistema.",
                "Only the part of household consumption attributed to the utility. DO NOT compare this with the bill/meter because it does not include all energy imported by the system."));

        AddFamilyCard(
            sheet,
            "D10:F15",
            L(report, "2. DIRECTAMENTE DEL SOL", "2. DIRECTLY FROM SOLAR"),
            report.Attribution.ObservedHouseKwh > 0
                ? $"{report.Attribution.SolarToHouseKwh:N2} kWh"
                : L(report, "Sin datos suficientes", "Insufficient data"),
            AttributionCoverageLabel(report),
            L(
                report,
                "Solar usado directamente por la casa; no es lo mismo que toda la producción de los paneles.",
                "Solar used directly by the home; this is not the same as total panel production."));

        AddFamilyCard(
            sheet,
            "G10:I15",
            L(report, "3. DESDE LA BATERÍA", "3. FROM THE BATTERY"),
            report.Attribution.ObservedHouseKwh > 0
                ? $"{report.Attribution.BatteryToHouseKwh:N2} kWh"
                : L(report, "Sin datos suficientes", "Insufficient data"),
            AttributionCoverageLabel(report),
            L(
                report,
                "Energía atribuida a batería → casa. No se sustituye por la descarga total de batería.",
                "Energy attributed to battery → home. It is not replaced by total battery discharge."));

        AddFamilyCard(
            sheet,
            "A17:I22",
            L(
                report,
                "IMPORTACIÓN TOTAL DESDE ENEL — ESTA SÍ SE COMPARA CON MEDIDOR / BOLETA",
                "TOTAL IMPORT FROM UTILITY — THIS IS THE ONE TO COMPARE WITH METER / BILL"),
            report.Summary.GridImportPower.SampleCount >= 2
                ? $"{report.Summary.GridImportEnergyKwh:N2} kWh"
                : L(report, "Sin datos suficientes", "Insufficient data"),
            string.Format(
                L(report, "Cobertura: {0:N1}%", "Coverage: {0:N1}%"),
                report.Summary.GridImportPower.CoveragePercent),
            L(
                report,
                "Toda la energía que entró desde Enel al sistema. Puede incluir Enel → Casa, carga/mantenimiento de batería y pérdidas/consumos internos; por eso puede ser mayor que Enel → Casa.",
                "All energy entering the system from the utility. It can include Utility → Home, battery charging/maintenance, and internal losses/consumption; therefore it can be higher than Utility → Home."));

        sheet.Range("A23:I24").Merge();
        sheet.Cell("A23").Value =
            report.Summary.HouseLoadPower.SampleCount >= 2
                ? string.Format(
                    L(
                        report,
                        partial
                            ? "CONSUMO DE LA CASA REGISTRADO: {0:N2} kWh en los datos disponibles"
                            : "CONSUMO TOTAL DE LA CASA: {0:N2} kWh",
                        partial
                            ? "RECORDED HOME CONSUMPTION: {0:N2} kWh in available data"
                            : "TOTAL HOME CONSUMPTION: {0:N2} kWh"),
                    report.Summary.HouseEnergyKwh)
                : L(report, "CONSUMO DE LA CASA: sin datos suficientes", "HOME CONSUMPTION: insufficient data");
        sheet.Cell("A23").Style.Font.Bold = true;
        sheet.Cell("A23").Style.Font.FontSize = 15;
        sheet.Cell("A23").Style.Alignment.Horizontal =
            XLAlignmentHorizontalValues.Center;
        sheet.Cell("A23").Style.Alignment.Vertical =
            XLAlignmentVerticalValues.Center;
        sheet.Cell("A23").Style.Fill.BackgroundColor =
            XLColor.FromHtml("#D9EAF7");
        sheet.Range("A23:I24").Style.Border.OutsideBorder =
            XLBorderStyleValues.Thin;

        sheet.Range("A26:I26").Merge();
        sheet.Cell("A26").Value =
            L(report, "4. ¿ALCANZÓ LA BATERÍA DURANTE LA NOCHE?", "4. DID THE BATTERY COVER THE NIGHT?");
        sheet.Cell("A26").Style.Font.Bold = true;
        sheet.Cell("A26").Style.Font.FontSize = 13;

        AddNightCard(
            sheet,
            "A28:C31",
            L(report, "SIN PROBLEMAS DE ALIMENTACION", "NO SUPPLY PROBLEMS"),
            nightsWithoutReserveGrid.ToString("N0"),
            string.Format(
                L(report, "de {0} noches observables", "of {0} observable nights"),
                family.ObservableNightCount),
            "#E2F0D9");

        AddNightCard(
            sheet,
            "D28:F31",
            L(report, "QUEDAMOS CORTOS", "BATTERY RAN SHORT"),
            family.NightsWithReserveGridUse.ToString("N0"),
            string.Format(
                L(report, "{0} episodios · {1:N2} h", "{0} episodes · {1:N2} h"),
                family.ReserveGridEpisodeCount,
                family.ReserveGridTotalMinutes / 60.0),
            "#FCE4D6");

        AddNightCard(
            sheet,
            "G28:I31",
            L(report, "NOCHES SIN DATOS SUFICIENTES", "NIGHTS WITHOUT ENOUGH DATA"),
            unknownNights.ToString("N0"),
            string.Format(
                L(report, "de {0} noches completas del rango", "of {0} complete nights in range"),
                selectedNights),
            "#E7E6E6");

        sheet.Range("A33:I36").Merge();
        var interpretation = partial
            ? string.Format(
                L(
                    report,
                    "¿QUÉ SIGNIFICA ESTO? Este informe es parcial. En la parte observada se pudieron atribuir {0:N2} kWh del consumo de la casa a Enel/red, y se registraron {1:N2} kWh de consumo de la casa. Sólo {2} de {3} noches completas fueron suficientemente observables; en esas noches se detectaron {4} episodios en que la batería llegó a su reserva normal y fue necesario usar red por falta de solar suficiente.",
                    "WHAT DOES THIS MEAN? This report is partial. In the observed portion, {0:N2} kWh of household consumption could be attributed to the utility/grid, and {1:N2} kWh of household consumption were recorded. Only {2} of {3} complete nights were sufficiently observable; those nights contained {4} episodes where the battery reached its normal reserve and grid was needed while solar was insufficient."),
                report.Attribution.GridToHouseKwh,
                report.Summary.HouseEnergyKwh,
                family.ObservableNightCount,
                selectedNights,
                family.ReserveGridEpisodeCount)
            : string.Format(
                L(
                    report,
                    "¿QUÉ SIGNIFICA ESTO? La casa consumió {0:N2} kWh; de ese consumo, {1:N2} kWh pudieron atribuirse a Enel/red. En {2} de {3} noches observables se detectó al menos un episodio en que la batería llegó a su reserva normal y fue necesario usar red por falta de solar suficiente.",
                    "WHAT DOES THIS MEAN? The home used {0:N2} kWh; of that consumption, {1:N2} kWh could be attributed to the utility/grid. On {2} of {3} observable nights, at least one episode was detected where the battery reached its normal reserve and grid was needed while solar was insufficient."),
                report.Summary.HouseEnergyKwh,
                report.Attribution.GridToHouseKwh,
                family.NightsWithReserveGridUse,
                family.ObservableNightCount);
        sheet.Cell("A33").Value = interpretation;
        sheet.Cell("A33").Style.Font.Italic = true;
        sheet.Cell("A33").Style.Alignment.Vertical =
            XLAlignmentVerticalValues.Center;
        sheet.Cell("A33").Style.Fill.BackgroundColor =
            XLColor.FromHtml("#F3F6F9");
        sheet.Range("A33:I36").Style.Border.OutsideBorder =
            XLBorderStyleValues.Thin;

        StyleFamilySection(
            sheet,
            "A38:I38",
            L(report, "PÁGINA 2 — ¿QUÉ PASÓ CON TODA LA ENERGÍA?", "PAGE 2 — WHAT HAPPENED TO ALL THE ENERGY?"));

        AddFamilyCard(
            sheet,
            "A40:C45",
            partial
                ? L(report, "SOLAR PRODUCIDO REGISTRADO", "RECORDED SOLAR PRODUCTION")
                : L(report, "SOLAR PRODUCIDO", "SOLAR PRODUCTION"),
            report.Summary.PvPower.SampleCount >= 2
                ? $"{report.Summary.PvEnergyKwh:N2} kWh"
                : L(report, "Sin datos suficientes", "Insufficient data"),
            string.Format(
                L(report, "Cobertura: {0:N1}%", "Coverage: {0:N1}%"),
                report.Summary.PvPower.CoveragePercent),
            L(
                report,
                "Toda la energía producida por los paneles en la parte observada.",
                "All energy produced by the panels in the observed portion."));

        AddFamilyCard(
            sheet,
            "D40:F45",
            partial
                ? L(report, "CONSUMO DE CASA REGISTRADO", "RECORDED HOME CONSUMPTION")
                : L(report, "CONSUMO DE LA CASA", "HOME CONSUMPTION"),
            report.Summary.HouseLoadPower.SampleCount >= 2
                ? $"{report.Summary.HouseEnergyKwh:N2} kWh"
                : L(report, "Sin datos suficientes", "Insufficient data"),
            string.Format(
                L(report, "Cobertura: {0:N1}%", "Coverage: {0:N1}%"),
                report.Summary.HouseLoadPower.CoveragePercent),
            L(
                report,
                "Todo lo usado por la vivienda, sin importar de qué fuente vino.",
                "Everything used by the home, regardless of source."));

        AddFamilyCard(
            sheet,
            "G40:I45",
            partial
                ? L(report, "IMPORTACIÓN TOTAL DE ENEL REGISTRADA", "RECORDED TOTAL UTILITY IMPORT")
                : L(report, "IMPORTACIÓN TOTAL DESDE ENEL", "TOTAL IMPORT FROM UTILITY"),
            report.Summary.GridImportPower.SampleCount >= 2
                ? $"{report.Summary.GridImportEnergyKwh:N2} kWh"
                : L(report, "Sin datos suficientes", "Insufficient data"),
            string.Format(
                L(report, "Cobertura: {0:N1}%", "Coverage: {0:N1}%"),
                report.Summary.GridImportPower.CoveragePercent),
            L(
                report,
                "ESTA SÍ se compara con el medidor/boleta de Enel. Es toda la energía que entró desde la red al sistema; puede incluir casa, carga/mantenimiento de batería y pérdidas/consumos internos.",
                "THIS is the value to compare with the utility meter/bill. It is all energy entering the system from the grid; it can include the home, battery charging/maintenance, and internal losses/consumption."));

        AddFamilyCard(
            sheet,
            "A47:C52",
            L(report, "BATERÍA: ENERGÍA ENTREGADA", "BATTERY: ENERGY DISCHARGED"),
            report.Summary.BatteryPower.SampleCount >= 2
                ? $"{report.Summary.BatteryDischargedEnergyKwh:N2} kWh"
                : L(report, "Sin datos suficientes", "Insufficient data"),
            string.Format(
                L(report, "Cobertura: {0:N1}%", "Coverage: {0:N1}%"),
                report.Summary.BatteryPower.CoveragePercent),
            L(
                report,
                "Movimiento total de salida de la batería durante la parte observada.",
                "Total energy movement out of the battery during the observed portion."));

        AddFamilyCard(
            sheet,
            "D47:F52",
            L(report, "BATERÍA: ENERGÍA RECIBIDA", "BATTERY: ENERGY CHARGED"),
            report.Summary.BatteryPower.SampleCount >= 2
                ? $"{report.Summary.BatteryChargedEnergyKwh:N2} kWh"
                : L(report, "Sin datos suficientes", "Insufficient data"),
            string.Format(
                L(report, "Cobertura: {0:N1}%", "Coverage: {0:N1}%"),
                report.Summary.BatteryPower.CoveragePercent),
            L(
                report,
                "Movimiento total de entrada a la batería durante la parte observada.",
                "Total energy movement into the battery during the observed portion."));

        AddFamilyCard(
            sheet,
            "G47:I52",
            L(report, "ATRIBUCIÓN DE ORIGEN", "SOURCE ATTRIBUTION"),
            $"{report.Attribution.AttributionCoverageOfObservedPercent:N1} %",
            string.Format(
                L(report, "Sin atribuir: {0:N2} kWh", "Unattributed: {0:N2} kWh"),
                report.Attribution.UnattributedHouseKwh),
            L(
                report,
                "Qué parte del consumo observado pudo repartirse con evidencia entre Solar, Batería y Enel.",
                "How much observed household consumption could be assigned with evidence to Solar, Battery and Utility."));

        sheet.Range("A54:I56").Merge();
        sheet.Cell("A54").Value =
            L(
                report,
                "SOLAR QUE NO PUDIMOS APROVECHAR: todavía no puede calcularse con suficiente confianza. No se inventa como “solar producido menos solar usado”.",
                "SOLAR WE COULD NOT USE: it still cannot be calculated with enough confidence. It is not invented as “solar produced minus solar used”.");
        sheet.Cell("A54").Style.Font.Bold = true;
        sheet.Cell("A54").Style.Fill.BackgroundColor =
            XLColor.FromHtml("#FFF2CC");
        sheet.Cell("A54").Style.Alignment.Vertical =
            XLAlignmentVerticalValues.Center;
        sheet.Range("A54:I56").Style.Border.OutsideBorder =
            XLBorderStyleValues.Thin;
        sheet.Row(54).Height = 24;
        sheet.Row(55).Height = 24;
        sheet.Row(56).Height = 24;
    }

    private static void AddFamilyChartsExcel(
        IXLWorksheet sheet,
        EnergyReportData report,
        IReadOnlyList<string>? chartPaths)
    {
        if (chartPaths is null || chartPaths.Count < 3)
        {
            return;
        }

        var anchors = new[] { 59, 80, 101 };

        for (var index = 0; index < 3; index++)
        {
            var picture = sheet.AddPicture(
                chartPaths[index],
                $"family-chart-{index + 1}");
            picture
                .MoveTo(sheet.Cell(anchors[index], 1))
                .WithSize(900, 270);

            for (var row = anchors[index]; row < anchors[index] + 18; row++)
            {
                sheet.Row(row).Height = 22;
            }
        }

        sheet.Range("A122:I123").Merge();
        sheet.Cell("A122").Value =
            L(
                report,
                "Los gráficos usan exactamente la agrupación elegida para este reporte. La energía que no pudo atribuirse permanece visible como “Sin atribuir”.",
                "Charts use exactly the aggregation selected for this report. Energy that could not be attributed remains visible as “Unattributed”.");
        sheet.Cell("A122").Style.Font.Italic = true;
    }

    private static void AddFamilyReportChartsPdf(
        Section section,
        EnergyReportData report,
        IReadOnlyList<string>? chartPaths)
    {
        if (chartPaths is null || chartPaths.Count < 3)
        {
            var unavailable = section.AddParagraph(
                L(
                    report,
                    "Los gráficos no pudieron generarse en esta exportación.",
                    "Charts could not be generated in this export."));
            unavailable.Format.Font.Italic = true;
            return;
        }

        var sourceCaption = section.AddParagraph(
            L(
                report,
                "Origen del consumo por período",
                "Household-source mix by period"));
        sourceCaption.Format.Font.Bold = true;
        sourceCaption.Format.SpaceBefore = Unit.FromPoint(8);
        sourceCaption.Format.SpaceAfter = Unit.FromPoint(3);

        var sourceImage = section.AddImage(chartPaths[0]);
        sourceImage.LockAspectRatio = true;
        sourceImage.Width = Unit.FromCentimeter(16.5);
        sourceImage.WrapFormat.DistanceBottom = Unit.FromPoint(4);

        var sourceNote = section.AddParagraph(
            L(
                report,
                "Cada barra es el consumo de la casa del período y se divide sólo entre las fuentes que pudieron demostrarse. Un segmento Enel indica aporte de Enel en ese período; no equivale automáticamente a un episodio nocturno de reserva. Si una barra contiene varias fuentes, significa que varias contribuyeron dentro del período, no necesariamente de forma simultánea. La parte “Sin atribuir” permanece separada.",
                "Each bar is household consumption for the period, split only among sources supported by evidence. A utility segment means utility power contributed during that period; it does not automatically mean a nighttime reserve episode. If a bar contains several sources, they contributed within the period, not necessarily simultaneously. The “Unattributed” portion remains separate."));
        sourceNote.Format.Font.Size = 8;
        sourceNote.Format.Font.Italic = true;

        var evolutionHeading = section.AddParagraph(
            L(
                report,
                "Página 3 — Evolución de producción, consumo y batería",
                "Page 3 — Production, consumption and battery evolution"));
        evolutionHeading.Format.PageBreakBefore = true;
        evolutionHeading.Format.Font.Size = 15;
        evolutionHeading.Format.Font.Bold = true;
        evolutionHeading.Format.SpaceAfter = Unit.FromPoint(6);

        var solarCaption = section.AddParagraph(
            L(
                report,
                "Producción solar frente al consumo de la casa",
                "Solar production versus household consumption"));
        solarCaption.Format.Font.Bold = true;
        solarCaption.Format.SpaceAfter = Unit.FromPoint(3);

        var solarImage = section.AddImage(chartPaths[1]);
        solarImage.LockAspectRatio = true;
        solarImage.Width = Unit.FromCentimeter(16.5);
        solarImage.WrapFormat.DistanceBottom = Unit.FromPoint(6);

        var batteryCaption = section.AddParagraph(
            L(
                report,
                "Energía estimada guardada en batería al cierre de cada período",
                "Estimated battery energy stored at the end of each period"));
        batteryCaption.Format.Font.Bold = true;
        batteryCaption.Format.SpaceAfter = Unit.FromPoint(3);

        var batteryImage = section.AddImage(chartPaths[2]);
        batteryImage.LockAspectRatio = true;
        batteryImage.Width = Unit.FromCentimeter(16.5);
        batteryImage.WrapFormat.DistanceBottom = Unit.FromPoint(4);

        var evolutionNote = section.AddParagraph(
            L(
                report,
                "Estos gráficos muestran evolución, no reparto de fuentes. La energía guardada es una estimación de capacidad útil configurada × SOC.",
                "These charts show evolution, not source allocation. Stored energy is estimated as configured usable capacity × SOC."));
        evolutionNote.Format.Font.Size = 8;
        evolutionNote.Format.Font.Italic = true;
    }

    private static void AddEvolutionSheet(
        XLWorkbook workbook,
        EnergyReportData report)
    {
        var sheet = workbook.Worksheets.Add(
            L(report, "Evolución", "Evolution"));

        sheet.Cell("A1").Value =
            string.Format(
                L(report, "Evolución técnica ({0})", "Technical evolution ({0})"),
                AggregationLabel(report, report.Family.EvolutionAggregation));
        sheet.Cell("A1").Style.Font.Bold = true;
        sheet.Cell("A1").Style.Font.FontSize = 14;

        var headers = new[]
        {
            L(report, "Período", "Period"),
            L(report, "Solar kWh", "Solar kWh"),
            L(report, "Casa kWh", "Home kWh"),
            L(report, "Red kWh", "Grid kWh"),
            L(report, "Batería entregada kWh", "Battery discharged kWh"),
            L(report, "Cobertura mín. %", "Min coverage %")
        };

        for (var column = 0; column < headers.Length; column++)
        {
            sheet.Cell(3, column + 1).Value = headers[column];
        }

        sheet.Range(3, 1, 3, headers.Length).Style.Font.Bold = true;

        var row = 4;
        foreach (var item in report.Family.Evolution)
        {
            sheet.Cell(row, 1).Value = item.LocalLabel;
            SetNullableNumber(sheet.Cell(row, 2), item.PvEnergyKwh);
            SetNullableNumber(sheet.Cell(row, 3), item.HouseEnergyKwh);
            SetNullableNumber(sheet.Cell(row, 4), item.GridImportEnergyKwh);
            SetNullableNumber(sheet.Cell(row, 5), item.BatteryDischargedEnergyKwh);
            sheet.Cell(row, 6).Value = item.MinimumCoveragePercent;
            sheet.Cell(row, 6).Style.NumberFormat.Format = "0.0";
            row++;
        }

        sheet.SheetView.FreezeRows(3);
        sheet.Range(3, 1, Math.Max(3, row - 1), headers.Length).SetAutoFilter();
        sheet.Columns().AdjustToContents();
    }

    private static string AttributionCoverageLabel(EnergyReportData report) =>
        string.Format(
            L(
                report,
                "Atribución: {0:N1}% de la energía observada de la casa",
                "Attribution: {0:N1}% of observed household energy"),
            report.Attribution.AttributionCoverageOfObservedPercent);

    private static string ExactLocalWindowLabel(EnergyReportData report)
    {
        var start = SolarApiTime.ConvertToLocalTime(
            report.Request.StartUtc,
            report.Request.TimeZoneId);
        var end = SolarApiTime.ConvertToLocalTime(
            report.Request.EndUtc,
            report.Request.TimeZoneId);

        return $"{start:dd-MM-yyyy HH:mm:ss} — {end:dd-MM-yyyy HH:mm:ss}";
    }

    private static string DisplayReportTitle(EnergyReportData report) =>
        report.Request.Title;

    private static string CreateChartTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "SolarEnergyMonitor",
            "report-charts",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteChartTempDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup must never invalidate a completed export.
        }
    }

    private static string PreferredPdfTextFont() =>
        WindowsFontExists("aptos", "narrow")
            ? "Aptos Narrow"
            : "Arial";

    private static string PreferredPdfNumericFont() =>
        WindowsFontExists("aptos", "mono")
            ? "Aptos Mono"
            : WindowsFontExists("cour")
                ? "Courier New"
                : "Arial";

    private static bool WindowsFontExists(params string[] fragments)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            var windows = Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);
            var fonts = Path.Combine(windows, "Fonts");

            return Directory
                .EnumerateFiles(fonts)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Any(name =>
                    fragments.All(fragment =>
                        name!.Contains(
                            fragment,
                            StringComparison.OrdinalIgnoreCase)));
        }
        catch
        {
            return false;
        }
    }

    private static void ApplyWorkbookTypography(XLWorkbook workbook)
    {
        foreach (var sheet in workbook.Worksheets)
        {
            var used = sheet.RangeUsed();
            if (used is null)
            {
                continue;
            }

            foreach (var cell in used.Cells())
            {
                var preserveMono =
                    string.Equals(
                        cell.Style.Font.FontName,
                        "Aptos Mono",
                        StringComparison.OrdinalIgnoreCase);

                cell.Style.Font.FontName =
                    preserveMono ||
                    cell.DataType is XLDataType.Number or XLDataType.DateTime or XLDataType.TimeSpan
                        ? "Aptos Mono"
                        : "Aptos Narrow";
            }

            used.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            foreach (var row in sheet.RowsUsed())
            {
                var longest = row.CellsUsed()
                    .Select(cell => cell.GetString().Length)
                    .DefaultIfEmpty(0)
                    .Max();

                var minimumHeight = longest switch
                {
                    > 120 => 44.0,
                    > 70 => 36.0,
                    > 35 => 28.0,
                    _ => 22.0
                };

                if (row.Height < minimumHeight)
                {
                    row.Height = minimumHeight;
                }
            }
        }
    }

    private static void StyleFamilySection(
        IXLWorksheet sheet,
        string rangeAddress,
        string text)
    {
        var range = sheet.Range(rangeAddress);
        range.Merge();
        range.FirstCell().Value = text;
        range.Style.Font.Bold = true;
        range.Style.Font.FontSize = 13;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9EAF7");
        range.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
        sheet.Row(range.RangeAddress.FirstAddress.RowNumber).Height = 24;
    }

    private static void AddFamilyCard(
        IXLWorksheet sheet,
        string rangeAddress,
        string title,
        string value,
        string context,
        string explanation)
    {
        var range = sheet.Range(rangeAddress);
        var firstRow = range.Range(1, 1, 1, 3);
        var valueRows = range.Range(2, 1, 3, 3);
        var contextRow = range.Range(4, 1, 4, 3);
        var explanationRows = range.Range(5, 1, 6, 3);

        firstRow.Merge();
        valueRows.Merge();
        contextRow.Merge();
        explanationRows.Merge();

        firstRow.FirstCell().Value = title;
        valueRows.FirstCell().Value = value;
        contextRow.FirstCell().Value = context;
        explanationRows.FirstCell().Value = explanation;

        firstRow.Style.Font.Bold = true;
        firstRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        valueRows.Style.Font.Bold = true;
        valueRows.Style.Font.FontName = "Aptos Mono";
        valueRows.Style.Font.FontSize = 16;
        valueRows.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        valueRows.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        contextRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        explanationRows.Style.Font.FontSize = 9;
        explanationRows.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        explanationRows.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FBFD");
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        var firstRowNumber = range.RangeAddress.FirstAddress.RowNumber;
        sheet.Row(firstRowNumber).Height = 22;
        sheet.Row(firstRowNumber + 1).Height = 24;
        sheet.Row(firstRowNumber + 2).Height = 24;
        sheet.Row(firstRowNumber + 3).Height = 20;
        sheet.Row(firstRowNumber + 4).Height = 24;
        sheet.Row(firstRowNumber + 5).Height = 24;
    }

    private static void AddNightCard(
        IXLWorksheet sheet,
        string rangeAddress,
        string title,
        string value,
        string context,
        string fillColor)
    {
        var range = sheet.Range(rangeAddress);
        var titleRow = range.Range(1, 1, 1, 3);
        var valueRows = range.Range(2, 1, 3, 3);
        var contextRow = range.Range(4, 1, 4, 3);

        titleRow.Merge();
        valueRows.Merge();
        contextRow.Merge();

        titleRow.FirstCell().Value = title;
        valueRows.FirstCell().Value = value;
        contextRow.FirstCell().Value = context;

        titleRow.Style.Font.Bold = true;
        titleRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        valueRows.Style.Font.Bold = true;
        valueRows.Style.Font.FontName = "Aptos Mono";
        valueRows.Style.Font.FontSize = 18;
        valueRows.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        valueRows.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        contextRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml(fillColor);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        var firstRowNumber = range.RangeAddress.FirstAddress.RowNumber;
        sheet.Row(firstRowNumber).Height = 22;
        sheet.Row(firstRowNumber + 1).Height = 26;
        sheet.Row(firstRowNumber + 2).Height = 26;
        sheet.Row(firstRowNumber + 3).Height = 22;
    }

    private static void AddTechnicalSummarySheet(
        IXLWorksheet sheet,
        EnergyReportData report)
    {
        sheet.Cell("A8").Value = L(report, "Métrica", "Metric");
        sheet.Cell("B8").Value = L(report, "Energía (kWh)", "Energy (kWh)");
        sheet.Cell("C8").Value = L(report, "Cobertura (%)", "Coverage (%)");
        sheet.Range("A8:C8").Style.Font.Bold = true;

        var metrics = new[]
        {
            (L(report, "Solar generado", "Solar produced"), report.Summary.PvPower, report.Summary.PvEnergyKwh),
            (L(report, "Consumo de la casa", "Home use"), report.Summary.HouseLoadPower, report.Summary.HouseEnergyKwh),
            (L(report, "Importación de red", "Grid import"), report.Summary.GridImportPower, report.Summary.GridImportEnergyKwh),
            (L(report, "Batería entregada", "Battery supplied"), report.Summary.BatteryPower, report.Summary.BatteryDischargedEnergyKwh),
            (L(report, "Batería recibida", "Battery received"), report.Summary.BatteryPower, report.Summary.BatteryChargedEnergyKwh)
        };

        var row = 9;
        foreach (var item in metrics)
        {
            sheet.Cell(row, 1).Value = item.Item1;
            if (item.Item2.SampleCount >= 2)
            {
                sheet.Cell(row, 2).Value = item.Item3;
                sheet.Cell(row, 2).Style.NumberFormat.Format = "0.00";
            }
            sheet.Cell(row, 3).Value = item.Item2.CoveragePercent;
            sheet.Cell(row, 3).Style.NumberFormat.Format = "0.0";
            row++;
        }
    }

    private static void AddPatternsSheet(
        XLWorkbook workbook,
        EnergyReportData report)
    {
        var sheet = workbook.Worksheets.Add(
            L(report, "Patrones", "Patterns"));

        sheet.Cell("A1").Value =
            L(report, "Patrones del período", "Patterns for the selected period");
        sheet.Cell("A1").Style.Font.Bold = true;
        sheet.Cell("A1").Style.Font.FontSize = 15;
        sheet.Range("A1:E1").Merge();

        sheet.Cell("A3").Value =
            L(report, "Sólo se muestran patrones con repetición suficiente. Los huecos de datos no cuentan como ausencia de un evento.", "Only sufficiently repeated patterns are shown. Data gaps do not count as absence of an event.");
        sheet.Range("A3:E3").Merge();
        sheet.Cell("A3").Style.Font.Italic = true;

        var row = 5;
        if (report.Family.HighestHouseConsumptionWindow is null &&
            report.Family.HighestSolarGenerationWindow is null &&
            report.Family.HighestGridUseWindow is null)
        {
            sheet.Range(row, 1, row + 1, 5).Merge();
            sheet.Cell(row, 1).Value =
                L(
                    report,
                    "No hay suficientes días observables para afirmar patrones horarios habituales con confianza en este período.",
                    "There are not enough observable days to state typical hourly patterns confidently for this period.");
            sheet.Cell(row, 1).Style.Font.Italic = true;
            row += 3;
        }

        row = AddPatternRow(sheet, report, row, report.Family.HighestHouseConsumptionWindow,
            L(report, "Mayor consumo habitual de la casa", "Highest typical home consumption"));
        row = AddPatternRow(sheet, report, row, report.Family.HighestSolarGenerationWindow,
            L(report, "Mayor producción solar habitual", "Highest typical solar production"));
        row = AddPatternRow(sheet, report, row, report.Family.HighestGridUseWindow,
            L(report, "Mayor uso habitual de la red", "Highest typical grid use"));

        if (report.Family.TypicalReserveTime.HasValue)
        {
            sheet.Cell(row, 1).Value =
                L(report, "Hora típica de llegada de batería a reserva", "Typical battery reserve-arrival time");
            sheet.Cell(row, 2).Value = report.Family.TypicalReserveTime.Value.ToString("HH:mm");
            sheet.Cell(row, 3).Value = report.Family.ReserveGridEpisodeCount;
            sheet.Cell(row, 4).Value = L(report, "episodios observados", "observed episodes");
            row += 2;
        }

        sheet.Cell(row, 1).Value = L(report, "Destacados", "Highlights");
        sheet.Cell(row, 1).Style.Font.Bold = true;
        row++;

        sheet.Cell(row, 1).Value = L(report, "Métrica", "Metric");
        sheet.Cell(row, 2).Value = L(report, "Período", "Period");
        sheet.Cell(row, 3).Value = L(report, "Valor", "Value");
        sheet.Cell(row, 4).Value = L(report, "Cobertura %", "Coverage %");
        sheet.Range(row, 1, row, 4).Style.Font.Bold = true;
        row++;

        foreach (var highlight in report.Family.Highlights)
        {
            sheet.Cell(row, 1).Value = HighlightLabel(report, highlight.MetricKey);
            sheet.Cell(row, 2).Value = highlight.LocalLabel;
            sheet.Cell(row, 3).Value = highlight.Value;
            sheet.Cell(row, 3).Style.NumberFormat.Format = "0.00";
            sheet.Cell(row, 4).Value = highlight.CoveragePercent;
            sheet.Cell(row, 4).Style.NumberFormat.Format = "0.0";
            row++;
        }

        row += 2;
        sheet.Range(row, 1, row, 5).Merge();
        sheet.Cell(row, 1).Value =
            L(
                report,
                "Episodios nocturnos de reserva + red",
                "Nighttime reserve + grid episodes");
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Row(row).Height = 24;
        row++;

        sheet.Range(row, 1, row + 1, 5).Merge();
        sheet.Row(row).Height = 34;
        sheet.Row(row + 1).Height = 34;
        sheet.Cell(row, 1).Value =
            report.Family.Events.Count == 0
                ? L(
                    report,
                    "No se detectaron episodios nocturnos de reserva + red con evidencia suficiente en la parte observable. La hoja Uso de Enel resume por separado todos los días en que Enel aportó a la casa; la hoja Eventos conserva el detalle nocturno.",
                    "No sufficiently supported nighttime reserve + grid episodes were detected in the observable portion. The Utility Use sheet separately summarizes every day when utility power contributed to the home; the Events sheet keeps the nighttime detail.")
                : string.Format(
                    L(
                        report,
                        "Se detectaron {0} episodios nocturnos de reserva + red en {1} noches observables, con {2:N1} minutos acumulados. Esto no representa todos los usos de Enel; la hoja Uso de Enel conserva ese resumen por día.",
                        "{0} nighttime reserve + grid episodes were detected across {1} observable nights, totaling {2:N1} minutes. This is not the count of all utility use; the Utility Use sheet preserves that daily summary."),
                    report.Family.Events.Count,
                    report.Family.NightsWithReserveGridUse,
                    report.Family.ReserveGridTotalMinutes);
        sheet.Cell(row, 1).Style.Font.Italic = true;
        sheet.Cell(row, 1).Style.Alignment.Vertical =
            XLAlignmentVerticalValues.Center;
        sheet.Range(row, 1, row + 1, 5).Style.Alignment.WrapText = true;
        row += 3;

        sheet.RangeUsed()?.Style.Alignment.WrapText = true;
        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(3);
    }

    private static int AddPatternRow(
        IXLWorksheet sheet,
        EnergyReportData report,
        int row,
        FamilyHourlyPattern? pattern,
        string label)
    {
        if (pattern is null)
        {
            return row;
        }

        sheet.Cell(row, 1).Value = label;
        sheet.Cell(row, 2).Value =
            $"{pattern.StartHour:00}:00–{pattern.EndHourExclusive:00}:00";
        sheet.Cell(row, 3).Value = pattern.TypicalWatts / 1000.0;
        sheet.Cell(row, 3).Style.NumberFormat.Format = "0.00";
        sheet.Cell(row, 4).Value = "kW";
        sheet.Cell(row, 5).Value =
            string.Format(
                L(report, "{0} días válidos; {1} días con datos de {2} del período", "{0} valid days; {1} days with data out of {2} in the period"),
                pattern.ObservedDays,
                pattern.OpportunityDays,
                SelectedDayCount(report));
        return row + 2;
    }

    private static void AddGridUseSheet(
        XLWorkbook workbook,
        EnergyReportData report)
    {
        var sheet = workbook.Worksheets.Add(
            L(report, "Uso de Enel", "Utility Use"));
        var days = BuildGridUseDays(report);
        var mixedDays = days.Count(item => item.Kind == GridUseDayKind.Mixed);
        var nearExclusiveDays = days.Count(item => item.Kind == GridUseDayKind.NearExclusive);
        var reserveDays = days.Count(item => item.HasReserveGridEpisode);

        sheet.Cell("A1").Value =
            L(report, "Uso de Enel / red en la casa", "Utility / grid use in the home");
        sheet.Cell("A1").Style.Font.Bold = true;
        sheet.Cell("A1").Style.Font.FontSize = 15;
        sheet.Range("A1:E1").Merge();

        sheet.Cell("A3").Value =
            L(report, "Días con Enel → Casa", "Days with Utility → Home");
        sheet.Cell("B3").Value = days.Count;
        sheet.Cell("C3").Value =
            L(report, "Aporte mixto en el día", "Mixed-source days");
        sheet.Cell("D3").Value = mixedDays;
        sheet.Cell("E3").Value = string.Format(
            L(
                report,
                "{0} casi sólo Enel · {1} con reserva+red nocturna",
                "{0} nearly all utility · {1} with nighttime reserve+grid"),
            nearExclusiveDays,
            reserveDays);
        sheet.Range("A3:E3").Style.Font.Bold = true;

        sheet.Range("A5:E6").Merge();
        sheet.Cell("A5").Value =
            L(
                report,
                "“Aporte mixto en el día” significa que varias fuentes contribuyeron al consumo total de ese día; no afirma que hayan alimentado la casa al mismo instante. “Reserva+red” es el detector nocturno específico y es sólo un subconjunto de los días con Enel.",
                "“Mixed sources in the day” means several sources contributed to that day's total household consumption; it does not assert simultaneous supply. “Reserve+grid” is the specific nighttime detector and is only a subset of days with utility use.");
        sheet.Cell("A5").Style.Font.Italic = true;
        sheet.Range("A5:E6").Style.Alignment.WrapText = true;

        var row = 8;
        var headers = new[]
        {
            L(report, "Fecha", "Date"),
            L(report, "Enel → Casa kWh", "Utility → Home kWh"),
            L(report, "% consumo casa", "% household use"),
            L(report, "Tipo de día", "Day type"),
            L(report, "Episodio reserva+red nocturno", "Night reserve+grid episode")
        };
        for (var col = 0; col < headers.Length; col++)
            sheet.Cell(row, col + 1).Value = headers[col];

        sheet.Range(row, 1, row, headers.Length).Style.Font.Bold = true;
        row++;

        foreach (var item in days)
        {
            if (DateOnly.TryParse(item.LocalLabel, out var date))
            {
                sheet.Cell(row, 1).Value = date.ToDateTime(TimeOnly.MinValue);
                sheet.Cell(row, 1).Style.DateFormat.Format = "dd-mm-yyyy";
            }
            else
            {
                sheet.Cell(row, 1).Value = item.LocalLabel;
            }

            sheet.Cell(row, 2).Value = item.GridToHouseKwh;
            sheet.Cell(row, 2).Style.NumberFormat.Format = "0.00";
            sheet.Cell(row, 3).Value = item.GridSharePercent;
            sheet.Cell(row, 3).Style.NumberFormat.Format = "0.0";
            sheet.Cell(row, 4).Value = GridUseKindLabel(report, item.Kind);
            sheet.Cell(row, 5).Value =
                item.HasReserveGridEpisode
                    ? L(report, "Sí", "Yes")
                    : L(report, "No", "No");
            row++;
        }

        sheet.Columns(1, 5).AdjustToContents();
        sheet.Column(4).Width = Math.Max(sheet.Column(4).Width, 28);
        sheet.Column(5).Width = Math.Max(sheet.Column(5).Width, 28);
        sheet.RangeUsed()?.Style.Alignment.WrapText = true;
        sheet.SheetView.FreezeRows(8);
        if (row > 9)
            sheet.Range(8, 1, row - 1, 5).SetAutoFilter();
    }

    private static void AddEventsSheet(
        XLWorkbook workbook,
        EnergyReportData report)
    {
        var sheet = workbook.Worksheets.Add(
            L(report, "Eventos", "Events"));

        sheet.Cell("A1").Value =
            L(
                report,
                "Noches observables — detector específico de reserva + red",
                "Observable nights — specific reserve + grid detector");
        sheet.Cell("A1").Style.Font.Bold = true;
        sheet.Cell("A2").Value =
            string.Format(
                L(report, "{0} noches observables; {1} con al menos un episodio de reserva+red.", "{0} observable nights; {1} with at least one reserve+grid episode."),
                report.Family.ObservableNightCount,
                report.Family.NightsWithReserveGridUse);

        var row = 4;
        var nightHeaders = new[]
        {
            L(report, "Noche desde", "Night starting"),
            L(report, "Cobertura %", "Coverage %"),
            L(report, "Observable", "Observable"),
            L(report, "Episodios", "Episodes"),
            L(report, "Minutos reserva+red", "Reserve+grid minutes")
        };
        for (var col = 0; col < nightHeaders.Length; col++)
            sheet.Cell(row, col + 1).Value = nightHeaders[col];
        sheet.Range(row, 1, row, nightHeaders.Length).Style.Font.Bold = true;
        row++;

        foreach (var night in report.Family.Nights)
        {
            sheet.Cell(row, 1).Value = night.NightStartDate.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(row, 1).Style.DateFormat.Format = "dd-mm-yyyy";
            sheet.Cell(row, 2).Value = night.CoveragePercent;
            sheet.Cell(row, 2).Style.NumberFormat.Format = "0.0";
            sheet.Cell(row, 3).Value = night.IsObservable
                ? L(report, "Sí", "Yes")
                : L(report, "No", "No");
            sheet.Cell(row, 4).Value = night.ReserveGridEpisodeCount;
            sheet.Cell(row, 5).Value = night.ReserveGridMinutes;
            sheet.Cell(row, 5).Style.NumberFormat.Format = "0.0";
            row++;
        }

        row += 2;
        sheet.Cell(row, 1).Value =
            L(
                report,
                "Episodios nocturnos de batería en reserva + red",
                "Nighttime battery-reserve + grid episodes");
        sheet.Cell(row, 1).Style.Font.Bold = true;
        row++;

        if (report.Family.Events.Count == 0)
        {
            sheet.Range(row, 1, row + 1, 5).Merge();
            sheet.Cell(row, 1).Value =
                L(
                    report,
                    "No se detectaron episodios con evidencia suficiente en la parte observable del período.",
                    "No sufficiently supported episodes were detected in the observable part of the period.");
            sheet.Cell(row, 1).Style.Font.Italic = true;
            sheet.RangeUsed()?.Style.Alignment.WrapText = true;
            sheet.Columns().AdjustToContents();
            sheet.SheetView.FreezeRows(4);
            return;
        }

        var eventHeaders = new[]
        {
            L(report, "Inicio", "Start"),
            L(report, "Fin", "End"),
            L(report, "Duración min", "Duration min"),
            L(report, "SOC inicial %", "Start SOC %"),
            L(report, "SOC mínimo %", "Min SOC %"),
            L(report, "Umbral %", "Threshold %"),
            L(report, "Red máx. kW", "Max grid kW"),
            L(report, "Solar máx. kW", "Max solar kW")
        };
        for (var col = 0; col < eventHeaders.Length; col++)
            sheet.Cell(row, col + 1).Value = eventHeaders[col];
        sheet.Range(row, 1, row, eventHeaders.Length).Style.Font.Bold = true;
        row++;

        foreach (var item in report.Family.Events)
        {
            sheet.Cell(row, 1).Value = item.StartLocal.DateTime;
            sheet.Cell(row, 2).Value = item.EndLocal.DateTime;
            sheet.Range(row, 1, row, 2).Style.DateFormat.Format = "dd-mm-yyyy hh:mm";
            sheet.Cell(row, 3).Value = item.DurationMinutes;
            sheet.Cell(row, 4).Value = item.StartSocPercent;
            sheet.Cell(row, 5).Value = item.MinimumSocPercent;
            sheet.Cell(row, 6).Value = item.NormalGridTransferSocPercent;
            sheet.Cell(row, 7).Value = item.MaximumGridWatts / 1000.0;
            sheet.Cell(row, 8).Value = item.MaximumPvWatts / 1000.0;
            sheet.Range(row, 3, row, 8).Style.NumberFormat.Format = "0.00";
            row++;
        }

        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(4);
    }

    private static void SetNullableNumber(IXLCell cell, double? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        cell.Value = value.Value;
        cell.Style.NumberFormat.Format = "0.00";
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
                "Solar generado kWh", "Casa kWh", "Red importada kWh",
                "Batería entregada kWh", "Batería recibida kWh",
                "SOC prom %", "SOC mín %", "SOC máx %", "SOC fin %",
                "Solar → Casa kWh", "Batería → Casa kWh", "Enel → Casa kWh",
                "Casa sin atribuir kWh", "Cobertura atribución %", "Batería guardada fin kWh (estimada)",
                "Cobertura solar %", "Cobertura casa %", "Cobertura red %",
                "Cobertura batería %", "Cobertura SOC %", "Cobertura mínima %",
                "Residual balance medio abs %", "Residual balance máximo abs %"
            }
            : new[]
            {
                "Period", "Start UTC", "End UTC",
                "Solar generated kWh", "Home kWh", "Grid imported kWh",
                "Battery discharged kWh", "Battery charged kWh",
                "SOC avg %", "SOC min %", "SOC max %", "SOC end %",
                "Solar → Home kWh", "Battery → Home kWh", "Utility → Home kWh",
                "Unattributed home kWh", "Attribution coverage %", "Battery stored end kWh (estimate)",
                "PV coverage %", "Home coverage %", "Grid coverage %",
                "Battery coverage %", "SOC coverage %", "Minimum coverage %",
                "Mean abs balance residual %", "Maximum abs balance residual %"
            };

        for (var column = 0; column < headers.Length; column++)
            sheet.Cell(1, column + 1).Value = headers[column];

        sheet.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;

        var attributionByStart = report.Attribution.Buckets
            .ToDictionary(item => item.StartUtc);

        var targetRow = 2;
        foreach (var item in report.Table.Rows)
        {
            attributionByStart.TryGetValue(item.StartUtc, out var attribution);

            sheet.Cell(targetRow, 1).Value = item.LocalLabel;
            sheet.Cell(targetRow, 2).Value = item.StartUtc.UtcDateTime;
            sheet.Cell(targetRow, 3).Value = item.EndUtcExclusive.UtcDateTime;

            SetNullableNumber(sheet.Cell(targetRow, 4), item.PvEnergyDisplayKwh);
            SetNullableNumber(sheet.Cell(targetRow, 5), item.HouseEnergyDisplayKwh);
            SetNullableNumber(sheet.Cell(targetRow, 6), item.GridImportEnergyDisplayKwh);
            SetNullableNumber(sheet.Cell(targetRow, 7), item.BatteryDischargedEnergyDisplayKwh);
            SetNullableNumber(sheet.Cell(targetRow, 8), item.BatteryChargedEnergyDisplayKwh);

            SetNullableNumber(sheet.Cell(targetRow, 9), item.SocAveragePercent);
            SetNullableNumber(sheet.Cell(targetRow, 10), item.SocMinimumPercent);
            SetNullableNumber(sheet.Cell(targetRow, 11), item.SocMaximumPercent);
            SetNullableNumber(sheet.Cell(targetRow, 12), item.SocEndingPercent);

            if (attribution is not null)
            {
                sheet.Cell(targetRow, 13).Value = attribution.SolarToHouseKwh;
                sheet.Cell(targetRow, 14).Value = attribution.BatteryToHouseKwh;
                sheet.Cell(targetRow, 15).Value = attribution.GridToHouseKwh;
                sheet.Cell(targetRow, 16).Value = attribution.UnattributedHouseKwh;
                sheet.Cell(targetRow, 17).Value = attribution.AttributionCoverageOfObservedPercent;
                SetNullableNumber(sheet.Cell(targetRow, 18), attribution.BatteryStoredEndingKwh);
                sheet.Cell(targetRow, 25).Value = attribution.MeanAbsoluteBalanceResidualPercent;
                sheet.Cell(targetRow, 26).Value = attribution.MaximumAbsoluteBalanceResidualPercent;
            }

            sheet.Cell(targetRow, 19).Value = item.PvCoveragePercent;
            sheet.Cell(targetRow, 20).Value = item.HouseCoveragePercent;
            sheet.Cell(targetRow, 21).Value = item.GridCoveragePercent;
            sheet.Cell(targetRow, 22).Value = item.BatteryCoveragePercent;
            sheet.Cell(targetRow, 23).Value = item.SocCoveragePercent;
            sheet.Cell(targetRow, 24).Value = item.MinimumAvailableCoveragePercent;

            sheet.Range(targetRow, 4, targetRow, 18).Style.NumberFormat.Format = "0.00";
            sheet.Range(targetRow, 19, targetRow, 26).Style.NumberFormat.Format = "0.0";
            targetRow++;
        }

        sheet.Columns(2, 3).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, Math.Max(1, targetRow - 1), headers.Length).SetAutoFilter();
        sheet.Columns().AdjustToContents();
        for (var column = 1; column <= headers.Length; column++)
        {
            sheet.Column(column).Width = Math.Min(sheet.Column(column).Width, 28);
        }
    }

    private static void AddQualitySheet(
        XLWorkbook workbook,
        EnergyReportData report)
    {
        var sheet = workbook.Worksheets.Add(
            L(report, "Calidad", "Quality"));

        sheet.Cell("A1").Value =
            L(report, "Calidad y límites de interpretación", "Quality and interpretation limits");
        sheet.Cell("A1").Style.Font.Bold = true;
        sheet.Cell("A1").Style.Font.FontSize = 15;
        sheet.Range("A1:D1").Merge();

        sheet.Cell("A3").Value = L(report, "Indicador", "Indicator");
        sheet.Cell("B3").Value = L(report, "Valor", "Value");
        sheet.Cell("C3").Value = L(report, "Unidad / base", "Unit / basis");
        sheet.Cell("D3").Value = L(report, "Qué significa", "What it means");
        sheet.Range("A3:D3").Style.Font.Bold = true;

        var completeNights = Math.Max(
            0,
            report.Request.LocalEndDate.DayNumber -
            report.Request.LocalStartDate.DayNumber);
        var meanResidual = report.Attribution.Buckets.Count > 0
            ? report.Attribution.Buckets.Average(item => item.MeanAbsoluteBalanceResidualPercent)
            : 0;
        var maxResidual = report.Attribution.Buckets.Count > 0
            ? report.Attribution.Buckets.Max(item => item.MaximumAbsoluteBalanceResidualPercent)
            : 0;

        var rows = new (string Label, double Value, string Unit, string Meaning)[]
        {
            (
                L(report, "Cobertura solar", "Solar coverage"),
                report.Summary.PvPower.CoveragePercent,
                "%",
                L(report, "Parte del rango con continuidad suficiente de producción solar.", "Part of the range with sufficient solar-data continuity.")),
            (
                L(report, "Cobertura casa", "Home coverage"),
                report.Summary.HouseLoadPower.CoveragePercent,
                "%",
                L(report, "Parte del rango con continuidad suficiente del consumo de la casa.", "Part of the range with sufficient home-consumption continuity.")),
            (
                L(report, "Cobertura red", "Grid coverage"),
                report.Summary.GridImportPower.CoveragePercent,
                "%",
                L(report, "Parte del rango con continuidad suficiente de la señal de red.", "Part of the range with sufficient grid-signal continuity.")),
            (
                L(report, "Cobertura batería", "Battery coverage"),
                report.Summary.BatteryPower.CoveragePercent,
                "%",
                L(report, "Parte del rango con potencia de batería utilizable.", "Part of the range with usable battery-power data.")),
            (
                L(report, "Tiempo observado para atribución", "Observed time for attribution"),
                report.Attribution.ObservedTimeCoveragePercent,
                "%",
                L(report, "Tiempo en que fue posible observar el consumo de casa con continuidad.", "Time where household consumption could be observed continuously.")),
            (
                L(report, "Cobertura de atribución del consumo observado", "Attribution coverage of observed consumption"),
                report.Attribution.AttributionCoverageOfObservedPercent,
                "%",
                L(report, "Qué parte del consumo observado pudo asignarse con evidencia a Solar, Batería o Enel.", "Share of observed consumption that could be assigned with evidence to Solar, Battery or Utility.")),
            (
                L(report, "Consumo observado sin atribuir", "Observed consumption left unattributed"),
                report.Attribution.UnattributedHouseKwh,
                "kWh",
                L(report, "Energía observada que no se forzó artificialmente a una fuente.", "Observed energy that was not artificially forced into a source.")),
            (
                L(report, "Noches observables", "Observable nights"),
                report.Family.ObservableNightCount,
                string.Format(L(report, "de {0} completas", "of {0} complete"), completeNights),
                L(report, "Noches con evidencia suficiente para evaluar si la batería cubrió la alimentación.", "Nights with enough evidence to evaluate whether the battery covered supply.")),
            (
                L(report, "Residual medio de balance", "Mean balance residual"),
                meanResidual,
                "%",
                L(report, "Diferencia diagnóstica entre entradas y salidas medidas; no se usa para forzar un balance perfecto.", "Diagnostic difference between measured inputs and outputs; it is not used to force a perfect balance.")),
            (
                L(report, "Residual máximo de balance", "Maximum balance residual"),
                maxResidual,
                "%",
                L(report, "Peor diferencia observada dentro de los buckets del reporte.", "Worst observed difference within report buckets.")),
            (
                L(report, "Cambios históricos de configuración detectados", "Detected historical configuration changes"),
                report.Attribution.HistoricalConfigurationChangeCount,
                L(report, "cambios", "changes"),
                L(report, "Confirma que la configuración del inversor no se trata como constante en toda la historia.", "Confirms inverter configuration is not treated as constant through all history.")),
            (
                L(report, "Snapshots explícitos de modo", "Explicit mode snapshots"),
                report.Attribution.ExplicitModeSnapshotCount,
                L(report, "snapshots", "snapshots"),
                L(report, "Lecturas actuales con evidencia explícita SBU/OSO/LBU disponibles para contexto.", "Current-state readings with explicit SBU/OSO/LBU evidence available for context."))
        };

        var row = 4;
        foreach (var item in rows)
        {
            sheet.Cell(row, 1).Value = item.Label;
            sheet.Cell(row, 2).Value = item.Value;
            sheet.Cell(row, 2).Style.NumberFormat.Format = "0.00";
            sheet.Cell(row, 3).Value = item.Unit;
            sheet.Cell(row, 4).Value = item.Meaning;
            row++;
        }

        row += 2;
        sheet.Range(row, 1, row, 4).Merge();
        sheet.Cell(row, 1).Value =
            L(report, "REGLAS DE CALIDAD", "QUALITY RULES");
        sheet.Cell(row, 1).Style.Font.Bold = true;
        row++;

        var rules = new[]
        {
            L(report, "Faltante o desconocido nunca significa cero medido.", "Missing or unknown never means measured zero."),
            L(report, "La energía no se extrapola a través de huecos largos.", "Energy is not extrapolated across long gaps."),
            L(report, "Descarga total de batería no se iguala automáticamente a Batería → Casa.", "Total battery discharge is not automatically equated with Battery → Home."),
            L(report, "Producción solar total no se iguala a Solar → Casa.", "Total solar generation is not equated with Solar → Home."),
            L(report, "Solar no aprovechado/curtailment no se calcula como un residuo ingenuo.", "Unused/curtailed solar is not calculated as a naive residual."),
            L(report, "Un residual de balance se reporta como diagnóstico; no se reparte entre fuentes para hacer cerrar la ecuación.", "A balance residual is reported diagnostically; it is not distributed among sources to force the equation to close.")
        };

        foreach (var rule in rules)
        {
            sheet.Range(row, 1, row, 4).Merge();
            sheet.Cell(row, 1).Value = "• " + rule;
            row++;
        }

        sheet.Column(1).Width = 34;
        sheet.Column(2).Width = 16;
        sheet.Column(3).Width = 18;
        sheet.Column(4).Width = 72;
        sheet.RangeUsed()?.Style.Alignment.WrapText = true;
        for (var visualRow = 4; visualRow < row; visualRow++)
        {
            sheet.Row(visualRow).Height = 34;
        }
        sheet.SheetView.FreezeRows(3);
    }

    private static void AddGlossarySheet(
        XLWorkbook workbook,
        EnergyReportData report)
    {
        var sheet = workbook.Worksheets.Add(
            L(report, "Glosario", "Glossary"));

        sheet.Cell("A1").Value =
            L(report, "Guía para leer el reporte", "Report reading guide");
        sheet.Cell("A1").Style.Font.Bold = true;
        sheet.Cell("A1").Style.Font.FontSize = 15;
        sheet.Range("A1:C1").Merge();

        sheet.Cell("A3").Value = L(report, "Término / campo", "Term / field");
        sheet.Cell("B3").Value = L(report, "Categoría", "Category");
        sheet.Cell("C3").Value = L(report, "Explicación", "Explanation");
        sheet.Range("A3:C3").Style.Font.Bold = true;

        var rows = new (string Term, string Category, string Explanation)[]
        {
            (L(report, "Consumo de la casa", "Home consumption"), L(report, "Concepto", "Concept"), L(report, "Energía usada por todas las cargas de la vivienda, sin importar de qué fuente provino.", "Energy used by all household loads, regardless of source.")),
            (L(report, "Generación solar", "Solar generation"), L(report, "Concepto", "Concept"), L(report, "Toda la electricidad producida por los paneles en el período observado.", "All electricity produced by the panels during the observed period.")),
            (L(report, "Solar → Casa", "Solar → Home"), L(report, "Origen", "Source"), L(report, "Parte del consumo de la casa atribuida directamente a los paneles. No es igual a toda la generación solar.", "Household consumption attributed directly to the panels. It is not the same as total solar generation.")),
            (L(report, "Batería → Casa", "Battery → Home"), L(report, "Origen", "Source"), L(report, "Parte del consumo de la casa atribuida a energía que salió de la batería.", "Household consumption attributed to energy supplied from the battery.")),
            (L(report, "Enel / Red → Casa", "Utility / Grid → Home"), L(report, "Origen", "Source"), L(report, "Parte del consumo de la casa atribuida a Enel. NO representa toda la energía comprada/importada y NO se compara directamente con la boleta/medidor.", "Household consumption attributed to the utility grid. It does NOT represent all imported/purchased energy and should NOT be compared directly with the bill/meter.")),
            (L(report, "Importación total desde Enel", "Total import from utility"), L(report, "Concepto", "Concept"), L(report, "Toda la energía medida entrando desde Enel al sistema. ESTA SÍ es la cifra del reporte para comparar con el medidor/boleta, siempre usando la misma ventana horaria y considerando la cobertura. Puede superar Enel → Casa si parte de la energía fue a batería o a consumos/pérdidas internas.", "All energy measured entering the system from the utility. THIS is the report value to compare with the meter/bill, using the same time window and accounting for coverage. It may exceed Utility → Home if some energy went to the battery or internal consumption/losses.")),
            (L(report, "Sin atribuir", "Unattributed"), L(report, "Calidad", "Quality"), L(report, "Consumo observado cuyo origen no pudo demostrarse con suficiente evidencia. No se reparte artificialmente.", "Observed consumption whose source could not be demonstrated with enough evidence. It is not artificially distributed.")),
            ("SOC", L(report, "Batería", "Battery"), L(report, "Estado de carga de la batería en porcentaje. 100% significa llena según el BMS.", "Battery state of charge as a percentage. 100% means full according to the BMS.")),
            (L(report, "Energía guardada (estimada)", "Stored energy (estimate)"), L(report, "Batería", "Battery"), L(report, "Estimación en kWh calculada con capacidad útil configurada × SOC. No es una medición directa de kWh dentro de la batería.", "Estimated kWh calculated as configured usable capacity × SOC. It is not a direct kWh measurement inside the battery.")),
            (L(report, "Reserva normal", "Normal reserve"), L(report, "Batería", "Battery"), L(report, "Nivel en que el inversor normalmente puede pasar la casa a red según la configuración vigente.", "Level where the inverter can normally transfer the home to grid according to active configuration.")),
            (L(report, "Nivel mínimo protegido", "Protected minimum level"), L(report, "Batería", "Battery"), L(report, "Límite inferior que el sistema intenta no cruzar para proteger la batería, especialmente durante cortes o situaciones excepcionales.", "Lower limit the system tries not to cross to protect the battery, especially during outages or exceptional operation.")),
            (L(report, "Noche observable", "Observable night"), L(report, "Eventos", "Events"), L(report, "Noche con cobertura suficiente para decidir si hubo o no un problema de alimentación relacionado con reserva y uso de red.", "Night with enough coverage to decide whether a supply problem related to reserve/grid use occurred.")),
            (L(report, "Episodio reserva + red", "Reserve + grid episode"), L(report, "Eventos", "Events"), L(report, "Ocurrencia nocturna en que la batería llegó al umbral normal, hubo uso de red y el solar era ausente o insuficiente.", "Night occurrence where the battery reached the normal threshold, grid was used, and solar was absent or insufficient.")),
            (L(report, "Patrón", "Pattern"), L(report, "Análisis", "Analysis"), L(report, "Conclusión basada en observaciones repetidas con evidencia suficiente; no se construye a partir de un único máximo.", "Conclusion based on repeated observations with enough evidence; it is not built from one isolated maximum.")),
            (L(report, "Evento", "Event"), L(report, "Análisis", "Analysis"), L(report, "Ocurrencia concreta con inicio/fin u otra evidencia individual preservada.", "Concrete occurrence with start/end or other individual evidence preserved.")),
            (L(report, "Cobertura", "Coverage"), L(report, "Calidad", "Quality"), L(report, "Proporción del período con continuidad suficiente para calcular la métrica. Un hueco no se convierte en cero.", "Share of the period with enough continuity to calculate the metric. A gap is not converted to zero.")),
            (L(report, "Cobertura de atribución", "Attribution coverage"), L(report, "Calidad", "Quality"), L(report, "Porcentaje del consumo observado de la casa cuyo origen pudo asignarse con evidencia.", "Percentage of observed household consumption whose source could be assigned with evidence.")),
            ("W", L(report, "Unidad", "Unit"), L(report, "Watt: potencia instantánea.", "Watt: instantaneous power.")),
            ("kW", L(report, "Unidad", "Unit"), L(report, "Kilowatt: 1.000 W de potencia.", "Kilowatt: 1,000 W of power.")),
            ("kWh", L(report, "Unidad", "Unit"), L(report, "Kilowatt-hora: cantidad de energía acumulada durante un período.", "Kilowatt-hour: amount of energy accumulated over a period.")),
            ("%", L(report, "Unidad", "Unit"), L(report, "Porcentaje; en SOC indica qué tan cargada está la batería.", "Percentage; for SOC it indicates how full the battery is.")),
            (L(report, "Período", "Period"), L(report, "Detalle", "Detail"), L(report, "Bucket temporal definido por la agrupación elegida: hora, día, semana, mes o año.", "Time bucket defined by the selected aggregation: hour, day, week, month or year.")),
            (L(report, "Inicio UTC / Fin UTC", "Start UTC / End UTC"), L(report, "Detalle", "Detail"), L(report, "Límites técnicos del bucket en UTC para trazabilidad y procesamiento.", "Technical bucket boundaries in UTC for traceability and processing.")),
            (L(report, "Solar generado kWh", "Solar generated kWh"), L(report, "Detalle", "Detail"), L(report, "Energía solar total integrada en el bucket.", "Total solar energy integrated within the bucket.")),
            (L(report, "Casa kWh", "Home kWh"), L(report, "Detalle", "Detail"), L(report, "Consumo de la casa integrado en el bucket.", "Household consumption integrated within the bucket.")),
            (L(report, "Red importada kWh", "Grid imported kWh"), L(report, "Detalle", "Detail"), L(report, "Importación total de red integrada en el bucket.", "Total grid import integrated within the bucket.")),
            (L(report, "Batería entregada / recibida kWh", "Battery discharged / charged kWh"), L(report, "Detalle", "Detail"), L(report, "Movimientos totales de salida/entrada de la batería. No equivalen automáticamente a Batería → Casa.", "Total battery discharge/charge movements. They do not automatically equal Battery → Home.")),
            (L(report, "SOC prom / mín / máx / fin", "SOC avg / min / max / end"), L(report, "Detalle", "Detail"), L(report, "Resumen del estado de carga dentro del bucket y valor al cierre.", "Summary of state of charge within the bucket and ending value.")),
            (L(report, "Coberturas por métrica", "Per-metric coverage"), L(report, "Detalle", "Detail"), L(report, "Cobertura individual de solar, casa, red, batería y SOC.", "Individual coverage for solar, home, grid, battery and SOC.")),
            (L(report, "Cobertura mínima", "Minimum coverage"), L(report, "Detalle", "Detail"), L(report, "La menor cobertura entre las métricas disponibles usadas para describir el bucket.", "Lowest coverage among available metrics used to describe the bucket.")),
            (L(report, "Residual de balance", "Balance residual"), L(report, "Detalle", "Detail"), L(report, "Diferencia diagnóstica entre entradas y salidas medidas. Puede reflejar pérdidas, desfase temporal o semántica de señales; no se fuerza a cero.", "Diagnostic difference between measured inputs and outputs. It can reflect losses, timing or signal semantics; it is not forced to zero.")),
            (L(report, "Solar no aprovechado", "Unused solar"), L(report, "Límite", "Limit"), L(report, "No se calcula actualmente como generación menos uso, porque el inversor puede limitar la producción y no medimos curtailment directamente.", "It is not currently calculated as generation minus use because the inverter can curtail production and curtailment is not directly measured."))
        };

        var row = 4;
        foreach (var item in rows)
        {
            sheet.Cell(row, 1).Value = item.Term;
            sheet.Cell(row, 2).Value = item.Category;
            sheet.Cell(row, 3).Value = item.Explanation;
            row++;
        }

        sheet.Column(1).Width = 34;
        sheet.Column(2).Width = 18;
        sheet.Column(3).Width = 90;
        sheet.RangeUsed()?.Style.Alignment.WrapText = true;
        for (var glossaryRow = 4; glossaryRow < row; glossaryRow++)
        {
            sheet.Row(glossaryRow).Height = 42;
        }
        sheet.SheetView.FreezeRows(3);
        sheet.Range(3, 1, Math.Max(3, row - 1), 3).SetAutoFilter();
    }

    private static double FamilyCoveragePercent(EnergyReportData report)
    {
        var values = new[]
        {
            report.Summary.PvPower.CoveragePercent,
            report.Summary.HouseLoadPower.CoveragePercent,
            report.Summary.GridImportPower.CoveragePercent,
            report.Summary.BatteryPower.CoveragePercent
        };

        return values.Min();
    }

    private static int SelectedDayCount(EnergyReportData report) =>
        Math.Max(
            1,
            report.Request.LocalEndDate.DayNumber -
            report.Request.LocalStartDate.DayNumber + 1);

    private static string AggregationLabel(EnergyReportData report) =>
        AggregationLabel(report, report.Request.Aggregation.ToString());

    private static string AggregationLabel(
        EnergyReportData report,
        string aggregation) =>
        aggregation switch
        {
            "Hour" => L(report, "Hora", "Hour"),
            "Day" => L(report, "Día", "Day"),
            "Week" => L(report, "Semana", "Week"),
            "Month" => L(report, "Mes", "Month"),
            "Year" => L(report, "Año", "Year"),
            _ => aggregation
        };

    private static string ReportKindLabel(EnergyReportData report) =>
        report.Request.Kind switch
        {
            ReportKind.SimpleEnergy =>
                L(report, "Resumen simple de energía", "Simple energy summary"),
            ReportKind.DetailedEnergy =>
                L(report, "Informe detallado de energía", "Detailed energy report"),
            ReportKind.Battery =>
                L(report, "Informe de batería", "Battery report"),
            _ => report.Request.Kind.ToString()
        };

    private static string HighlightLabel(
        EnergyReportData report,
        string metricKey) =>
        metricKey switch
        {
            "solar-day-max" =>
                L(report, "Mayor producción solar diaria", "Highest daily solar production"),
            "house-day-max" =>
                L(report, "Mayor consumo diario de la casa", "Highest daily home consumption"),
            "grid-day-max" =>
                L(report, "Mayor uso diario de la red", "Highest daily grid use"),
            _ => metricKey
        };

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

    private static void AddGridImportPredictiveCardsPdf(
        Section section,
        EnergyReportData report,
        bool compact)
    {
        var statistical = report.GridImportStatistical;

        var heading = section.AddParagraph(
            L(
                report,
                "Rango estadístico de importación de red",
                "Statistical grid-import range"));
        heading.Format.Font.Size = compact ? 10.5 : 11.5;
        heading.Format.Font.Bold = true;
        heading.Format.SpaceBefore = Unit.FromPoint(compact ? 5 : 7);
        heading.Format.SpaceAfter = Unit.FromPoint(4);

        if (!statistical.HasPredictiveInterval)
        {
            var unavailable = section.AddParagraph(
                L(
                    report,
                    "No hay datos suficientes para construir P5/P50/P95 de forma independiente.",
                    "There is not enough data to build P5/P50/P95 independently."));
            unavailable.Format.Font.Size = 8;
            unavailable.Format.Font.Color = Colors.DimGray;
            return;
        }

        var cards = section.AddTable();
        cards.Borders.Width = 0;
        cards.AddColumn(Unit.FromCentimeter(5.43));
        cards.AddColumn(Unit.FromCentimeter(5.43));
        cards.AddColumn(Unit.FromCentimeter(5.43));
        var row = cards.AddRow();

        AddPdfFamilyCard(
            row.Cells[0],
            L(report, "P5 · MARGEN INFERIOR", "P5 · LOWER MARGIN"),
            $"{statistical.LowerKwh!.Value:N2} kWh",
            L(
                report,
                "Margen inferior del rango central del 90%.",
                "Lower edge of the central 90% range."),
            Colors.AliceBlue,
            compact ? 12 : 14);

        AddPdfFamilyCard(
            row.Cells[1],
            L(report, "P50 · ESTIMACIÓN CENTRAL", "P50 · CENTRAL ESTIMATE"),
            $"{statistical.MedianKwh!.Value:N2} kWh",
            L(
                report,
                "Mediana y estimación central.",
                "Median and central estimate."),
            Colors.Honeydew,
            compact ? 12 : 14);

        AddPdfFamilyCard(
            row.Cells[2],
            L(report, "P95 · MARGEN SUPERIOR", "P95 · UPPER MARGIN"),
            $"{statistical.UpperKwh!.Value:N2} kWh",
            L(
                report,
                "Margen superior del rango central del 90%.",
                "Upper edge of the central 90% range."),
            Colors.AliceBlue,
            compact ? 12 : 14);

        var note = section.AddParagraph(
            string.Format(
                L(
                    report,
                    "P5–P95 contiene el 90% central de las simulaciones y P50 es la estimación central. El modelo completa estadísticamente {0:N2} h sin cobertura usando telemetría comparable. No son lecturas Enel ni intervalos de calibración del medidor.",
                    "P5–P95 contains the central 90% of simulations and P50 is the central estimate. The model statistically completes {0:N2} h without coverage using comparable telemetry. They are not utility readings or meter-calibration intervals."),
                statistical.MissingHours));
        note.Format.Font.Size = 7.8;
        note.Format.Font.Italic = true;
        note.Format.Font.Color = Colors.DimGray;
        note.Format.SpaceAfter = Unit.FromPoint(4);
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

    private static void AddPdfMetricCard(
        Cell cell,
        string label,
        string value,
        string context)
    {
        AddPdfFamilyCard(
            cell,
            label,
            value,
            context,
            Colors.AliceBlue,
            14);
    }

    private static void AddPdfFamilyCard(
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
        labelParagraph.Format.Font.Size = 9;
        labelParagraph.Format.SpaceBefore = Unit.FromPoint(4);
        labelParagraph.Format.SpaceAfter = Unit.FromPoint(4);

        var valueParagraph = cell.AddParagraph(value);
        valueParagraph.Format.Font.Name = PreferredPdfNumericFont();
        valueParagraph.Format.Font.Bold = true;
        valueParagraph.Format.Font.Size = valueFontSize;
        valueParagraph.Format.SpaceAfter = Unit.FromPoint(4);

        var contextParagraph = cell.AddParagraph(context);
        contextParagraph.Format.Font.Size = 8;
        contextParagraph.Format.SpaceAfter = Unit.FromPoint(5);
    }

    private static string SourceShareContext(
        EnergyReportData report,
        double sourceKwh,
        string suffix)
    {
        if (report.Attribution.ObservedHouseKwh <= 0)
        {
            return L(report, "Sin base observable", "No observable base");
        }

        var percent =
            sourceKwh /
            report.Attribution.ObservedHouseKwh *
            100.0;

        return $"{percent:N1}% {suffix}";
    }

    private static void AddPdfValueRow(
        Table table,
        string label,
        string value)
    {
        var row = table.AddRow();
        row.Cells[0].AddParagraph(label);
        row.Cells[1].Format.Font.Name = PreferredPdfNumericFont();
        row.Cells[1].AddParagraph(value);
    }

    private static IReadOnlyList<GridUseDaySummary> BuildGridUseDays(
        EnergyReportData report)
    {
        var reserveDates = report.Family.Events
            .Select(item => DateOnly.FromDateTime(item.StartLocal.DateTime))
            .ToHashSet();

        return report.DailyAttribution.Buckets
            .Where(item =>
                item.ObservedHouseKwh > 0.01 &&
                item.GridToHouseKwh > 0.01)
            .Select(item =>
            {
                var share =
                    item.GridToHouseKwh /
                    item.ObservedHouseKwh *
                    100.0;
                var otherAttributed =
                    item.SolarToHouseKwh +
                    item.BatteryToHouseKwh;
                var kind =
                    share >= 99.0
                        ? GridUseDayKind.NearExclusive
                        : otherAttributed > 0.01
                            ? GridUseDayKind.Mixed
                            : GridUseDayKind.GridPlusUnknown;

                var hasReserve =
                    DateOnly.TryParse(item.LocalLabel, out var date) &&
                    reserveDates.Contains(date);

                return new GridUseDaySummary(
                    item.LocalLabel,
                    item.GridToHouseKwh,
                    item.ObservedHouseKwh,
                    share,
                    kind,
                    hasReserve);
            })
            .OrderBy(item => item.LocalLabel, StringComparer.Ordinal)
            .ToArray();
    }

    private static string GridUseKindLabel(
        EnergyReportData report,
        GridUseDayKind kind) =>
        kind switch
        {
            GridUseDayKind.NearExclusive =>
                L(report, "Enel casi exclusivo", "Nearly all utility"),
            GridUseDayKind.Mixed =>
                L(report, "Aporte mixto en el día", "Mixed sources in the day"),
            _ =>
                L(report, "Enel + origen no resuelto", "Utility + unresolved source")
        };

    private enum GridUseDayKind
    {
        Mixed,
        NearExclusive,
        GridPlusUnknown
    }

    private sealed record GridUseDaySummary(
        string LocalLabel,
        double GridToHouseKwh,
        double ObservedHouseKwh,
        double GridSharePercent,
        GridUseDayKind Kind,
        bool HasReserveGridEpisode);

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
