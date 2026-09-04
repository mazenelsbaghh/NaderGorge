using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;

namespace NaderGorge.Infrastructure.Services;

internal static class LiveSupportScheduleRules
{
    private const long DayTicks = TimeSpan.TicksPerDay;
    private const long WeekTicks = DayTicks * 7;

    public static void Validate(IReadOnlyList<LiveSupportScheduleWindowDto> schedule)
    {
        if (schedule.Any(window => window.DayOfWeek is < 0 or > 6 || window.StartLocalTime == window.EndLocalTime))
        {
            throw new LiveSupportException(
                "VALIDATION_ERROR",
                "فترة الدعم غير صحيحة؛ يمكن أن تعبر منتصف الليل لكن لا يمكن أن يتساوى وقت البداية والنهاية.");
        }

        var ranges = schedule
            .SelectMany(ToWeeklyRanges)
            .OrderBy(range => range.Start)
            .ThenBy(range => range.End)
            .ToArray();

        for (var index = 1; index < ranges.Length; index++)
        {
            if (ranges[index].Start < ranges[index - 1].End)
            {
                throw new LiveSupportException("VALIDATION_ERROR", "فترات الدعم متداخلة.");
            }
        }
    }

    public static bool Contains(DateTime localNow, LiveSupportScheduleWindowDto window)
    {
        var currentDay = (int)localNow.DayOfWeek;
        var localTime = TimeOnly.FromDateTime(localNow);

        if (window.EndLocalTime > window.StartLocalTime)
        {
            return window.DayOfWeek == currentDay
                && localTime >= window.StartLocalTime
                && localTime < window.EndLocalTime;
        }

        var followingDay = (window.DayOfWeek + 1) % 7;
        return window.DayOfWeek == currentDay && localTime >= window.StartLocalTime
            || followingDay == currentDay && localTime < window.EndLocalTime;
    }

    private static IEnumerable<(long Start, long End)> ToWeeklyRanges(LiveSupportScheduleWindowDto window)
    {
        var start = window.DayOfWeek * DayTicks + window.StartLocalTime.Ticks;
        var end = window.DayOfWeek * DayTicks + window.EndLocalTime.Ticks;
        if (end <= start) end += DayTicks;

        if (end <= WeekTicks)
        {
            yield return (start, end);
            yield break;
        }

        yield return (start, WeekTicks);
        yield return (0, end - WeekTicks);
    }
}
