using NaderGorge.Infrastructure.Services;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class StudentContextTests
{
    [Fact]
    public async Task CurrentOwnerGetsLinkedStudentProjectionOnly()
    {
        await using var fixture = await LiveSupportTestDb.CreateSeededAsync();
        var service = new LiveSupportService(fixture.Db, new LiveSupportEnabledSettings(), new LiveSupportConnectedPresence());
        var context = await service.GetStudentContextAsync(LiveSupportTestData.StaffAId, false, LiveSupportTestData.Conversation().Id, CancellationToken.None);
        Assert.Equal(LiveSupportTestData.StudentId, context.UserId);
        Assert.Equal("طالب الاختبار", context.FullName);
        var forbidden = await Assert.ThrowsAsync<NaderGorge.Application.Features.LiveSupport.Interfaces.LiveSupportException>(() => service.GetStudentContextAsync(LiveSupportTestData.StaffBId, false, LiveSupportTestData.Conversation().Id, CancellationToken.None));
        Assert.Equal(LiveSupportErrorCodes.Forbidden, forbidden.Code);
    }

    [Fact]
    public async Task UnlinkedGuest_GetContext_ThrowsStudentNotLinked()
    {
        await using var fixture = await LiveSupportTestDb.CreateSeededAsync();
        var service = new LiveSupportService(fixture.Db, new LiveSupportEnabledSettings(), new LiveSupportConnectedPresence());
        
        var conversation = new NaderGorge.Domain.Entities.LiveSupport.LiveSupportConversation
        {
            Id = Guid.NewGuid(),
            ParticipantType = LiveSupportParticipantType.Guest,
            Status = LiveSupportConversationStatus.Assigned,
            CurrentOwnerUserId = LiveSupportTestData.StaffAId,
            LinkedStudentUserId = null,
            Version = 1
        };
        fixture.Db.LiveSupportConversations.Add(conversation);
        await fixture.Db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<NaderGorge.Application.Features.LiveSupport.Interfaces.LiveSupportException>(
            () => service.GetStudentContextAsync(LiveSupportTestData.StaffAId, false, conversation.Id, CancellationToken.None));
        
        Assert.Equal("STUDENT_NOT_LINKED", exception.Code);
    }

    [Fact]
    public async Task AIHandoffOwnership_OwnerCanGetStudentContext()
    {
        await using var fixture = await LiveSupportTestDb.CreateSeededAsync();
        var service = new LiveSupportService(fixture.Db, new LiveSupportEnabledSettings(), new LiveSupportConnectedPresence());
        
        var context = await service.GetStudentContextAsync(LiveSupportTestData.StaffAId, false, LiveSupportTestData.Conversation().Id, CancellationToken.None);
        Assert.NotNull(context);
        Assert.Equal(LiveSupportTestData.StudentId, context.UserId);
    }

    [Fact]
    public async Task LinkReplacementAndUnlinkChangeWhichStudentContextIsVisible()
    {
        await using var fixture = await LiveSupportTestDb.CreateSeededAsync();
        var replacement = LiveSupportTestData.User(Guid.NewGuid(), "طالب بديل", "01000000999");
        fixture.Db.Users.Add(replacement);
        fixture.Db.StudentProfiles.Add(new NaderGorge.Domain.Entities.StudentProfile { UserId = replacement.Id, StudentCode = "ALT-142" });
        await fixture.Db.SaveChangesAsync();

        var service = new LiveSupportService(fixture.Db, new LiveSupportEnabledSettings(), new LiveSupportConnectedPresence());
        var conversationId = LiveSupportTestData.Conversation().Id;
        var linked = await service.ChangeStudentLinkAsync(LiveSupportTestData.StaffAId, false, conversationId, replacement.Id, "استبدال الربط", 1, CancellationToken.None);
        Assert.Equal(replacement.Id, linked.LinkedStudentUserId);
        var replacementContext = await service.GetStudentContextAsync(LiveSupportTestData.StaffAId, false, conversationId, CancellationToken.None);
        Assert.Equal(replacement.Id, replacementContext.UserId);
        Assert.Equal("طالب بديل", replacementContext.FullName);

        var unlinked = await service.ChangeStudentLinkAsync(LiveSupportTestData.StaffAId, false, conversationId, null, "إلغاء الربط", 2, CancellationToken.None);
        Assert.Null(unlinked.LinkedStudentUserId);
        var notLinked = await Assert.ThrowsAsync<NaderGorge.Application.Features.LiveSupport.Interfaces.LiveSupportException>(() => service.GetStudentContextAsync(LiveSupportTestData.StaffAId, false, conversationId, CancellationToken.None));
        Assert.Equal("STUDENT_NOT_LINKED", notLinked.Code);
        Assert.Equal(2, await fixture.Db.LiveSupportStudentLinkHistories.CountAsync(x => x.ConversationId == conversationId));
    }

    [Fact]
    public async Task StudentContextSectionRequiresLinkAndReturnsRequestedSection()
    {
        await using var fixture = await LiveSupportTestDb.CreateSeededAsync();
        var service = new LiveSupportService(fixture.Db, new LiveSupportEnabledSettings(), new LiveSupportConnectedPresence());
        var section = await service.GetStudentContextSectionAsync(LiveSupportTestData.StaffAId, false, LiveSupportTestData.Conversation().Id, "basic", CancellationToken.None);

        Assert.Equal("basic", section.Section);
        Assert.Equal("طالب الاختبار", section.Data.GetProperty("fullName").GetString());
    }
}
