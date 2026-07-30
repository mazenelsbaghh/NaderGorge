using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services;
using NaderGorge.Infrastructure.Data;
using StackExchange.Redis;

namespace NaderGorge.Integration.Tests.LiveSupport;

/// <summary>
/// Requires real PostgreSQL. The routing service is intentionally exercised with
/// separate database transactions and the production advisory lock; no EF InMemory
/// provider or fake routing implementation is allowed in this suite.
/// </summary>
public sealed class LiveSupportRoutingAcceptanceIntegrationTests
{
    [Fact]
    public async Task CapacityAndFifo_AreMaintained_WhenAConversationCloses()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var (staffA, staffB) = await SeedStaffAsync(fixture.Db, 1, 1);
        var service = new LiveSupportService(fixture.Db, new EnabledSettings());

        var conversations = new List<LiveSupportConversationDto>();
        for (var i = 0; i < 3; i++)
        {
            var student = NewUser($"fifo-student-{i}");
            fixture.Db.Users.Add(student);
            await fixture.Db.SaveChangesAsync();
            conversations.Add(await service.CreateConversationAsync(
                new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student.Id, null),
                $"fifo-{i}", null, CancellationToken.None));
        }

        Assert.Equal(2, await fixture.Db.LiveSupportAssignments.CountAsync(x => x.EndedAt == null));
        var queued = await fixture.Db.LiveSupportQueueEntries.SingleAsync(x => x.DequeuedAt == null);
        Assert.Equal(conversations[2].Id, queued.ConversationId);

        var firstOwner = (conversations[0].CurrentOwnerUserId ?? conversations[1].CurrentOwnerUserId).GetValueOrDefault();
        var firstConversation = conversations.Single(x => x.CurrentOwnerUserId == firstOwner);
        await service.CloseAsync(firstOwner, false, firstConversation.Id, "resolved", CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        var assignedQueued = await fixture.Db.LiveSupportConversations.SingleAsync(x => x.Id == conversations[2].Id);
        Assert.Equal(LiveSupportConversationStatus.Assigned, assignedQueued.Status);
        Assert.NotNull(assignedQueued.CurrentOwnerUserId);
        Assert.Equal(1, await fixture.Db.LiveSupportAssignments.CountAsync(x => x.EndedAt == null && x.StaffUserId == assignedQueued.CurrentOwnerUserId));
        Assert.Equal(0, await fixture.Db.LiveSupportQueueEntries.CountAsync(x => x.DequeuedAt == null));
        Assert.Contains(assignedQueued.CurrentOwnerUserId.Value, new[] { staffA, staffB });
    }

    [Fact]
    public async Task TransferToUnavailableTarget_RollsBackOwnershipAndQueueChanges()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var (owner, unavailable) = await SeedStaffAsync(fixture.Db, 1, 1);
        await fixture.Db.LiveSupportStaffConfigs.Where(x => x.UserId == unavailable).ExecuteUpdateAsync(x => x.SetProperty(p => p.IsEnabled, false));
        var student = NewUser("transfer-student");
        fixture.Db.Users.Add(student);
        await fixture.Db.SaveChangesAsync();
        var service = new LiveSupportService(fixture.Db, new EnabledSettings());
        var conversation = await service.CreateConversationAsync(new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student.Id, null), "transfer", null, CancellationToken.None);
        fixture.Db.ChangeTracker.Clear();
        var persistedOwner = await fixture.Db.LiveSupportConversations
            .Where(x => x.Id == conversation.Id)
            .Select(x => x.CurrentOwnerUserId)
            .SingleAsync();
        Assert.True(persistedOwner.HasValue);
        Assert.NotEqual(unavailable, persistedOwner.Value);

        var error = await Assert.ThrowsAsync<LiveSupportException>(() => service.TransferAsync(persistedOwner.Value, false, conversation.Id, unavailable, "unavailable target", CancellationToken.None));
        Assert.Equal("TARGET_UNAVAILABLE", error.Code);

        fixture.Db.ChangeTracker.Clear();
        var persisted = await fixture.Db.LiveSupportConversations.SingleAsync(x => x.Id == conversation.Id);
        Assert.Equal(persistedOwner.Value, persisted.CurrentOwnerUserId);
        Assert.Equal(LiveSupportConversationStatus.Assigned, persisted.Status);
        Assert.Equal(1, await fixture.Db.LiveSupportAssignments.CountAsync(x => x.ConversationId == conversation.Id && x.EndedAt == null && x.StaffUserId == persistedOwner.Value));
        Assert.Equal(0, await fixture.Db.LiveSupportQueueEntries.CountAsync(x => x.ConversationId == conversation.Id && x.DequeuedAt == null));
    }

    [Fact]
    public async Task LeastLoad_UsesTieRotation_WhenConnectedStaffHaveEqualCapacity()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        await using var redis = await OpenRedisAsync();
        var presence = new LiveSupportPresenceStore(redis);
        var (staffA, staffB) = await SeedStaffAsync(fixture.Db, 2, 2);
        await presence.ConnectedAsync(staffA, $"least-load-a-{Guid.NewGuid():N}");
        await presence.ConnectedAsync(staffB, $"least-load-b-{Guid.NewGuid():N}");

        var students = await SeedStudentsAsync(fixture.Db, "least-load", 4);
        var service = new LiveSupportService(fixture.Db, new EnabledSettings(), presence);
        var owners = new List<Guid>();
        foreach (var student in students)
        {
            var conversation = await service.CreateConversationAsync(
                new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student.Id, null),
                "least-load", null, CancellationToken.None);
            Assert.True(conversation.CurrentOwnerUserId.HasValue);
            owners.Add(conversation.CurrentOwnerUserId.Value);
        }

        Assert.Equal(2, owners.Count(x => x == staffA));
        Assert.Equal(2, owners.Count(x => x == staffB));
        Assert.NotEqual(owners[0], owners[1]);
        Assert.Equal(owners[0], owners[2]);
        Assert.Equal(owners[1], owners[3]);
    }

    [Fact]
    public async Task ConcurrentRequests_NeverExceedStaffCapacity_AndQueueOverflow()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        await using var redis = await OpenRedisAsync();
        var presence = new LiveSupportPresenceStore(redis);
        var (staffA, staffB) = await SeedStaffAsync(fixture.Db, 1, 1);
        await presence.ConnectedAsync(staffA, $"capacity-a-{Guid.NewGuid():N}");
        await presence.ConnectedAsync(staffB, $"capacity-b-{Guid.NewGuid():N}");

        var students = await SeedStudentsAsync(fixture.Db, "capacity", 4);
        var results = await Task.WhenAll(students.Select(student => CreateConversationWithNewContextAsync(
            fixture.ConnectionString, presence, student.Id)));

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(2, await fixture.Db.LiveSupportAssignments.CountAsync(x => x.EndedAt == null));
        Assert.Equal(2, await fixture.Db.LiveSupportQueueEntries.CountAsync(x => x.DequeuedAt == null));
        Assert.Equal(1, await fixture.Db.LiveSupportAssignments.CountAsync(x => x.EndedAt == null && x.StaffUserId == staffA));
        Assert.Equal(1, await fixture.Db.LiveSupportAssignments.CountAsync(x => x.EndedAt == null && x.StaffUserId == staffB));
        Assert.Equal(4, results.Length);
    }

    [Fact]
    public async Task DisconnectTimeout_ReleasesAssignments_AndRedistributesQueuedConversation()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        await using var redis = await OpenRedisAsync();
        var presence = new LiveSupportPresenceStore(redis);
        var (disconnectedStaff, availableStaff) = await SeedStaffAsync(fixture.Db, 1, 2);
        var disconnectedConnection = $"timeout-a-{Guid.NewGuid():N}";
        var availableConnection = $"timeout-b-{Guid.NewGuid():N}";
        await presence.ConnectedAsync(disconnectedStaff, disconnectedConnection);

        var firstStudent = (await SeedStudentsAsync(fixture.Db, "timeout-first", 1)).Single();
        var service = new LiveSupportService(fixture.Db, new EnabledSettings(), presence);
        var firstConversation = await service.CreateConversationAsync(
            new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, firstStudent.Id, null),
            "timeout-first", null, CancellationToken.None);
        Assert.Equal(disconnectedStaff, firstConversation.CurrentOwnerUserId);

        await presence.ConnectedAsync(availableStaff, availableConnection);
        var secondStudent = (await SeedStudentsAsync(fixture.Db, "timeout-second", 1)).Single();
        var secondConversation = await service.CreateConversationAsync(
            new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, secondStudent.Id, null),
            "timeout-second", null, CancellationToken.None);
        Assert.Equal(availableStaff, secondConversation.CurrentOwnerUserId);

        await presence.DisconnectedAsync(disconnectedStaff, disconnectedConnection);
        var claimed = await presence.ClaimExpiredDisconnectsAsync(DateTime.UtcNow.AddMinutes(3));
        Assert.Contains(disconnectedStaff, claimed);

        await service.ReleaseStaffAssignmentsAsync(disconnectedStaff, LiveSupportAssignmentEndReason.DisconnectTimeout, CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        var redistributed = await fixture.Db.LiveSupportConversations.SingleAsync(x => x.Id == firstConversation.Id);
        Assert.Equal(LiveSupportConversationStatus.Assigned, redistributed.Status);
        Assert.Equal(availableStaff, redistributed.CurrentOwnerUserId);
        Assert.Equal(0, await fixture.Db.LiveSupportQueueEntries.CountAsync(x => x.ConversationId == firstConversation.Id && x.DequeuedAt == null));
        Assert.Contains(
            await fixture.Db.LiveSupportAssignments
                .Where(x => x.ConversationId == firstConversation.Id)
                .Select(x => x.EndReason)
                .ToListAsync(),
            x => x == LiveSupportAssignmentEndReason.DisconnectTimeout);
    }

    [Fact]
    public async Task StudentContext_IsRestrictedToCurrentOwner_AndRequiresLinkedStudent()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var (owner, other) = await SeedStaffAsync(fixture.Db, 2, 2);
        var student = NewUser("context-student");
        fixture.Db.Users.Add(student);
        await fixture.Db.SaveChangesAsync();
        var conversation = new LiveSupportConversation
        {
            ParticipantType = LiveSupportParticipantType.Student,
            StudentUserId = student.Id,
            LinkedStudentUserId = student.Id,
            Status = LiveSupportConversationStatus.Assigned,
            CurrentOwnerUserId = owner,
            Version = 1
        };
        fixture.Db.LiveSupportConversations.Add(conversation);
        await fixture.Db.SaveChangesAsync();
        var service = new LiveSupportService(fixture.Db, new EnabledSettings());

        var context = await service.GetStudentContextAsync(owner, false, conversation.Id, CancellationToken.None);
        Assert.Equal(student.Id, context.UserId);
        var forbidden = await Assert.ThrowsAsync<LiveSupportException>(() => service.GetStudentContextAsync(other, false, conversation.Id, CancellationToken.None));
        Assert.Equal(LiveSupportErrorCodes.Forbidden, forbidden.Code);

        conversation.LinkedStudentUserId = null;
        await fixture.Db.SaveChangesAsync();
        var unlinked = await Assert.ThrowsAsync<LiveSupportException>(() => service.GetStudentContextAsync(owner, false, conversation.Id, CancellationToken.None));
        Assert.Equal("STUDENT_NOT_LINKED", unlinked.Code);
    }

    [Fact]
    public async Task PresenceStore_TracksMultipleConnections_AndClaimsDisconnectAfterTwoMinutes()
    {
        var redisConnection = Environment.GetEnvironmentVariable("ConnectionStrings__Redis")
            ?? Environment.GetEnvironmentVariable("REDIS_URL")
            ?? "localhost:6379";
        ConnectionMultiplexer redis;
        try
        {
            redis = await ConnectionMultiplexer.ConnectAsync(redisConnection);
            if (!redis.IsConnected) throw new InvalidOperationException("Redis connection is not active.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Redis integration blocker: {ex.GetType().Name}: {ex.Message}", ex);
        }

        await using (redis)
        {
            var store = new LiveSupportPresenceStore(redis);
            var staffId = Guid.NewGuid();
            var connectionA = $"acceptance-a-{Guid.NewGuid():N}";
            var connectionB = $"acceptance-b-{Guid.NewGuid():N}";
            await store.ConnectedAsync(staffId, connectionA);
            await store.ConnectedAsync(staffId, connectionB);
            Assert.True(await store.IsConnectedAsync(staffId));
            await store.DisconnectedAsync(staffId, connectionA);
            Assert.True(await store.IsConnectedAsync(staffId));
            await store.DisconnectedAsync(staffId, connectionB);
            Assert.False(await store.IsConnectedAsync(staffId));
            var claimed = await store.ClaimExpiredDisconnectsAsync(DateTime.UtcNow.AddMinutes(3));
            Assert.Contains(staffId, claimed);
        }
    }

    private static async Task<(Guid A, Guid B)> SeedStaffAsync(AppDbContext db, int capacityA, int capacityB)
    {
        var a = NewUser($"staff-a-{Guid.NewGuid():N}");
        var b = NewUser($"staff-b-{Guid.NewGuid():N}");
        db.Users.AddRange(a, b);
        await db.SaveChangesAsync();
        foreach (var (user, capacity) in new[] { (a, capacityA), (b, capacityB) })
        {
            var employee = new EmployeeProfile { UserId = user.Id, BasicSalary = 1 };
            db.EmployeeProfiles.Add(employee);
            db.AttendanceLogs.Add(new AttendanceLog { EmployeeId = employee.Id, Date = DateOnly.FromDateTime(DateTime.UtcNow), ClockIn = DateTime.UtcNow, Status = AttendanceStatus.Present, IpAddress = "integration", UserAgent = "integration" });
            db.LiveSupportStaffConfigs.Add(new LiveSupportStaffConfig { UserId = user.Id, IsEnabled = true, MaxActiveConversations = capacity, ConfiguredByUserId = user.Id, Version = 1 });
        }
        await db.SaveChangesAsync();
        return (a.Id, b.Id);
    }

    private static async Task<List<User>> SeedStudentsAsync(AppDbContext db, string prefix, int count)
    {
        var students = Enumerable.Range(0, count).Select(i => NewUser($"{prefix}-{i}-{Guid.NewGuid():N}")).ToList();
        db.Users.AddRange(students);
        await db.SaveChangesAsync();
        return students;
    }

    private static async Task<LiveSupportConversationDto> CreateConversationWithNewContextAsync(
        string connectionString,
        ILiveSupportPresenceStore presence,
        Guid studentId)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);
        var service = new LiveSupportService(db, new EnabledSettings(), presence);
        return await service.CreateConversationAsync(
            new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, studentId, null),
            "concurrent-capacity", null, CancellationToken.None);
    }

    private static async Task<ConnectionMultiplexer> OpenRedisAsync()
    {
        var redisConnection = Environment.GetEnvironmentVariable("ConnectionStrings__Redis")
            ?? Environment.GetEnvironmentVariable("REDIS_URL")
            ?? "localhost:6379";
        try
        {
            var redis = await ConnectionMultiplexer.ConnectAsync(redisConnection);
            if (!redis.IsConnected) throw new InvalidOperationException("Redis connection is not active.");
            return redis;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Redis integration blocker: {ex.GetType().Name}: {ex.Message}", ex);
        }
    }

    private static User NewUser(string prefix) => new() { FullName = prefix, PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}", PasswordHash = "integration" };

    private sealed class EnabledSettings : ICachedPlatformSettingsReader
    {
        public Task<CachedPlatformSettings> GetAsync(CancellationToken cancellationToken) => Task.FromResult(CachedPlatformSettings.Default with { LiveSupportEnabled = true });
        public void Invalidate() { }
    }
}
