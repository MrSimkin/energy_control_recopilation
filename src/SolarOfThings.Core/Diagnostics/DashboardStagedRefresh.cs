namespace SolarOfThings.Core.Diagnostics;

/// <summary>
/// Dashboard presentation pipeline: display quick, last-observed power/SOC
/// before expensive stored-day energy integration has finished. Both stages
/// recheck the caller's generation/device/page gate on the captured context.
/// No query, UI object or write operation is owned by this coordinator.
/// </summary>
public static class DashboardStagedRefresh
{
    /// <returns>False if the request became stale at either stage.</returns>
    public static async Task<bool> RunAsync<TInstant, TDaily>(
        Func<TInstant> readInstant,
        Func<TDaily> readDaily,
        Func<bool> canApply,
        Action<TInstant> showInstant,
        Action<TDaily> showDaily,
        Action<Exception> showDailyUnavailable)
    {
        ArgumentNullException.ThrowIfNull(readInstant);
        ArgumentNullException.ThrowIfNull(readDaily);
        ArgumentNullException.ThrowIfNull(canApply);
        ArgumentNullException.ThrowIfNull(showInstant);
        ArgumentNullException.ThrowIfNull(showDaily);
        ArgumentNullException.ThrowIfNull(showDailyUnavailable);

        // Do not read Daily before the first values can paint.
        var instant = await Task.Run(readInstant);
        if (!canApply()) return false;
        showInstant(instant);

        TDaily daily;
        try
        {
            daily = await Task.Run(readDaily);
        }
        catch (Exception ex)
        {
            // Failure to integrate daily kWh must NOT erase a valid fresh
            // instantaneous-power read; report secondary failure separately.
            if (!canApply()) return false;
            showDailyUnavailable(ex);
            return true;
        }
        if (!canApply()) return false;
        showDaily(daily);
        return true;
    }
}
