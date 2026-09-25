namespace SolarOfThings.Core.Statistics;

public enum TimeRangePreset
{
    Day,
    CalendarWeek,
    Rolling7Days,
    Month,
    LastNMonthsRolling,
    LastNCompleteCalendarMonths,
    MonthRange,
    Year,
    Rolling12Months,
    YearToDate,
    ArbitraryDateRange
}

public enum AggregationPeriod
{
    Hour,
    Day,
    Week,
    Month,
    Year
}

public sealed record ResolvedTimeRange(
    TimeRangePreset Preset,
    DateOnly LocalStartDate,
    DateOnly LocalEndDate,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string TimeZoneId);
