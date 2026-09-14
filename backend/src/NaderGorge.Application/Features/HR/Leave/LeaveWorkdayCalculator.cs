using System.Text.Json;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Features.HR.Leave;

public static class LeaveWorkdayCalculator
{
    public static decimal Calculate(DateOnly startDate, DateOnly endDate, decimal dayFraction, WorkCalendar calendar)
        => EnumerateWorkingDates(startDate, endDate, calendar).Count * dayFraction;

    public static IReadOnlyList<DateOnly> EnumerateWorkingDates(DateOnly startDate, DateOnly endDate, WorkCalendar calendar)
    {
        if (endDate < startDate) return [];
        HashSet<DateOnly> holidays;
        try
        {
            holidays = JsonSerializer.Deserialize<string[]>(calendar.HolidaysJson)
                ?.Select(DateOnly.Parse).ToHashSet() ?? [];
        }
        catch (JsonException)
        {
            holidays = [];
        }

        var dates = new List<DateOnly>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var mask = 1 << (int)date.DayOfWeek;
            if ((calendar.WorkingDaysMask & mask) != 0 && !holidays.Contains(date)) dates.Add(date);
        }
        return dates;
    }
}
