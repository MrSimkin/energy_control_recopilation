using System.Globalization;
using System.Text.Json;
using SolarOfThings.Core.Data;
using SolarOfThings.Core.Settings;
using SolarOfThings.Core.Statistics;

namespace SolarOfThings.Core.Reporting;

public sealed class SourceAttributionService
{
    public const string RuleVersion = "hpvinv02.source-attribution.v2";

    private const double GridActiveThresholdWatts = 100;
    private const double BatteryActiveThresholdWatts = 100;

    private readonly SqliteDatabase _database;
    private readonly BatteryConfigurationService _batteryConfiguration;

    public SourceAttributionService(
        SqliteDatabase database,
        BatteryConfigurationService batteryConfiguration)
    {
        _database = database;
        _batteryConfiguration = batteryConfiguration;
    }

    public SourceAttributionReport Get(
        string deviceId,
        DateTimeOffset rangeStartUtc,
        DateTimeOffset rangeEndUtc,
        string timeZoneId,
        AggregationPeriod aggregation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (rangeEndUtc < rangeStartUtc)
        {
            (rangeStartUtc, rangeEndUtc) = (rangeEndUtc, rangeStartUtc);
        }

        rangeStartUtc = rangeStartUtc.ToUniversalTime();
        rangeEndUtc = rangeEndUtc.ToUniversalTime();
        var rangeEndExclusive = rangeEndUtc == DateTimeOffset.MaxValue
            ? rangeEndUtc
            : rangeEndUtc.AddTicks(1);

        var frames = LoadFrames(deviceId, rangeStartUtc, rangeEndExclusive, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var modes = LoadModeContexts(deviceId);
        var configuration = LoadHistoricalConfiguration(deviceId);
        cancellationToken.ThrowIfCancellationRequested();
        ApplyAsOfContext(frames, modes, configuration);
        cancellationToken.ThrowIfCancellationRequested();

        var medianGapMinutes = CalculateMedianGapMinutes(frames);
        var continuityThresholdMinutes = medianGapMinutes > 0
            ? Math.Clamp(medianGapMinutes * 3.0, 10.0, 20.0)
            : 15.0;

        var attributionIndex = 0;
        foreach (var frame in frames)
        {
            if ((++attributionIndex & 255) == 0)
                cancellationToken.ThrowIfCancellationRequested();
            frame.Attribution = Attribute(frame);
        }
        cancellationToken.ThrowIfCancellationRequested();

        var capacityKwh = _batteryConfiguration.Get().UsableCapacityKwh;
        var buckets = AggregationBucketPlanner
            .Build(
                aggregation,
                rangeStartUtc,
                rangeEndExclusive,
                timeZoneId)
            .Select(window => new BucketAccumulator(
                window.LocalLabel,
                window.StartUtc,
                window.EndUtcExclusive,
                capacityKwh))
            .ToArray();

        var bucketCursor = 0;

        for (var index = 0; index < frames.Count - 1; index++)
        {
            if ((index & 255) == 0)
                cancellationToken.ThrowIfCancellationRequested();
            var first = frames[index];
            var second = frames[index + 1];
            var gapMinutes = (second.TimestampUtc - first.TimestampUtc).TotalMinutes;

            if (gapMinutes <= 0 ||
                gapMinutes > continuityThresholdMinutes ||
                !first.HouseWatts.HasValue ||
                !second.HouseWatts.HasValue)
            {
                continue;
            }

            var segmentStart = first.TimestampUtc < rangeStartUtc
                ? rangeStartUtc
                : first.TimestampUtc;
            var segmentEnd = second.TimestampUtc > rangeEndExclusive
                ? rangeEndExclusive
                : second.TimestampUtc;

            if (segmentEnd <= segmentStart)
            {
                continue;
            }

            while (bucketCursor < buckets.Length &&
                   buckets[bucketCursor].EndUtcExclusive <= segmentStart)
            {
                bucketCursor++;
            }

            var bucketIndex = bucketCursor;
            while (bucketIndex < buckets.Length &&
                   buckets[bucketIndex].StartUtc < segmentEnd)
            {
                var bucket = buckets[bucketIndex];
                var pieceStart = segmentStart > bucket.StartUtc
                    ? segmentStart
                    : bucket.StartUtc;
                var pieceEnd = segmentEnd < bucket.EndUtcExclusive
                    ? segmentEnd
                    : bucket.EndUtcExclusive;

                if (pieceEnd > pieceStart)
                {
                    var houseStart = Interpolate(
                        first.TimestampUtc,
                        second.TimestampUtc,
                        first.HouseWatts.Value,
                        second.HouseWatts.Value,
                        pieceStart);
                    var houseEnd = Interpolate(
                        first.TimestampUtc,
                        second.TimestampUtc,
                        first.HouseWatts.Value,
                        second.HouseWatts.Value,
                        pieceEnd);

                    bucket.AddObservedSegment(
                        pieceStart,
                        pieceEnd,
                        houseStart,
                        houseEnd);

                    if (first.Attribution?.IsAttributed == true &&
                        second.Attribution?.IsAttributed == true)
                    {
                        bucket.AddAttributedSegment(
                            pieceStart,
                            pieceEnd,
                            InterpolateAttribution(first, second, pieceStart),
                            InterpolateAttribution(first, second, pieceEnd));
                    }
                }

                if (bucket.EndUtcExclusive >= segmentEnd)
                {
                    break;
                }

                bucketIndex++;
            }
        }

        foreach (var frame in frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bucket = buckets.FirstOrDefault(item =>
                frame.TimestampUtc >= item.StartUtc &&
                frame.TimestampUtc < item.EndUtcExclusive);

            bucket?.AddFrame(frame);
        }

        var records = buckets.Select(bucket => bucket.ToRecord()).ToArray();
        var observed = records.Sum(row => row.ObservedHouseKwh);
        var attributed = records.Sum(row => row.AttributedHouseKwh);
        var observedHours = buckets.Sum(bucket => bucket.ObservedHours);
        var totalHours = buckets.Sum(bucket => bucket.TotalHours);

        return new SourceAttributionReport(
            deviceId,
            aggregation,
            rangeStartUtc,
            rangeEndUtc,
            timeZoneId,
            RuleVersion,
            records.Sum(row => row.SolarToHouseKwh),
            records.Sum(row => row.BatteryToHouseKwh),
            records.Sum(row => row.GridToHouseKwh),
            Math.Max(0, observed - attributed),
            observed,
            observed > 0
                ? Math.Clamp(attributed / observed * 100.0, 0, 100)
                : 0,
            totalHours > 0
                ? Math.Clamp(observedHours / totalHours * 100.0, 0, 100)
                : 0,
            modes.Count,
            CountHistoricalConfigurationChanges(configuration),
            records);
    }

    private List<Frame> LoadFrames(
        string deviceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtcExclusive,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT recorded_at_utc, metric_key, normalized_value
            FROM normalized_metric_sample
            WHERE device_id = $deviceId
              AND recorded_at_utc >= $fromUtc
              AND recorded_at_utc < $toUtc
              AND normalized_value IS NOT NULL
              AND confidence <> 'UNRESOLVED'
              AND metric_key IN (
                  'pv_power_w',
                  'house_load_power_w',
                  'grid_import_power_w',
                  'battery_power_w',
                  'battery_soc_pct'
              )
            ORDER BY recorded_at_utc, metric_key;
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);
        command.Parameters.AddWithValue("$fromUtc", fromUtc.ToString("O"));
        command.Parameters.AddWithValue("$toUtc", toUtcExclusive.ToString("O"));

        using var reader = command.ExecuteReader();
        var map = new SortedDictionary<DateTimeOffset, Frame>();
        var scanned = 0;

        while (reader.Read())
        {
            if ((++scanned & 255) == 0)
                cancellationToken.ThrowIfCancellationRequested();
            if (!DateTimeOffset.TryParse(
                    reader.GetString(0),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var timestamp))
            {
                continue;
            }

            timestamp = timestamp.ToUniversalTime();
            if (!map.TryGetValue(timestamp, out var frame))
            {
                frame = new Frame(timestamp);
                map[timestamp] = frame;
            }

            var value = reader.GetDouble(2);
            switch (reader.GetString(1))
            {
                case "pv_power_w":
                    frame.PvWatts = Math.Max(0, value);
                    break;
                case "house_load_power_w":
                    frame.HouseWatts = Math.Max(0, value);
                    break;
                case "grid_import_power_w":
                    frame.GridWatts = value;
                    break;
                case "battery_power_w":
                    frame.BatteryWatts = value;
                    break;
                case "battery_soc_pct":
                    frame.SocPercent = Math.Clamp(value, 0, 100);
                    break;
            }
        }

        return map.Values.ToList();
    }

    private List<ModeContext> LoadModeContexts(string deviceId)
    {
        var byTimestamp =
            new SortedDictionary<DateTimeOffset, Dictionary<string, string?>>();

        var keys = new[]
        {
            "workingMode",
            "chargingPriorityOrder",
            "pvEnergyFeedingPriority",
            "mode",
            "powerSupplyFromPVToLoadInACState",
            "mainsCurrentFlowDirection",
            "acChargingSwitch",
            "solarChargingSwitch",
            "chargingMainSwitch",
            "batteryStatus",
            "gridConnectionSign",
            "mainOutputRelayStatus"
        };

        using (var connection = _database.OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
                SELECT recorded_at_utc, attribute_key, value_json
                FROM history_sample
                WHERE device_id = $deviceId
                  AND is_missing = 0
                  AND attribute_key IN ({string.Join(",", keys.Select((_, index) => $"$key{index}"))})
                ORDER BY recorded_at_utc, attribute_key;
                """;
            command.Parameters.AddWithValue("$deviceId", deviceId);
            for (var index = 0; index < keys.Length; index++)
            {
                command.Parameters.AddWithValue($"$key{index}", keys[index]);
            }

            using var reader = command.ExecuteReader();
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

                timestamp = timestamp.ToUniversalTime();
                if (!byTimestamp.TryGetValue(timestamp, out var values))
                {
                    values = new Dictionary<string, string?>(StringComparer.Ordinal);
                    byTimestamp[timestamp] = values;
                }

                values[reader.GetString(1)] =
                    ReadJsonScalar(reader.GetString(2));
            }
        }

        var result = byTimestamp
            .Select(item => BuildModeContext(item.Key, item.Value))
            .ToList();

        using (var connection = _database.OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT response_json, retrieved_utc
                FROM raw_api_capture
                WHERE device_id = $deviceId
                  AND operation = 'LatestStateSnapshot'
                ORDER BY retrieved_utc;
                """;
            command.Parameters.AddWithValue("$deviceId", deviceId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!DateTimeOffset.TryParse(
                        reader.GetString(1),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out var retrieved))
                {
                    continue;
                }

                try
                {
                    using var document = JsonDocument.Parse(reader.GetString(0));
                    var root = document.RootElement;
                    var timestamp = retrieved.ToUniversalTime();

                    if (root.ValueKind == JsonValueKind.Object &&
                        root.TryGetProperty("time", out var time) &&
                        DateTimeOffset.TryParse(
                            time.ToString(),
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind,
                            out var observed))
                    {
                        timestamp = observed.ToUniversalTime();
                    }

                    if (root.ValueKind != JsonValueKind.Object ||
                        !root.TryGetProperty("fields", out var fields) ||
                        fields.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    result.Add(new ModeContext(
                        timestamp,
                        NormalizeContextValue("workingMode", ReadDisplay(fields, "workingMode")),
                        NormalizeContextValue("chargingPriorityOrder", ReadDisplay(fields, "chargingPriorityOrder")),
                        NormalizeContextValue("pvEnergyFeedingPriority", ReadDisplay(fields, "pvEnergyFeedingPriority")),
                        NormalizeContextValue("mode", ReadDisplay(fields, "mode")),
                        NormalizeContextValue("powerSupplyFromPVToLoadInACState", ReadDisplay(fields, "powerSupplyFromPVToLoadInACState")),
                        NormalizeContextValue("mainsCurrentFlowDirection", ReadDisplay(fields, "mainsCurrentFlowDirection")),
                        NormalizeContextValue("acChargingSwitch", ReadDisplay(fields, "acChargingSwitch")),
                        NormalizeContextValue("solarChargingSwitch", ReadDisplay(fields, "solarChargingSwitch")),
                        NormalizeContextValue("chargingMainSwitch", ReadDisplay(fields, "chargingMainSwitch")),
                        NormalizeContextValue("batteryStatus", ReadDisplay(fields, "batteryStatus")),
                        NormalizeContextValue("gridConnectionSign", ReadDisplay(fields, "gridConnectionSign")),
                        NormalizeContextValue("mainOutputRelayStatus", ReadDisplay(fields, "mainOutputRelayStatus"))));
                }
                catch
                {
                    // Raw evidence that cannot be parsed must not break attribution.
                }
            }
        }

        return result
            .OrderBy(item => item.TimestampUtc)
            .GroupBy(item => item.TimestampUtc)
            .Select(group => group.Last())
            .ToList();
    }

    private static ModeContext BuildModeContext(
        DateTimeOffset timestamp,
        IReadOnlyDictionary<string, string?> values) =>
        new(
            timestamp,
            NormalizeContextValue("workingMode", Value(values, "workingMode")),
            NormalizeContextValue("chargingPriorityOrder", Value(values, "chargingPriorityOrder")),
            NormalizeContextValue("pvEnergyFeedingPriority", Value(values, "pvEnergyFeedingPriority")),
            NormalizeContextValue("mode", Value(values, "mode")),
            NormalizeContextValue("powerSupplyFromPVToLoadInACState", Value(values, "powerSupplyFromPVToLoadInACState")),
            NormalizeContextValue("mainsCurrentFlowDirection", Value(values, "mainsCurrentFlowDirection")),
            NormalizeContextValue("acChargingSwitch", Value(values, "acChargingSwitch")),
            NormalizeContextValue("solarChargingSwitch", Value(values, "solarChargingSwitch")),
            NormalizeContextValue("chargingMainSwitch", Value(values, "chargingMainSwitch")),
            NormalizeContextValue("batteryStatus", Value(values, "batteryStatus")),
            NormalizeContextValue("gridConnectionSign", Value(values, "gridConnectionSign")),
            NormalizeContextValue("mainOutputRelayStatus", Value(values, "mainOutputRelayStatus")));

    private static string? Value(
        IReadOnlyDictionary<string, string?> values,
        string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static string? ReadJsonScalar(string raw)
    {
        try
        {
            using var document = JsonDocument.Parse(raw);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.String => document.RootElement.GetString(),
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False =>
                    document.RootElement.ToString(),
                _ => null
            };
        }
        catch
        {
            return raw.Trim().Trim('"');
        }
    }

    private static string? NormalizeContextValue(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return key switch
        {
            "workingMode" when value == "1" => "SBU",
            "chargingPriorityOrder" when value == "2" => "OSO",
            "pvEnergyFeedingPriority" when value == "0" => "BLU",
            "pvEnergyFeedingPriority" when value == "1" => "LBU",
            "mode" when value == "L" => "Mains Mode",
            "mode" when value == "B" => "Battery Mode",
            "powerSupplyFromPVToLoadInACState" when value == "0" => "No",
            "powerSupplyFromPVToLoadInACState" when value == "1" => "Yes",
            "mainsCurrentFlowDirection" when value == "+" => "Mains To Inverter",
            "batteryStatus" when value == "0" => "Static",
            "batteryStatus" when value == "1" => "Discharge",
            "batteryStatus" when value == "2" => "Charge",
            "acChargingSwitch" when value == "0" => "Close",
            "acChargingSwitch" when value == "1" => "Open",
            "solarChargingSwitch" when value == "0" => "Close",
            "solarChargingSwitch" when value == "1" => "Open",
            "chargingMainSwitch" when value == "0" => "Close",
            "chargingMainSwitch" when value == "1" => "Open",
            "mainOutputRelayStatus" when value == "0" => "Off",
            "mainOutputRelayStatus" when value == "1" => "On",
            _ => value
        };
    }

    private List<ConfigPoint> LoadHistoricalConfiguration(string deviceId)
    {
        var keys = new[]
        {
            "bmsReturnsToMainsModeSOC",
            "bmsReturnsToBatteryModeSOC",
            "bmsLowPowerSOC"
        };

        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT attribute_key, recorded_at_utc, value_json
            FROM history_sample
            WHERE device_id = $deviceId
              AND is_missing = 0
              AND attribute_key IN ({string.Join(",", keys.Select((_, index) => $"$key{index}"))})
            ORDER BY recorded_at_utc, attribute_key;
            """;
        command.Parameters.AddWithValue("$deviceId", deviceId);
        for (var index = 0; index < keys.Length; index++)
        {
            command.Parameters.AddWithValue($"$key{index}", keys[index]);
        }

        using var reader = command.ExecuteReader();
        var result = new List<ConfigPoint>();

        while (reader.Read())
        {
            if (!DateTimeOffset.TryParse(reader.GetString(1), out var timestamp) ||
                !TryParseJsonDouble(reader.GetString(2), out var value))
            {
                continue;
            }

            result.Add(new ConfigPoint(
                timestamp.ToUniversalTime(),
                reader.GetString(0),
                value));
        }

        return result;
    }

    private static void ApplyAsOfContext(
        IReadOnlyList<Frame> frames,
        IReadOnlyList<ModeContext> modes,
        IReadOnlyList<ConfigPoint> configuration)
    {
        var modeIndex = -1;
        var configIndex = -1;
        ModeContext? currentMode = null;
        double? transferSoc = null;
        double? returnSoc = null;
        double? protectedSoc = null;

        foreach (var frame in frames)
        {
            while (modeIndex + 1 < modes.Count &&
                   modes[modeIndex + 1].TimestampUtc <= frame.TimestampUtc)
            {
                modeIndex++;
                currentMode = modes[modeIndex];
            }

            while (configIndex + 1 < configuration.Count &&
                   configuration[configIndex + 1].TimestampUtc <= frame.TimestampUtc)
            {
                configIndex++;
                var item = configuration[configIndex];
                switch (item.Key)
                {
                    case "bmsReturnsToMainsModeSOC":
                        transferSoc = item.Value;
                        break;
                    case "bmsReturnsToBatteryModeSOC":
                        returnSoc = item.Value;
                        break;
                    case "bmsLowPowerSOC":
                        protectedSoc = item.Value;
                        break;
                }
            }

            frame.Mode =
                currentMode is not null &&
                frame.TimestampUtc >= currentMode.TimestampUtc &&
                frame.TimestampUtc - currentMode.TimestampUtc <= TimeSpan.FromMinutes(20)
                    ? currentMode
                    : null;
            frame.TransferToGridSocPercent = transferSoc;
            frame.ReturnToBatterySocPercent = returnSoc;
            frame.ProtectedSocPercent = protectedSoc;
        }
    }

    private static AttributionFrame Attribute(Frame frame)
    {
        if (!frame.HouseWatts.HasValue ||
            !frame.PvWatts.HasValue ||
            !frame.GridWatts.HasValue ||
            !frame.BatteryWatts.HasValue)
        {
            return AttributionFrame.Unresolved("MISSING_CORE_METRIC", frame);
        }

        if (frame.GridWatts.Value < -50)
        {
            return AttributionFrame.Unresolved(
                "NEGATIVE_GRID_SIGN_SEMANTICS_UNRESOLVED",
                frame);
        }

        var house = frame.HouseWatts.Value;
        var pv = frame.PvWatts.Value;
        var grid = Math.Max(0, frame.GridWatts.Value);
        var battery = frame.BatteryWatts.Value;
        var discharge = Math.Max(battery, 0);
        var charge = Math.Max(-battery, 0);
        var scale = Math.Max(
            500,
            new[] { house, pv, grid, Math.Abs(battery) }.Max());
        var tolerance = Math.Max(120, scale * 0.15);
        var balanceResidual = pv + grid + discharge - house - charge;
        var balanceResidualPercent = balanceResidual / scale * 100.0;

        if (house <= 50)
        {
            return AttributionFrame.Resolved(
                0,
                0,
                0,
                balanceResidualPercent,
                "NO_MEANINGFUL_HOUSE_LOAD",
                frame.Mode is not null);
        }

        var explicitSbu =
            string.Equals(frame.Mode?.WorkingMode, "SBU", StringComparison.OrdinalIgnoreCase);
        var explicitOso =
            string.Equals(frame.Mode?.ChargingPriority, "OSO", StringComparison.OrdinalIgnoreCase);
        var explicitLbu =
            string.Equals(frame.Mode?.PvFeedingPriority, "LBU", StringComparison.OrdinalIgnoreCase);
        var explicitMainsMode =
            string.Equals(frame.Mode?.OperatingMode, "Mains Mode", StringComparison.OrdinalIgnoreCase);
        var explicitBatteryMode =
            string.Equals(frame.Mode?.OperatingMode, "Battery Mode", StringComparison.OrdinalIgnoreCase);
        var explicitNoPvToLoadInAc =
            string.Equals(frame.Mode?.PvToLoadInAcState, "No", StringComparison.OrdinalIgnoreCase);

        var gridActive = grid >= GridActiveThresholdWatts;
        var batteryDischarging = discharge >= BatteryActiveThresholdWatts;
        var batteryCharging = charge >= BatteryActiveThresholdWatts;

        if (!gridActive)
        {
            var batteryToHouse = batteryDischarging
                ? Math.Min(discharge, house)
                : 0;
            var solarToHouse = house - batteryToHouse;

            if (solarToHouse < -tolerance ||
                pv + tolerance < Math.Max(0, solarToHouse))
            {
                return AttributionFrame.Unresolved(
                    "ISLAND_SUPPLY_DOES_NOT_CLOSE",
                    frame,
                    balanceResidualPercent);
            }

            solarToHouse = Math.Clamp(solarToHouse, 0, house);
            batteryToHouse = Math.Clamp(house - solarToHouse, 0, house);

            var confidence =
                explicitSbu && explicitLbu && explicitOso
                    ? "EXPLICIT_CONFIG+PHYSICAL"
                    : "PHYSICAL_INFERENCE";

            return AttributionFrame.Resolved(
                solarToHouse,
                batteryToHouse,
                0,
                balanceResidualPercent,
                confidence,
                explicitSbu || explicitLbu || explicitOso);
        }

        if (explicitMainsMode &&
            explicitSbu &&
            explicitNoPvToLoadInAc &&
            !batteryDischarging)
        {
            if (grid + tolerance < house)
            {
                return AttributionFrame.Unresolved(
                    "EXPLICIT_GRID_MODE_BUT_GRID_BELOW_LOAD",
                    frame,
                    balanceResidualPercent);
            }

            return AttributionFrame.Resolved(
                0,
                0,
                house,
                balanceResidualPercent,
                "EXPLICIT_SBU_GRID_MODE",
                true);
        }

        if (!batteryDischarging)
        {
            var gridCanSupplyHouse = grid + tolerance >= house;

            if (gridCanSupplyHouse)
            {
                if (pv < 100)
                {
                    return AttributionFrame.Resolved(
                        0,
                        0,
                        house,
                        balanceResidualPercent,
                        "GRID_ONLY_PHYSICAL",
                        false);
                }

                if (explicitMainsMode &&
                    explicitSbu &&
                    explicitOso &&
                    explicitNoPvToLoadInAc)
                {
                    return AttributionFrame.Resolved(
                        0,
                        0,
                        house,
                        balanceResidualPercent,
                        "EXPLICIT_SBU_GRID_MODE",
                        true);
                }

                return AttributionFrame.Unresolved(
                    "GRID_ACTIVE_PV_PRESENT_ALLOCATION_AMBIGUOUS",
                    frame,
                    balanceResidualPercent);
            }

            var gridToHouse = Math.Clamp(grid, 0, house);
            var solarNeeded = house - gridToHouse;

            if (pv + tolerance >= solarNeeded)
            {
                if (batteryCharging && !explicitOso)
                {
                    return AttributionFrame.Unresolved(
                        "GRID_SOLAR_CHARGE_SOURCE_AMBIGUOUS",
                        frame,
                        balanceResidualPercent);
                }

                if (batteryCharging &&
                    explicitOso &&
                    pv + tolerance < solarNeeded + charge)
                {
                    return AttributionFrame.Unresolved(
                        "OSO_PV_INSUFFICIENT_FOR_HOUSE_AND_CHARGE",
                        frame,
                        balanceResidualPercent);
                }

                return AttributionFrame.Resolved(
                    Math.Clamp(solarNeeded, 0, house),
                    0,
                    gridToHouse,
                    balanceResidualPercent,
                    explicitOso
                        ? "GRID_PLUS_SOLAR_EXPLICIT_OSO"
                        : "GRID_PLUS_SOLAR_PHYSICAL",
                    explicitOso);
            }

            return AttributionFrame.Unresolved(
                "GRID_ACTIVE_SUPPLY_DOES_NOT_CLOSE",
                frame,
                balanceResidualPercent);
        }

        if (explicitBatteryMode && gridActive)
        {
            return AttributionFrame.Unresolved(
                "BATTERY_MODE_WITH_GRID_ACTIVE_REQUIRES_ROUTE_EVIDENCE",
                frame,
                balanceResidualPercent);
        }

        // Transitional/mixed frame: without an explicit contradictory operating
        // state, measured grid and measured battery discharge are used first;
        // PV may cover the remaining load only if sufficient.
        var mixedGrid = Math.Clamp(grid, 0, house);
        var remainingAfterGrid = house - mixedGrid;
        var mixedBattery = Math.Min(discharge, remainingAfterGrid);
        var mixedSolar = remainingAfterGrid - mixedBattery;

        if (pv + tolerance < mixedSolar)
        {
            return AttributionFrame.Unresolved(
                "GRID_BATTERY_MIX_DOES_NOT_CLOSE",
                frame,
                balanceResidualPercent);
        }

        return AttributionFrame.Resolved(
            Math.Clamp(mixedSolar, 0, house),
            Math.Clamp(mixedBattery, 0, house),
            Math.Clamp(mixedGrid, 0, house),
            balanceResidualPercent,
            "MIXED_PHYSICAL_INFERENCE",
            false);
    }

    private static AttributionFrame InterpolateAttribution(
        Frame first,
        Frame second,
        DateTimeOffset instant)
    {
        var a = first.Attribution!;
        var b = second.Attribution!;

        return AttributionFrame.Resolved(
            Interpolate(
                first.TimestampUtc,
                second.TimestampUtc,
                a.SolarToHouseWatts,
                b.SolarToHouseWatts,
                instant),
            Interpolate(
                first.TimestampUtc,
                second.TimestampUtc,
                a.BatteryToHouseWatts,
                b.BatteryToHouseWatts,
                instant),
            Interpolate(
                first.TimestampUtc,
                second.TimestampUtc,
                a.GridToHouseWatts,
                b.GridToHouseWatts,
                instant),
            Interpolate(
                first.TimestampUtc,
                second.TimestampUtc,
                a.BalanceResidualPercent,
                b.BalanceResidualPercent,
                instant),
            "INTERPOLATED",
            a.UsesExplicitMode || b.UsesExplicitMode);
    }

    private static double Interpolate(
        DateTimeOffset firstTime,
        DateTimeOffset secondTime,
        double firstValue,
        double secondValue,
        DateTimeOffset instant)
    {
        if (instant <= firstTime)
        {
            return firstValue;
        }

        if (instant >= secondTime)
        {
            return secondValue;
        }

        var totalTicks = (secondTime - firstTime).Ticks;
        if (totalTicks <= 0)
        {
            return firstValue;
        }

        var fraction = (double)(instant - firstTime).Ticks / totalTicks;
        return firstValue + (secondValue - firstValue) * fraction;
    }

    private static double CalculateMedianGapMinutes(IReadOnlyList<Frame> frames)
    {
        if (frames.Count < 2)
        {
            return 0;
        }

        var gaps = new List<double>(frames.Count - 1);
        for (var index = 0; index < frames.Count - 1; index++)
        {
            var gap =
                (frames[index + 1].TimestampUtc -
                 frames[index].TimestampUtc).TotalMinutes;
            if (gap > 0)
            {
                gaps.Add(gap);
            }
        }

        if (gaps.Count == 0)
        {
            return 0;
        }

        gaps.Sort();
        var middle = gaps.Count / 2;
        return gaps.Count % 2 == 0
            ? (gaps[middle - 1] + gaps[middle]) / 2.0
            : gaps[middle];
    }

    private static int CountHistoricalConfigurationChanges(
        IReadOnlyList<ConfigPoint> points)
    {
        var count = 0;
        foreach (var group in points.GroupBy(item => item.Key, StringComparer.Ordinal))
        {
            double? previous = null;
            foreach (var item in group.OrderBy(item => item.TimestampUtc))
            {
                if (!previous.HasValue)
                {
                    previous = item.Value;
                    continue;
                }

                if (Math.Abs(previous.Value - item.Value) > 0.0001)
                {
                    count++;
                    previous = item.Value;
                }
            }
        }

        return count;
    }

    private static string? ReadDisplay(JsonElement fields, string key)
    {
        if (!fields.TryGetProperty(key, out var field) ||
            field.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (field.TryGetProperty("valueDisplay", out var display) &&
            display.ValueKind == JsonValueKind.String)
        {
            return display.GetString();
        }

        if (field.TryGetProperty("value", out var value))
        {
            return value.ToString();
        }

        return null;
    }

    private static bool TryParseJsonDouble(string raw, out double value)
    {
        value = 0;

        try
        {
            using var document = JsonDocument.Parse(raw);
            var element = document.RootElement;

            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.TryGetDouble(out value);
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                return double.TryParse(
                    element.GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value);
            }
        }
        catch
        {
            return double.TryParse(
                raw.Trim('"'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }

        return false;
    }

    private sealed record ModeContext(
        DateTimeOffset TimestampUtc,
        string? WorkingMode,
        string? ChargingPriority,
        string? PvFeedingPriority,
        string? OperatingMode,
        string? PvToLoadInAcState,
        string? MainsFlowDirection,
        string? AcChargingSwitch,
        string? SolarChargingSwitch,
        string? ChargingMainSwitch,
        string? BatteryStatus,
        string? GridConnectionSign,
        string? MainOutputRelayStatus);

    private sealed record ConfigPoint(
        DateTimeOffset TimestampUtc,
        string Key,
        double Value);

    private sealed class Frame
    {
        public Frame(DateTimeOffset timestampUtc)
        {
            TimestampUtc = timestampUtc;
        }

        public DateTimeOffset TimestampUtc { get; }
        public double? PvWatts { get; set; }
        public double? HouseWatts { get; set; }
        public double? GridWatts { get; set; }
        public double? BatteryWatts { get; set; }
        public double? SocPercent { get; set; }
        public double? TransferToGridSocPercent { get; set; }
        public double? ReturnToBatterySocPercent { get; set; }
        public double? ProtectedSocPercent { get; set; }
        public ModeContext? Mode { get; set; }
        public AttributionFrame? Attribution { get; set; }
    }

    private sealed record AttributionFrame(
        bool IsAttributed,
        double SolarToHouseWatts,
        double BatteryToHouseWatts,
        double GridToHouseWatts,
        double BalanceResidualPercent,
        string Reason,
        bool UsesExplicitMode)
    {
        public static AttributionFrame Resolved(
            double solar,
            double battery,
            double grid,
            double residualPercent,
            string reason,
            bool explicitMode) =>
            new(
                true,
                solar,
                battery,
                grid,
                residualPercent,
                reason,
                explicitMode);

        public static AttributionFrame Unresolved(
            string reason,
            Frame frame,
            double? residualPercent = null)
        {
            var pv = frame.PvWatts ?? 0;
            var house = frame.HouseWatts ?? 0;
            var grid = frame.GridWatts ?? 0;
            var battery = frame.BatteryWatts ?? 0;
            var discharge = Math.Max(battery, 0);
            var charge = Math.Max(-battery, 0);
            var scale = Math.Max(
                500,
                new[] { house, pv, grid, Math.Abs(battery) }.Max());
            var residual =
                residualPercent ??
                ((pv + grid + discharge - house - charge) / scale * 100.0);

            return new(
                false,
                0,
                0,
                0,
                residual,
                reason,
                false);
        }
    }

    private sealed class BucketAccumulator
    {
        private readonly double _capacityKwh;
        private double _solarWh;
        private double _batteryWh;
        private double _gridWh;
        private double _houseWh;
        private double _attributedHouseWh;
        private double _observedHours;
        private double _attributedHours;
        private double _residualAbsSum;
        private double _residualAbsMax;
        private int _residualCount;
        private double? _endingSoc;
        private DateTimeOffset? _endingSocTimestamp;

        public BucketAccumulator(
            string label,
            DateTimeOffset startUtc,
            DateTimeOffset endUtcExclusive,
            double capacityKwh)
        {
            LocalLabel = label;
            StartUtc = startUtc;
            EndUtcExclusive = endUtcExclusive;
            _capacityKwh = capacityKwh;
        }

        public string LocalLabel { get; }
        public DateTimeOffset StartUtc { get; }
        public DateTimeOffset EndUtcExclusive { get; }
        public double TotalHours => Math.Max(
            0,
            (EndUtcExclusive - StartUtc).TotalHours);
        public double ObservedHours => _observedHours;
        public int ObservedFrameCount { get; private set; }
        public int AttributedFrameCount { get; private set; }

        public void AddObservedSegment(
            DateTimeOffset start,
            DateTimeOffset end,
            double houseStart,
            double houseEnd)
        {
            var hours = (end - start).TotalHours;
            if (hours <= 0)
            {
                return;
            }

            _observedHours += hours;
            _houseWh += (Math.Max(0, houseStart) + Math.Max(0, houseEnd)) /
                        2.0 * hours;
        }

        public void AddAttributedSegment(
            DateTimeOffset start,
            DateTimeOffset end,
            AttributionFrame first,
            AttributionFrame second)
        {
            var hours = (end - start).TotalHours;
            if (hours <= 0)
            {
                return;
            }

            _attributedHours += hours;
            _solarWh += (first.SolarToHouseWatts + second.SolarToHouseWatts) /
                        2.0 * hours;
            _batteryWh += (first.BatteryToHouseWatts + second.BatteryToHouseWatts) /
                          2.0 * hours;
            _gridWh += (first.GridToHouseWatts + second.GridToHouseWatts) /
                       2.0 * hours;
            _attributedHouseWh +=
                (first.SolarToHouseWatts +
                 first.BatteryToHouseWatts +
                 first.GridToHouseWatts +
                 second.SolarToHouseWatts +
                 second.BatteryToHouseWatts +
                 second.GridToHouseWatts) /
                2.0 * hours;
        }

        public void AddFrame(Frame frame)
        {
            if (frame.HouseWatts.HasValue)
            {
                ObservedFrameCount++;
            }

            if (frame.Attribution?.IsAttributed == true)
            {
                AttributedFrameCount++;
            }

            if (frame.Attribution is not null)
            {
                var abs = Math.Abs(frame.Attribution.BalanceResidualPercent);
                _residualAbsSum += abs;
                _residualAbsMax = Math.Max(_residualAbsMax, abs);
                _residualCount++;
            }

            if (frame.SocPercent.HasValue &&
                (!_endingSocTimestamp.HasValue ||
                 frame.TimestampUtc >= _endingSocTimestamp.Value))
            {
                _endingSocTimestamp = frame.TimestampUtc;
                _endingSoc = frame.SocPercent.Value;
            }
        }

        public SourceAttributionBucket ToRecord()
        {
            var observedKwh = _houseWh / 1000.0;
            var attributedKwh = _attributedHouseWh / 1000.0;
            var coverage = TotalHours > 0
                ? Math.Clamp(_observedHours / TotalHours * 100.0, 0, 100)
                : 0;
            var attributionCoverage = observedKwh > 0
                ? Math.Clamp(attributedKwh / observedKwh * 100.0, 0, 100)
                : (_observedHours > 0 ? 100 : 0);

            return new SourceAttributionBucket(
                LocalLabel,
                StartUtc,
                EndUtcExclusive,
                _solarWh / 1000.0,
                _batteryWh / 1000.0,
                _gridWh / 1000.0,
                Math.Max(0, observedKwh - attributedKwh),
                observedKwh,
                attributedKwh,
                coverage,
                attributionCoverage,
                _endingSoc.HasValue
                    ? _capacityKwh * _endingSoc.Value / 100.0
                    : null,
                _endingSoc,
                ObservedFrameCount,
                AttributedFrameCount,
                _residualCount > 0
                    ? _residualAbsSum / _residualCount
                    : 0,
                _residualAbsMax);
        }
    }
}
