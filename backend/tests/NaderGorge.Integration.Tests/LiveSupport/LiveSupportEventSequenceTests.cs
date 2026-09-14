using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class LiveSupportEventSequenceTests
{
    [Fact]
    public async Task Incident20260912_ConcurrentEqualTimestampsSaveDistinctEventsAndMatchingOutboxCursors()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var student = new User { FullName = "Sequence regression", PhoneNumber = "01" + Random.Shared.NextInt64(100000000, 999999999), PasswordHash = "test" };
        fixture.Db.Users.Add(student);
        var conversation = new LiveSupportConversation { ParticipantType = LiveSupportParticipantType.Student, StudentUserId = student.Id, Status = LiveSupportConversationStatus.Waiting, Version = 1 };
        fixture.Db.LiveSupportConversations.Add(conversation);
        await fixture.Db.SaveChangesAsync();
        var timestamp = DateTime.UtcNow;

        await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(fixture.ConnectionString).Options);
            var supportEvent = new LiveSupportEvent { ConversationId = conversation.Id, Sequence = timestamp.Ticks, OccurredAt = timestamp, Type = LiveSupportEventType.MessageSent };
            db.LiveSupportEvents.Add(supportEvent);
            db.OutboxEvents.Add(new OutboxEvent { Type = "LiveSupportEvent", TargetGroup = "LiveSupport:Admins",
                PayloadJson = JsonSerializer.Serialize(new { eventId = supportEvent.Id, conversationId = conversation.Id, sequence = timestamp.Ticks }) });
            await db.SaveChangesAsync();
        }));

        var events = await fixture.Db.LiveSupportEvents.Where(e => e.ConversationId == conversation.Id).ToDictionaryAsync(e => e.Id);
        Assert.Equal(8, events.Count);
        Assert.Equal(8, events.Values.Select(e => e.Sequence).Distinct().Count());
        var outboxes = await fixture.Db.OutboxEvents.Where(e => e.Type == "LiveSupportEvent").ToListAsync();
        Assert.Equal(8, outboxes.Count);
        foreach (var outbox in outboxes)
        {
            using var payload = JsonDocument.Parse(outbox.PayloadJson);
            Assert.Equal(events[payload.RootElement.GetProperty("eventId").GetGuid()].Sequence,
                payload.RootElement.GetProperty("sequence").GetInt64());
        }
    }
}
