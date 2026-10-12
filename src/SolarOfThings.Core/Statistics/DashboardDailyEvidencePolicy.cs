namespace SolarOfThings.Core.Statistics;

/// <summary>
/// Describes quality of the LAST SAVED local day from the three separately
/// measured power streams. It never infers absent readings as zero energy.
/// Coverage for an available stream must not conceal an absent third stream.
/// </summary>
public static class DashboardDailyEvidencePolicy
{
    public static DashboardDailyEvidence Assess(
        int solarSamples, double solarCoverage,
        int houseSamples, double houseCoverage,
        int gridSamples, double gridCoverage)
    {
        var samples = new[] { solarSamples, houseSamples, gridSamples };
        var coverages = new[] { solarCoverage, houseCoverage, gridCoverage };
        var valid = 0;
        var lowest = 100.0;
        for (var i = 0; i < samples.Length; i++)
        {
            if (samples[i] < 2 || !double.IsFinite(coverages[i]) ||
                coverages[i] < 0 || coverages[i] > 100)
                continue;
            valid++;
            lowest = Math.Min(lowest, coverages[i]);
        }

        // A partial day is not comparable as a complete home energy balance,
        // regardless of its maximum / available-stream coverage.
        var state = valid switch
        {
            0 => "NO_SAMPLES",
            < 3 => "MISSING_STREAMS",
            _ when lowest < 80 => "LOW_COVERAGE",
            _ => "ADEQUATE"
        };
        return new DashboardDailyEvidence(state, valid,
            samples.Length - valid, valid == 0 ? null : lowest);
    }
}

public sealed record DashboardDailyEvidence(
    string State, int AvailableStreams, int MissingStreams,
    double? MinimumAvailableCoveragePercent)
{
    public bool HasCompleteThreeStreamCoverage => MissingStreams == 0;
    public bool ShouldWarn => State != "ADEQUATE";
}
