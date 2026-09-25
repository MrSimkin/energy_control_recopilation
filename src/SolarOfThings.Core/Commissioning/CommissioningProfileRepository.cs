using Microsoft.Data.Sqlite;
using SolarOfThings.Core.Data;

namespace SolarOfThings.Core.Commissioning;

public sealed class CommissioningProfileRepository
{
    private readonly SqliteDatabase _database;

    public CommissioningProfileRepository(SqliteDatabase database)
    {
        _database = database;
    }

    public void Save(CommissioningProfile profile)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO commissioning_profile (
                profile_id,
                station_id,
                station_name,
                station_timezone,
                device_id,
                device_name,
                serial_number,
                model,
                manufacturer,
                dtu_id,
                gather_protocol_number,
                software_version,
                device_sort_key,
                device_type_number,
                rated_power,
                is_online,
                last_data_at,
                data_source,
                gather_attributes_status,
                gather_attribute_count,
                latest_state_status,
                energy_flow_status,
                history_status,
                aggregate_status,
                alarm_status,
                capabilities_json,
                attribute_catalog_json,
                station_json,
                device_json,
                updated_utc
            )
            VALUES (
                1,
                $stationId,
                $stationName,
                $stationTimezone,
                $deviceId,
                $deviceName,
                $serialNumber,
                $model,
                $manufacturer,
                $dtuId,
                $gatherProtocolNumber,
                $softwareVersion,
                $deviceSortKey,
                $deviceTypeNumber,
                $ratedPower,
                $isOnline,
                $lastDataAt,
                $dataSource,
                $gatherAttributesStatus,
                $gatherAttributeCount,
                $latestStateStatus,
                $energyFlowStatus,
                $historyStatus,
                $aggregateStatus,
                $alarmStatus,
                $capabilitiesJson,
                $attributeCatalogJson,
                $stationJson,
                $deviceJson,
                $updatedUtc
            )
            ON CONFLICT(profile_id) DO UPDATE SET
                station_id = excluded.station_id,
                station_name = excluded.station_name,
                station_timezone = excluded.station_timezone,
                device_id = excluded.device_id,
                device_name = excluded.device_name,
                serial_number = excluded.serial_number,
                model = excluded.model,
                manufacturer = excluded.manufacturer,
                dtu_id = excluded.dtu_id,
                gather_protocol_number = excluded.gather_protocol_number,
                software_version = excluded.software_version,
                device_sort_key = excluded.device_sort_key,
                device_type_number = excluded.device_type_number,
                rated_power = excluded.rated_power,
                is_online = excluded.is_online,
                last_data_at = excluded.last_data_at,
                data_source = excluded.data_source,
                gather_attributes_status = excluded.gather_attributes_status,
                gather_attribute_count = excluded.gather_attribute_count,
                latest_state_status = excluded.latest_state_status,
                energy_flow_status = excluded.energy_flow_status,
                history_status = excluded.history_status,
                aggregate_status = excluded.aggregate_status,
                alarm_status = excluded.alarm_status,
                capabilities_json = excluded.capabilities_json,
                attribute_catalog_json = excluded.attribute_catalog_json,
                station_json = excluded.station_json,
                device_json = excluded.device_json,
                updated_utc = excluded.updated_utc;
            """;

        command.Parameters.AddWithValue("$stationId", profile.StationId);
        command.Parameters.AddWithValue("$stationName", Db(profile.StationName));
        command.Parameters.AddWithValue("$stationTimezone", Db(profile.StationTimeZone));
        command.Parameters.AddWithValue("$deviceId", profile.DeviceId);
        command.Parameters.AddWithValue("$deviceName", Db(profile.DeviceName));
        command.Parameters.AddWithValue("$serialNumber", Db(profile.SerialNumber));
        command.Parameters.AddWithValue("$model", Db(profile.Model));
        command.Parameters.AddWithValue("$manufacturer", Db(profile.Manufacturer));
        command.Parameters.AddWithValue("$dtuId", Db(profile.DtuId));
        command.Parameters.AddWithValue("$gatherProtocolNumber", Db(profile.GatherProtocolNumber));
        command.Parameters.AddWithValue("$softwareVersion", Db(profile.SoftwareVersion));
        command.Parameters.AddWithValue("$deviceSortKey", Db(profile.DeviceSortKey));
        command.Parameters.AddWithValue("$deviceTypeNumber", Db(profile.DeviceTypeNumber));
        command.Parameters.AddWithValue("$ratedPower", profile.RatedPower.HasValue ? profile.RatedPower.Value : DBNull.Value);
        command.Parameters.AddWithValue("$isOnline", profile.IsOnline.HasValue ? (profile.IsOnline.Value ? 1 : 0) : DBNull.Value);
        command.Parameters.AddWithValue("$lastDataAt", profile.LastDataAt.HasValue ? profile.LastDataAt.Value.ToString("O") : DBNull.Value);
        command.Parameters.AddWithValue("$dataSource", Db(profile.DataSource));
        command.Parameters.AddWithValue("$gatherAttributesStatus", profile.GatherAttributesStatus);
        command.Parameters.AddWithValue("$gatherAttributeCount", profile.GatherAttributeCount);
        command.Parameters.AddWithValue("$latestStateStatus", profile.LatestStateStatus);
        command.Parameters.AddWithValue("$energyFlowStatus", profile.EnergyFlowStatus);
        command.Parameters.AddWithValue("$historyStatus", profile.HistoryStatus);
        command.Parameters.AddWithValue("$aggregateStatus", profile.AggregateStatus);
        command.Parameters.AddWithValue("$alarmStatus", profile.AlarmStatus);
        command.Parameters.AddWithValue("$capabilitiesJson", profile.CapabilitiesJson);
        command.Parameters.AddWithValue("$attributeCatalogJson", profile.AttributeCatalogJson);
        command.Parameters.AddWithValue("$stationJson", profile.StationJson);
        command.Parameters.AddWithValue("$deviceJson", profile.DeviceJson);
        command.Parameters.AddWithValue("$updatedUtc", profile.UpdatedUtc.ToString("O"));

        command.ExecuteNonQuery();
    }

    public CommissioningProfile? Get()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM commissioning_profile WHERE profile_id = 1;";

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new CommissioningProfile(
            reader.GetString(reader.GetOrdinal("station_id")),
            NullableString(reader, "station_name"),
            NullableString(reader, "station_timezone"),
            reader.GetString(reader.GetOrdinal("device_id")),
            NullableString(reader, "device_name"),
            NullableString(reader, "serial_number"),
            NullableString(reader, "model"),
            NullableString(reader, "manufacturer"),
            NullableString(reader, "dtu_id"),
            NullableString(reader, "gather_protocol_number"),
            NullableString(reader, "software_version"),
            NullableString(reader, "device_sort_key"),
            NullableString(reader, "device_type_number"),
            NullableDecimal(reader, "rated_power"),
            NullableBool(reader, "is_online"),
            NullableDateTimeOffset(reader, "last_data_at"),
            NullableString(reader, "data_source"),
            reader.GetString(reader.GetOrdinal("gather_attributes_status")),
            reader.GetInt32(reader.GetOrdinal("gather_attribute_count")),
            reader.GetString(reader.GetOrdinal("latest_state_status")),
            reader.GetString(reader.GetOrdinal("energy_flow_status")),
            reader.GetString(reader.GetOrdinal("history_status")),
            reader.GetString(reader.GetOrdinal("aggregate_status")),
            reader.GetString(reader.GetOrdinal("alarm_status")),
            reader.GetString(reader.GetOrdinal("capabilities_json")),
            reader.GetString(reader.GetOrdinal("attribute_catalog_json")),
            reader.GetString(reader.GetOrdinal("station_json")),
            reader.GetString(reader.GetOrdinal("device_json")),
            DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("updated_utc"))));
    }

    private static object Db(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

    private static string? NullableString(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static decimal? NullableDecimal(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static bool? NullableBool(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal) != 0;
    }

    private static DateTimeOffset? NullableDateTimeOffset(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return DateTimeOffset.TryParse(reader.GetString(ordinal), out var value)
            ? value
            : null;
    }
}
