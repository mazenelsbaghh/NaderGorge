using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.HR.Attendance.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.HR;

public sealed class AttendanceCancellationTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private AppDbContext _db = null!;
    private readonly User _admin = new() { FullName = "Admin", PhoneNumber = "01010000001", PasswordHash = "test" };
    private readonly EmployeeProfile _employee = new() { EmployeeNumber = "EMP-CANCEL", User = new User { FullName = "Employee", PhoneNumber = "01010000002", PasswordHash = "test" } };
    private AttendanceSession _session = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        _db.AddRange(_employee, new UserRole { User = _admin, Role = new Role { Name = "Admin", Type = RoleType.Admin } });
        _session = new AttendanceSession { Employee = _employee,
            ShiftAssignment = new ShiftAssignment { Employee = _employee, PublishedByUserId = _admin.Id,
                ShiftTemplate = new ShiftTemplate { Code = "SHIFT", Name = "Shift", WorkCalendar = new WorkCalendar { Name = "Calendar", Code = "CAL" } } }, WorkDate = new DateOnly(2026, 9, 12),
            ClockedInAt = new DateTime(2026, 9, 12, 6, 0, 0, DateTimeKind.Utc),
            ClockedOutAt = new DateTime(2026, 9, 12, 14, 0, 0, DateTimeKind.Utc),
            State = AttendanceSessionState.Completed, WorkedMinutes = 450, LateMinutes = 10, EarlyLeaveMinutes = 5, OvertimeMinutes = 20 };
        _db.Add(_session);
        await _db.SaveChangesAsync();
    }

    [Theory]
    [InlineData(AttendanceEventType.ClockIn)]
    [InlineData(AttendanceEventType.ClockOut)]
    public async Task Cancellation_preserves_evidence_withdraws_pending_corrections_and_updates_work_totals(AttendanceEventType eventType)
    {
        var originalIn = _session.ClockedInAt;
        var originalOut = _session.ClockedOutAt;
        _db.Add(new AttendanceCorrection { EmployeeId = _employee.Id, AttendanceSessionId = _session.Id, Reason = "wrong time" });
        await _db.SaveChangesAsync();
        var result = await Cancel(eventType);
        Assert.True(result.Success, result.Message);
        _db.ChangeTracker.Clear();
        var saved = await _db.AttendanceSessions.SingleAsync();
        Assert.Equal(originalIn, saved.ClockedInAt);
        Assert.Equal(eventType == AttendanceEventType.ClockIn ? originalOut : null, saved.ClockedOutAt);
        Assert.Equal(eventType == AttendanceEventType.ClockIn ? AttendanceSessionState.Cancelled : AttendanceSessionState.Open, saved.State);
        Assert.Equal(eventType == AttendanceEventType.ClockIn ? 0 : 10, saved.LateMinutes);
        Assert.Equal(0, saved.WorkedMinutes + saved.EarlyLeaveMinutes + saved.OvertimeMinutes);
        Assert.Equal(2, saved.Version);
        Assert.Equal(AttendanceCorrectionState.Withdrawn, (await _db.AttendanceCorrections.SingleAsync()).State);
        var audit = await _db.AuditLogs.SingleAsync();
        Assert.Equal(_admin.Id, audit.PerformedByUserId);
        Assert.Equal("تسجيل بالخطأ", audit.Reason);
        Assert.Contains("ClockedOutAt", audit.OldValues!);
        Assert.False((await Cancel(eventType)).Success);
        Assert.Equal(1, await _db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task Cancelled_open_session_does_not_appear_in_daily_totals_or_block_a_new_session()
    {
        _session.State = AttendanceSessionState.Open;
        _session.ClockedOutAt = null;
        _db.Add(new AttendanceBreak { AttendanceSessionId = _session.Id, StartedAt = _session.ClockedInAt.AddHours(1) });
        await _db.SaveChangesAsync();
        Assert.True((await Cancel(AttendanceEventType.ClockIn)).Success);
        var controller = new NaderGorge.API.Controllers.HrAttendanceController(_db, null!);
        var report = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(await controller.DailyReport(_session.WorkDate, _session.WorkDate, CancellationToken.None));
        var json = System.Text.Json.JsonSerializer.Serialize(report.Value);
        Assert.Equal("[]", json);
        Assert.Single(await _db.AttendanceBreaks.ToListAsync());
        _db.Add(new AttendanceSession { EmployeeId = _employee.Id, ShiftAssignmentId = _session.ShiftAssignmentId,
            WorkDate = _session.WorkDate, ClockedInAt = _session.ClockedInAt.AddHours(2) });
        await _db.SaveChangesAsync();
        Assert.Single(await _db.AttendanceSessions.Where(x => x.State == AttendanceSessionState.Open).ToListAsync());
    }

    [Fact]
    public async Task Employee_cannot_cancel_even_their_own_session()
    {
        var result = await Cancel(AttendanceEventType.ClockIn, actor: _employee.UserId);
        Assert.False(result.Success);
        Assert.Contains("ADMIN_REQUIRED", result.Errors!);
        Assert.Equal(AttendanceSessionState.Completed, _session.State);
        Assert.Empty(await _db.AuditLogs.ToListAsync());
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("reason", 0)]
    public async Task Missing_reason_or_stale_version_does_not_change_attendance(string reason, int version)
    {
        Assert.False((await Cancel(AttendanceEventType.ClockIn, reason, version)).Success);
        Assert.Equal(450, _session.WorkedMinutes);
        Assert.Empty(await _db.AuditLogs.ToListAsync());
    }

    [Theory]
    [InlineData(AttendanceSessionState.Open)]
    [InlineData(AttendanceSessionState.Completed)]
    public async Task Checkout_cannot_reopen_before_a_later_session(AttendanceSessionState state)
    {
        _db.Add(new AttendanceSession { EmployeeId = _employee.Id, ShiftAssignmentId = _session.ShiftAssignmentId, WorkDate = _session.WorkDate.AddDays(1),
            ClockedInAt = _session.ClockedInAt.AddDays(1), State = state,
            ClockedOutAt = state == AttendanceSessionState.Completed ? _session.ClockedOutAt!.Value.AddDays(1) : null });
        await _db.SaveChangesAsync();
        var result = await Cancel(AttendanceEventType.ClockOut);
        Assert.Contains("ATTENDANCE_LATER_SESSION_EXISTS", result.Errors!);
        Assert.Equal(AttendanceSessionState.Completed, _session.State);
    }

    [Theory]
    [InlineData(AttendanceEventType.ClockIn)]
    [InlineData(AttendanceEventType.ClockOut)]
    public async Task Snapshotted_payroll_blocks_cancellation(AttendanceEventType eventType)
    {
        _db.Add(new EmployeePayroll { EmployeeId = _employee.Id, PayrollRun = new HrPayrollRun {
            RunNumber = "PAY-CANCEL", PeriodStart = _session.WorkDate, PeriodEnd = _session.WorkDate, Status = HrPayrollRunStatus.Prepared } });
        await _db.SaveChangesAsync();
        var result = await Cancel(eventType);
        Assert.Contains("ATTENDANCE_PAYROLL_LOCKED", result.Errors!);
        Assert.Equal(AttendanceSessionState.Completed, _session.State);
        Assert.Empty(await _db.AuditLogs.ToListAsync());
    }

    private Task<NaderGorge.Application.Common.ApiResponse<bool>> Cancel(AttendanceEventType eventType,
        string reason = "تسجيل بالخطأ", int version = 1, Guid? actor = null) =>
        new CancelAttendanceEventCommandHandler(_db).Handle(new(_session.Id, eventType, version, reason, actor ?? _admin.Id), CancellationToken.None);

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _connection.DisposeAsync(); }
}
