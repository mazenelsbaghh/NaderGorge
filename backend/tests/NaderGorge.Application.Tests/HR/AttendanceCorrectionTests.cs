using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.HR.Attendance;
using NaderGorge.Application.Features.HR.Attendance.Corrections;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.HR;

public sealed class AttendanceCorrectionTests
{
    [Fact]
    public void Calculator_AppliesGraceBreakEarlyLeaveAndOvertime()
    {
        var result = AttendanceCalculator.Calculate(new AttendanceCalculationInput(
            new DateTime(2026, 7, 20, 9, 20, 0, DateTimeKind.Utc), new DateTime(2026, 7, 20, 18, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 20, 17, 0, 0, DateTimeKind.Utc), 30, 10, 480));
        Assert.Equal(10, result.LateMinutes);
        Assert.Equal(0, result.EarlyLeaveMinutes);
        Assert.Equal(60, result.OvertimeMinutes);
        Assert.Equal(520, result.WorkedMinutes);
    }

    [Fact]
    public void MissingClockPolicy_AutoClosesAtBoundedScheduledTime()
    {
        var close = AttendanceCalculator.ResolveMissingClockOut(
            new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 20, 17, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 22, 9, 0, 0, DateTimeKind.Utc), 720);
        Assert.Equal(new DateTime(2026, 7, 20, 17, 0, 0, DateTimeKind.Utc), close);
    }

    [Fact]
    public async Task Correction_ManagerThenHr_AppliesOnceWithBeforeAfterAndVersion()
    {
        await using var db = TestAppDbContextFactory.Create();
        var employeeUser = await TestAppDbContextFactory.SeedUserAsync(db, "Employee", "01066666661");
        var manager = await TestAppDbContextFactory.SeedUserAsync(db, "Manager", "01066666662");
        var hr = await TestAppDbContextFactory.SeedUserAsync(db, "HR", "01066666663");
        var employee = new EmployeeProfile { UserId = employeeUser.Id, User = employeeUser }; employee.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(employee.Id);
        var calendar = new WorkCalendar { Code = "CORR", Name = "Calendar" }; var template = new ShiftTemplate { Code = "CORR", Name = "Shift", WorkCalendarId = calendar.Id };
        template.Segments.Add(new ShiftSegment { ShiftTemplateId = template.Id, Sequence = 1, StartsAt = TimeSpan.FromHours(9), EndsAt = TimeSpan.FromHours(17) });
        var assignment = new ShiftAssignment { EmployeeId = employee.Id, ShiftTemplateId = template.Id, EffectiveFrom = new DateOnly(2026, 7, 1), Status = ShiftAssignmentStatus.Published, PublishedByUserId = hr.Id, Reason = "test" };
        var session = new AttendanceSession { EmployeeId = employee.Id, ShiftAssignmentId = assignment.Id, WorkDate = new DateOnly(2026, 7, 20), ClockedInAt = new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc), State = AttendanceSessionState.Open };
        db.EmployeeProfiles.Add(employee); db.WorkCalendars.Add(calendar); db.ShiftTemplates.Add(template); db.ShiftAssignments.Add(assignment); db.AttendanceSessions.Add(session); await db.SaveChangesAsync();
        var submitted = await new SubmitAttendanceCorrectionCommandHandler(db).Handle(new SubmitAttendanceCorrectionCommand(employeeUser.Id, session.Id, null, new DateTime(2026, 7, 20, 17, 0, 0, DateTimeKind.Utc), "forgot", null), default);
        await new DecideAttendanceCorrectionCommandHandler(db).Handle(new DecideAttendanceCorrectionCommand(submitted.Data, true, false, "manager ok", manager.Id, 1), default);
        var final = await new DecideAttendanceCorrectionCommandHandler(db).Handle(new DecideAttendanceCorrectionCommand(submitted.Data, true, true, "hr ok", hr.Id, 2), default);
        var replay = await new DecideAttendanceCorrectionCommandHandler(db).Handle(new DecideAttendanceCorrectionCommand(submitted.Data, true, true, "again", hr.Id, 3), default);
        Assert.True(final.Success); Assert.False(replay.Success);
        var corrected = await db.AttendanceSessions.SingleAsync(item => item.Id == session.Id);
        var correction = await db.AttendanceCorrections.SingleAsync();
        Assert.Equal(AttendanceSessionState.Corrected, corrected.State); Assert.NotNull(corrected.ClockedOutAt);
        Assert.NotNull(correction.AppliedJson); Assert.NotNull(correction.AppliedAt);
    }

    [Fact]
    public async Task RejectedCorrection_LeavesOriginalSessionUntouched()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Employee", "01066666664"); var reviewer = await TestAppDbContextFactory.SeedUserAsync(db, "Reviewer", "01066666665");
        var employee = new EmployeeProfile { UserId = user.Id, User = user }; employee.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(employee.Id);
        var session = new AttendanceSession { EmployeeId = employee.Id, ShiftAssignmentId = Guid.NewGuid(), WorkDate = new DateOnly(2026, 7, 20), ClockedInAt = DateTime.UtcNow, State = AttendanceSessionState.Open };
        db.EmployeeProfiles.Add(employee); db.AttendanceSessions.Add(session); await db.SaveChangesAsync();
        var correction = new AttendanceCorrection { EmployeeId = employee.Id, AttendanceSessionId = session.Id, Reason = "test", BeforeJson = "{}" };
        db.AttendanceCorrections.Add(correction); await db.SaveChangesAsync();
        var result = await new DecideAttendanceCorrectionCommandHandler(db).Handle(new DecideAttendanceCorrectionCommand(correction.Id, false, false, "invalid evidence", reviewer.Id, 1), default);
        Assert.True(result.Success, string.Join(",", result.Errors ?? [])); Assert.Null(session.ClockedOutAt); Assert.Equal(AttendanceSessionState.Open, session.State);
    }

    [Fact]
    public async Task Correction_WithoutChangedTimes_IsRejected()
    {
        await using var db = TestAppDbContextFactory.Create();
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Employee", "01066666666");
        var employee = new EmployeeProfile { UserId = user.Id, User = user };
        employee.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(employee.Id);
        var session = new AttendanceSession { EmployeeId = employee.Id, ShiftAssignmentId = Guid.NewGuid(), WorkDate = new DateOnly(2026, 8, 4), ClockedInAt = DateTime.UtcNow, State = AttendanceSessionState.Open };
        db.EmployeeProfiles.Add(employee); db.AttendanceSessions.Add(session); await db.SaveChangesAsync();

        var result = await new SubmitAttendanceCorrectionCommandHandler(db).Handle(
            new SubmitAttendanceCorrectionCommand(user.Id, session.Id, null, null, "missing time", null), default);

        Assert.False(result.Success);
        Assert.Contains("ATTENDANCE_CORRECTION_NO_CHANGES", result.Errors ?? []);
        Assert.Empty(await db.AttendanceCorrections.ToListAsync());
    }
}
