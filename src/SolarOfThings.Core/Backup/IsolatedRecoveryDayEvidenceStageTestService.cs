using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;

namespace SolarOfThings.Core.Backup;

/// <summary>
/// TEST ONLY: combines independently approved raw-sample staging with source
/// API page witnesses and PARTIAL (never COMPLETE) day statuses in a freshly
/// generated synthetic fixture. No activation or production database access.
/// </summary>
public sealed class IsolatedRecoveryDayEvidenceStageTestService
{
    private readonly IsolatedRecoveryPlanService _planner = new();
    private readonly IsolatedRecoveryRawHistoryStageTestService _raw = new();
    private readonly IsolatedRecoveryPreviewService _preview = new();
    private const int MaxDays = 256;
    private const int MaxPages = 128;
    private const int MaxRawRows = 100_000;
    private const int MaxJsonCharacters = 2_000_000;

    public SyntheticDayEvidenceStageReceipt Stage(
        SyntheticRecoveryPlan plan, string archivePath, string syntheticTarget,
        string deviceId, CancellationToken cancellationToken = default,
        bool simulateInterruptionAfterFirstCapture = false)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(syntheticTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        cancellationToken.ThrowIfCancellationRequested();
        var archive = Path.GetFullPath(archivePath);
        var target = Path.GetFullPath(syntheticTarget);
        var root = IsolatedRecoveryAdditiveTestService.RequireSyntheticFixtureRoot(target);
        var current = _planner.CreatePlan(archive, target);
        if (current.Status != "SYNTHETIC_SETTINGS_PLAN" ||
            plan.PlanId != current.PlanId ||
            plan.SourcePackageSha256 != current.SourcePackageSha256 ||
            plan.TargetSettingsSha256 != current.TargetSettingsSha256 ||
            plan.TargetOtherTablesSha256 != current.TargetOtherTablesSha256 ||
            !plan.Steps.SequenceEqual(current.Steps) ||
            !string.Equals(plan.TargetDatabasePath, target, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(plan.PackagePath, archive, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unapproved or stale synthetic day evidence plan.");

        // Do not accept a caller-supplied stage or a claimed physical identity.
        // Reconstruct the safe raw stage under this service's control.
        var raw = _raw.Stage(plan, archive, target, deviceId, cancellationToken);
        var temp = Path.Combine(root, ".day-evidence-source-" + Guid.NewGuid().ToString("N") + ".db");
        var success = false;
        try
        {
            using (var zip = ZipFile.OpenRead(archive))
            using (var input = (zip.GetEntry("database/energy.db") ??
                       throw new InvalidDataException("Verified source DB missing.")).Open())
            using (var output = new FileStream(temp, FileMode.CreateNew,
                       FileAccess.Write, FileShare.None))
                input.CopyTo(output);
            using var source = Open(temp, false);
            using var staged = Open(raw.StagedDatabasePath, true);
            IsolatedRecoveryTargetHealth.RequireHealthy(source);
            IsolatedRecoveryTargetHealth.RequireHealthy(staged);
            var before = _preview.Preview(archive, raw.StagedDatabasePath);
            if (before.Status != "READ_ONLY_PREVIEW")
                throw new InvalidDataException("Synthetic day evidence pre-preview incomplete.");
            var sourceDays = ReadRows(source, """
                SELECT local_date,timezone,source,frame_count,page_count,
                       first_at_utc,last_at_utc,detail_json,updated_utc,retry_count
                FROM history_day_status WHERE device_id=$device AND status='PARTIAL'
                ORDER BY local_date;
                """, deviceId, MaxDays);
            var addedDays = 0;
            var addedCaptures = 0;
            var skipped = 0;
            var retained = 0;
            using (var tx = staged.BeginTransaction())
            {
                foreach (var day in sourceDays)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var date = day[0] ?? "";
                    var zoneName = day[1] ?? "";
                    var dataSource = day[2] ?? "";
                    if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out var localDate) ||
                        !int.TryParse(day[3], out var frames) ||
                        !int.TryParse(day[4], out var pages) ||
                        frames < 1 || pages < 1 || pages > MaxPages)
                    { skipped++; continue; }

                    var operation = dataSource switch
                    {
                        "selected-key-v1" => "selected-key-history",
                        "record-list" => "record-list-history",
                        _ => null
                    };
                    if (operation is null || zoneName.Length > 128)
                    { skipped++; continue; }
                    if (Scalar(staged, tx, """
                            SELECT COUNT(*) FROM history_day_status
                            WHERE device_id=$device AND local_date=$day;
                            """, deviceId, date) > 0)
                    { retained++; continue; }

                    TimeZoneInfo zone;
                    try { zone = TimeZoneInfo.FindSystemTimeZoneById(zoneName); }
                    catch (TimeZoneNotFoundException) { skipped++; continue; }
                    catch (InvalidTimeZoneException) { skipped++; continue; }
                    using (var commissioning = source.CreateCommand())
                    {
                        commissioning.CommandText = """
                            SELECT station_timezone FROM commissioning_profile
                            WHERE profile_id=1 AND device_id=$device;
                            """;
                        commissioning.Parameters.AddWithValue("$device", deviceId);
                        if (!string.Equals(Convert.ToString(commissioning.ExecuteScalar()),
                                zoneName, StringComparison.Ordinal))
                        { skipped++; continue; }
                    }
                    if (!RawWitnessesAgree(source, staged, tx, deviceId, localDate,
                            zone, dataSource, frames))
                    { skipped++; continue; }

                    var captured = ReadRows(source, """
                        SELECT operation,source,page,request_json,response_json,retrieved_utc
                        FROM raw_api_capture WHERE device_id=$device AND local_date=$day
                        ORDER BY capture_id;
                        """, deviceId, MaxPages, date);
                    if (captured.Count != pages ||
                        captured.Any(c => c[0] != operation || c[1] != dataSource ||
                            !int.TryParse(c[2], out var p) || p < 1 || p > pages ||
                            c[3] is null || c[4] is null ||
                            c[3]!.Length > MaxJsonCharacters ||
                            c[4]!.Length > MaxJsonCharacters ||
                            !PageRequestMatches(c[3]!, deviceId, p)) ||
                        captured.Select(c => c[2]).Distinct(StringComparer.Ordinal).Count() != pages ||
                        !DateTimeOffset.TryParse(day[8], CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind, out var statusAt) ||
                        captured.Any(c =>
                            !DateTimeOffset.TryParse(c[5], CultureInfo.InvariantCulture,
                                DateTimeStyles.RoundtripKind, out var capturedAt) ||
                            capturedAt > statusAt))
                    { skipped++; continue; }

                    var absent = new List<string?[]>();
                    var pageConflict = false;
                    foreach (var c in captured)
                    {
                        using var lookup = staged.CreateCommand();
                        lookup.Transaction = tx;
                        lookup.CommandText = """
                            SELECT request_json,response_json,retrieved_utc
                            FROM raw_api_capture WHERE device_id=$device AND
                            local_date=$day AND operation=$op AND source=$source AND page=$page;
                            """;
                        lookup.Parameters.AddWithValue("$device", deviceId);
                        lookup.Parameters.AddWithValue("$day", date);
                        lookup.Parameters.AddWithValue("$op", c[0]!);
                        lookup.Parameters.AddWithValue("$source", c[1]!);
                        lookup.Parameters.AddWithValue("$page", int.Parse(c[2]!));
                        using var found = lookup.ExecuteReader();
                        if (!found.Read()) absent.Add(c);
                        else if ((found.IsDBNull(0) ? null : found.GetString(0)) != c[3] ||
                                 found.GetString(1) != c[4] || found.GetString(2) != c[5] ||
                                 found.Read())
                        { pageConflict = true; break; }
                    }
                    if (pageConflict) { skipped++; continue; }
                    foreach (var c in absent)
                    {
                        using var insert = staged.CreateCommand();
                        insert.Transaction = tx;
                        insert.CommandText = """
                            INSERT INTO raw_api_capture(operation,device_id,local_date,
                                source,page,request_json,response_json,retrieved_utc)
                            VALUES($op,$device,$day,$source,$page,$request,$response,$retrieved);
                            """;
                        insert.Parameters.AddWithValue("$op", c[0]!);
                        insert.Parameters.AddWithValue("$device", deviceId);
                        insert.Parameters.AddWithValue("$day", date);
                        insert.Parameters.AddWithValue("$source", c[1]!);
                        insert.Parameters.AddWithValue("$page", int.Parse(c[2]!));
                        insert.Parameters.AddWithValue("$request", c[3]!);
                        insert.Parameters.AddWithValue("$response", c[4]!);
                        insert.Parameters.AddWithValue("$retrieved", c[5]!);
                        if (insert.ExecuteNonQuery() != 1)
                            throw new InvalidDataException("Synthetic API capture staging failed.");
                        addedCaptures++;
                        if (simulateInterruptionAfterFirstCapture)
                            throw new InvalidOperationException(
                                "SYNTHETIC_DAY_EVIDENCE_INTERRUPTED_BEFORE_COMMIT");
                    }
                    using var dayInsert = staged.CreateCommand();
                    dayInsert.Transaction = tx;
                    dayInsert.CommandText = """
                        INSERT INTO history_day_status(device_id,local_date,timezone,
                            source,status,frame_count,page_count,first_at_utc,
                            last_at_utc,detail_json,updated_utc,retry_count)
                        VALUES($device,$day,$zone,$source,'PARTIAL',$frames,$pages,
                               $first,$last,$detail,$updated,$retries);
                        """;
                    dayInsert.Parameters.AddWithValue("$device", deviceId);
                    dayInsert.Parameters.AddWithValue("$day", date);
                    dayInsert.Parameters.AddWithValue("$zone", zoneName);
                    dayInsert.Parameters.AddWithValue("$source", dataSource);
                    dayInsert.Parameters.AddWithValue("$frames", frames);
                    dayInsert.Parameters.AddWithValue("$pages", pages);
                    dayInsert.Parameters.AddWithValue("$first", (object?)day[5] ?? DBNull.Value);
                    dayInsert.Parameters.AddWithValue("$last", (object?)day[6] ?? DBNull.Value);
                    dayInsert.Parameters.AddWithValue("$detail", (object?)day[7] ?? DBNull.Value);
                    dayInsert.Parameters.AddWithValue("$updated", day[8]!);
                    dayInsert.Parameters.AddWithValue("$retries", int.Parse(day[9]!));
                    if (dayInsert.ExecuteNonQuery() != 1)
                        throw new InvalidDataException("PARTIAL day insert failed.");
                    addedDays++;
                }
                tx.Commit();
            }
            IsolatedRecoveryTargetHealth.RequireHealthy(staged);
            var after = _preview.Preview(archive, raw.StagedDatabasePath);
            var b = before.Categories.Single(c => c.Category == "HISTORY_DAY_COVERAGE");
            var a = after.Categories.Single(c => c.Category == "HISTORY_DAY_COVERAGE");
            if (after.Status != "READ_ONLY_PREVIEW" || b.UnsupportedReason is not null ||
                a.UnsupportedReason is not null || a.Missing != b.Missing - addedDays ||
                a.Identical != b.Identical + addedDays || a.Conflicts != b.Conflicts ||
                a.TargetOnly != b.TargetOnly)
                throw new InvalidDataException("Partial-day post-stage preview is inconsistent.");
            foreach (var originalCategory in before.Categories
                         .Where(c => c.Category != "HISTORY_DAY_COVERAGE"))
            {
                var check = after.Categories.Single(c => c.Category == originalCategory.Category);
                if (check.Missing != originalCategory.Missing ||
                    check.Identical != originalCategory.Identical ||
                    check.Conflicts != originalCategory.Conflicts ||
                    check.TargetOnly != originalCategory.TargetOnly)
                    throw new InvalidDataException("Day evidence changed unrelated categories.");
            }
            var unchanged = _planner.CreatePlan(archive, target);
            if (unchanged.PlanId != current.PlanId ||
                unchanged.SourcePackageSha256 != current.SourcePackageSha256 ||
                unchanged.TargetSettingsSha256 != current.TargetSettingsSha256 ||
                unchanged.TargetOtherTablesSha256 != current.TargetOtherTablesSha256)
                throw new InvalidDataException("Original synthetic DB or archive was modified.");
            success = true;
            return new SyntheticDayEvidenceStageReceipt(raw.StagedDatabasePath,
                deviceId, raw.Added, addedCaptures, addedDays, retained, skipped,
                "STAGED_SYNTHETIC_PARTIAL_DAY_WITH_CAPTURE_WITNESSES", false,
                "Only synthetic original API pages and witnessed PARTIAL days are appended. " +
                "COMPLETE, normalized metrics and owner data remain untouched.");
        }
        finally
        {
            TryDelete(temp);
            if (!success)
            {
                TryDelete(raw.StagedDatabasePath);
                TryDelete(raw.StagedDatabasePath + "-wal");
                TryDelete(raw.StagedDatabasePath + "-shm");
            }
        }
    }

    private static bool RawWitnessesAgree(SqliteConnection source,
        SqliteConnection staged, SqliteTransaction tx, string deviceId,
        DateOnly day, TimeZoneInfo zone, string sourceName, int frameCount)
    {
        using var cmd = source.CreateCommand();
        cmd.CommandText = """
            SELECT attribute_key,recorded_at_utc,value_json,is_missing,source,
                   retrieved_utc FROM history_sample WHERE device_id=$device;
            """;
        cmd.Parameters.AddWithValue("$device", deviceId);
        using var rows = cmd.ExecuteReader();
        var timestamps = new HashSet<string>(StringComparer.Ordinal);
        var seen = 0;
        while (rows.Read())
        {
            if (++seen > MaxRawRows)
                throw new InvalidDataException("Bounded raw-day inspection exceeded.");
            if (!DateTimeOffset.TryParse(rows.GetString(1),
                    CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                    out var timestamp))
                return false;
            if (DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timestamp, zone).Date) != day)
                continue;
            if (rows.GetString(4) != sourceName) return false;
            timestamps.Add(rows.GetString(1));
            using var lookup = staged.CreateCommand();
            lookup.Transaction = tx;
            lookup.CommandText = """
                SELECT value_json,is_missing,source,retrieved_utc
                FROM history_sample WHERE device_id=$device AND
                attribute_key=$attribute AND recorded_at_utc=$at;
                """;
            lookup.Parameters.AddWithValue("$device", deviceId);
            lookup.Parameters.AddWithValue("$attribute", rows.GetString(0));
            lookup.Parameters.AddWithValue("$at", rows.GetString(1));
            using var match = lookup.ExecuteReader();
            if (!match.Read() ||
                (rows.IsDBNull(2) ? null : rows.GetString(2)) !=
                    (match.IsDBNull(0) ? null : match.GetString(0)) ||
                rows.GetInt64(3) != match.GetInt64(1) ||
                rows.GetString(4) != match.GetString(2) ||
                rows.GetString(5) != match.GetString(3))
                return false;
        }
        return timestamps.Count == frameCount;
    }

    private static List<string?[]> ReadRows(SqliteConnection db, string sql,
        string device, int limit, string? date = null)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$device", device);
        if (date is not null) cmd.Parameters.AddWithValue("$day", date);
        using var reader = cmd.ExecuteReader();
        var results = new List<string?[]>();
        while (reader.Read())
        {
            if (results.Count >= limit)
                throw new InvalidDataException("Synthetic day/page evidence limit exceeded.");
            var fields = new string?[reader.FieldCount];
            for (var i = 0; i < fields.Length; i++)
                fields[i] = reader.IsDBNull(i) ? null :
                    Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture);
            results.Add(fields);
        }
        return results;
    }

    private static long Scalar(SqliteConnection db, SqliteTransaction tx,
        string sql, string device, string date)
    {
        using var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$device", device);
        cmd.Parameters.AddWithValue("$day", date);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static bool PageRequestMatches(string json, string device, int page)
    {
        try
        {
            using var parsed = JsonDocument.Parse(json);
            var root = parsed.RootElement;
            return root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("deviceId", out var id) &&
                id.ValueKind == JsonValueKind.String && id.GetString() == device &&
                root.TryGetProperty("page", out var p) &&
                p.ValueKind == JsonValueKind.Number &&
                p.TryGetInt32(out var number) && number == page;
        }
        catch (JsonException) { return false; }
    }

    private static SqliteConnection Open(string path, bool writable)
    {
        var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Pooling = false,
            Mode = writable ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadOnly
        }.ToString());
        conn.Open();
        if (!writable)
        {
            using var pragma = conn.CreateCommand();
            pragma.CommandText = "PRAGMA query_only=ON;";
            pragma.ExecuteNonQuery();
        }
        return conn;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

public sealed record SyntheticDayEvidenceStageReceipt(
    string StagedDatabasePath, string DeviceId, int AddedRawSamples,
    int AddedCaptures, int AddedPartialDays, int ExistingDays,
    int SkippedUnverifiedDays, string Status, bool RealRestoreAuthorized,
    string SafetyExplanation);
