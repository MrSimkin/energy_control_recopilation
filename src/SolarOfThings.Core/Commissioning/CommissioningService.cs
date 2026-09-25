using System.Text.Json;
using SolarOfThings.Core.Diagnostics;
using SolarOfThings.Core.SolarOfThings;

namespace SolarOfThings.Core.Commissioning;

public sealed class CommissioningService
{
    private readonly SolarOfThingsSessionManager _session;
    private readonly CommissioningProfileRepository _profiles;
    private readonly ApiDiagnosticsStore _diagnostics;

    public CommissioningService(
        SolarOfThingsSessionManager session,
        CommissioningProfileRepository profiles,
        ApiDiagnosticsStore diagnostics)
    {
        _session = session;
        _profiles = profiles;
        _diagnostics = diagnostics;
    }

    public async Task<DiscoveryResult> DiscoverStationsAsync(
        string timeZone,
        IProgress<CommissioningProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new("StationDiscovery", "RUNNING", "Consultando estaciones accesibles..."));

        var stations = new List<DiscoveryItem>();
        var page = 1;
        const int count = 50;

        while (true)
        {
            var response = await _session.PostAsync(
                "Commissioning",
                "StationList",
                "station/list",
                new { page, count },
                timeZone,
                cancellationToken);

            SolarOfThingsApiClient.EnsureSuccess(response, "Station discovery failed.");

            var batch = ExtractList(response.Data);
            foreach (var item in batch)
            {
                var id = ExtractId(item, "id", "stationId");
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                stations.Add(new DiscoveryItem(
                    id,
                    ExtractDisplayName(item, "name", "stationName", id),
                    item.GetRawText()));
            }

            var total = ExtractInt(response.Data, "total");
            if (batch.Count == 0 ||
                batch.Count < count ||
                (total.HasValue && stations.Count >= total.Value))
            {
                break;
            }

            page++;
            if (page > 50)
            {
                throw new SolarApiException(
                    "Station pagination exceeded the commissioning safety limit.");
            }
        }

        progress?.Report(new(
            "StationDiscovery",
            "PASS",
            $"Estaciones encontradas: {stations.Count}."));

        _diagnostics.RecordLocal(
            "Commissioning",
            "StationDiscoverySummary",
            stations.Count > 0 ? "SUCCESS" : "EMPTY",
            $"Accessible stations: {stations.Count}.",
            JsonSerializer.Serialize(new
            {
                count = stations.Count,
                stations = stations.Select(station => new
                {
                    station.Id,
                    station.DisplayName
                })
            }));

        return new DiscoveryResult(stations);
    }

    public async Task<DeviceDiscoveryResult> DiscoverDevicesAsync(
        DiscoveryItem station,
        string timeZone,
        IProgress<CommissioningProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new(
            "DeviceDiscovery",
            "RUNNING",
            $"Consultando dispositivos de {station.DisplayName}..."));

        var devices = new List<DiscoveryItem>();
        var page = 1;
        const int count = 50;

        while (true)
        {
            var response = await _session.PostAsync(
                "Commissioning",
                "DeviceList",
                "device/list",
                new
                {
                    page,
                    count,
                    stationId = station.Id
                },
                timeZone,
                cancellationToken);

            SolarOfThingsApiClient.EnsureSuccess(response, "Device discovery failed.");

            var batch = ExtractList(response.Data);
            foreach (var item in batch)
            {
                var id = ExtractId(item, "id", "deviceId");
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var serial = ExtractString(item, "serialNumber", "deviceSerialNumber");
                var name = ExtractDisplayName(item, "name", "deviceName", serial ?? id);

                devices.Add(new DiscoveryItem(id, name, item.GetRawText()));
            }

            var total = ExtractInt(response.Data, "total");
            if (batch.Count == 0 ||
                batch.Count < count ||
                (total.HasValue && devices.Count >= total.Value))
            {
                break;
            }

            page++;
            if (page > 50)
            {
                throw new SolarApiException(
                    "Device pagination exceeded the commissioning safety limit.");
            }
        }

        progress?.Report(new(
            "DeviceDiscovery",
            "PASS",
            $"Dispositivos encontrados: {devices.Count}."));

        _diagnostics.RecordLocal(
            "Commissioning",
            "DeviceDiscoverySummary",
            devices.Count > 0 ? "SUCCESS" : "EMPTY",
            $"Devices under selected station: {devices.Count}.",
            JsonSerializer.Serialize(new
            {
                station = new { station.Id, station.DisplayName },
                devices = devices.Select(device => new
                {
                    device.Id,
                    device.DisplayName
                })
            }));

        return new DeviceDiscoveryResult(station, devices);
    }

    public async Task<CommissioningProfile> CommissionAsync(
        DiscoveryItem station,
        DiscoveryItem device,
        string requestedTimeZone,
        IProgress<CommissioningProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var capabilities = new Dictionary<string, object?>(StringComparer.Ordinal);

        progress?.Report(new("StationDetails", "RUNNING", "Leyendo detalle de estación..."));
        var stationDetails = await _session.GetAsync(
            "Commissioning",
            "StationDetails",
            $"station/details?stationId={Uri.EscapeDataString(station.Id)}",
            requestedTimeZone,
            cancellationToken);
        JsonElement stationData;
        if (stationDetails.IsSuccess && stationDetails.Data.ValueKind == JsonValueKind.Object)
        {
            stationData = stationDetails.Data;
            progress?.Report(new("StationDetails", "PASS", "Detalle de estación disponible."));
        }
        else
        {
            stationData = ParseRawItem(station.RawJson);
            progress?.Report(new(
                "StationDetails",
                "WARN",
                $"Detalle de estación no disponible ({stationDetails.Code ?? stationDetails.HttpStatus.ToString()}); se continúa con el payload de station/list."));
        }

        var stationTimeZone = ExtractString(
            stationData,
            "timeZone",
            "timezone",
            "timeZoneId",
            "zoneId") ?? requestedTimeZone;

        progress?.Report(new("DeviceDetails", "RUNNING", "Leyendo identidad completa del dispositivo..."));
        var deviceDetails = await _session.GetAsync(
            "Commissioning",
            "DeviceDetails",
            $"device/details?deviceId={Uri.EscapeDataString(device.Id)}",
            stationTimeZone,
            cancellationToken);
        JsonElement deviceData;
        if (deviceDetails.IsSuccess && deviceDetails.Data.ValueKind == JsonValueKind.Object)
        {
            deviceData = deviceDetails.Data;
            progress?.Report(new("DeviceDetails", "PASS", "Identidad de dispositivo disponible."));
        }
        else
        {
            deviceData = ParseRawItem(device.RawJson);
            progress?.Report(new(
                "DeviceDetails",
                "WARN",
                $"Detalle de dispositivo no disponible ({deviceDetails.Code ?? deviceDetails.HttpStatus.ToString()}); se continúa con el payload de device/list."));
        }

        progress?.Report(new("GatherAttributes", "RUNNING", "Descubriendo catálogo real de atributos..."));
        var attributesResponse = await _session.GetAsync(
            "Commissioning",
            "GatherAttributes",
            $"deviceState/simple/gatherAttributes/v1?deviceId={Uri.EscapeDataString(device.Id)}&category=1&renderIn=2",
            stationTimeZone,
            cancellationToken);

        string gatherStatus;
        var attributeCount = 0;
        var attributeCatalogJson = "[]";
        var attributeKeys = new List<string>();

        if (attributesResponse.IsSuccess)
        {
            var attributes = attributesResponse.Data.ValueKind == JsonValueKind.Array
                ? attributesResponse.Data
                : default;

            if (attributes.ValueKind == JsonValueKind.Array)
            {
                attributeCount = attributes.GetArrayLength();
                attributeCatalogJson = attributes.GetRawText();

                foreach (var attribute in attributes.EnumerateArray())
                {
                    var key = ExtractString(attribute, "key");
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        attributeKeys.Add(key);
                    }
                }
            }

            gatherStatus = attributeCount > 0 ? "SUPPORTED" : "EMPTY";
        }
        else
        {
            gatherStatus = $"ERROR:{attributesResponse.Code ?? attributesResponse.HttpStatus.ToString()}";
        }

        progress?.Report(new(
            "GatherAttributes",
            gatherStatus == "SUPPORTED" ? "PASS" : "WARN",
            $"Atributos descubiertos: {attributeCount}."));

        string? selectedDataSource = null;
        var latestStatus = "UNAVAILABLE";

        progress?.Report(new("LatestState", "RUNNING", "Validando dataSource para estado actual..."));

        foreach (var dataSource in new[] { "1", "2" })
        {
            var latest = await _session.GetAsync(
                "Commissioning",
                $"LatestStateDataSource{dataSource}",
                $"deviceState/simple/state/latest/v1?deviceId={Uri.EscapeDataString(device.Id)}&dataSource={dataSource}",
                stationTimeZone,
                cancellationToken);

            capabilities[$"latestState.dataSource.{dataSource}"] = new
            {
                latest.HttpStatus,
                latest.Code,
                latest.Message,
                success = latest.IsSuccess
            };

            if (latest.IsSuccess && !IsEffectivelyEmpty(latest.Data))
            {
                selectedDataSource = dataSource;
                latestStatus = "SUPPORTED";
                break;
            }

            latestStatus = $"ERROR:{latest.Code ?? latest.HttpStatus.ToString()}";
        }

        progress?.Report(new(
            "LatestState",
            selectedDataSource is not null ? "PASS" : "WARN",
            selectedDataSource is not null
                ? $"dataSource validado: {selectedDataSource}."
                : "No se validó dataSource 1 o 2; revisar diagnóstico."));

        string energyFlowStatus;
        if (selectedDataSource is not null)
        {
            progress?.Report(new("EnergyFlow", "RUNNING", "Probando flujo de energía en modo lectura..."));
            var energyFlow = await _session.GetAsync(
                "Commissioning",
                "EnergyFlow",
                $"deviceState/simple/energy/flow/v1?deviceId={Uri.EscapeDataString(device.Id)}&dataSource={selectedDataSource}",
                stationTimeZone,
                cancellationToken);

            energyFlowStatus = energyFlow.IsSuccess
                ? "SUPPORTED"
                : energyFlow.Code == "70132"
                    ? "UNSUPPORTED:70132"
                    : $"ERROR:{energyFlow.Code ?? energyFlow.HttpStatus.ToString()}";

            progress?.Report(new(
                "EnergyFlow",
                energyFlow.IsSuccess || energyFlow.Code == "70132" ? "PASS" : "WARN",
                energyFlow.Code == "70132"
                    ? "El servidor informa que no existe regla de flujo; se registra como capacidad no disponible."
                    : $"Resultado flujo de energía: {energyFlowStatus}."));
        }
        else
        {
            energyFlowStatus = "NOT_TESTED_NO_DATASOURCE";
        }

        string historyStatus;
        if (attributeKeys.Count > 0)
        {
            progress?.Report(new("History", "RUNNING", "Probando historial reciente con claves descubiertas..."));

            var now = DateTimeOffset.UtcNow;
            var from = now.AddHours(-2);

            var history = await _session.PostAsync(
                "Commissioning",
                "HistoryCapability",
                "deviceState/simple/attribute/keys/history/v1",
                new
                {
                    deviceId = device.Id,
                    keys = attributeKeys.Take(5).ToArray(),
                    fromTime = SolarApiTime.FormatDateTime(from, stationTimeZone),
                    toTime = SolarApiTime.FormatDateTime(now, stationTimeZone),
                    page = 1,
                    count = 100,
                    orderByTimeAsc = true
                },
                stationTimeZone,
                cancellationToken);

            historyStatus = history.IsSuccess
                ? "SUPPORTED"
                : $"ERROR:{history.Code ?? history.HttpStatus.ToString()}";

            progress?.Report(new(
                "History",
                history.IsSuccess ? "PASS" : "WARN",
                $"Resultado historial: {historyStatus}."));
        }
        else
        {
            historyStatus = "NOT_TESTED_NO_ATTRIBUTE_KEYS";
        }

        progress?.Report(new("Aggregate", "RUNNING", "Probando agregado diario de solo lectura..."));
        var aggregate = await _session.PostAsync(
            "Commissioning",
            "DailyAggregate",
            $"deviceOverView/generatedEnergy/daily?deviceId={Uri.EscapeDataString(device.Id)}",
            new { time = SolarApiTime.FormatDate(DateTimeOffset.UtcNow, stationTimeZone) },
            stationTimeZone,
            cancellationToken);

        var aggregateStatus = aggregate.IsSuccess
            ? "SUPPORTED"
            : $"ERROR:{aggregate.Code ?? aggregate.HttpStatus.ToString()}";

        progress?.Report(new(
            "Aggregate",
            aggregate.IsSuccess ? "PASS" : "WARN",
            $"Resultado agregado: {aggregateStatus}."));

        progress?.Report(new("Alarms", "RUNNING", "Probando consulta de alarmas en modo lectura..."));

        var alarmTo = DateTimeOffset.UtcNow;
        var alarmFrom = alarmTo.AddDays(-7);
        var deviceSerialNumber = ExtractString(
            deviceData,
            "serialNumber",
            "deviceSerialNumber");
        var certificateDtuId = ExtractString(
            deviceData,
            "certificateDtuID",
            "dtuDtuid",
            "dtuId",
            "dtuID");

        var alarms = await _session.PostAsync(
            "Commissioning",
            "AlarmCapability",
            "alarm/query/list",
            new
            {
                certificateDtuID = certificateDtuId,
                deviceSerialNumber,
                fromTime = SolarApiTime.FormatDateTime(alarmFrom, stationTimeZone),
                toTime = SolarApiTime.FormatDateTime(alarmTo, stationTimeZone),
                orderByCreatedTimeDesc = true,
                page = 1,
                count = 20
            },
            stationTimeZone,
            cancellationToken);

        var alarmStatus = alarms.IsSuccess
            ? "SUPPORTED"
            : $"ERROR:{alarms.Code ?? alarms.HttpStatus.ToString()}";

        progress?.Report(new(
            "Alarms",
            alarms.IsSuccess ? "PASS" : "WARN",
            $"Resultado alarmas: {alarmStatus}."));

        capabilities["stationDetails"] = stationDetails.IsSuccess;
        capabilities["deviceDetails"] = deviceDetails.IsSuccess;
        capabilities["gatherAttributes"] = gatherStatus;
        capabilities["attributeCount"] = attributeCount;
        capabilities["selectedDataSource"] = selectedDataSource;
        capabilities["latestState"] = latestStatus;
        capabilities["energyFlow"] = energyFlowStatus;
        capabilities["history"] = historyStatus;
        capabilities["aggregate"] = aggregateStatus;
        capabilities["alarms"] = alarmStatus;

        var profile = new CommissioningProfile(
            station.Id,
            ExtractString(stationData, "name", "stationName") ?? station.DisplayName,
            stationTimeZone,
            device.Id,
            ExtractString(deviceData, "name", "deviceName") ?? device.DisplayName,
            ExtractString(deviceData, "serialNumber", "deviceSerialNumber"),
            ExtractString(deviceData, "model", "deviceModel", "modelName"),
            ExtractString(deviceData, "manufacturer", "manufacturerName", "deviceManufacturerName"),
            ExtractString(deviceData, "dtuId", "dtuID", "dtuDtuid", "certificateDtuID"),
            ExtractString(deviceData, "gatherProtocolNumber", "protocolNo", "protocolNumber"),
            ExtractString(deviceData, "softwareVersion", "version", "firmwareVersion"),
            ExtractString(deviceData, "deviceSortKey"),
            ExtractString(deviceData, "deviceTypeNumber"),
            ExtractDecimal(deviceData, "ratedPower"),
            ExtractBool(deviceData, "isOnline"),
            ExtractDateTimeOffset(deviceData, "lastDataAt"),
            selectedDataSource,
            gatherStatus,
            attributeCount,
            latestStatus,
            energyFlowStatus,
            historyStatus,
            aggregateStatus,
            alarmStatus,
            JsonSerializer.Serialize(capabilities),
            attributeCatalogJson,
            stationData.GetRawText(),
            deviceData.GetRawText(),
            DateTimeOffset.UtcNow);

        _profiles.Save(profile);

        progress?.Report(new(
            "PersistProfile",
            "PASS",
            "Perfil de capacidades guardado localmente en SQLite."));

        _diagnostics.RecordLocal(
            "Commissioning",
            "CommissioningComplete",
            "SUCCESS",
            "Read-only commissioning completed and profile persisted.",
            JsonSerializer.Serialize(new
            {
                profile.StationId,
                profile.StationName,
                profile.StationTimeZone,
                profile.DeviceId,
                profile.DeviceName,
                profile.SerialNumber,
                profile.Model,
                profile.Manufacturer,
                profile.DtuId,
                profile.GatherProtocolNumber,
                profile.SoftwareVersion,
                profile.DeviceSortKey,
                profile.DeviceTypeNumber,
                profile.RatedPower,
                profile.IsOnline,
                profile.LastDataAt,
                profile.DataSource,
                profile.GatherAttributesStatus,
                profile.GatherAttributeCount,
                profile.LatestStateStatus,
                profile.EnergyFlowStatus,
                profile.HistoryStatus,
                profile.AggregateStatus,
                profile.AlarmStatus
            }));

        return profile;
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

    private static int? ExtractInt(JsonElement data, string name)
    {
        if (data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty(name, out var value))
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

    private static string? ExtractId(JsonElement element, params string[] names) =>
        ExtractString(element, names);

    private static string? ExtractString(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var property))
            {
                continue;
            }

            return property.ValueKind switch
            {
                JsonValueKind.String => property.GetString(),
                JsonValueKind.Number => property.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }

        return null;
    }

    private static string ExtractDisplayName(
        JsonElement element,
        string primary,
        string secondary,
        string fallback)
    {
        return ExtractString(element, primary, secondary) ?? fallback;
    }

    private static decimal? ExtractDecimal(JsonElement element, params string[] names)
    {
        var value = ExtractString(element, names);
        return decimal.TryParse(
            value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }

    private static bool? ExtractBool(JsonElement element, params string[] names)
    {
        var value = ExtractString(element, names);
        return bool.TryParse(value, out var parsed) ? parsed : null;
    }

    private static DateTimeOffset? ExtractDateTimeOffset(JsonElement element, params string[] names)
    {
        var value = ExtractString(element, names);
        return DateTimeOffset.TryParse(
            value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : null;
    }

    private static bool IsEffectivelyEmpty(JsonElement data)
    {
        return data.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => true,
            JsonValueKind.Object => !data.EnumerateObject().Any(),
            JsonValueKind.Array => data.GetArrayLength() == 0,
            _ => false
        };
    }

    private static JsonElement ParseRawItem(string raw)
    {
        using var document = JsonDocument.Parse(raw);
        return document.RootElement.Clone();
    }
}
