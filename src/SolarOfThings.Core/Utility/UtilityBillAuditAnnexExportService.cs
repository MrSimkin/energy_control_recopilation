using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Microsoft.Data.Sqlite;
using PdfSharp.Fonts;
using SolarOfThings.Core.Data;
using SolarOfThings.Core.SolarOfThings;

namespace SolarOfThings.Core.Utility;

/// <summary>
/// Separate evidence annex for one Enel bill audit.
/// Produces a ZIP containing:
/// - printable raw numerical annex PDF;
/// - frame-level CSV;
/// - gap CSV;
/// - economic-scenario CSV;
/// - provenance/integrity manifest.
/// </summary>
public sealed class UtilityBillAuditAnnexExportService
{
    public const string ExportVersion =
        "bill-audit-evidence-annex.v2";

    private readonly SqliteDatabase _database;
    private readonly UtilityMeterRepository _repository;
    private readonly UtilityBillGapStatisticalCompletionService _gapCompletion;
    private readonly UtilityBillTariffScenarioAnalysisService _tariffScenarios;
    private static int _pdfFontsInitialized;

    public UtilityBillAuditAnnexExportService(
        SqliteDatabase database,
        UtilityMeterRepository repository,
        UtilityBillGapStatisticalCompletionService gapCompletion,
        UtilityBillTariffScenarioAnalysisService tariffScenarios)
    {
        _database = database;
        _repository = repository;
        _gapCompletion = gapCompletion;
        _tariffScenarios = tariffScenarios;
    }

    public void ExportZip(
        string path,
        string deviceId,
        long billId,
        string timeZoneId,
        string languageCode,
        string? producerIdentity = null)
    {
        var bill = _repository.GetBills()
            .SingleOrDefault(item => item.BillId == billId)
            ?? throw new InvalidOperationException(
                "Bill was not found.");

        var startLocalDate =
            SolarApiTime.GetLocalDate(
                bill.PeriodStartUtc,
                timeZoneId);
        var endLocalDate =
            SolarApiTime.GetLocalDate(
                bill.PeriodEndUtc,
                timeZoneId);

        var analysis =
            _gapCompletion.Analyze(
                deviceId,
                startLocalDate,
                endLocalDate,
                timeZoneId);

        var tariff =
            _tariffScenarios.Analyze(
                billId,
                timeZoneId,
                analysis.Completion);

        var telemetry =
            LoadTelemetry(
                deviceId,
                analysis.StartUtc,
                analysis.EndUtcExclusive,
                analysis.ContinuityThresholdMinutes,
                timeZoneId);

        var entries =
            new Dictionary<string, byte[]>(
                StringComparer.Ordinal)
            {
                ["01-telemetria-importacion-red.csv"] =
                    Utf8(
                        TelemetryCsv(
                            telemetry)),
                ["02-intervalos-sin-telemetria.csv"] =
                    Utf8(
                        GapsCsv(
                            analysis)),
                ["03-escenarios-economicos.csv"] =
                    Utf8(
                        EconomicCsv(
                            tariff,
                            bill)),
                ["04-fuentes-y-metodo.txt"] =
                    Utf8(
                        ProvenanceText(
                            bill,
                            analysis,
                            tariff,
                            telemetry.Count,
                            languageCode,
                            producerIdentity)),
                ["05-cobertura-diaria.csv"] =
                    Utf8(
                        DailyCoverageCsv(
                            analysis))
            };

        var tempPdf =
            Path.Combine(
                Path.GetTempPath(),
                $"Anexo-Tecnico-Enel-{Guid.NewGuid():N}.pdf");

        try
        {
            ExportPrintablePdf(
                tempPdf,
                bill,
                analysis,
                tariff,
                telemetry,
                languageCode,
                producerIdentity);

            entries[
                "00-Anexo-Tecnico-Evidencia-Numerica.pdf"] =
                File.ReadAllBytes(
                    tempPdf);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPdf))
                    File.Delete(tempPdf);
            }
            catch
            {
            }
        }

        var manifest =
            new
            {
                export_version = ExportVersion,
                generated_utc =
                    DateTimeOffset.UtcNow.ToString(
                        "O",
                        CultureInfo.InvariantCulture),
                interval =
                    new
                    {
                        start_local =
                            analysis.StartLocalDate
                                .ToString("yyyy-MM-dd"),
                        end_local_inclusive =
                            analysis.EndLocalDateInclusive
                                .ToString("yyyy-MM-dd"),
                        start_utc =
                            analysis.StartUtc.ToString("O"),
                        end_utc_exclusive =
                            analysis.EndUtcExclusive.ToString("O")
                    },
                producer_identity =
                    producerIdentity,
                statistical_method =
                    analysis.Completion.MethodVersion,
                statistical_status =
                    analysis.Status,
                enel_value_used_in_construction =
                    analysis.EnelValueUsedInConstruction,
                tariff_sources =
                    tariff.PublicationPeriods
                        .Select(item =>
                            new
                            {
                                publication_id =
                                    item.PublicationId,
                                effective_from =
                                    item.EffectiveFrom?
                                        .ToString("yyyy-MM-dd"),
                                is_retroactive =
                                    item.IsRetroactive,
                                applied_from =
                                    item.AppliedFrom
                                        .ToString("yyyy-MM-dd"),
                                applied_to =
                                    item.AppliedTo
                                        .ToString("yyyy-MM-dd"),
                                days =
                                    item.Days,
                                weight =
                                    item.Weight,
                                title =
                                    item.PublicationTitle,
                                source_url =
                                    item.SourceUrl,
                                sha256 =
                                    item.ContentSha256
                            })
                        .ToArray(),
                files =
                    entries
                        .OrderBy(item => item.Key)
                        .Select(item =>
                            new
                            {
                                name = item.Key,
                                bytes =
                                    item.Value.Length,
                                sha256 =
                                    Convert.ToHexString(
                                            SHA256.HashData(
                                                item.Value))
                                        .ToLowerInvariant()
                            })
                        .ToArray()
            };

        entries[
            "99-manifest-integridad.json"] =
            Utf8(
                JsonSerializer.Serialize(
                    manifest,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));

        Directory.CreateDirectory(
            Path.GetDirectoryName(path) ??
            Environment.CurrentDirectory);

        if (File.Exists(path))
            File.Delete(path);

        using var file =
            File.Create(path);
        using var zip =
            new ZipArchive(
                file,
                ZipArchiveMode.Create);

        foreach (var entry in entries
                     .OrderBy(item => item.Key))
        {
            var zipEntry =
                zip.CreateEntry(
                    entry.Key,
                    CompressionLevel.Optimal);
            using var stream =
                zipEntry.Open();
            stream.Write(
                entry.Value,
                0,
                entry.Value.Length);
        }
    }

    private IReadOnlyList<TelemetryRow>
        LoadTelemetry(
            string deviceId,
            DateTimeOffset startUtc,
            DateTimeOffset endUtcExclusive,
            double thresholdMinutes,
            string timeZoneId)
    {
        using var connection =
            _database.OpenConnection();
        using var command =
            connection.CreateCommand();

        command.CommandText = """
            SELECT recorded_at_utc,
                   normalized_value,
                   confidence,
                   quality,
                   source_attribute_key,
                   normalization_rule_version
            FROM normalized_metric_sample
            WHERE device_id = $deviceId
              AND metric_key = 'grid_import_power_w'
              AND normalized_value IS NOT NULL
              AND confidence <> 'UNRESOLVED'
              AND recorded_at_utc >= $startUtc
              AND recorded_at_utc < $endUtc
            ORDER BY recorded_at_utc;
            """;
        command.Parameters.AddWithValue(
            "$deviceId",
            deviceId);
        command.Parameters.AddWithValue(
            "$startUtc",
            startUtc.ToUniversalTime()
                .ToString("O"));
        command.Parameters.AddWithValue(
            "$endUtc",
            endUtcExclusive.ToUniversalTime()
                .ToString("O"));

        using var reader =
            command.ExecuteReader();
        var raw =
            new List<RawTelemetry>();

        while (reader.Read())
        {
            if (!DateTimeOffset.TryParse(
                    reader.GetString(0),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var timestamp))
            {
                continue;
            }

            raw.Add(
                new RawTelemetry(
                    timestamp.ToUniversalTime(),
                    reader.GetDouble(1),
                    reader.IsDBNull(2)
                        ? string.Empty
                        : reader.GetString(2),
                    reader.IsDBNull(3)
                        ? string.Empty
                        : reader.GetString(3),
                    reader.IsDBNull(4)
                        ? string.Empty
                        : reader.GetString(4),
                    reader.IsDBNull(5)
                        ? string.Empty
                        : reader.GetString(5)));
        }

        var deduped =
            raw
                .GroupBy(item =>
                    item.TimestampUtc)
                .Select(group =>
                    group.Last())
                .OrderBy(item =>
                    item.TimestampUtc)
                .ToArray();

        var output =
            new List<TelemetryRow>(
                deduped.Length);

        for (var index = 0;
             index < deduped.Length;
             index++)
        {
            var current =
                deduped[index];
            var next =
                index + 1 < deduped.Length
                    ? deduped[index + 1]
                    : null;

            double? deltaMinutes = null;
            double? energyWh = null;
            var linkStatus = "LAST";

            if (next is not null)
            {
                deltaMinutes =
                    (next.TimestampUtc -
                     current.TimestampUtc)
                    .TotalMinutes;

                if (deltaMinutes > 0 &&
                    deltaMinutes <= thresholdMinutes)
                {
                    var hours =
                        deltaMinutes.Value /
                        60.0;
                    energyWh =
                        (Math.Max(
                             0,
                             current.Watts) +
                         Math.Max(
                             0,
                             next.Watts)) /
                        2.0 *
                        hours;
                    linkStatus = "OBSERVED_CONTINUITY";
                }
                else if (deltaMinutes > thresholdMinutes)
                {
                    linkStatus = "GAP_TO_NEXT";
                }
                else
                {
                    linkStatus = "NON_POSITIVE_DELTA";
                }
            }

            output.Add(
                new TelemetryRow(
                    index + 1,
                    current.TimestampUtc,
                    SolarApiTime.ConvertToLocalTime(
                        current.TimestampUtc,
                        timeZoneId),
                    current.Watts,
                    deltaMinutes,
                    energyWh,
                    linkStatus,
                    current.Confidence,
                    current.Quality,
                    current.SourceAttributeKey,
                    current.NormalizationRuleVersion));
        }

        return output;
    }

    private static string TelemetryCsv(
        IReadOnlyList<TelemetryRow> rows)
    {
        var sb =
            new StringBuilder();
        sb.AppendLine(
            "fila,timestamp_local,timestamp_utc,grid_import_w,delta_next_min,observed_energy_wh_to_next,link_status,confidence,quality,source_attribute,normalization_rule");

        foreach (var row in rows)
        {
            sb.AppendLine(
                string.Join(
                    ",",
                    Csv(
                        row.RowNumber.ToString(
                            CultureInfo.InvariantCulture)),
                    Csv(
                        row.TimestampLocal.ToString(
                            "O")),
                    Csv(
                        row.TimestampUtc.ToString(
                            "O")),
                    Csv(
                        row.Watts.ToString(
                            "0.###",
                            CultureInfo.InvariantCulture)),
                    Csv(
                        row.DeltaNextMinutes?
                            .ToString(
                                "0.######",
                                CultureInfo.InvariantCulture)),
                    Csv(
                        row.ObservedEnergyWhToNext?
                            .ToString(
                                "0.######",
                                CultureInfo.InvariantCulture)),
                    Csv(
                        row.LinkStatus),
                    Csv(
                        row.Confidence),
                    Csv(
                        row.Quality),
                    Csv(
                        row.SourceAttributeKey),
                    Csv(
                        row.NormalizationRuleVersion)));
        }

        return sb.ToString();
    }

    private static string GapsCsv(
        UtilityBillGapStatisticalAnalysis analysis)
    {
        var sb =
            new StringBuilder();
        sb.AppendLine(
            "gap_index,kind,start_local,end_local,duration_minutes,start_w,end_w,day_type,calibration_days,calibration_first_date,calibration_last_date,q05_kwh,q50_kwh,q95_kwh,backtest_cases,backtest_coverage,p50_bias_kwh,p50_mae_kwh,mean_interval_score");

        foreach (var gap in analysis.Gaps)
        {
            sb.AppendLine(
                string.Join(
                    ",",
                    Csv(
                        gap.GapIndex.ToString(
                            CultureInfo.InvariantCulture)),
                    Csv(gap.Kind),
                    Csv(gap.StartLocal.ToString("O")),
                    Csv(gap.EndLocal.ToString("O")),
                    Csv(
                        gap.DurationMinutes.ToString(
                            "0.######",
                            CultureInfo.InvariantCulture)),
                    Csv(
                        gap.TargetStartWatts?
                            .ToString(
                                "0.###",
                                CultureInfo.InvariantCulture)),
                    Csv(
                        gap.TargetEndWatts?
                            .ToString(
                                "0.###",
                                CultureInfo.InvariantCulture)),
                    Csv(gap.DayType),
                    Csv(
                        gap.CalibrationDays.ToString(
                            CultureInfo.InvariantCulture)),
                    Csv(
                        gap.CalibrationFirstDate?
                            .ToString("yyyy-MM-dd")),
                    Csv(
                        gap.CalibrationLastDate?
                            .ToString("yyyy-MM-dd")),
                    Csv(
                        gap.Q05Kwh?
                            .ToString(
                                "0.######",
                                CultureInfo.InvariantCulture)),
                    Csv(
                        gap.Q50Kwh?
                            .ToString(
                                "0.######",
                                CultureInfo.InvariantCulture)),
                    Csv(
                        gap.Q95Kwh?
                            .ToString(
                                "0.######",
                                CultureInfo.InvariantCulture)),
                    Csv(
                        gap.BacktestCases.ToString(
                            CultureInfo.InvariantCulture)),
                    Csv(
                        gap.BacktestCoverage?
                            .ToString(
                                "0.######",
                                CultureInfo.InvariantCulture)),
                    Csv(
                        gap.BacktestP50BiasKwh?
                            .ToString(
                                "0.######",
                                CultureInfo.InvariantCulture)),
                    Csv(
                        gap.BacktestP50MaeKwh?
                            .ToString(
                                "0.######",
                                CultureInfo.InvariantCulture)),
                    Csv(
                        gap.BacktestMeanIntervalScore?
                            .ToString(
                                "0.######",
                                CultureInfo.InvariantCulture))));
        }

        return sb.ToString();
    }

    private static string DailyCoverageCsv(
        UtilityBillGapStatisticalAnalysis analysis)
    {
        var sb =
            new StringBuilder();
        sb.AppendLine(
            "local_date,valid_samples,covered_hours,uncovered_hours,coverage_percent,observed_positive_kwh");

        foreach (var day in analysis.DailyEvidence)
        {
            sb.AppendLine(
                string.Join(
                    ",",
                    Csv(
                        day.LocalDate.ToString(
                            "yyyy-MM-dd")),
                    Csv(
                        day.ValidSamples.ToString(
                            CultureInfo.InvariantCulture)),
                    Csv(
                        day.CoveredHours.ToString(
                            "0.######",
                            CultureInfo.InvariantCulture)),
                    Csv(
                        day.UncoveredHours.ToString(
                            "0.######",
                            CultureInfo.InvariantCulture)),
                    Csv(
                        day.CoveragePercent.ToString(
                            "0.######",
                            CultureInfo.InvariantCulture)),
                    Csv(
                        day.ObservedPositiveKwh.ToString(
                            "0.######",
                            CultureInfo.InvariantCulture))));
        }

        return sb.ToString();
    }

    private static string EconomicCsv(
        UtilityBillTariffScenarioAnalysis analysis,
        UtilityBillRecord bill)
    {
        var sb =
            new StringBuilder();
        sb.AppendLine(
            "scenario,energy_kwh,difference_vs_enel_kwh,difference_vs_enel_pct,supported_variable_rate_clp_per_kwh,supported_fixed_amount_clp,supported_modeled_subtotal_clp,comparable_total_clp,difference_vs_printed_total_clp");

        var printed =
            bill.TotalDueClp ??
            bill.AmountClp;
        var enel =
            analysis.Scenarios.FirstOrDefault(
                item => item.Key == "ENEL_BILLED");
        var preserved =
            printed.HasValue &&
            enel is not null
                ? printed.Value -
                  enel.SupportedTariffSubtotalClp
                : (double?)null;

        foreach (var row in analysis.Scenarios)
        {
            var total =
                preserved.HasValue
                    ? preserved.Value +
                      row.SupportedTariffSubtotalClp
                    : (double?)null;
            var difference =
                total.HasValue &&
                printed.HasValue
                    ? total.Value -
                      printed.Value
                    : (double?)null;

            sb.AppendLine(
                string.Join(
                    ",",
                    Csv(row.Key),
                    Csv(
                        row.EnergyKwh.ToString(
                            "0.######",
                            CultureInfo.InvariantCulture)),
                    Csv(
                        row.DifferenceVsEnelKwh.ToString(
                            "0.######",
                            CultureInfo.InvariantCulture)),
                    Csv(
                        row.DifferenceVsEnelPercent?
                            .ToString(
                                "0.######",
                                CultureInfo.InvariantCulture)),
                    Csv(
                        analysis.SupportedVariableRateClpPerKwh?
                            .ToString(
                                "0.######",
                                CultureInfo.InvariantCulture)),
                    Csv(
                        analysis.SupportedFixedAmountClp?
                            .ToString(
                                "0.######",
                                CultureInfo.InvariantCulture)),
                    Csv(
                        row.SupportedTariffSubtotalClp.ToString(
                            "0.######",
                            CultureInfo.InvariantCulture)),
                    Csv(
                        total?
                            .ToString(
                                "0.######",
                                CultureInfo.InvariantCulture)),
                    Csv(
                        difference?
                            .ToString(
                                "0.######",
                                CultureInfo.InvariantCulture))));
        }

        return sb.ToString();
    }

    private static string ProvenanceText(
        UtilityBillRecord bill,
        UtilityBillGapStatisticalAnalysis analysis,
        UtilityBillTariffScenarioAnalysis tariff,
        int frameCount,
        string languageCode,
        string? producerIdentity)
    {
        var spanish =
            languageCode.StartsWith(
                "es",
                StringComparison.OrdinalIgnoreCase);

        var sb =
            new StringBuilder();
        sb.AppendLine(
            spanish
                ? "ANEXO TÉCNICO — FUENTES Y MÉTODO"
                : "TECHNICAL ANNEX — SOURCES AND METHOD");
        sb.AppendLine(
            new string('=', 45));
        sb.AppendLine(
            $"Export version: {ExportVersion}");
        sb.AppendLine(
            $"Producer: {producerIdentity ?? "-"}");
        sb.AppendLine(
            $"Generated UTC: {DateTimeOffset.UtcNow:O}");
        sb.AppendLine(
            $"Invoice reference: {bill.InvoiceReference ?? "-"}");
        sb.AppendLine(
            $"Interval: {analysis.StartLocalDate:yyyy-MM-dd} through {analysis.EndLocalDateInclusive:yyyy-MM-dd}");
        sb.AppendLine(
            $"Frames: {frameCount}");
        sb.AppendLine(
            $"Observed grid import: {analysis.Completion.ObservedKwh:0.######} kWh");
        sb.AppendLine(
            $"Coverage: {analysis.Completion.CoveragePercent:0.######}%");
        sb.AppendLine(
            $"Missing hours: {analysis.UncoveredHours:0.######}");
        sb.AppendLine(
            $"Method: {analysis.Completion.MethodVersion}");
        sb.AppendLine(
            $"Method status: {analysis.Status}");
        sb.AppendLine(
            $"Enel value used in construction: {analysis.EnelValueUsedInConstruction}");
        sb.AppendLine(
            $"Tariff model status: {tariff.Status}");
        sb.AppendLine(
            $"Supported variable rate: {tariff.SupportedVariableRateClpPerKwh?.ToString("0.######", CultureInfo.InvariantCulture) ?? "-"} CLP/kWh");
        sb.AppendLine(
            $"Supported fixed amount: {tariff.SupportedFixedAmountClp?.ToString("0.######", CultureInfo.InvariantCulture) ?? "-"} CLP");
        foreach (var component in tariff.Components)
        {
            sb.AppendLine(
                $"Modeled component: {component.BillLineDescription} | key={component.ComponentKey} | " +
                $"fixed={component.FixedAmountClp.ToString("0.######", CultureInfo.InvariantCulture)} | " +
                $"variableRate={component.RateClpPerKwh.ToString("0.######", CultureInfo.InvariantCulture)} | " +
                $"basis={component.CalculationBasis ?? component.RateBasis ?? "-"}");
        }
        foreach (var period in tariff.PublicationPeriods)
        {
            sb.AppendLine(
                $"Tariff source: {period.AppliedFrom:yyyy-MM-dd}..{period.AppliedTo:yyyy-MM-dd} | " +
                $"{period.PublicationTitle} | retroactive={period.IsRetroactive} | " +
                $"sha256={period.ContentSha256 ?? "-"} | {period.SourceUrl}");
        }
        sb.AppendLine();
        sb.AppendLine(
            spanish
                ? "Semántica: la telemetría del inversor es evidencia técnica independiente. Los períodos faltantes no se convierten en cero. P5/P50/P95 son percentiles del método provisional y no tolerancias metrológicas."
                : "Semantics: inverter telemetry is independent technical evidence. Missing periods are not converted to zero. P5/P50/P95 are percentiles of the provisional method and not metrological tolerances.");
        return sb.ToString();
    }

    private static void ExportPrintablePdf(
        string path,
        UtilityBillRecord bill,
        UtilityBillGapStatisticalAnalysis analysis,
        UtilityBillTariffScenarioAnalysis tariff,
        IReadOnlyList<TelemetryRow> rows,
        string languageCode,
        string? producerIdentity)
    {
        EnsurePdfFonts();

        var spanish =
            languageCode.StartsWith(
                "es",
                StringComparison.OrdinalIgnoreCase);
        string L(string es, string en) =>
            spanish ? es : en;

        var document =
            new Document();
        document.Info.Title =
            L(
                "Anexo técnico de evidencia numérica",
                "Technical numerical evidence annex");

        var normal =
            document.Styles["Normal"];
        normal.Font.Name = "Arial";
        normal.Font.Size = 7;

        var section =
            document.AddSection();
        section.PageSetup.Orientation =
            MigraDoc.DocumentObjectModel.Orientation.Landscape;
        section.PageSetup.TopMargin =
            Unit.FromCentimeter(0.9);
        section.PageSetup.BottomMargin =
            Unit.FromCentimeter(1.7);
        section.PageSetup.FooterDistance =
            Unit.FromCentimeter(0.35);
        section.PageSetup.LeftMargin =
            Unit.FromCentimeter(0.8);
        section.PageSetup.RightMargin =
            Unit.FromCentimeter(0.8);

        var title =
            section.AddParagraph(
                document.Info.Title);
        title.Format.Font.Size = 15;
        title.Format.Font.Bold = true;
        title.Format.SpaceAfter =
            Unit.FromPoint(5);

        var summary =
            section.AddParagraph(
                string.Format(
                    L(
                        "Período {0:dd-MM-yyyy} → {1:dd-MM-yyyy} · {2:N0} frames · observado {3:N3} kWh · cobertura {4:N3}% · método {5}.",
                        "Period {0:dd-MM-yyyy} → {1:dd-MM-yyyy} · {2:N0} frames · observed {3:N3} kWh · coverage {4:N3}% · method {5}."),
                    analysis.StartLocalDate.ToDateTime(
                        TimeOnly.MinValue),
                    analysis.EndLocalDateInclusive.ToDateTime(
                        TimeOnly.MinValue),
                    rows.Count,
                    analysis.Completion.ObservedKwh,
                    analysis.Completion.CoveragePercent,
                    analysis.Completion.MethodVersion));
        summary.Format.SpaceAfter =
            Unit.FromPoint(6);

        var note =
            section.AddParagraph(
                L(
                    "Las filas siguientes son la serie numérica utilizada para integrar importación desde red. 'GAP_TO_NEXT' marca un enlace que no se integra como observación continua.",
                    "The following rows are the numerical series used to integrate grid import. 'GAP_TO_NEXT' marks a link that is not integrated as continuous observation."));
        note.Format.Font.Color =
            Colors.DimGray;
        note.Format.SpaceAfter =
            Unit.FromPoint(7);

        var table =
            section.AddTable();
        table.Borders.Width = 0.2;
        table.Format.Font.Size = 6.2;
        table.AddColumn(
            Unit.FromCentimeter(1.2));
        table.AddColumn(
            Unit.FromCentimeter(4.2));
        table.AddColumn(
            Unit.FromCentimeter(2.3));
        table.AddColumn(
            Unit.FromCentimeter(2.4));
        table.AddColumn(
            Unit.FromCentimeter(3.0));
        table.AddColumn(
            Unit.FromCentimeter(4.0));
        table.AddColumn(
            Unit.FromCentimeter(2.4));
        table.AddColumn(
            Unit.FromCentimeter(5.0));

        var header =
            table.AddRow();
        header.HeadingFormat = true;
        header.Format.Font.Bold = true;
        header.Shading.Color =
            Colors.LightGray;
        header.Cells[0].AddParagraph("#");
        header.Cells[1].AddParagraph(
            L("Timestamp local", "Local timestamp"));
        header.Cells[2].AddParagraph("Grid W");
        header.Cells[3].AddParagraph(
            L("Δ siguiente", "Δ next"));
        header.Cells[4].AddParagraph(
            L("Energía Wh", "Energy Wh"));
        header.Cells[5].AddParagraph(
            L("Estado enlace", "Link status"));
        header.Cells[6].AddParagraph(
            L("Confianza", "Confidence"));
        header.Cells[7].AddParagraph(
            L("Regla / fuente", "Rule / source"));

        foreach (var row in rows)
        {
            var r =
                table.AddRow();
            r.Cells[0].AddParagraph(
                row.RowNumber.ToString(
                    CultureInfo.InvariantCulture));
            r.Cells[1].AddParagraph(
                row.TimestampLocal.ToString(
                    "yyyy-MM-dd HH:mm:ss.fff"));
            r.Cells[2].AddParagraph(
                row.Watts.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture));
            r.Cells[3].AddParagraph(
                row.DeltaNextMinutes.HasValue
                    ? $"{row.DeltaNextMinutes.Value:0.###} min"
                    : "—");
            r.Cells[4].AddParagraph(
                row.ObservedEnergyWhToNext.HasValue
                    ? row.ObservedEnergyWhToNext.Value
                        .ToString(
                            "0.######",
                            CultureInfo.InvariantCulture)
                    : "—");
            r.Cells[5].AddParagraph(
                row.LinkStatus);
            r.Cells[6].AddParagraph(
                row.Confidence);
            r.Cells[7].AddParagraph(
                $"{row.SourceAttributeKey} · {row.NormalizationRuleVersion}");
        }

        var footer =
            section.Footers.Primary
                .AddParagraph();
        footer.Format.Alignment =
            ParagraphAlignment.Center;
        footer.AddText(
            L(
                "Anexo técnico",
                "Technical annex"));
        if (!string.IsNullOrWhiteSpace(producerIdentity))
        {
            footer.AddText($" · {producerIdentity}");
        }
        footer.AddText(
            L(
                " · página ",
                " · page "));
        footer.AddPageField();
        footer.AddText("/");
        footer.AddNumPagesField();

        var renderer =
            new PdfDocumentRenderer
            {
                Document = document
            };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(path);
    }

    private static void EnsurePdfFonts()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "PDF export is supported by the Windows desktop application.");
        }

        if (Interlocked.Exchange(
                ref _pdfFontsInitialized,
                1) == 0)
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows =
                true;
        }
    }

    private static byte[] Utf8(
        string value) =>
        new UTF8Encoding(false)
            .GetBytes(value);

    private static string Csv(
        string? value)
    {
        value ??= string.Empty;
        return value.IndexOfAny(
                   [',', '"', '\r', '\n']) >= 0
            ? "\"" +
              value.Replace(
                  "\"",
                  "\"\"",
                  StringComparison.Ordinal) +
              "\""
            : value;
    }

    private sealed record RawTelemetry(
        DateTimeOffset TimestampUtc,
        double Watts,
        string Confidence,
        string Quality,
        string SourceAttributeKey,
        string NormalizationRuleVersion);

    private sealed record TelemetryRow(
        int RowNumber,
        DateTimeOffset TimestampUtc,
        DateTimeOffset TimestampLocal,
        double Watts,
        double? DeltaNextMinutes,
        double? ObservedEnergyWhToNext,
        string LinkStatus,
        string Confidence,
        string Quality,
        string SourceAttributeKey,
        string NormalizationRuleVersion);
}
