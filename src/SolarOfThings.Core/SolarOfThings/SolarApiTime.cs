using System.Globalization;

namespace SolarOfThings.Core.SolarOfThings;

public static class SolarApiTime
{
    private const string WireDateTimeFormat = "yyyy-MM-dd'T'HH:mm:sszzz";
    private const string WireDateFormat = "yyyy-MM-dd";

    public static string FormatDateTime(DateTimeOffset instant, string timeZoneId)
    {
        var local = ConvertToTimeZone(instant, timeZoneId);
        return local.ToString(WireDateTimeFormat, CultureInfo.InvariantCulture);
    }

    public static string FormatDate(DateTimeOffset instant, string timeZoneId)
    {
        var local = ConvertToTimeZone(instant, timeZoneId);
        return local.ToString(WireDateFormat, CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset ConvertToTimeZone(
        DateTimeOffset instant,
        string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return instant;
        }

        try
        {
            return TimeZoneInfo.ConvertTime(
                instant,
                TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
        }
        catch (TimeZoneNotFoundException)
        {
            if (OperatingSystem.IsWindows() &&
                TimeZoneInfo.TryConvertIanaIdToWindowsId(
                    timeZoneId,
                    out var windowsId))
            {
                try
                {
                    return TimeZoneInfo.ConvertTime(
                        instant,
                        TimeZoneInfo.FindSystemTimeZoneById(windowsId));
                }
                catch (TimeZoneNotFoundException)
                {
                }
            }

            return instant;
        }
        catch (InvalidTimeZoneException)
        {
            return instant;
        }
    }
}
