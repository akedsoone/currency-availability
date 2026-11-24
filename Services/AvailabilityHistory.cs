using CurrencyAvailability.Models;

namespace CurrencyAvailability.Services;

public static class AvailabilityHistory
{
    // Один замер описывает ближайшие пять минут. Пропущенные интервалы остаются пустыми.
    public static List<LevelInterval> BuildIntervals(List<AvailabilityRecord> records, DateTime start, DateTime end)
    {
        var intervals = new List<LevelInterval>();
        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];
            var from = record.MeasuredAt < start ? start : record.MeasuredAt;
            var to = record.MeasuredAt + AvailabilityWorker.Interval;
            if (index + 1 < records.Count && records[index + 1].MeasuredAt < to)
            {
                to = records[index + 1].MeasuredAt;
            }

            if (to > end)
            {
                to = end;
            }

            if (to <= from)
            {
                continue;
            }

            if (intervals.Count > 0 && intervals[^1].End == from && intervals[^1].Level == record.Level)
            {
                intervals[^1] = intervals[^1] with { End = to };
            }
            else
            {
                intervals.Add(new LevelInterval(from, to, record.Level));
            }
        }

        return intervals;
    }

    public static List<LevelShare> CalculateShares(List<LevelInterval> intervals, TimeSpan period)
    {
        var shares = new List<LevelShare>();
        foreach (var level in Enum.GetValues<AvailabilityLevel>())
        {
            var duration = intervals.Where(item => item.Level == level).Sum(item => (item.End - item.Start).Ticks);
            shares.Add(new LevelShare(level, 100.0 * duration / period.Ticks, TimeSpan.FromTicks(duration)));
        }

        var unknown = period - TimeSpan.FromTicks(shares.Sum(item => item.Duration.Ticks));
        shares.Add(new LevelShare(null, 100.0 * unknown.Ticks / period.Ticks, unknown));
        return shares;
    }
}
