using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.HR.Attendance;
using NaderGorge.Application.Features.HR.Attendance.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using Microsoft.EntityFrameworkCore.Metadata;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupport.Dtos;

namespace NaderGorge.Application.Tests.HR;

public sealed class AttendancePolicyTests
{
    [Fact]
    public void ConcurrencyModel_EnforcesOneOpenSessionAndUniqueEventReplay()
    {
        using var db = TestAppDbContextFactory.Create();
        var session = db.Model.FindEntityType(typeof(AttendanceSession))!;
        var attempt = db.Model.FindEntityType(typeof(AttendanceAttempt))!;
        Assert.Contains(session.GetIndexes(), index => index.IsUnique && index.Properties.Select(item => item.Name).SequenceEqual([nameof(AttendanceSession.EmployeeId)]));
        Assert.Contains(attempt.GetIndexes(), index => index.IsUnique && index.Properties.Select(item => item.Name).SequenceEqual([nameof(AttendanceAttempt.EmployeeId), nameof(AttendanceAttempt.EventType), nameof(AttendanceAttempt.IdempotencyKey)]));
    }

    [Theory]
    [InlineData(AttendancePolicyKind.Unrestricted, 30.0444, 31.2357, null, true, "ATTENDANCE_ACCEPTED")]
    [InlineData(AttendancePolicyKind.Geofence, 30.0445, 31.2358, null, true, "ATTENDANCE_ACCEPTED")]
    [InlineData(AttendancePolicyKind.Geofence, 30.1000, 31.3000, null, false, "OUTSIDE_GEOFENCE")]
    [InlineData(AttendancePolicyKind.TrustedDevice, null, null, "trusted-token", true, "ATTENDANCE_ACCEPTED")]
    [InlineData(AttendancePolicyKind.TrustedDevice, null, null, "other-token", false, "DEVICE_NOT_TRUSTED")]
    public async Task Evaluator_EnforcesThreePolicyModes(AttendancePolicyKind kind, double? lat, double? lon, string? token, bool accepted, string code)
    {
        await using var db = TestAppDbContextFactory.Create();
        var seeded = await SeedAsync(db, kind);
        if (kind == AttendancePolicyKind.TrustedDevice)
        {
            db.TrustedAttendanceDevices.Add(new TrustedAttendanceDevice { EmployeeId = seeded.Employee.Id, Name = "phone", TokenHash = Hash("trusted-token"), ApprovedByUserId = seeded.User.Id });
            await db.SaveChangesAsync();
        }
        var result = await new AttendancePolicyEvaluator(db).EvaluateAsync(new AttendanceEvaluationInput(
            seeded.Employee.Id, seeded.Template.Id, DateTime.UtcNow, lat, lon, 15, token), default);
        Assert.Equal(accepted, result.Accepted);
        Assert.Equal(code, result.Code);
    }

    [Fact]
    public async Task RemoteException_OverridesGeofenceRejection()
    {
        await using var db = TestAppDbContextFactory.Create();
        var seeded = await SeedAsync(db, AttendancePolicyKind.Geofence);
        db.AttendancePolicyExceptions.Add(new AttendancePolicyException
        {
            EmployeeId = seeded.Employee.Id, AllowRemote = true, StartsAt = DateTime.UtcNow.AddHours(-1),
            EndsAt = DateTime.UtcNow.AddHours(1), Reason = "remote day", ApprovedByUserId = seeded.User.Id
        });
        await db.SaveChangesAsync();
        var result = await new AttendancePolicyEvaluator(db).EvaluateAsync(new AttendanceEvaluationInput(
            seeded.Employee.Id, seeded.Template.Id, DateTime.UtcNow, 31, 32, 10, null), default);
        Assert.True(result.Accepted);
        Assert.Equal("REMOTE_EXCEPTION", result.Code);
    }

    [Fact]
    public async Task Evaluator_AllowsPublishedShiftWhenNoPolicyIsConfigured()
    {
        await using var db = TestAppDbContextFactory.Create();
        var seeded = await SeedAsync(db, AttendancePolicyKind.Unrestricted);
        db.AttendancePolicyAssignments.RemoveRange(db.AttendancePolicyAssignments);
        await db.SaveChangesAsync();

        var result = await new AttendancePolicyEvaluator(db).EvaluateAsync(new AttendanceEvaluationInput(
            seeded.Employee.Id, seeded.Template.Id, DateTime.UtcNow, null, null, null, null), default);

        Assert.True(result.Accepted);
        Assert.Equal("ATTENDANCE_ACCEPTED", result.Code);
        Assert.Null(result.PolicyId);
        Assert.Equal("shift-default", result.Source);
    }

    [Fact]
    public async Task ClockInReplay_CreatesOneAcceptedAttemptAndOneSession()
    {
        await using var db = TestAppDbContextFactory.Create();
        var seeded = await SeedAsync(db, AttendancePolicyKind.Unrestricted);
        var command = new ClockInAttendanceCommand(seeded.User.Id, "clock-replay", DateTime.UtcNow, null, null, null, null, "127.0.0.1", "test");
        var handler = new ClockInAttendanceCommandHandler(db, new AttendancePolicyEvaluator(db));
        var first = await handler.Handle(command, default);
        var replay = await handler.Handle(command, default);
        Assert.True(first.Success); Assert.True(replay.Success); Assert.Equal(first.Data!.SessionId, replay.Data!.SessionId);
        Assert.Single(await db.AttendanceSessions.ToListAsync());
        Assert.Single(await db.AttendanceAttempts.ToListAsync());
    }

    [Fact]
    public async Task RejectedAttempt_IsRecordedWithoutSession()
    {
        await using var db = TestAppDbContextFactory.Create();
        var seeded = await SeedAsync(db, AttendancePolicyKind.Geofence);
        var result = await new ClockInAttendanceCommandHandler(db, new AttendancePolicyEvaluator(db)).Handle(
            new ClockInAttendanceCommand(seeded.User.Id, "outside", DateTime.UtcNow, 31, 32, 10, null, "ip", "ua"), default);
        Assert.False(result.Success); Assert.Contains("OUTSIDE_GEOFENCE", result.Errors!);
        Assert.Single(await db.AttendanceAttempts.Where(item => !item.Accepted).ToListAsync());
        Assert.Empty(await db.AttendanceSessions.ToListAsync());
    }

    [Fact]
    public async Task AcceptedClockIn_InvokesLiveSupportAssignmentCoordinator()
    {
        await using var db = TestAppDbContextFactory.Create();
        var seeded = await SeedAsync(db, AttendancePolicyKind.Unrestricted);
        var coordinator = new CoordinatorSpy();
        var result = await new ClockInAttendanceCommandHandler(db, new AttendancePolicyEvaluator(db), coordinator: coordinator).Handle(
            new ClockInAttendanceCommand(seeded.User.Id, "live-support", DateTime.UtcNow, null, null, null, null, "ip", "ua"), default);
        Assert.True(result.Success);
        Assert.Equal(1, coordinator.AssignCalls);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static async Task<(User User, EmployeeProfile Employee, ShiftTemplate Template)> SeedAsync(NaderGorge.Infrastructure.Data.AppDbContext db, AttendancePolicyKind kind)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, "Attendance Employee", $"0105{Random.Shared.Next(1000000, 9999999)}");
        var employee = new EmployeeProfile { UserId = user.Id, User = user }; employee.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(employee.Id);
        var calendar = new WorkCalendar { Code = Guid.NewGuid().ToString("N")[..8], Name = "Calendar" };
        var template = new ShiftTemplate { Code = Guid.NewGuid().ToString("N")[..8], Name = "Shift", WorkCalendarId = calendar.Id, WorkCalendar = calendar };
        template.Segments.Add(new ShiftSegment { ShiftTemplateId = template.Id, Sequence = 1, StartsAt = TimeSpan.Zero, EndsAt = TimeSpan.FromHours(23.99) });
        var policy = new AttendancePolicy { Code = Guid.NewGuid().ToString("N")[..8], Name = "Policy", Kind = kind, Latitude = 30.0444m, Longitude = 31.2357m, RadiusMeters = 300, MaximumAccuracyMeters = 100 };
        db.EmployeeProfiles.Add(employee); db.WorkCalendars.Add(calendar); db.ShiftTemplates.Add(template); db.AttendancePolicies.Add(policy);
        db.AttendancePolicyAssignments.Add(new AttendancePolicyAssignment { AttendancePolicyId = policy.Id, EmployeeId = employee.Id, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1) });
        db.ShiftAssignments.Add(new ShiftAssignment { EmployeeId = employee.Id, ShiftTemplateId = template.Id, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), Status = ShiftAssignmentStatus.Published, PublishedByUserId = user.Id, Reason = "test" });
        await db.SaveChangesAsync(); return (user, employee, template);
    }

    private sealed class CoordinatorSpy : ILiveSupportAssignmentCoordinator
    {
        public int AssignCalls { get; private set; }
        public Task AssignWaitingAsync(CancellationToken ct) { AssignCalls++; return Task.CompletedTask; }
        public Task ReleaseStaffAssignmentsAsync(Guid staffUserId, LiveSupportAssignmentEndReason reason, CancellationToken ct) => Task.CompletedTask;
        public Task<LiveSupportConversationDto> TransferAsync(Guid actorUserId, bool isAdmin, Guid conversationId, Guid? targetStaffUserId, string reason, CancellationToken ct) => throw new NotSupportedException();
    }
}
