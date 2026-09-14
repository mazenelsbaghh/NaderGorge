using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class WhatsAppThreadPostgresTests
{
    private const string WhatsAppUserId = "201099999999";

    [Fact]
    public async Task ContactThread_PaginatesPostgresUuidAndEnforcesStaffAndAdminScopes()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var staffA = User("موظف أول");
        var staffB = User("موظف ثان");
        var contactGuest = Guest("01099999999");
        var otherOpenGuest = Guest("01099999999");
        var otherContactGuest = Guest("01088888888");
        var oldConversation = Conversation(contactGuest.Id, LiveSupportConversationStatus.Closed, null);
        oldConversation.ClosedAt = DateTime.UtcNow.AddDays(-1);
        var currentConversation = Conversation(contactGuest.Id, LiveSupportConversationStatus.Assigned, staffA.Id);
        var otherOpenConversation = Conversation(otherOpenGuest.Id, LiveSupportConversationStatus.Assigned, staffB.Id);
        var otherContactConversation = Conversation(otherContactGuest.Id, LiveSupportConversationStatus.Closed, null);
        otherContactConversation.ClosedAt = DateTime.UtcNow.AddDays(-1);
        fixture.Db.Users.AddRange(staffA, staffB);
        fixture.Db.LiveSupportGuestSessions.AddRange(contactGuest, otherOpenGuest, otherContactGuest);
        fixture.Db.LiveSupportConversations.AddRange(
            oldConversation, currentConversation, otherOpenConversation, otherContactConversation);
        fixture.Db.LiveSupportWhatsAppBindings.AddRange(
            Binding(oldConversation.Id, contactGuest, WhatsAppUserId),
            Binding(currentConversation.Id, contactGuest, WhatsAppUserId),
            Binding(otherOpenConversation.Id, otherOpenGuest, WhatsAppUserId),
            Binding(otherContactConversation.Id, otherContactGuest, "201088888888"));
        fixture.Db.LiveSupportAssignments.AddRange(
            Assignment(currentConversation.Id, staffA.Id),
            Assignment(otherOpenConversation.Id, staffB.Id));

        var sentAt = DateTime.UtcNow.AddHours(-2);
        var expectedMessages = Enumerable.Range(0, 151).Select(index => Message(
            index < 80 ? oldConversation : currentConversation,
            $"contact-{index}",
            sentAt)).ToArray();
        var otherOpenMessage = Message(otherOpenConversation, "other-owner", sentAt);
        var otherContactMessage = Message(otherContactConversation, "other-contact", sentAt);
        fixture.Db.LiveSupportMessages.AddRange(expectedMessages);
        fixture.Db.LiveSupportMessages.AddRange(otherOpenMessage, otherContactMessage);
        await fixture.Db.SaveChangesAsync();

        var expectedIds = expectedMessages.Select(message => message.Id).ToArray();
        var expectedOrder = await fixture.Db.LiveSupportMessages.AsNoTracking()
            .Where(message => expectedIds.Contains(message.Id))
            .OrderBy(message => message.SentAt).ThenBy(message => message.Id)
            .Select(message => message.Id)
            .ToArrayAsync();
        var service = new LiveSupportService(fixture.Db, new EnabledSettings());
        var stitchedIds = new List<Guid>();
        string? cursor = null;
        for (var pageNumber = 0; pageNumber < 10; pageNumber++)
        {
            var page = await service.GetStaffWhatsAppThreadAsync(
                new LiveSupportStaffWhatsAppThreadQuery(staffA.Id, false, currentConversation.Id, 37, cursor),
                CancellationToken.None);
            stitchedIds.InsertRange(0, page.Items.Select(message => message.Id));
            cursor = page.NextCursor;
            if (cursor is null) break;
        }

        Assert.Null(cursor);
        Assert.Equal(expectedOrder, stitchedIds);
        Assert.DoesNotContain(otherOpenMessage.Id, stitchedIds);
        Assert.DoesNotContain(otherContactMessage.Id, stitchedIds);

        var adminIds = new HashSet<Guid>();
        cursor = null;
        for (var pageNumber = 0; pageNumber < 10; pageNumber++)
        {
            var page = await service.GetStaffWhatsAppThreadAsync(
                new LiveSupportStaffWhatsAppThreadQuery(staffA.Id, true, oldConversation.Id, 37, cursor),
                CancellationToken.None);
            Assert.True(page.Items.All(message => adminIds.Add(message.Id)));
            cursor = page.NextCursor;
            if (cursor is null) break;
        }

        Assert.Null(cursor);
        Assert.Equal(expectedIds.Append(otherOpenMessage.Id).OrderBy(id => id), adminIds.OrderBy(id => id));
        Assert.DoesNotContain(otherContactMessage.Id, adminIds);

        var unauthorized = await Assert.ThrowsAsync<LiveSupportException>(() =>
            service.GetStaffWhatsAppThreadAsync(
                new LiveSupportStaffWhatsAppThreadQuery(staffB.Id, false, currentConversation.Id, 37, null),
                CancellationToken.None));
        Assert.Equal(LiveSupportErrorCodes.Forbidden, unauthorized.Code);
    }

    private static User User(string fullName) => new()
    {
        FullName = fullName,
        PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}",
        PasswordHash = "integration"
    };

    private static LiveSupportGuestSession Guest(string phoneNumber) => new()
    {
        DisplayName = "عميل واتساب",
        PhoneNumber = phoneNumber,
        SecurityStampHash = new string('a', 64),
        CreatedIpHash = new string('b', 64),
        ExpiresAt = DateTime.UtcNow.AddYears(1),
        LastSeenAt = DateTime.UtcNow
    };

    private static LiveSupportConversation Conversation(
        Guid guestId,
        LiveSupportConversationStatus status,
        Guid? ownerId) => new()
    {
        ParticipantType = LiveSupportParticipantType.Guest,
        GuestSessionId = guestId,
        Status = status,
        CurrentOwnerUserId = ownerId,
        QueuedAt = DateTime.UtcNow.AddHours(-3),
        LastMessageAt = DateTime.UtcNow.AddHours(-2),
        Version = 1
    };

    private static LiveSupportWhatsAppBinding Binding(
        Guid conversationId,
        LiveSupportGuestSession guest,
        string whatsAppUserId) => new()
    {
        ConversationId = conversationId,
        GuestSessionId = guest.Id,
        WhatsAppUserId = whatsAppUserId,
        PhoneNumber = guest.PhoneNumber,
        DisplayName = guest.DisplayName,
        LastInboundAt = DateTime.UtcNow.AddHours(-2),
        CustomerServiceWindowExpiresAt = DateTime.UtcNow.AddHours(22),
        Version = 1
    };

    private static LiveSupportAssignment Assignment(Guid conversationId, Guid staffId) => new()
    {
        ConversationId = conversationId,
        StaffUserId = staffId,
        StartedAt = DateTime.UtcNow,
        AssignmentSequence = 1
    };

    private static LiveSupportMessage Message(
        LiveSupportConversation conversation,
        string content,
        DateTime sentAt) => new()
    {
        ConversationId = conversation.Id,
        SenderType = LiveSupportSenderType.Guest,
        SenderGuestSessionId = conversation.GuestSessionId,
        ClientMessageId = Guid.NewGuid().ToString("N"),
        Type = LiveSupportMessageType.Text,
        Content = content,
        SentAt = sentAt
    };

    private sealed class EnabledSettings : ICachedPlatformSettingsReader
    {
        public Task<CachedPlatformSettings> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(CachedPlatformSettings.Default with { LiveSupportEnabled = true });

        public void Invalidate() { }
    }
}
