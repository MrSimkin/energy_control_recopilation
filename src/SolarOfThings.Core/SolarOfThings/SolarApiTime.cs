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

    public static DateOnly GetLocalDate(DateTimeOffset instant, string timeZoneId)
    {
        var local = ConvertToTimeZone(instant, timeZoneId);
        return DateOnly.FromDateTime(local.DateTime);
    }

    public static DateTimeOffset ConvertToLocalTime(
        DateTimeOffset instant,
        string timeZoneId)
    {
        return ConvertToTimeZone(instant, timeZoneId);
    }

    public static TimeZoneInfo GetTimeZoneInfo(string timeZoneId)
    {
        return ResolveTimeZone(timeZoneId);
    }

    public static (DateTimeOffset Start, DateTimeOffset End) GetLocalDayWindow(
        DateOnly localDate,
        string timeZoneId)
    {
        var zone = ResolveTimeZone(timeZoneId);

        var startLocal = DateTime.SpecifyKind(
            localDate.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);

        var nextLocal = DateTime.SpecifyKind(
            localDate.AddDays(1).ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(startLocal))
        {
            startLocal = startLocal.AddHours(1);
        }

        if (zone.IsInvalidTime(nextLocal))
        {
            nextLocal = nextLocal.AddHours(1);
        }

        var start = new DateTimeOffset(startLocal, zone.GetUtcOffset(startLocal));
        var next = new DateTimeOffset(nextLocal, zone.GetUtcOffset(nextLocal));

        return (start, next.AddTicks(-1));
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                if (OperatingSystem.IsWindows() &&
                    TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out var windowsId))
                {
                    try
                    {
                        return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                    }
                    catch (TimeZoneNotFoundException)
                    {
                    }
                }
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    private static DateTimeOffset ConvertToTimeZone(
        DateTimeOffset instant,
        string timeZoneId)
    {
        return TimeZoneInfo.ConvertTime(instant, ResolveTimeZone(timeZoneId));
    }
}
