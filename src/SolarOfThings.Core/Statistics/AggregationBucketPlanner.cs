using SolarOfThings.Core.SolarOfThings;

namespace SolarOfThings.Core.Statistics;

internal sealed record AggregationBucketWindow(
    AggregationPeriod Period,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtcExclusive,
    string LocalLabel);

internal static class AggregationBucketPlanner
{
    public static IReadOnlyList<AggregationBucketWindow> Build(
        AggregationPeriod period,
        DateTimeOffset rangeStartUtc,
        DateTimeOffset rangeEndExclusive,
        string timeZoneId)
    {
        var buckets = new List<AggregationBucketWindow>();

        if (rangeEndExclusive <= rangeStartUtc)
        {
            return buckets;
        }

        if (period == AggregationPeriod.Hour)
        {
            var localStart =
                SolarApiTime.ConvertToLocalTime(
                    rangeStartUtc,
                    timeZoneId);

            var localHour = new DateTimeOffset(
                localStart.Year,
                localStart.Month,
                localStart.Day,
                localStart.Hour,
                0,
                0,
                localStart.Offset);

            var naturalStartUtc =
                localHour.ToUniversalTime();

            while (naturalStartUtc < rangeEndExclusive)
            {
                var naturalEndUtc = naturalStartUtc.AddHours(1);
                var clippedStart = naturalStartUtc < rangeStartUtc
                    ? rangeStartUtc
                    : naturalStartUtc;
                var clippedEnd = naturalEndUtc > rangeEndExclusive
                    ? rangeEndExclusive
                    : naturalEndUtc;

                if (clippedEnd > clippedStart)
                {
                    var labelLocal =
                        SolarApiTime.ConvertToLocalTime(
                            naturalStartUtc,
                            timeZoneId);

                    buckets.Add(new AggregationBucketWindow(
                        period,
                        clippedStart,
                        clippedEnd,
                        $"{labelLocal:yyyy-MM-dd HH:mm} {labelLocal:zzz}"));
                }

                naturalStartUtc = naturalEndUtc;
            }

            return buckets;
        }

        var localDate =
            SolarApiTime.GetLocalDate(
                rangeStartUtc,
                timeZoneId);

        var bucketStartDate = period switch
        {
            AggregationPeriod.Day => localDate,
            AggregationPeriod.Week => StartOfWeek(localDate),
            AggregationPeriod.Month => new DateOnly(
                localDate.Year,
                localDate.Month,
                1),
            AggregationPeriod.Year => new DateOnly(
                localDate.Year,
                1,
                1),
            _ => localDate
        };

        while (true)
        {
            var nextStartDate = period switch
            {
                AggregationPeriod.Day =>
                    bucketStartDate.AddDays(1),
                AggregationPeriod.Week =>
                    bucketStartDate.AddDays(7),
                AggregationPeriod.Month =>
                    bucketStartDate.AddMonths(1),
                AggregationPeriod.Year =>
                    bucketStartDate.AddYears(1),
                _ =>
                    bucketStartDate.AddDays(1)
            };

            var naturalStartUtc =
                SolarApiTime.GetLocalDayWindow(
                    bucketStartDate,
                    timeZoneId).Start.ToUniversalTime();

            var naturalEndUtc =
                SolarApiTime.GetLocalDayWindow(
                    nextStartDate,
                    timeZoneId).Start.ToUniversalTime();

            if (naturalStartUtc >= rangeEndExclusive)
            {
                break;
            }

            var clippedStart = naturalStartUtc < rangeStartUtc
                ? rangeStartUtc
                : naturalStartUtc;
            var clippedEnd = naturalEndUtc > rangeEndExclusive
                ? rangeEndExclusive
                : naturalEndUtc;

            if (clippedEnd > clippedStart)
            {
                buckets.Add(new AggregationBucketWindow(
                    period,
                    clippedStart,
                    clippedEnd,
                    BuildLabel(
                        period,
                        bucketStartDate,
                        nextStartDate.AddDays(-1))));
            }

            bucketStartDate = nextStartDate;
        }

        return buckets;
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }

    private static string BuildLabel(
        AggregationPeriod period,
        DateOnly startDate,
        DateOnly endDate)
    {
        return period switch
        {
            AggregationPeriod.Day =>
                startDate.ToString("yyyy-MM-dd"),
            AggregationPeriod.Week =>
                $"{startDate:yyyy-MM-dd}–{endDate:yyyy-MM-dd}",
            AggregationPeriod.Month =>
                startDate.ToString("yyyy-MM"),
            AggregationPeriod.Year =>
                startDate.ToString("yyyy"),
            _ =>
                startDate.ToString("yyyy-MM-dd")
        };
    }
}
