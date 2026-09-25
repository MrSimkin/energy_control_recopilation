using SolarOfThings.Core.SolarOfThings;

namespace SolarOfThings.Core.Statistics;

public sealed class TimeRangeSelectionService
{
    public ResolvedTimeRange ForDay(
        DateOnly localDate,
        string timeZoneId)
    {
        return Resolve(
            TimeRangePreset.Day,
            localDate,
            localDate,
            timeZoneId);
    }

    public ResolvedTimeRange ForCalendarWeek(
        DateOnly anyDateInWeek,
        string timeZoneId)
    {
        var offset = ((int)anyDateInWeek.DayOfWeek + 6) % 7;
        var monday = anyDateInWeek.AddDays(-offset);
        return Resolve(
            TimeRangePreset.CalendarWeek,
            monday,
            monday.AddDays(6),
            timeZoneId);
    }

    public ResolvedTimeRange ForRolling7Days(
        DateOnly throughDate,
        string timeZoneId)
    {
        return Resolve(
            TimeRangePreset.Rolling7Days,
            throughDate.AddDays(-6),
            throughDate,
            timeZoneId);
    }

    public ResolvedTimeRange ForMonth(
        int year,
        int month,
        string timeZoneId)
    {
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        return Resolve(
            TimeRangePreset.Month,
            start,
            end,
            timeZoneId);
    }

    public ResolvedTimeRange ForLastNMonthsRolling(
        DateOnly throughDate,
        int monthCount,
        string timeZoneId)
    {
        if (monthCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(monthCount));
        }

        var start = throughDate.AddMonths(-monthCount).AddDays(1);

        return Resolve(
            TimeRangePreset.LastNMonthsRolling,
            start,
            throughDate,
            timeZoneId);
    }

    public ResolvedTimeRange ForLastNCompleteCalendarMonths(
        DateOnly referenceDate,
        int monthCount,
        string timeZoneId)
    {
        if (monthCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(monthCount));
        }

        var currentMonth = new DateOnly(
            referenceDate.Year,
            referenceDate.Month,
            1);

        var end = currentMonth.AddDays(-1);
        var start = currentMonth.AddMonths(-monthCount);

        return Resolve(
            TimeRangePreset.LastNCompleteCalendarMonths,
            start,
            end,
            timeZoneId);
    }

    public ResolvedTimeRange ForMonthRange(
        int startYear,
        int startMonth,
        int endYear,
        int endMonth,
        string timeZoneId)
    {
        var start = new DateOnly(startYear, startMonth, 1);
        var endStart = new DateOnly(endYear, endMonth, 1);
        var end = endStart.AddMonths(1).AddDays(-1);

        if (end < start)
        {
            (start, end) = (
                new DateOnly(endYear, endMonth, 1),
                new DateOnly(startYear, startMonth, 1)
                    .AddMonths(1)
                    .AddDays(-1));
        }

        return Resolve(
            TimeRangePreset.MonthRange,
            start,
            end,
            timeZoneId);
    }

    public ResolvedTimeRange ForYear(
        int year,
        string timeZoneId)
    {
        return Resolve(
            TimeRangePreset.Year,
            new DateOnly(year, 1, 1),
            new DateOnly(year, 12, 31),
            timeZoneId);
    }

    public ResolvedTimeRange ForRolling12Months(
        DateOnly throughDate,
        string timeZoneId)
    {
        var start = throughDate.AddMonths(-12).AddDays(1);

        return Resolve(
            TimeRangePreset.Rolling12Months,
            start,
            throughDate,
            timeZoneId);
    }

    public ResolvedTimeRange ForYearToDate(
        DateOnly throughDate,
        string timeZoneId)
    {
        return Resolve(
            TimeRangePreset.YearToDate,
            new DateOnly(throughDate.Year, 1, 1),
            throughDate,
            timeZoneId);
    }

    public ResolvedTimeRange ForArbitraryDateRange(
        DateOnly firstDate,
        DateOnly secondDate,
        string timeZoneId)
    {
        var start = firstDate <= secondDate ? firstDate : secondDate;
        var end = firstDate <= secondDate ? secondDate : firstDate;

        return Resolve(
            TimeRangePreset.ArbitraryDateRange,
            start,
            end,
            timeZoneId);
    }

    private static ResolvedTimeRange Resolve(
        TimeRangePreset preset,
        DateOnly localStartDate,
        DateOnly localEndDate,
        string timeZoneId)
    {
        var startWindow = SolarApiTime.GetLocalDayWindow(
            localStartDate,
            timeZoneId);
        var endWindow = SolarApiTime.GetLocalDayWindow(
            localEndDate,
            timeZoneId);

        return new ResolvedTimeRange(
            preset,
            localStartDate,
            localEndDate,
            startWindow.Start.ToUniversalTime(),
            endWindow.End.ToUniversalTime(),
            timeZoneId);
    }
}
