namespace SolarOfThings.Core.Installation;

public sealed record HouseholdBehaviorResult(
    string DeviceId,
    string ContextVersion,
    string Status,
    int InputFrames,
    int OutputSamples,
    IReadOnlyDictionary<string, int> StateCounts,
    string? Detail);
