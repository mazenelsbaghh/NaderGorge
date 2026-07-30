using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Features.HR.Scheduling;

public static class ShiftScheduleRules
{
    public static ShiftSegment? SegmentForWorkDate(IEnumerable<ShiftSegment> segments, DateOnly workDate)
    {
        var rows = segments.OrderBy(item => item.Sequence).ToList();
        return rows.FirstOrDefault(item => item.DayOfWeek == workDate.DayOfWeek) ??
               rows.FirstOrDefault(item => item.DayOfWeek is null);
    }

    public static TimeSpan Duration(ShiftSegment segment)
    {
        var end = segment.EndsAt <= segment.StartsAt ? segment.EndsAt.Add(TimeSpan.FromDays(1)) : segment.EndsAt;
        return end - segment.StartsAt;
    }

    public static IReadOnlyList<string> ValidateSegments(IEnumerable<ShiftSegment> segments)
    {
        var rows = segments.OrderBy(item => item.Sequence).ToList();
        var errors = new HashSet<string>();
        if (rows.Count == 0) errors.Add("SHIFT_SEGMENT_REQUIRED");
        if (rows.Any(item => item.StartsAt == item.EndsAt || Duration(item) > TimeSpan.FromHours(18))) errors.Add("SHIFT_SEGMENT_DURATION_INVALID");
        if (rows.Select(item => item.Sequence).Distinct().Count() != rows.Count) errors.Add("SHIFT_SEGMENT_SEQUENCE_DUPLICATE");
        for (var i = 0; i < rows.Count; i++)
        for (var j = i + 1; j < rows.Count; j++)
        {
            if (rows[i].DayOfWeek != rows[j].DayOfWeek) continue;
            var firstEnd = rows[i].EndsAt <= rows[i].StartsAt ? rows[i].EndsAt.Add(TimeSpan.FromDays(1)) : rows[i].EndsAt;
            var secondEnd = rows[j].EndsAt <= rows[j].StartsAt ? rows[j].EndsAt.Add(TimeSpan.FromDays(1)) : rows[j].EndsAt;
            if (rows[i].StartsAt < secondEnd && rows[j].StartsAt < firstEnd) errors.Add("SHIFT_SEGMENT_OVERLAP");
        }
        return errors.ToList();
    }

    public static bool PeriodsOverlap(DateOnly firstStart, DateOnly? firstEnd, DateOnly secondStart, DateOnly? secondEnd)
    {
        var firstExclusiveEnd = firstEnd ?? DateOnly.MaxValue;
        var secondExclusiveEnd = secondEnd ?? DateOnly.MaxValue;
        return firstStart < secondExclusiveEnd && secondStart < firstExclusiveEnd;
    }
}

public static class ShiftWorkDateResolver
{
    public static DateOnly Resolve(DateTime occurredAtUtc, ShiftSegment segment, TimeZoneInfo timeZone)
    {
        var utc = occurredAtUtc.Kind == DateTimeKind.Utc ? occurredAtUtc : DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone);
        var date = DateOnly.FromDateTime(local);
        if (segment.EndsAt <= segment.StartsAt)
        {
            if (segment.WorkDateRule == Domain.Enums.ShiftWorkDateRule.SegmentStartDate && local.TimeOfDay < segment.EndsAt)
                return date.AddDays(-1);
            if (segment.WorkDateRule == Domain.Enums.ShiftWorkDateRule.SegmentEndDate && local.TimeOfDay >= segment.StartsAt)
                return date.AddDays(1);
        }
        return date;
    }
}
