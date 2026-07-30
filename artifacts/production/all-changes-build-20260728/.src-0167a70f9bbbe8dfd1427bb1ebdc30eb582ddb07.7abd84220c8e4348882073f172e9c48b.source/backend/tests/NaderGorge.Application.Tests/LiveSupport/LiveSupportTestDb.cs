using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.LiveSupport;

internal sealed class LiveSupportTestDb : IAsyncDisposable
{
    private LiveSupportTestDb(AppDbContext db) => Db = db;
    public AppDbContext Db { get; }

    public static async Task<LiveSupportTestDb> CreateSeededAsync()
    {
        var db = TestAppDbContextFactory.Create();
        db.Users.AddRange(
            LiveSupportTestData.User(LiveSupportTestData.StudentId, "طالب الاختبار", "01000000142"),
            LiveSupportTestData.User(LiveSupportTestData.AdminId, "مدير الاختبار", "01000000143"),
            LiveSupportTestData.User(LiveSupportTestData.StaffAId, "دعم أ", "01000000144"),
            LiveSupportTestData.User(LiveSupportTestData.StaffBId, "دعم ب", "01000000145"));
        db.LiveSupportGuestSessions.Add(LiveSupportTestData.Guest());
        foreach (var staffId in new[] { LiveSupportTestData.StaffAId, LiveSupportTestData.StaffBId })
        {
            var employee = new EmployeeProfile { Id = Guid.NewGuid(), UserId = staffId, BasicSalary = 1 };
            var config = new LiveSupportStaffConfig { Id = Guid.NewGuid(), UserId = staffId, IsEnabled = true, MaxActiveConversations = 2, ConfiguredByUserId = LiveSupportTestData.AdminId, Version = 1 };
            db.EmployeeProfiles.Add(employee);
            db.AttendanceLogs.Add(new AttendanceLog { EmployeeId = employee.Id, Date = DateOnly.FromDateTime(DateTime.UtcNow), ClockIn = DateTime.UtcNow, Status = AttendanceStatus.Present, IpAddress = "test", UserAgent = "test" });
            db.LiveSupportStaffConfigs.Add(config);
            db.LiveSupportScheduleWindows.Add(new LiveSupportScheduleWindow { StaffConfigId = config.Id, DayOfWeek = (int)DateTime.UtcNow.DayOfWeek, StartLocalTime = new TimeOnly(8, 0), EndLocalTime = new TimeOnly(22, 0) });
        }
        var conversation = LiveSupportTestData.Conversation(LiveSupportTestData.StaffAId);
        db.LiveSupportConversations.Add(conversation);
        db.LiveSupportAssignments.Add(new LiveSupportAssignment { ConversationId = conversation.Id, StaffUserId = LiveSupportTestData.StaffAId, StartedAt = DateTime.UtcNow, AssignmentSequence = 1 });
        await db.SaveChangesAsync();
        return new LiveSupportTestDb(db);
    }

    public ValueTask DisposeAsync() => Db.DisposeAsync();
}
