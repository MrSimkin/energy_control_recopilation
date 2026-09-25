namespace SolarOfThings.Core.Commissioning;

public sealed record DiscoveryItem(
    string Id,
    string DisplayName,
    string RawJson);

public sealed record DiscoveryResult(
    IReadOnlyList<DiscoveryItem> Stations);

public sealed record DeviceDiscoveryResult(
    DiscoveryItem Station,
    IReadOnlyList<DiscoveryItem> Devices);

public sealed record CommissioningProgress(
    string Step,
    string Status,
    string Message);

public sealed record CommissioningProfile(
    string StationId,
    string? StationName,
    string? StationTimeZone,
    string DeviceId,
    string? DeviceName,
    string? SerialNumber,
    string? Model,
    string? Manufacturer,
    string? DtuId,
    string? GatherProtocolNumber,
    string? SoftwareVersion,
    string? DeviceSortKey,
    string? DeviceTypeNumber,
    decimal? RatedPower,
    bool? IsOnline,
    DateTimeOffset? LastDataAt,
    string? DataSource,
    string GatherAttributesStatus,
    int GatherAttributeCount,
    string LatestStateStatus,
    string EnergyFlowStatus,
    string HistoryStatus,
    string AggregateStatus,
    string AlarmStatus,
    string CapabilitiesJson,
    string AttributeCatalogJson,
    string StationJson,
    string DeviceJson,
    DateTimeOffset UpdatedUtc);
