using System;

namespace NaderGorge.Application.Common;

/// <summary>Centralized calendar boundaries for business rules that operate in Cairo time.</summary>
public static class CairoTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(EnsureUtc(utc), Zone);

    public static DateOnly ToDate(DateTime utc) => DateOnly.FromDateTime(ToLocal(utc));

    public static DateOnly GetCurrentDate() => ToDate(DateTime.UtcNow);

    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => TimeZoneInfo.ConvertTimeToUtc(value, Zone)
    };

    public static (DateTime StartUtc, DateTime EndUtc) GetDayRangeUtc(DateTime localDate) =>
        GetRangeUtc(localDate.Date, localDate.Date.AddDays(1));

    public static (DateTime StartUtc, DateTime EndUtc) GetCurrentDayRangeUtc() =>
        GetDayRangeUtc(ToLocal(DateTime.UtcNow));

    public static (DateTime StartUtc, DateTime EndUtc) GetCurrentMonthRangeUtc()
    {
        var localNow = ToLocal(DateTime.UtcNow);
        var start = new DateTime(localNow.Year, localNow.Month, 1);
        return GetRangeUtc(start, start.AddMonths(1));
    }

    public static (DateTime StartUtc, DateTime EndUtc) GetRollingMonthRangeUtc(DateTime? fromDate, DateTime? toDate)
    {
        var currentDate = ToLocal(DateTime.UtcNow).Date;
        var startDate = (fromDate ?? currentDate.AddMonths(-1)).Date;
        var endDate = (toDate ?? currentDate).Date;
        return GetRangeUtc(startDate, endDate.AddDays(1));
    }

    private static (DateTime StartUtc, DateTime EndUtc) GetRangeUtc(DateTime startLocal, DateTime endLocal) =>
        (TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(startLocal, DateTimeKind.Unspecified), Zone),
         TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(endLocal, DateTimeKind.Unspecified), Zone));

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
