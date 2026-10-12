using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;
using SolarOfThings.Core.Infrastructure;

namespace SolarOfThings.Core.Diagnostics;

/// <summary>
/// Explicit H-01 diagnostic export. No writes to SQLite, no raw API payloads,
/// no changes to physical-flow attribution or the frozen R3 research protocol.
/// </summary>
public sealed class H01EnergyBalanceExportService
{
    private readonly SqliteDatabase _database;
    private readonly AppPaths _paths;
    private static readonly CultureInfo I = CultureInfo.InvariantCulture;

    public H01EnergyBalanceExportService(SqliteDatabase database, AppPaths paths)
    {
        _database = database;
        _paths = paths;
    }

    public string Export(DateTime firstDay, DateTime lastDay)
    {
        var from = DateOnly.FromDateTime(firstDay);
        var through = DateOnly.FromDateTime(lastDay);
        if (through < from || through.DayNumber - from.DayNumber > 366)
            throw new ArgumentOutOfRangeException(nameof(lastDay),
                "El rango debe ser válido y no superar 367 días civiles.");

        var zone = TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time");
        var start = ToUtc(from, zone);
        var end = ToUtc(through.AddDays(1), zone);

        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = _database.DatabasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private
        };
        using var db = new SqliteConnection(cs.ToString());
        db.Open();

        var device = SelectDevice(db, start, end);
        var frames = LoadFrames(db, device, start, end);
        if (frames.Count < 2)
            throw new InvalidOperationException("No hay muestras suficientes para H-01.");
        AttachContexts(db, device, frames, start, end);

        var gaps = new List<double>();
        for (var i = 0; i < frames.Count - 1; i++)
        {
            var minutes = (frames[i + 1].Utc - frames[i].Utc).TotalMinutes;
            if (minutes > 0) gaps.Add(minutes);
        }
        gaps.Sort();
        var median = gaps.Count == 0 ? 5d :
            gaps.Count % 2 != 0 ? gaps[gaps.Count / 2] :
            (gaps[gaps.Count / 2 - 1] + gaps[gaps.Count / 2]) / 2d;
        var threshold = Math.Clamp(median * 3d, 10d, 20d);

        var days = new SortedDictionary<string, Day>(StringComparer.Ordinal);
        var episodes = new List<Episode>();
        Episode? active = null;
        double hoursTotal = 0, nightHours = 0, nightExcess = 0;
        double suspectHours = 0, suspectGrid = 0, suspectHouse = 0, suspectCharge = 0;
        int staticCount = 0, chargeCount = 0;

        void EndEpisode()
        {
            if (active is not null && active.Hours >= 0.5)
                episodes.Add(active);
            active = null;
        }

        for (var i = 0; i < frames.Count - 1; i++)
        {
            var a = frames[i];
            var b = frames[i + 1];
            var h = (b.Utc - a.Utc).TotalHours;
            if (h <= 0 || h * 60 > threshold || !Complete(a) || !Complete(b))
            {
                EndEpisode();
                continue;
            }
            var pv = Area(Math.Max(0, a.Pv!.Value), Math.Max(0, b.Pv!.Value), h);
            var house = Area(Math.Max(0, a.House!.Value), Math.Max(0, b.House!.Value), h);
            var grid = Area(Math.Max(0, a.Grid!.Value), Math.Max(0, b.Grid!.Value), h);
            var (discharge, charge) = BatteryArea(a.Battery!.Value, b.Battery!.Value, h);
            var residual = pv + grid + discharge - charge - house;
            hoursTotal += h;

            var date = TimeZoneInfo.ConvertTime(a.Utc, zone).ToString("yyyy-MM-dd", I);
            if (!days.TryGetValue(date, out var day))
                days.Add(date, day = new Day(date));
            day.Hours += h;
            day.Pv += pv;
            day.House += house;
            day.Grid += grid;
            day.Discharge += discharge;
            day.Charge += charge;
            day.Residual += residual;
            if (a.Mode == "B") { day.BHours += h; day.BResidual += residual; }
            if (a.Mode == "L") { day.LHours += h; day.LResidual += residual; }

            if (a.Pv < 10 && b.Pv < 10 && a.Grid < 10 && b.Grid < 10 &&
                a.Battery > 100 && b.Battery > 100)
            {
                nightHours += h;
                nightExcess += discharge - house;
            }

            bool suspect = a.Pv < 10 && b.Pv < 10 &&
                a.Grid > 10 && b.Grid > 10 &&
                a.Battery < -30 && b.Battery < -30;
            if (!suspect)
            {
                EndEpisode();
                continue;
            }
            suspectHours += h;
            suspectGrid += grid;
            suspectHouse += house;
            suspectCharge += charge;
            if (a.BatteryStatus == "0") staticCount++;
            if (a.BatteryStatus == "2") chargeCount++;
            if (active is null || active.EndUtc != a.Utc)
            {
                EndEpisode();
                active = new Episode(a.Utc, a.Mode, a.Soc);
            }
            active.EndUtc = b.Utc;
            active.Hours += h;
            active.Grid += grid;
            active.House += house;
            active.Pv += pv;
            active.Charge += charge;
            active.EndSoc = b.Soc;
        }
        EndEpisode();
        if (hoursTotal <= 0)
            throw new InvalidOperationException("No hay intervalos completos continuos en el período.");

        var sum = new Day("TOTAL");
        foreach (var d in days.Values)
        {
            sum.Hours += d.Hours; sum.Pv += d.Pv; sum.House += d.House;
            sum.Grid += d.Grid; sum.Discharge += d.Discharge; sum.Charge += d.Charge;
            sum.Residual += d.Residual; sum.BHours += d.BHours; sum.BResidual += d.BResidual;
            sum.LHours += d.LHours; sum.LResidual += d.LResidual;
        }

        var primitiveCsv = ExportPrimitiveSignals(db, device, start, end, out var primitiveCount);

        var daily = new StringBuilder();
        daily.AppendLine("fecha_local;horas_integradas;solar_kWh;casa_kWh;red_kWh;bateria_descarga_kWh;bateria_carga_kWh;residuo_kWh;horas_modo_B;residuo_modo_B_kWh;horas_modo_L;residuo_modo_L_kWh");
        foreach (var d in days.Values)
            daily.AppendLine(string.Join(";", d.Date, N(d.Hours), N(d.Pv), N(d.House),
                N(d.Grid), N(d.Discharge), N(d.Charge), N(d.Residual),
                N(d.BHours), N(d.BResidual), N(d.LHours), N(d.LResidual)));

        episodes.Sort((a, b) => b.Charge.CompareTo(a.Charge));
        var csv = new StringBuilder();
        csv.AppendLine("inicio;fin;horas;red_kwh;casa_kwh;bateria_carga_kwh;solar_kwh;soc_inicial;soc_final;modo");
        foreach (var e in episodes)
            csv.AppendLine(string.Join(";",
                TimeZoneInfo.ConvertTime(e.StartUtc, zone).ToString("O", I),
                TimeZoneInfo.ConvertTime(e.EndUtc, zone).ToString("O", I),
                N(e.Hours), N(e.Grid), N(e.House), N(e.Charge), N(e.Pv),
                e.StartSoc.HasValue ? N(e.StartSoc.Value) : "",
                e.EndSoc.HasValue ? N(e.EndSoc.Value) : "", e.Mode ?? ""));

        var md = new StringBuilder();
        md.AppendLine("# H-01 · Auditoría exploratoria de balance energético");
        md.AppendLine();
        md.AppendLine($"**Período:** {from:yyyy-MM-dd} a {through:yyyy-MM-dd} inclusive, America/Santiago.");
        md.AppendLine("**Estado:** ABIERTO · exportación manual de solo lectura · sin alterar la base de datos.");
        md.AppendLine("**Datos:** normalized_metric_sample + contextos mode/batteryStatus de history_sample. Equipo dominante por muestras de red. No incluye ID, tokens, PDF, boletas ni respuestas API.");
        md.AppendLine("**Formato CSV:** UTF-8, punto y coma, punto decimal, kWh.");
        md.AppendLine($"**Señales primitivas exportadas:** {primitiveCount} filas de campos expresamente permitidos; CSV sin identificadores del equipo.");
        md.AppendLine();
        md.AppendLine("## Balance aparente de señales");
        md.AppendLine("| Señal | kWh |");
        md.AppendLine("|---|---:|");
        md.AppendLine($"| Solar PV | {N(sum.Pv)} |");
        md.AppendLine($"| Consumo vivienda | {N(sum.House)} |");
        md.AppendLine($"| Importación desde red (inversor) | {N(sum.Grid)} |");
        md.AppendLine($"| Descarga batería (potencia derivada) | {N(sum.Discharge)} |");
        md.AppendLine($"| Carga batería (potencia derivada) | {N(sum.Charge)} |");
        md.AppendLine($"| **Residuo PV + RED + DESCARGA - CARGA - CASA** | **{N(sum.Residual)}** |");
        md.AppendLine();
        md.AppendLine($"Muestras únicas: {frames.Count}; horas integradas: {N(hoursTotal)}; separación mediana: {N(median)} min; umbral de hueco: {N(threshold)} min.");
        md.AppendLine($"Modo batería B: {N(sum.BHours)} h, residuo {N(sum.BResidual)} kWh.");
        md.AppendLine($"Modo red L: {N(sum.LHours)} h, residuo {N(sum.LResidual)} kWh.");
        md.AppendLine();
        md.AppendLine("## Indicadores auxiliares (subconjuntos solapables)");
        md.AppendLine($"- Sin sol ni red (PV < 10 W; red < 10 W; batería > 100 W, ambos extremos): {N(nightHours)} h; batería menos casa: {N(nightExcess)} kWh.");
        md.AppendLine($"- Aparente carga sin sol (PV < 10 W; red > 10 W; batería < -30 W, ambos extremos): {N(suspectHours)} h; importación {N(suspectGrid)} kWh; casa {N(suspectHouse)} kWh; batería cargada {N(suspectCharge)} kWh.");
        md.AppendLine($"- Estado batería al comienzo de estos tramos: 0/estático {staticCount}; 2/cargando {chargeCount} intervalos.");
        md.AppendLine($"- Episodios continuos de 30 min o más: {episodes.Count}.");
        md.AppendLine();
        md.AppendLine("## Límites de interpretación");
        md.AppendLine("- El residuo NO mide pérdidas físicas ni consumo propio. Mezcla señales AC y potencia de batería derivada DC.");
        md.AppendLine("- Los kWh importados proceden de la señal del inversor; no son prueba independiente del medidor Enel.");
        md.AppendLine("- La potencia de batería es derivada; el archivo H01_senales_originales.csv permite contrastar voltaje y corrientes sin extraer la base completa.");
        md.AppendLine("- Se exportan solo los nombres de atributos de la lista permitida y sus valores escalares históricos; no incluir la base ni respuestas API.");
        md.AppendLine("- Se excluyen huecos superiores al umbral y se asigna cada segmento al día local de inicio. La suma diaria puede diferir de buckets Excel que interpolan sus bordes.");
        md.AppendLine("- Contextos B/L y batteryStatus se consideran solo cuando su antigüedad no supera 20 minutos.");
        md.AppendLine("- H-01 sigue abierto. No ajustar atribución de flujos, configuraciones ni el modelo estadístico R3.");

        var folder = Path.Combine(_paths.LogDirectory, "Exports");
        Directory.CreateDirectory(folder);
        var destination = Path.Combine(folder,
            $"H01-balance-{from:yyyyMMdd}-{through:yyyyMMdd}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.zip");
        using (var file = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
        {
            Add(zip, "H01_balance_energetico_por_dia.csv", daily.ToString());
            Add(zip, "H01_episodios_carga_sin_solar.csv", csv.ToString());
            Add(zip, "H01_diagnostico_balance_energetico.md", md.ToString());
            Add(zip, "H01_senales_originales.csv", primitiveCsv);
        }
        return destination;
    }

    private static string ExportPrimitiveSignals(SqliteConnection db, string device,
        DateTimeOffset start, DateTimeOffset end, out int count)
    {
        // Exact allowlist: scalar physical telemetry and operating states only.
        // Never export raw_api_capture, device identifiers, config credentials,
        // boletas, customer metadata or unrestricted history fields.
        var csv = new StringBuilder();
        csv.AppendLine("recorded_at_utc;attribute_key;value_json");
        count = 0;
        using var c = db.CreateCommand();
        c.CommandText = """
            SELECT recorded_at_utc, attribute_key, value_json
            FROM history_sample
            WHERE device_id = $device AND is_missing = 0
              AND recorded_at_utc >= $from AND recorded_at_utc < $to
              AND attribute_key IN (
                'batteryVoltage', 'bmsChargingCurrent', 'batteryDischargeCurrent',
                'bmsCurrentSOC', 'mainsPower', 'outputActivePower',
                'generationPower', 'batteryStatus', 'mode',
                'chargingPriorityOrder', 'acChargingSwitch',
                'chargingMainSwitch', 'mainsCurrentFlowDirection',
                'gridConnectionSign', 'powerSupplyFromPVToLoadInACState'
              )
            ORDER BY recorded_at_utc, attribute_key;
            """;
        c.Parameters.AddWithValue("$device", device);
        c.Parameters.AddWithValue("$from", start.ToString("O", I));
        c.Parameters.AddWithValue("$to", end.ToString("O", I));
        using var reader = c.ExecuteReader();
        while (reader.Read())
        {
            // Scalar-only. JSON arrays/objects and malformed values are excluded.
            var value = reader.IsDBNull(2) ? null : Scalar(reader.GetString(2));
            if (value is null) continue;
            csv.Append(Csv(reader.GetString(0))).Append(';')
                .Append(Csv(reader.GetString(1))).Append(';')
                .Append(Csv(value)).AppendLine();
            count++;
        }
        return csv.ToString();
    }

    private static string Csv(string value) =>
        "\"" + value.Replace("\"", "\"\"")
            .Replace("\r", " ").Replace("\n", " ") + "\"";

    private static string SelectDevice(SqliteConnection db, DateTimeOffset start, DateTimeOffset end)
    {
        using var c = db.CreateCommand();
        c.CommandText = """
            SELECT device_id FROM normalized_metric_sample
            WHERE metric_key = 'grid_import_power_w'
              AND recorded_at_utc >= $from AND recorded_at_utc < $to
            GROUP BY device_id ORDER BY COUNT(*) DESC LIMIT 1;
            """;
        c.Parameters.AddWithValue("$from", start.ToString("O", I));
        c.Parameters.AddWithValue("$to", end.ToString("O", I));
        return c.ExecuteScalar() as string ??
            throw new InvalidOperationException("No hay datos de importación en esas fechas.");
    }

    private static List<Frame> LoadFrames(SqliteConnection db, string device,
        DateTimeOffset start, DateTimeOffset end)
    {
        var timeline = new SortedDictionary<DateTimeOffset, Frame>();
        using var c = db.CreateCommand();
        c.CommandText = """
            SELECT recorded_at_utc, metric_key, normalized_value
            FROM normalized_metric_sample
            WHERE device_id = $device AND normalized_value IS NOT NULL
              AND confidence <> 'UNRESOLVED'
              AND recorded_at_utc >= $from AND recorded_at_utc < $to
              AND metric_key IN ('pv_power_w', 'house_load_power_w',
                'grid_import_power_w', 'battery_power_w', 'battery_soc_pct')
            ORDER BY recorded_at_utc, metric_key;
            """;
        c.Parameters.AddWithValue("$device", device);
        c.Parameters.AddWithValue("$from", start.ToString("O", I));
        c.Parameters.AddWithValue("$to", end.ToString("O", I));
        using var reader = c.ExecuteReader();
        while (reader.Read())
        {
            if (!DateTimeOffset.TryParse(reader.GetString(0), I,
                    DateTimeStyles.RoundtripKind, out var timestamp)) continue;
            var val = Convert.ToDouble(reader.GetValue(2), I);
            if (!double.IsFinite(val)) continue;
            if (!timeline.TryGetValue(timestamp, out var f))
                timeline.Add(timestamp, f = new Frame { Utc = timestamp });
            switch (reader.GetString(1))
            {
                case "pv_power_w": f.Pv = val; break;
                case "house_load_power_w": f.House = val; break;
                case "grid_import_power_w": f.Grid = val; break;
                case "battery_power_w": f.Battery = val; break;
                case "battery_soc_pct": f.Soc = val; break;
            }
        }
        return timeline.Values.ToList();
    }

    private static void AttachContexts(SqliteConnection db, string device,
        List<Frame> frames, DateTimeOffset start, DateTimeOffset end)
    {
        var contexts = new List<(DateTimeOffset Utc, string Key, string Value)>();
        using var c = db.CreateCommand();
        c.CommandText = """
            SELECT recorded_at_utc, attribute_key, value_json
            FROM history_sample WHERE device_id = $device AND is_missing = 0
              AND attribute_key IN ('mode', 'batteryStatus')
              AND recorded_at_utc >= $from AND recorded_at_utc < $to
            ORDER BY recorded_at_utc, attribute_key;
            """;
        c.Parameters.AddWithValue("$device", device);
        c.Parameters.AddWithValue("$from", start.AddDays(-1).ToString("O", I));
        c.Parameters.AddWithValue("$to", end.ToString("O", I));
        using (var reader = c.ExecuteReader())
            while (reader.Read())
            {
                if (!DateTimeOffset.TryParse(reader.GetString(0), I,
                        DateTimeStyles.RoundtripKind, out var time)) continue;
                var value = Scalar(reader.GetString(2));
                if (value is not null)
                    contexts.Add((time, reader.GetString(1), value));
            }

        int ix = 0;
        string? mode = null, status = null;
        DateTimeOffset modeAt = DateTimeOffset.MinValue, statusAt = DateTimeOffset.MinValue;
        foreach (var f in frames)
        {
            while (ix < contexts.Count && contexts[ix].Utc <= f.Utc)
            {
                var q = contexts[ix++];
                if (q.Key == "mode") { mode = q.Value; modeAt = q.Utc; }
                if (q.Key == "batteryStatus") { status = q.Value; statusAt = q.Utc; }
            }
            if (f.Utc - modeAt <= TimeSpan.FromMinutes(20)) f.Mode = mode;
            if (f.Utc - statusAt <= TimeSpan.FromMinutes(20)) f.BatteryStatus = status;
        }
    }

    private static string? Scalar(string json)
    {
        try
        {
            using var d = JsonDocument.Parse(json);
            return d.RootElement.ValueKind switch
            {
                JsonValueKind.String => d.RootElement.GetString(),
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False =>
                    d.RootElement.ToString(),
                _ => null
            };
        }
        catch (JsonException) { return null; }
    }

    private static DateTimeOffset ToUtc(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }
    private static bool Complete(Frame x) =>
        x.Pv.HasValue && x.House.HasValue && x.Grid.HasValue && x.Battery.HasValue;
    private static double Area(double x, double y, double h) => (x + y) * h / 2000;
    private static (double Discharge, double Charge) BatteryArea(double x, double y, double h)
    {
        if (x >= 0 && y >= 0) return (Area(x, y, h), 0);
        if (x <= 0 && y <= 0) return (0, Area(-x, -y, h));
        var fraction = Math.Abs(x) / (Math.Abs(x) + Math.Abs(y));
        var first = fraction * h;
        return x > 0
            ? (Area(x, 0, first), Area(0, -y, h - first))
            : (Area(0, y, h - first), Area(-x, 0, first));
    }
    private static string N(double value) => value.ToString("0.00000000", I);
    private static void Add(ZipArchive zip, string name, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(),
            new UTF8Encoding(false));
        writer.Write(content);
    }
    private sealed class Frame
    {
        public DateTimeOffset Utc;
        public double? Pv, House, Grid, Battery, Soc;
        public string? Mode, BatteryStatus;
    }
    private sealed class Day(string date)
    {
        public string Date { get; } = date;
        public double Hours, Pv, House, Grid, Discharge, Charge, Residual;
        public double BHours, BResidual, LHours, LResidual;
    }
    private sealed class Episode(DateTimeOffset start, string? mode, double? soc)
    {
        public DateTimeOffset StartUtc { get; } = start;
        public DateTimeOffset EndUtc { get; set; } = start;
        public string? Mode { get; } = mode;
        public double? StartSoc { get; } = soc;
        public double? EndSoc { get; set; }
        public double Hours, Pv, House, Grid, Charge;
    }
}
