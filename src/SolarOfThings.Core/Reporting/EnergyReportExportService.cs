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
    private readonly FamilyReportAnalysisService _familyAnalysis;
    private static int _pdfFontsInitialized;

    public EnergyReportExportService(
        EnergyRangeStatisticsService statistics,
        EnergyAggregationTableService aggregation,
        FamilyReportAnalysisService familyAnalysis)
    {
        _statistics = statistics;
        _aggregation = aggregation;
        _familyAnalysis = familyAnalysis;
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

        var family = _familyAnalysis.Analyze(request);

        return new EnergyReportData(
            request,
            summary,
            table,
            family,
            DateTimeOffset.UtcNow);
    }

    public void ExportExcel(
        string path,
        EnergyReportData report)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var workbook = new XLWorkbook();
        AddSummarySheet(workbook, report);
        if (report.Request.Kind == ReportKind.SimpleEnergy)
        {
            AddPatternsSheet(workbook, report);
            AddEventsSheet(workbook, report);
        }
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
            AggregationLabel(report)));
        section.AddParagraph(string.Format(
            L(report, "Generado: {0}", "Generated: {0}"),
            report.GeneratedUtc.ToLocalTime().ToString("dd-MM-yyyy HH:mm")));

        if (report.Request.Kind == ReportKind.SimpleEnergy)
        {
            AddFamilySimplePdf(section, report);
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

    private static void AddFamilySimplePdf(
        Section section,
        EnergyReportData report)
    {
        AddFamilyPageOne(section, report);

        var pageTwo = section.AddParagraph(
            L(report, "Página 2 — Qué pasó con toda la energía", "Page 2 — What happened to all the energy"));
        pageTwo.Format.PageBreakBefore = true;
        pageTwo.Format.Font.Size = 15;
        pageTwo.Format.Font.Bold = true;
        pageTwo.Format.SpaceAfter = Unit.FromPoint(6);

        var totals = section.AddTable();
        totals.Borders.Width = 0.4;
        totals.AddColumn(Unit.FromCentimeter(8.0));
        totals.AddColumn(Unit.FromCentimeter(4.0));
        AddPdfValueRow(totals, L(report, "Producción solar total", "Total solar production"),
            EnergyValue(report.Summary.PvPower, report.Summary.PvEnergyKwh, report));
        AddPdfValueRow(totals, L(report, "Consumo total de la casa", "Total home consumption"),
            EnergyValue(report.Summary.HouseLoadPower, report.Summary.HouseEnergyKwh, report));
        AddPdfValueRow(totals, L(report, "Total tomado de la red", "Total grid import"),
            EnergyValue(report.Summary.GridImportPower, report.Summary.GridImportEnergyKwh, report));
        AddPdfValueRow(totals, L(report, "Batería: energía entregada", "Battery energy discharged"),
            EnergyValue(report.Summary.BatteryPower, report.Summary.BatteryDischargedEnergyKwh, report));
        AddPdfValueRow(totals, L(report, "Batería: energía recibida", "Battery energy charged"),
            EnergyValue(report.Summary.BatteryPower, report.Summary.BatteryChargedEnergyKwh, report));

        var unavailable = section.AddParagraph(
            L(
                report,
                "Energía solar que no pudimos aprovechar: todavía no puede calcularse con suficiente confianza. No se estima como un residuo.",
                "Solar energy we could not use: it cannot yet be calculated with enough confidence. It is not estimated as a residual."));
        unavailable.Format.SpaceBefore = Unit.FromPoint(6);
        unavailable.Format.Font.Italic = true;

        AddEnergyChart(section, report);
        AddBatterySocChart(section, report);

        var pageThree = section.AddParagraph(
            L(report, "Página 3 — Patrones y eventos del período", "Page 3 — Patterns and events for the selected period"));
        pageThree.Format.PageBreakBefore = true;
        pageThree.Format.Font.Size = 15;
        pageThree.Format.Font.Bold = true;
        pageThree.Format.SpaceAfter = Unit.FromPoint(6);

        AddFamilyPatternsPdf(section, report);
        AddFamilyEventsPdf(section, report);
        AddQuality(section, report);
        AddGlossary(section, report);
    }

    private static void AddFamilyPageOne(
        Section section,
        EnergyReportData report)
    {
        var intro = section.AddParagraph(
            L(
                report,
                "Las cuatro preguntas principales de la casa",
                "The household's four main questions"));
        intro.Format.Font.Size = 13;
        intro.Format.Font.Bold = true;
        intro.Format.SpaceBefore = Unit.FromPoint(10);
        intro.Format.SpaceAfter = Unit.FromPoint(5);

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
            warning.Format.SpaceAfter = Unit.FromPoint(6);
        }

        var table = section.AddTable();
        table.Borders.Width = 0.4;
        table.AddColumn(Unit.FromCentimeter(8.5));
        table.AddColumn(Unit.FromCentimeter(4.5));

        AddPdfValueRow(
            table,
            L(report, "1. Tomado de Enel / red", "1. Taken from utility/grid"),
            EnergyValue(report.Summary.GridImportPower, report.Summary.GridImportEnergyKwh, report));

        AddPdfValueRow(
            table,
            L(report, "2. Directamente del sol a la casa", "2. Direct solar to home"),
            L(report, "Aún no disponible con suficiente confianza", "Not yet available with enough confidence"));

        AddPdfValueRow(
            table,
            L(report, "3. Desde la batería a la casa", "3. Battery to home"),
            L(report, "Aún no disponible con suficiente confianza", "Not yet available with enough confidence"));

        AddPdfValueRow(
            table,
            L(report, "4. Noches con reserva + uso de red", "4. Nights reaching reserve + grid use"),
            string.Format(
                L(report, "{0} de {1} noches observables", "{0} of {1} observable nights"),
                report.Family.NightsWithReserveGridUse,
                report.Family.ObservableNightCount));

        AddPdfValueRow(
            table,
            L(report, "Episodios detectados", "Detected episodes"),
            report.Family.ReserveGridEpisodeCount.ToString("N0"));

        AddPdfValueRow(
            table,
            L(report, "Tiempo total observado", "Total observed time"),
            $"{report.Family.ReserveGridTotalMinutes / 60.0:N2} " +
            L(report, "horas", "hours"));

        if (report.Family.TypicalReserveTime.HasValue)
        {
            AddPdfValueRow(
                table,
                L(report, "Hora típica de llegada a reserva", "Typical reserve-arrival time"),
                report.Family.TypicalReserveTime.Value.ToString("HH:mm"));
        }

        var note = section.AddParagraph(
            L(
                report,
                "Un episodio nocturno se cuenta sólo cuando la batería está en su umbral normal de transferencia, hay uso de red y el solar es ausente o insuficiente. Una noche sin datos suficientes no se cuenta como una noche sin problemas.",
                "A night episode is counted only when the battery is at its normal transfer threshold, grid is in use, and solar is absent or insufficient. A night without enough data is not counted as a problem-free night."));
        note.Format.SpaceBefore = Unit.FromPoint(6);
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

    private static void AddFamilyEventsPdf(
        Section section,
        EnergyReportData report)
    {
        var heading = section.AddParagraph(
            L(report, "Todas las ocurrencias detectadas", "All detected occurrences"));
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

        if (report.Request.Kind == ReportKind.SimpleEnergy)
        {
            sheet.Range("A1:I1").Merge();
            sheet.Cell("A1").Value = report.Request.Title;
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

            AddFamilySummarySheet(sheet, report);

            for (var column = 1; column <= 9; column++)
            {
                sheet.Column(column).Width = 14;
            }

            sheet.RangeUsed()?.Style.Alignment.WrapText = true;
            sheet.SheetView.FreezeRows(3);
        }
        else
        {
            sheet.Cell("A1").Value = report.Request.Title;
            sheet.Cell("A1").Style.Font.Bold = true;
            sheet.Cell("A1").Style.Font.FontSize = 16;

            sheet.Cell("A3").Value = L(report, "Período", "Period");
            sheet.Cell("B3").Value =
                $"{report.Request.LocalStartDate:dd-MM-yyyy} — {report.Request.LocalEndDate:dd-MM-yyyy}";
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
            L(report, "1. DESDE ENEL / RED", "1. FROM UTILITY / GRID"),
            report.Summary.GridImportPower.SampleCount >= 2
                ? $"{report.Summary.GridImportEnergyKwh:N2} kWh"
                : L(report, "Sin datos suficientes", "Insufficient data"),
            string.Format(
                L(report, "Cobertura del dato: {0:N1}%", "Data coverage: {0:N1}%"),
                report.Summary.GridImportPower.CoveragePercent),
            L(
                report,
                partial
                    ? "Energía tomada de la red en la parte del período que sí fue medida."
                    : "Energía tomada de la compañía eléctrica durante el período.",
                partial
                    ? "Energy taken from the grid in the measured part of the period."
                    : "Energy taken from the utility during the period."));

        AddFamilyCard(
            sheet,
            "D10:F15",
            L(report, "2. DIRECTAMENTE DEL SOL", "2. DIRECTLY FROM SOLAR"),
            L(report, "Aún no disponible", "Not yet available"),
            L(report, "Requiere atribución PV → Casa validada", "Requires validated PV → Home attribution"),
            L(
                report,
                "No se sustituye por “solar producido”, porque responde una pregunta diferente.",
                "It is not replaced by “solar produced”, because that answers a different question."));

        AddFamilyCard(
            sheet,
            "G10:I15",
            L(report, "3. DESDE LA BATERÍA", "3. FROM THE BATTERY"),
            L(report, "Aún no disponible", "Not yet available"),
            L(report, "Requiere atribución Batería → Casa validada", "Requires validated Battery → Home attribution"),
            L(
                report,
                "La energía descargada de la batería se informa en la Página 2, pero no se presenta como si toda hubiese ido a la casa.",
                "Battery discharge is shown on Page 2, but is not presented as if all of it necessarily supplied the home."));

        sheet.Range("A17:I18").Merge();
        sheet.Cell("A17").Value =
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
        sheet.Cell("A17").Style.Font.Bold = true;
        sheet.Cell("A17").Style.Font.FontSize = 15;
        sheet.Cell("A17").Style.Alignment.Horizontal =
            XLAlignmentHorizontalValues.Center;
        sheet.Cell("A17").Style.Alignment.Vertical =
            XLAlignmentVerticalValues.Center;
        sheet.Cell("A17").Style.Fill.BackgroundColor =
            XLColor.FromHtml("#D9EAF7");
        sheet.Range("A17:I18").Style.Border.OutsideBorder =
            XLBorderStyleValues.Thin;

        sheet.Range("A20:I20").Merge();
        sheet.Cell("A20").Value =
            L(report, "4. ¿ALCANZÓ LA BATERÍA DURANTE LA NOCHE?", "4. DID THE BATTERY COVER THE NIGHT?");
        sheet.Cell("A20").Style.Font.Bold = true;
        sheet.Cell("A20").Style.Font.FontSize = 13;

        AddNightCard(
            sheet,
            "A22:C25",
            L(report, "SIN EPISODIO RESERVA + RED", "NO RESERVE + GRID EPISODE"),
            nightsWithoutReserveGrid.ToString("N0"),
            string.Format(
                L(report, "de {0} noches observables", "of {0} observable nights"),
                family.ObservableNightCount),
            "#E2F0D9");

        AddNightCard(
            sheet,
            "D22:F25",
            L(report, "QUEDAMOS CORTOS", "BATTERY RAN SHORT"),
            family.NightsWithReserveGridUse.ToString("N0"),
            string.Format(
                L(report, "{0} episodios · {1:N2} h", "{0} episodes · {1:N2} h"),
                family.ReserveGridEpisodeCount,
                family.ReserveGridTotalMinutes / 60.0),
            "#FCE4D6");

        AddNightCard(
            sheet,
            "G22:I25",
            L(report, "NOCHES SIN DATOS SUFICIENTES", "NIGHTS WITHOUT ENOUGH DATA"),
            unknownNights.ToString("N0"),
            string.Format(
                L(report, "de {0} noches completas del rango", "of {0} complete nights in range"),
                selectedNights),
            "#E7E6E6");

        sheet.Range("A27:I30").Merge();
        var interpretation = partial
            ? string.Format(
                L(
                    report,
                    "¿QUÉ SIGNIFICA ESTO? Este informe es parcial. En la parte realmente observada se registraron {0:N2} kWh tomados de la red y {1:N2} kWh de consumo de la casa. Sólo {2} de {3} noches completas fueron suficientemente observables; en esas noches se detectaron {4} episodios en que la batería llegó a su reserva normal y fue necesario usar red por falta de solar suficiente.",
                    "WHAT DOES THIS MEAN? This report is partial. In the actually observed portion, {0:N2} kWh were taken from the grid and {1:N2} kWh of home consumption were recorded. Only {2} of {3} complete nights were sufficiently observable; those nights contained {4} episodes where the battery reached its normal reserve and grid was needed while solar was insufficient."),
                report.Summary.GridImportEnergyKwh,
                report.Summary.HouseEnergyKwh,
                family.ObservableNightCount,
                selectedNights,
                family.ReserveGridEpisodeCount)
            : string.Format(
                L(
                    report,
                    "¿QUÉ SIGNIFICA ESTO? La casa consumió {0:N2} kWh y tomó {1:N2} kWh desde la red. En {2} de {3} noches observables se detectó al menos un episodio en que la batería llegó a su reserva normal y fue necesario usar red por falta de solar suficiente.",
                    "WHAT DOES THIS MEAN? The home used {0:N2} kWh and took {1:N2} kWh from the grid. On {2} of {3} observable nights, at least one episode was detected where the battery reached its normal reserve and grid was needed while solar was insufficient."),
                report.Summary.HouseEnergyKwh,
                report.Summary.GridImportEnergyKwh,
                family.NightsWithReserveGridUse,
                family.ObservableNightCount);
        sheet.Cell("A27").Value = interpretation;
        sheet.Cell("A27").Style.Font.Italic = true;
        sheet.Cell("A27").Style.Alignment.Vertical =
            XLAlignmentVerticalValues.Center;
        sheet.Cell("A27").Style.Fill.BackgroundColor =
            XLColor.FromHtml("#F3F6F9");
        sheet.Range("A27:I30").Style.Border.OutsideBorder =
            XLBorderStyleValues.Thin;

        StyleFamilySection(
            sheet,
            "A32:I32",
            L(report, "PÁGINA 2 — ¿QUÉ PASÓ CON TODA LA ENERGÍA?", "PAGE 2 — WHAT HAPPENED TO ALL THE ENERGY?"));

        var pageTwoRows = new[]
        {
            (
                partial
                    ? L(report, "Producción solar registrada", "Recorded solar production")
                    : L(report, "Producción solar total", "Total solar production"),
                report.Summary.PvPower,
                report.Summary.PvEnergyKwh,
                L(report, "Todo lo que produjeron los paneles en la parte medida.", "Everything produced by the panels in the measured portion.")),
            (
                partial
                    ? L(report, "Consumo de la casa registrado", "Recorded home consumption")
                    : L(report, "Consumo total de la casa", "Total home consumption"),
                report.Summary.HouseLoadPower,
                report.Summary.HouseEnergyKwh,
                L(report, "Todo lo usado por la vivienda, sin importar de dónde vino.", "Everything used by the home, regardless of source.")),
            (
                partial
                    ? L(report, "Energía de red registrada", "Recorded grid energy")
                    : L(report, "Total tomado de la red", "Total grid import"),
                report.Summary.GridImportPower,
                report.Summary.GridImportEnergyKwh,
                L(report, "Electricidad tomada de la compañía.", "Electricity taken from the utility.")),
            (
                L(report, "Batería: energía entregada", "Battery energy discharged"),
                report.Summary.BatteryPower,
                report.Summary.BatteryDischargedEnergyKwh,
                L(report, "Movimiento de salida de la batería; no equivale todavía a Batería → Casa.", "Battery discharge movement; not yet equivalent to Battery → Home.")),
            (
                L(report, "Batería: energía recibida", "Battery energy charged"),
                report.Summary.BatteryPower,
                report.Summary.BatteryChargedEnergyKwh,
                L(report, "Energía que entró a la batería durante la parte medida.", "Energy that entered the battery during the measured portion."))
        };

        var row = 34;
        foreach (var item in pageTwoRows)
        {
            sheet.Range(row, 1, row, 3).Merge();
            sheet.Cell(row, 1).Value = item.Item1;
            sheet.Range(row, 4, row, 5).Merge();
            sheet.Cell(row, 4).Value =
                item.Item2.SampleCount >= 2
                    ? $"{item.Item3:N2} kWh"
                    : L(report, "Sin datos suficientes", "Insufficient data");
            sheet.Range(row, 6, row, 9).Merge();
            sheet.Cell(row, 6).Value = item.Item4;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 4).Style.Font.Bold = true;
            sheet.Range(row, 1, row, 9).Style.Border.BottomBorder =
                XLBorderStyleValues.Hair;
            row += 2;
        }

        sheet.Range(row, 1, row + 1, 3).Merge();
        sheet.Cell(row, 1).Value =
            L(report, "Solar que no pudimos aprovechar", "Solar energy we could not use");
        sheet.Range(row, 4, row + 1, 9).Merge();
        sheet.Cell(row, 4).Value =
            L(
                report,
                "Todavía no puede calcularse con suficiente confianza. No se inventa como “solar producido menos solar usado”.",
                "It still cannot be calculated with enough confidence. It is not invented as “solar produced minus solar used”.");
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Range(row, 1, row + 1, 9).Style.Fill.BackgroundColor =
            XLColor.FromHtml("#FFF2CC");
        sheet.Range(row, 1, row + 1, 9).Style.Alignment.Vertical =
            XLAlignmentVerticalValues.Center;
        sheet.Row(row).Height = 26;
        sheet.Row(row + 1).Height = 26;

        sheet.Range($"A{row + 3}:I{row + 5}").Merge();
        sheet.Cell($"A{row + 3}").Value =
            L(
                report,
                "La hoja Patrones sólo afirmará tendencias cuando haya suficientes días observables. Eventos conserva todas las ocurrencias y Calidad explica qué partes del período realmente tienen datos.",
                "The Patterns sheet only states tendencies when enough days are observable. Events preserves every occurrence, and Quality explains which parts of the period actually contain data.");
        sheet.Cell($"A{row + 3}").Style.Font.Italic = true;
        sheet.Range($"A{row + 3}:I{row + 5}").Style.Alignment.Vertical =
            XLAlignmentVerticalValues.Center;
        sheet.Row(row + 3).Height = 24;
        sheet.Row(row + 4).Height = 24;
        sheet.Row(row + 5).Height = 24;
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
        valueRows.Style.Font.FontSize = 16;
        valueRows.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        valueRows.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        contextRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        explanationRows.Style.Font.FontSize = 9;
        explanationRows.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        explanationRows.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FBFD");
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
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
        valueRows.Style.Font.FontSize = 18;
        valueRows.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        valueRows.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        contextRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml(fillColor);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
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
            L(report, "Patrones y eventos del período", "Patterns and events for the selected period");
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
        sheet.Cell(row, 1).Value =
            string.Format(
                L(report, "Evolución ({0})", "Evolution ({0})"),
                AggregationLabel(report, report.Family.EvolutionAggregation));
        sheet.Cell(row, 1).Style.Font.Bold = true;
        row++;

        var headers = new[]
        {
            L(report, "Período", "Period"),
            L(report, "Solar kWh", "Solar kWh"),
            L(report, "Casa kWh", "Home kWh"),
            L(report, "Red kWh", "Grid kWh"),
            L(report, "Cobertura mín. %", "Min coverage %")
        };
        for (var col = 0; col < headers.Length; col++)
            sheet.Cell(row, col + 1).Value = headers[col];
        sheet.Range(row, 1, row, headers.Length).Style.Font.Bold = true;
        row++;

        foreach (var item in report.Family.Evolution)
        {
            sheet.Cell(row, 1).Value = item.LocalLabel;
            SetNullableNumber(sheet.Cell(row, 2), item.PvEnergyKwh);
            SetNullableNumber(sheet.Cell(row, 3), item.HouseEnergyKwh);
            SetNullableNumber(sheet.Cell(row, 4), item.GridImportEnergyKwh);
            sheet.Cell(row, 5).Value = item.MinimumCoveragePercent;
            sheet.Cell(row, 5).Style.NumberFormat.Format = "0.0";
            row++;
        }

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

    private static void AddEventsSheet(
        XLWorkbook workbook,
        EnergyReportData report)
    {
        var sheet = workbook.Worksheets.Add(
            L(report, "Eventos", "Events"));

        sheet.Cell("A1").Value =
            L(report, "Noches observables", "Observable nights");
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
            L(report, "Todos los episodios detectados", "All detected episodes");
        sheet.Cell(row, 1).Style.Font.Bold = true;
        row++;

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
            sheet.Range(targetRow, 4, targetRow, 8).Style.NumberFormat.Format = "0.00";
            if (item.SocAveragePercent.HasValue) sheet.Cell(targetRow, 9).Value = item.SocAveragePercent.Value;
            if (item.SocMinimumPercent.HasValue) sheet.Cell(targetRow, 10).Value = item.SocMinimumPercent.Value;
            if (item.SocMaximumPercent.HasValue) sheet.Cell(targetRow, 11).Value = item.SocMaximumPercent.Value;
            if (item.SocEndingPercent.HasValue) sheet.Cell(targetRow, 12).Value = item.SocEndingPercent.Value;
            sheet.Range(targetRow, 9, targetRow, 12).Style.NumberFormat.Format = "0.00";
            sheet.Cell(targetRow, 13).Value = item.PvCoveragePercent;
            sheet.Cell(targetRow, 14).Value = item.HouseCoveragePercent;
            sheet.Cell(targetRow, 15).Value = item.GridCoveragePercent;
            sheet.Cell(targetRow, 16).Value = item.BatteryCoveragePercent;
            sheet.Cell(targetRow, 17).Value = item.SocCoveragePercent;
            sheet.Cell(targetRow, 18).Value = item.MinimumAvailableCoveragePercent;
            sheet.Range(targetRow, 13, targetRow, 18).Style.NumberFormat.Format = "0.0";
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
            sheet.Cell(row, 2).Style.NumberFormat.Format = "0.0";
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

    private static double FamilyCoveragePercent(EnergyReportData report)
    {
        var values = new[]
        {
            report.Summary.PvPower.CoveragePercent,
            report.Summary.HouseLoadPower.CoveragePercent,
            report.Summary.GridImportPower.CoveragePercent,
            report.Summary.BatteryPower.CoveragePercent
        };

        return values.Where(value => value > 0).DefaultIfEmpty(0).Min();
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
