namespace NaderGorge.Application.Features.HR.Attendance;

public sealed record AttendanceCalculationInput(DateTime ClockedInAt, DateTime ClockedOutAt,
    DateTime ScheduledStart, DateTime ScheduledEnd, int BreakMinutes, int GraceMinutes, int ExpectedMinutes);
public sealed record AttendanceCalculationResult(int WorkedMinutes, int LateMinutes, int EarlyLeaveMinutes, int OvertimeMinutes);

public static class AttendanceCalculator
{
    public static AttendanceCalculationResult Calculate(AttendanceCalculationInput input)
    {
        if (input.ClockedOutAt <= input.ClockedInAt) throw new ArgumentException("Clock-out must be after clock-in.");
        var worked = Math.Max(0, (int)(input.ClockedOutAt - input.ClockedInAt).TotalMinutes - Math.Max(0, input.BreakMinutes));
        var late = Math.Max(0, (int)(input.ClockedInAt - input.ScheduledStart).TotalMinutes - Math.Max(0, input.GraceMinutes));
        var early = Math.Max(0, (int)(input.ScheduledEnd - input.ClockedOutAt).TotalMinutes);
        var overtime = Math.Max(0, (int)(input.ClockedOutAt - input.ScheduledEnd).TotalMinutes - Math.Max(0, input.BreakMinutes));
        return new(worked, late, early, overtime);
    }

    public static DateTime ResolveMissingClockOut(DateTime clockedInAt, DateTime scheduledEnd, DateTime evaluatedAt, int maximumSessionMinutes)
    {
        if (maximumSessionMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumSessionMinutes));
        var hardLimit = clockedInAt.AddMinutes(maximumSessionMinutes);
        var candidate = scheduledEnd > clockedInAt ? scheduledEnd : hardLimit;
        if (candidate > hardLimit) candidate = hardLimit;
        return candidate > evaluatedAt ? evaluatedAt : candidate;
    }
}
