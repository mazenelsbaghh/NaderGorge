using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class StudentActionTests
{
    [Fact]
    public async Task BalanceAdjustment_ReplaysSameRequest_AndRejectsChangedPayload()
    {
        await using var fixture = await ActionFixture.CreateAsync();
        var definition = (await fixture.Actions.GetCatalogAsync(fixture.StaffId, false, fixture.ConversationId, CancellationToken.None)).Single(x => x.Key == "student.balance.adjust");
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = fixture.Request(definition, idempotencyKey, "{\"amount\":100,\"reason\":\"تعويض موثق\"}");

        var first = await fixture.Actions.ExecuteAsync(request, CancellationToken.None);
        var replay = await fixture.Actions.ExecuteAsync(request, CancellationToken.None);
        var conflict = fixture.Request(definition, idempotencyKey, "{\"amount\":200,\"reason\":\"تعويض موثق\"}");

        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        Assert.Equal(100, await fixture.Db.StudentBalances.Where(x => x.UserId == fixture.StudentId).Select(x => x.CurrentBalance).SingleAsync());
        Assert.Equal(1, await fixture.Db.BalanceTransactions.CountAsync());
        var error = await Assert.ThrowsAsync<LiveSupportException>(() => fixture.Actions.ExecuteAsync(conflict, CancellationToken.None));
        Assert.Equal("IDEMPOTENCY_CONFLICT", error.Code);
    }

    [Fact]
    public async Task BalanceAdjustment_RejectsStaleConfirmationAfterExternalBalanceChange()
    {
        await using var fixture = await ActionFixture.CreateAsync();
        var definition = (await fixture.Actions.GetCatalogAsync(fixture.StaffId, false, fixture.ConversationId, CancellationToken.None)).Single(x => x.Key == "student.balance.adjust");
        fixture.Db.StudentBalances.Add(new StudentBalance { UserId = fixture.StudentId, CurrentBalance = 50 });
        await fixture.Db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<LiveSupportException>(() => fixture.Actions.ExecuteAsync(fixture.Request(definition, Guid.NewGuid().ToString(), "{\"amount\":10,\"reason\":\"تعويض\"}"), CancellationToken.None));

        Assert.Equal("CONFIRMATION_STALE", error.Code);
        Assert.Equal(50, await fixture.Db.StudentBalances.Where(x => x.UserId == fixture.StudentId).Select(x => x.CurrentBalance).SingleAsync());
    }

    [Fact]
    public async Task DeviceDisconnect_RejectsDeviceOwnedByDifferentStudent()
    {
        await using var fixture = await ActionFixture.CreateAsync();
        var other = await TestAppDbContextFactory.SeedUserAsync(fixture.Db, "Other", "01077777777");
        var device = new Device { UserId = other.Id, DeviceFingerprint = "other-device", IsActive = true };
        fixture.Db.Devices.Add(device);
        await fixture.Db.SaveChangesAsync();
        var definition = (await fixture.Actions.GetCatalogAsync(fixture.StaffId, false, fixture.ConversationId, CancellationToken.None)).Single(x => x.Key == "student.device.disconnect");
        var payload = JsonSerializer.Serialize(new { deviceId = device.Id, reason = "طلب الطالب" });

        var error = await Assert.ThrowsAsync<LiveSupportException>(() => fixture.Actions.ExecuteAsync(fixture.Request(definition, Guid.NewGuid().ToString(), payload), CancellationToken.None));

        Assert.Equal("ACTION_VALIDATION_FAILED", error.Code);
        Assert.True(await fixture.Db.Devices.AnyAsync(x => x.Id == device.Id));
    }

    [Fact]
    public async Task ActionRejectsMissingLinkWrongOwnerAndCheckedOutStaff()
    {
        await using var fixture = await ActionFixture.CreateAsync();
        var definition = (await fixture.Actions.GetCatalogAsync(fixture.StaffId, false, fixture.ConversationId, CancellationToken.None)).Single(x => x.Key == "student.balance.adjust");
        var conversation = await fixture.Db.LiveSupportConversations.SingleAsync(x => x.Id == fixture.ConversationId);
        conversation.LinkedStudentUserId = null;
        await fixture.Db.SaveChangesAsync();
        var noLink = await Assert.ThrowsAsync<LiveSupportException>(() => fixture.Actions.ExecuteAsync(fixture.Request(definition, Guid.NewGuid().ToString(), "{\"amount\":1,\"reason\":\"test reason\"}"), CancellationToken.None));
        Assert.Equal("STUDENT_NOT_LINKED", noLink.Code);

        conversation.LinkedStudentUserId = fixture.StudentId;
        conversation.CurrentOwnerUserId = Guid.NewGuid();
        await fixture.Db.SaveChangesAsync();
        var wrongOwner = await Assert.ThrowsAsync<LiveSupportException>(() => fixture.Actions.ExecuteAsync(fixture.Request(definition, Guid.NewGuid().ToString(), "{\"amount\":1,\"reason\":\"test reason\"}"), CancellationToken.None));
        Assert.Equal(LiveSupportErrorCodes.Forbidden, wrongOwner.Code);

        conversation.CurrentOwnerUserId = fixture.StaffId;
        var attendance = await fixture.Db.AttendanceLogs.SingleAsync(x => x.ClockOut == null);
        attendance.ClockOut = DateTime.UtcNow;
        await fixture.Db.SaveChangesAsync();
        var checkedOut = await Assert.ThrowsAsync<LiveSupportException>(() => fixture.Actions.ExecuteAsync(fixture.Request(definition, Guid.NewGuid().ToString(), "{\"amount\":1,\"reason\":\"test reason\"}"), CancellationToken.None));
        Assert.Equal(LiveSupportErrorCodes.Forbidden, checkedOut.Code);
    }

    [Fact]
    public async Task PasswordPayloadIsRedactedFromExecutionAudit()
    {
        await using var fixture = await ActionFixture.CreateAsync();
        var definition = (await fixture.Actions.GetCatalogAsync(fixture.StaffId, false, fixture.ConversationId, CancellationToken.None)).Single(x => x.Key == "student.password.reset");
        await fixture.Actions.ExecuteAsync(fixture.Request(definition, Guid.NewGuid().ToString(), "{\"newPassword\":\"NeverLogThis123!\"}"), CancellationToken.None);
        var safe = await fixture.Db.LiveSupportActionExecutions.Select(x => x.SafeRequestJson).SingleAsync();
        Assert.DoesNotContain("NeverLogThis", safe);
        Assert.Contains("secretRedacted", safe);
    }

    private sealed class ActionFixture : IAsyncDisposable
    {
        private ActionFixture(AppDbContext db, LiveSupportActionService actions, Guid staffId, Guid studentId, Guid conversationId) => (Db, Actions, StaffId, StudentId, ConversationId) = (db, actions, staffId, studentId, conversationId);
        public AppDbContext Db { get; }
        public LiveSupportActionService Actions { get; }
        public Guid StaffId { get; }
        public Guid StudentId { get; }
        public Guid ConversationId { get; }

        public static async Task<ActionFixture> CreateAsync()
        {
            var db = TestAppDbContextFactory.Create();
            var staff = await TestAppDbContextFactory.SeedUserAsync(db, "Support", "01011111111");
            var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student", "01022222222");
            var employee = new EmployeeProfile { UserId = staff.Id, BasicSalary = 1 };
            db.EmployeeProfiles.Add(employee);
            db.LiveSupportStaffConfigs.Add(new LiveSupportStaffConfig { UserId = staff.Id, IsEnabled = true, MaxActiveConversations = 2, ConfiguredByUserId = staff.Id });
            db.AttendanceLogs.Add(new AttendanceLog { EmployeeId = employee.Id, ClockIn = DateTime.UtcNow, Date = DateOnly.FromDateTime(DateTime.UtcNow), Status = AttendanceStatus.Present, IpAddress = "tests", UserAgent = "tests" });
            await db.SaveChangesAsync();
            var presence = new ConnectedPresenceStore();
            var support = new LiveSupportService(db, new EnabledSettingsReader(), presence);
            var conversation = await support.CreateConversationAsync(new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student.Id, null), "Action", null, CancellationToken.None);
            var services = new ServiceCollection().AddSingleton<IAppDbContext>(db).AddMediatR(config => config.RegisterServicesFromAssembly(typeof(ApiResponse).Assembly)).BuildServiceProvider();
            return new ActionFixture(db, new LiveSupportActionService(db, services.GetRequiredService<IMediator>(), support, presence), staff.Id, student.Id, conversation.Id);
        }

        public LiveSupportActionRequest Request(LiveSupportActionDefinitionDto definition, string key, string json) => new(StaffId, false, ConversationId, definition.Key, key, definition.ConfirmationVersion, JsonDocument.Parse(json).RootElement.Clone());
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class ConnectedPresenceStore : ILiveSupportPresenceStore
    {
        public Task ConnectedAsync(Guid staffUserId, string connectionId) => Task.CompletedTask;
        public Task DisconnectedAsync(Guid staffUserId, string connectionId) => Task.CompletedTask;
        public Task HeartbeatAsync(Guid staffUserId) => Task.CompletedTask;
        public Task<bool> IsConnectedAsync(Guid staffUserId) => Task.FromResult(true);
        public Task<IReadOnlyList<Guid>> ClaimExpiredDisconnectsAsync(DateTime utcNow) => Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    private sealed class EnabledSettingsReader : ICachedPlatformSettingsReader
    {
        public Task<CachedPlatformSettings> GetAsync(CancellationToken cancellationToken) => Task.FromResult(CachedPlatformSettings.Default with { LiveSupportEnabled = true });
        public void Invalidate() { }
    }
}
