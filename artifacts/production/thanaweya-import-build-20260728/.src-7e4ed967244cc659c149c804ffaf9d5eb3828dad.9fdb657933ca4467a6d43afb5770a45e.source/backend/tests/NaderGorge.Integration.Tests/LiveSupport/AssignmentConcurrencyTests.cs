using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class LiveSupportAssignmentConcurrencyTests
{
    [Fact]
    public async Task SimultaneousOwners_AreRejectedByPostgresFilteredUniqueIndex()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var student = NewUser("student");
        var staffA = NewUser("staff-a");
        var staffB = NewUser("staff-b");
        fixture.Db.Users.AddRange(student, staffA, staffB);
        var conversation = new LiveSupportConversation { ParticipantType = LiveSupportParticipantType.Student, StudentUserId = student.Id, LinkedStudentUserId = student.Id, Status = LiveSupportConversationStatus.Assigned, CurrentOwnerUserId = staffA.Id, Version = 1 };
        fixture.Db.LiveSupportConversations.Add(conversation);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.LiveSupportAssignments.AddRange(
            new LiveSupportAssignment { ConversationId = conversation.Id, StaffUserId = staffA.Id, StartedAt = DateTime.UtcNow, AssignmentSequence = 1 },
            new LiveSupportAssignment { ConversationId = conversation.Id, StaffUserId = staffB.Id, StartedAt = DateTime.UtcNow, AssignmentSequence = 2 });
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => fixture.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task RandomizedParallelQueueWrites_PreserveSingleActiveEntry()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var student = NewUser("queue-student");
        fixture.Db.Users.Add(student);
        var conversation = new LiveSupportConversation { ParticipantType = LiveSupportParticipantType.Student, StudentUserId = student.Id, LinkedStudentUserId = student.Id, Status = LiveSupportConversationStatus.Waiting, Version = 1 };
        fixture.Db.LiveSupportConversations.Add(conversation);
        await fixture.Db.SaveChangesAsync();
        await Parallel.ForEachAsync(Enumerable.Range(0, 12), async (i, ct) =>
        {
            await using var db = new NaderGorge.Infrastructure.Data.AppDbContext(new DbContextOptionsBuilder<NaderGorge.Infrastructure.Data.AppDbContext>().UseNpgsql(fixture.ConnectionString).Options);
            db.LiveSupportQueueEntries.Add(new LiveSupportQueueEntry { ConversationId = conversation.Id, EnteredAt = DateTime.UtcNow, Sequence = i + 1 });
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { }
        });
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(1, await fixture.Db.LiveSupportQueueEntries.CountAsync(x => x.ConversationId == conversation.Id && x.DequeuedAt == null));
    }

    private static User NewUser(string prefix) => new() { FullName = prefix, PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}", PasswordHash = "integration" };
}
