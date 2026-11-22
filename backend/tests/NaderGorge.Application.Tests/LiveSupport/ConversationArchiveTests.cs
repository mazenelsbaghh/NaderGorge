using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class ConversationArchiveTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private AppDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        _db.Users.Add(LiveSupportTestData.User(LiveSupportTestData.StudentId, "طالب الأرشيف", "01000000142"));
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _connection.DisposeAsync(); }

    [Fact]
    public async Task SearchFindsOlderClosedChatByMessageAndComplaintWithDateAndPagination()
    {
        var created = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        var old = new LiveSupportConversation { StudentUserId = LiveSupportTestData.StudentId,
            ParticipantType = LiveSupportParticipantType.Student, Status = LiveSupportConversationStatus.Closed, CreatedAt = created };
        _db.LiveSupportConversations.Add(old);
        _db.LiveSupportConversations.AddRange(Enumerable.Range(1, 120).Select(index => new LiveSupportConversation {
            StudentUserId = LiveSupportTestData.StudentId, ParticipantType = LiveSupportParticipantType.Student,
            Status = LiveSupportConversationStatus.Closed, CreatedAt = created.AddDays(index) }));
        _db.LiveSupportMessages.Add(new LiveSupportMessage { ConversationId = old.Id, Content = "مشكلة الاشتراك القديمة", SentAt = created, ClientMessageId = "archive-search" });
        _db.LiveSupportRatings.Add(new LiveSupportRating { ConversationId = old.Id, Stars = 1, Comment = "شكوى عن الأسلوب", SubmittedAt = created });
        await _db.SaveChangesAsync();
        var service = new LiveSupportService(_db, new LiveSupportEnabledSettings());

        var messageMatch = await service.SearchAdminConversationsAsync(new(Search: "الاشتراك القديمة", From: created, To: created, Status: LiveSupportConversationStatus.Closed), CancellationToken.None);
        Assert.Equal(old.Id, Assert.Single(messageMatch.Items).Id);
        var complaint = await service.SearchAdminConversationsAsync(new(Search: "الأسلوب", WithComment: true), CancellationToken.None);
        Assert.Equal("شكوى عن الأسلوب", Assert.Single(complaint.Items).RatingComment);
        var outside = await service.SearchAdminConversationsAsync(new(Search: "الأسلوب", From: created.AddSeconds(1)), CancellationToken.None);
        Assert.Empty(outside.Items);
        var lastPage = await service.SearchAdminConversationsAsync(new(Page: 3), CancellationToken.None);
        Assert.Equal(121, lastPage.TotalCount);
        Assert.Equal(21, lastPage.Items.Count);
        Assert.Contains(lastPage.Items, item => item.Id == old.Id);
    }

    [Fact]
    public async Task FiveStarRatingKeepsStudentNoteAndExposesItToAdmin()
    {
        var conversation = new LiveSupportConversation { StudentUserId = LiveSupportTestData.StudentId,
            ParticipantType = LiveSupportParticipantType.Student, Status = LiveSupportConversationStatus.Closed };
        _db.LiveSupportConversations.Add(conversation);
        await _db.SaveChangesAsync();
        var service = new LiveSupportService(_db, new LiveSupportEnabledSettings());
        var participant = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Student, LiveSupportTestData.StudentId, null);
        var invalid = await Assert.ThrowsAsync<LiveSupportException>(() => service.SubmitRatingAsync(participant, conversation.Id, 5, new string('x', 1001), CancellationToken.None));
        Assert.Equal("VALIDATION_ERROR", invalid.Code);
        await service.SubmitRatingAsync(participant, conversation.Id, 5, "  ملاحظة للإدارة عن التعامل  ", CancellationToken.None);

        var rating = Assert.Single(await service.GetAdminRatingsAsync(null, null, CancellationToken.None));
        Assert.Equal(5, rating.Stars);
        Assert.Equal("ملاحظة للإدارة عن التعامل", rating.Comment);
        Assert.Equal("ملاحظة للإدارة عن التعامل", (await service.GetAdminTimelineAsync(conversation.Id, CancellationToken.None)).RatingComment);
    }
}
