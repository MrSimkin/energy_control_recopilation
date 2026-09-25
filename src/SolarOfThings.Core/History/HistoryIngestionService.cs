using System.Globalization;
using System.Text.Json;
using SolarOfThings.Core.Commissioning;
using SolarOfThings.Core.Diagnostics;
using SolarOfThings.Core.SolarOfThings;

namespace SolarOfThings.Core.History;

public sealed class HistoryIngestionService
{
    private const int SelectedKeyPageSize = 300;
    private const int SelectedKeyMaxPages = 500;
    private const int RecordListPageSize = 300;
    private const int RecordListMaxPages = 500;

    private readonly SolarOfThingsSessionManager _session;
    private readonly HistoryRepository _history;
    private readonly ApiDiagnosticsStore _diagnostics;

    public HistoryIngestionService(
        SolarOfThingsSessionManager session,
        HistoryRepository history,
        ApiDiagnosticsStore diagnostics)
    {
        _session = session;
        _history = history;
        _diagnostics = diagnostics;
    }

    public async Task<HistorySyncResult> SyncAsync(
        CommissioningProfile profile,
        bool fullBackfill,
        IProgress<HistorySyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var timeZone = string.IsNullOrWhiteSpace(profile.StationTimeZone)
            ? "America/Santiago"
            : profile.StationTimeZone;

        var today = SolarApiTime.GetLocalDate(DateTimeOffset.UtcNow, timeZone);
        var startDate = DetermineStartDate(profile, today, timeZone, fullBackfill);
        if (startDate > today)
        {
            startDate = today;
        }

        var totalDays = today.DayNumber - startDate.DayNumber + 1;
        var syncRunId = _history.StartSyncRun(JsonSerializer.Serialize(new
        {
            mode = fullBackfill ? "full-backfill" : "incremental",
            profile.DeviceId,
            from = startDate.ToString("yyyy-MM-dd"),
            to = today.ToString("yyyy-MM-dd"),
            timeZone
        }));

        var daysAttempted = 0;
        var daysCompleted = 0;
        var frames = 0;
        var samples = 0;
        var pages = 0;
        var source = "selected-key-v1";
        var cancelled = false;

        try
        {
            var keys = ReadAttributeKeys(profile.AttributeCatalogJson);
            if (keys.Count == 0)
            {
                keys =
                [
                    "generationPower",
                    "outputActivePower",
                    "mainsPower",
                    "batteryCapacity",
                    "bmsCurrentSOC",
                    "batteryVoltage",
                    "batteryChargingCurrent",
                    "batteryDischargeCurrent",
                    "pvPower",
                    "pvGeneratedEnergyOfDay",
                    "pvGeneratedEnergyOfTotal"
                ];
            }

            if (fullBackfill && startDate < today)
            {
                var validationDay = today.AddDays(-1);
                progress?.Report(new(
                    "CrossSourceValidation",
                    validationDay,
                    0,
                    totalDays,
                    0,
                    0,
                    "Comparando selected-key y record/list en un día reciente completo..."));

                try
                {
                    var selectedValidation = await FetchSelectedKeyDayAsync(
                        profile.DeviceId,
                        validationDay,
                        timeZone,
                        keys,
                        cancellationToken);

                    var recordValidation = await FetchRecordListDayAsync(
                        profile.DeviceId,
                        validationDay,
                        timeZone,
                        cancellationToken);

                    _diagnostics.RecordLocal(
                        "HistorySync",
                        "CrossSourceValidation",
                        selectedValidation.Complete && recordValidation.Complete
                            ? "SUCCESS"
                            : "PARTIAL",
                        "Compared both raw-history representations on a recent complete local day.",
                        JsonSerializer.Serialize(new
                        {
                            localDate = validationDay.ToString("yyyy-MM-dd"),
                            selectedKey = new
                            {
                                selectedValidation.Complete,
                                selectedValidation.FrameCount,
                                selectedValidation.SamplesUpserted,
                                selectedValidation.Pages,
                                selectedValidation.MedianGapSeconds,
                                selectedValidation.P90GapSeconds,
                                selectedValidation.MaxGapSeconds,
                                selectedValidation.Error
                            },
                            recordList = new
                            {
                                recordValidation.Complete,
                                recordValidation.FrameCount,
                                recordValidation.SamplesUpserted,
                                recordValidation.Pages,
                                recordValidation.MedianGapSeconds,
                                recordValidation.P90GapSeconds,
                                recordValidation.MaxGapSeconds,
                                recordValidation.Error
                            },
                            frameDelta = selectedValidation.FrameCount - recordValidation.FrameCount
                        }));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _diagnostics.RecordLocal(
                        "HistorySync",
                        "CrossSourceValidation",
                        "WARN",
                        $"Cross-source validation could not complete; full backfill will continue. {ex.Message}");
                }
            }

            for (var day = startDate; day <= today; day = day.AddDays(1))
            {
                cancellationToken.ThrowIfCancellationRequested();
                daysAttempted++;

                progress?.Report(new(
                    "Day",
                    day,
                    daysCompleted,
                    totalDays,
                    frames,
                    samples,
                    $"Descargando {day:yyyy-MM-dd}..."));

                var result = await FetchSelectedKeyDayAsync(
                    profile.DeviceId,
                    day,
                    timeZone,
                    keys,
                    cancellationToken);

                if (!result.Complete)
                {
                    progress?.Report(new(
                        "Fallback",
                        day,
                        daysCompleted,
                        totalDays,
                        frames,
                        samples,
                        result.Refused
                            ? "Historial por claves no disponible; usando record/list."
                            : "Historial por claves quedó parcial; completando el día con record/list."));

                    result = await FetchRecordListDayAsync(
                        profile.DeviceId,
                        day,
                        timeZone,
                        cancellationToken);

                    source = "record-list";
                }

                frames += result.FrameCount;
                samples += result.SamplesUpserted;
                pages += result.Pages;

                var dayStatus = !result.Complete
                    ? "PARTIAL"
                    : day == today
                        ? "OPEN"
                        : result.FrameCount == 0
                            ? "EMPTY"
                            : "COMPLETE";

                _history.SaveDayStatus(new HistoryDayStatus(
                    profile.DeviceId,
                    day,
                    timeZone,
                    result.Source,
                    dayStatus,
                    result.FrameCount,
                    result.Pages,
                    result.FirstAtUtc,
                    result.LastAtUtc,
                    JsonSerializer.Serialize(new
                    {
                        result.Error,
                        result.Refused,
                        result.SamplesUpserted,
                        result.MedianGapSeconds,
                        result.P90GapSeconds,
                        result.MaxGapSeconds
                    }),
                    DateTimeOffset.UtcNow));

                if (result.Complete)
                {
                    daysCompleted++;
                }

                progress?.Report(new(
                    "DayComplete",
                    day,
                    daysCompleted,
                    totalDays,
                    frames,
                    samples,
                    result.Complete
                        ? $"{day:yyyy-MM-dd}: {result.FrameCount} frames."
                        : $"{day:yyyy-MM-dd}: parcial — {result.Error ?? "sin detalle"}"));

                if (day < today)
                {
                    await Task.Delay(100, cancellationToken);
                }
            }

            var resultSummary = new HistorySyncResult(
                false,
                daysAttempted,
                daysCompleted,
                frames,
                samples,
                pages,
                startDate,
                today,
                source);

            _history.CompleteSyncRun(
                syncRunId,
                daysCompleted == daysAttempted ? "SUCCESS" : "PARTIAL",
                JsonSerializer.Serialize(resultSummary));

            _diagnostics.RecordLocal(
                "HistorySync",
                "Complete",
                daysCompleted == daysAttempted ? "SUCCESS" : "PARTIAL",
                $"History synchronization completed: {daysCompleted}/{daysAttempted} days.",
                JsonSerializer.Serialize(resultSummary));

            return resultSummary;
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            var resultSummary = new HistorySyncResult(
                true,
                daysAttempted,
                daysCompleted,
                frames,
                samples,
                pages,
                startDate,
                today,
                source);

            _history.CompleteSyncRun(
                syncRunId,
                "CANCELLED",
                JsonSerializer.Serialize(resultSummary));

            return resultSummary;
        }
        catch (Exception ex)
        {
            var failed = new HistorySyncResult(
                cancelled,
                daysAttempted,
                daysCompleted,
                frames,
                samples,
                pages,
                startDate,
                today,
                source);

            _history.CompleteSyncRun(
                syncRunId,
                "FAIL",
                JsonSerializer.Serialize(new
                {
                    result = failed,
                    error = ex.Message
                }));

            _diagnostics.RecordLocal(
                "HistorySync",
                "Failed",
                "FAIL",
                ex.Message);

            throw;
        }
    }

    private DateOnly DetermineStartDate(
        CommissioningProfile profile,
        DateOnly today,
        string timeZone,
        bool fullBackfill)
    {
        if (!fullBackfill)
        {
            var newest = _history.GetNewestTimestamp(profile.DeviceId);
            if (newest.HasValue)
            {
                var local = SolarApiTime.GetLocalDate(newest.Value, timeZone);
                return local.AddDays(-1);
            }
        }

        var installed = ReadInstalledAt(profile.DeviceJson);
        if (installed.HasValue)
        {
            return SolarApiTime.GetLocalDate(installed.Value, timeZone);
        }

        return today.AddDays(-120);
    }

    private async Task<DayFetchResult> FetchSelectedKeyDayAsync(
        string deviceId,
        DateOnly day,
        string timeZone,
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken)
    {
        var (start, end) = SolarApiTime.GetLocalDayWindow(day, timeZone);
        var allTimes = new HashSet<DateTimeOffset>();
        var sampleCount = 0;
        DateTimeOffset? first = null;
        DateTimeOffset? last = null;

        for (var page = 1; page <= SelectedKeyMaxPages; page++)
        {
            var body = new
            {
                deviceId,
                keys,
                fromTime = SolarApiTime.FormatDateTime(start, timeZone),
                toTime = SolarApiTime.FormatDateTime(end, timeZone),
                page,
                count = SelectedKeyPageSize,
                orderByTimeAsc = true
            };

            var response = await _session.PostAsync(
                "HistorySync",
                "SelectedKeyHistory",
                "deviceState/simple/attribute/keys/history/v1",
                body,
                timeZone,
                cancellationToken);

            var retrieved = DateTimeOffset.UtcNow;
            _history.CaptureRaw(
                "selected-key-history",
                deviceId,
                day,
                "selected-key-v1",
                page,
                SolarOfThingsApiClient.SerializeCompact(body),
                response.RawJson,
                retrieved);

            if (!response.IsSuccess)
            {
                return BuildDayFetchResult(
                    false,
                    page == 1,
                    "selected-key-v1",
                    allTimes,
                    sampleCount,
                    page,
                    $"{response.Code ?? response.HttpStatus.ToString()} {response.Message}");
            }

            if (response.Data.ValueKind != JsonValueKind.Object ||
                !response.Data.TryGetProperty("payload", out var payload) ||
                payload.ValueKind != JsonValueKind.Object ||
                !payload.TryGetProperty("timeSeries", out var timeSeries) ||
                timeSeries.ValueKind != JsonValueKind.Array ||
                !payload.TryGetProperty("fields", out var fields) ||
                fields.ValueKind != JsonValueKind.Object)
            {
                return BuildDayFetchResult(
                    false,
                    page == 1,
                    "selected-key-v1",
                    allTimes,
                    sampleCount,
                    page,
                    "Columnar history payload was missing.");
            }

            var times = timeSeries.EnumerateArray().ToArray();
            var samples = new List<HistorySample>(times.Length * keys.Count);

            for (var i = 0; i < times.Length; i++)
            {
                if (times[i].ValueKind != JsonValueKind.String ||
                    !DateTimeOffset.TryParse(
                        times[i].GetString(),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out var timestamp))
                {
                    continue;
                }

                timestamp = timestamp.ToUniversalTime();
                allTimes.Add(timestamp);
                first = !first.HasValue || timestamp < first.Value ? timestamp : first;
                last = !last.HasValue || timestamp > last.Value ? timestamp : last;

                foreach (var key in keys)
                {
                    var missing = true;
                    string? valueJson = null;

                    if (fields.TryGetProperty(key, out var values) &&
                        values.ValueKind == JsonValueKind.Array &&
                        i < values.GetArrayLength())
                    {
                        var value = values[i];
                        missing = value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
                        valueJson = missing ? null : value.GetRawText();
                    }

                    samples.Add(new HistorySample(
                        deviceId,
                        key,
                        timestamp,
                        valueJson,
                        missing,
                        "selected-key-v1",
                        retrieved));
                }
            }

            sampleCount += _history.UpsertSamples(samples);

            var total = ReadInt(response.Data, "total");
            if (times.Length < SelectedKeyPageSize ||
                (total.HasValue && total.Value > 0 && page >= total.Value))
            {
                return BuildDayFetchResult(
                    true,
                    false,
                    "selected-key-v1",
                    allTimes,
                    sampleCount,
                    page,
                    null);
            }
        }

        return BuildDayFetchResult(
            false,
            false,
            "selected-key-v1",
            allTimes,
            sampleCount,
            SelectedKeyMaxPages,
            "Selected-key history reached the pagination safety cap.");
    }

    private async Task<DayFetchResult> FetchRecordListDayAsync(
        string deviceId,
        DateOnly day,
        string timeZone,
        CancellationToken cancellationToken)
    {
        var (start, end) = SolarApiTime.GetLocalDayWindow(day, timeZone);
        var allTimes = new HashSet<DateTimeOffset>();
        var sampleCount = 0;
        DateTimeOffset? first = null;
        DateTimeOffset? last = null;

        for (var page = 1; page <= RecordListMaxPages; page++)
        {
            var body = new
            {
                deviceId,
                fromTime = SolarApiTime.FormatDateTime(start, timeZone),
                toTime = SolarApiTime.FormatDateTime(end, timeZone),
                page,
                count = RecordListPageSize,
                orderByTimeAsc = true
            };

            var response = await _session.PostAsync(
                "HistorySync",
                "RecordListHistory",
                "deviceState/attribute/record/list",
                body,
                timeZone,
                cancellationToken);

            var retrieved = DateTimeOffset.UtcNow;
            _history.CaptureRaw(
                "record-list-history",
                deviceId,
                day,
                "record-list",
                page,
                SolarOfThingsApiClient.SerializeCompact(body),
                response.RawJson,
                retrieved);

            if (!response.IsSuccess)
            {
                return BuildDayFetchResult(
                    false,
                    false,
                    "record-list",
                    allTimes,
                    sampleCount,
                    page,
                    $"{response.Code ?? response.HttpStatus.ToString()} {response.Message}");
            }

            var list = ExtractList(response.Data);
            var samples = new List<HistorySample>();

            foreach (var record in list)
            {
                var timeRaw = ReadString(record, "time", "recordedAt", "createdAt");
                if (string.IsNullOrWhiteSpace(timeRaw) ||
                    !DateTimeOffset.TryParse(
                        timeRaw,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out var timestamp))
                {
                    continue;
                }

                timestamp = timestamp.ToUniversalTime();
                allTimes.Add(timestamp);
                first = !first.HasValue || timestamp < first.Value ? timestamp : first;
                last = !last.HasValue || timestamp > last.Value ? timestamp : last;

                if (!record.TryGetProperty("fields", out var fields) ||
                    fields.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var field in fields.EnumerateObject())
                {
                    var valueJson = field.Value.ValueKind == JsonValueKind.Object &&
                                    field.Value.TryGetProperty("value", out var value)
                        ? value.GetRawText()
                        : field.Value.GetRawText();

                    samples.Add(new HistorySample(
                        deviceId,
                        field.Name,
                        timestamp,
                        valueJson,
                        false,
                        "record-list",
                        retrieved));
                }
            }

            sampleCount += _history.UpsertSamples(samples);

            var total = ReadInt(response.Data, "total");
            if (list.Count < RecordListPageSize ||
                (total.HasValue && total.Value > 0 && page >= total.Value))
            {
                return BuildDayFetchResult(
                    true,
                    false,
                    "record-list",
                    allTimes,
                    sampleCount,
                    page,
                    null);
            }
        }

        return BuildDayFetchResult(
            false,
            false,
            "record-list",
            allTimes,
            sampleCount,
            RecordListMaxPages,
            "Record-list history reached the pagination safety cap.");
    }

    private static List<string> ReadAttributeKeys(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return document.RootElement
                .EnumerateArray()
                .Select(item => ReadString(item, "key"))
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key!)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static DateTimeOffset? ReadInstalledAt(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var raw = ReadString(document.RootElement, "installedAt", "createdAt");
            return DateTimeOffset.TryParse(raw, out var parsed) ? parsed : null;
        }
        catch
        {
            return null;
        }
    }

    private static List<JsonElement> ExtractList(JsonElement data)
    {
        if (data.ValueKind == JsonValueKind.Array)
        {
            return data.EnumerateArray().Select(item => item.Clone()).ToList();
        }

        if (data.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in new[] { "list", "records", "rows", "items" })
            {
                if (data.TryGetProperty(name, out var list) &&
                    list.ValueKind == JsonValueKind.Array)
                {
                    return list.EnumerateArray().Select(item => item.Clone()).ToList();
                }
            }
        }

        return [];
    }

    private static int? ReadInt(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var number))
        {
            return number;
        }

        return int.TryParse(value.ToString(), out number) ? number : null;
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                continue;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null
            };
        }

        return null;
    }

    private static DayFetchResult BuildDayFetchResult(
        bool complete,
        bool refused,
        string source,
        IReadOnlyCollection<DateTimeOffset> timestamps,
        int samplesUpserted,
        int pages,
        string? error)
    {
        var ordered = timestamps
            .OrderBy(value => value)
            .ToArray();

        var gaps = ordered
            .Zip(ordered.Skip(1), (left, right) => (right - left).TotalSeconds)
            .Where(seconds => seconds >= 0)
            .OrderBy(seconds => seconds)
            .ToArray();

        double? median = null;
        double? p90 = null;
        double? max = null;

        if (gaps.Length > 0)
        {
            median = Percentile(gaps, 0.50);
            p90 = Percentile(gaps, 0.90);
            max = gaps[^1];
        }

        return new DayFetchResult(
            complete,
            refused,
            source,
            ordered.Length,
            samplesUpserted,
            pages,
            ordered.Length > 0 ? ordered[0] : null,
            ordered.Length > 0 ? ordered[^1] : null,
            error,
            median,
            p90,
            max);
    }

    private static double Percentile(IReadOnlyList<double> orderedValues, double percentile)
    {
        if (orderedValues.Count == 0)
        {
            return 0;
        }

        var index = Math.Clamp(
            (int)Math.Ceiling(percentile * orderedValues.Count) - 1,
            0,
            orderedValues.Count - 1);

        return orderedValues[index];
    }

    private sealed record DayFetchResult(
        bool Complete,
        bool Refused,
        string Source,
        int FrameCount,
        int SamplesUpserted,
        int Pages,
        DateTimeOffset? FirstAtUtc,
        DateTimeOffset? LastAtUtc,
        string? Error,
        double? MedianGapSeconds,
        double? P90GapSeconds,
        double? MaxGapSeconds);
}
