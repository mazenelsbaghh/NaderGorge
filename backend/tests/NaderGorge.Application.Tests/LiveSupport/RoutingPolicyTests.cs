using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class RoutingPolicyTests
{
    [Fact]
    public async Task CapacityAndFifo_ArePreserved_WhenAnOwnerCloses()
    {
        await using var db = TestAppDbContextFactory.Create();
        var staffA = await SeedStaffAsync(db, "A", 1);
        var staffB = await SeedStaffAsync(db, "B", 1);
        var service = new LiveSupportService(db, new EnabledSettingsReader());
        var conversations = new List<LiveSupportConversationDto>();
        for (var i = 0; i < 3; i++)
        {
            var student = await TestAppDbContextFactory.SeedUserAsync(db, $"Student {i}", $"0100000000{i}");
            conversations.Add(await service.CreateConversationAsync(new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, student.Id, null), $"C{i}", null, CancellationToken.None));
        }

        Assert.Equal(2, await db.LiveSupportAssignments.CountAsync(x => x.EndedAt == null));
        Assert.Equal(1, await db.LiveSupportQueueEntries.CountAsync(x => x.DequeuedAt == null));
        Assert.All(await db.LiveSupportAssignments.Where(x => x.EndedAt == null).GroupBy(x => x.StaffUserId).Select(x => x.Count()).ToListAsync(), load => Assert.Equal(1, load));

        var first = conversations.First(x => x.CurrentOwnerUserId.HasValue);
        await service.CloseAsync(first.CurrentOwnerUserId!.Value, false, first.Id, "resolved", CancellationToken.None);

        Assert.Equal(2, await db.LiveSupportAssignments.CountAsync(x => x.EndedAt == null));
        Assert.Equal(0, await db.LiveSupportQueueEntries.CountAsync(x => x.DequeuedAt == null));
        Assert.Contains(await db.LiveSupportAssignments.Where(x => x.EndedAt == null).Select(x => x.StaffUserId).ToListAsync(), id => id == staffA || id == staffB);
    }

    private static async Task<Guid> SeedStaffAsync(NaderGorge.Infrastructure.Data.AppDbContext db, string name, int capacity)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, $"Support {name}", $"0111111111{(name == "A" ? 1 : 2)}");
        var employee = new EmployeeProfile { UserId = user.Id, BasicSalary = 1 };
        db.EmployeeProfiles.Add(employee);
        db.LiveSupportStaffConfigs.Add(new LiveSupportStaffConfig { UserId = user.Id, IsEnabled = true, MaxActiveConversations = capacity, ConfiguredByUserId = user.Id });
        db.AttendanceLogs.Add(new AttendanceLog { EmployeeId = employee.Id, ClockIn = DateTime.UtcNow, Date = DateOnly.FromDateTime(DateTime.UtcNow), Status = AttendanceStatus.Present, IpAddress = "tests", UserAgent = "tests" });
        await db.SaveChangesAsync();
        return user.Id;
    }

    private sealed class EnabledSettingsReader : ICachedPlatformSettingsReader
    {
        public Task<CachedPlatformSettings> GetAsync(CancellationToken cancellationToken) => Task.FromResult(CachedPlatformSettings.Default with { LiveSupportEnabled = true });
        public void Invalidate() { }
    }
}
