using NaderGorge.Infrastructure.Services;
using NaderGorge.Domain.Enums;
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
        await Assert.ThrowsAsync<NaderGorge.Application.Features.LiveSupport.Interfaces.LiveSupportException>(() => service.GetStudentContextAsync(LiveSupportTestData.StaffBId, false, LiveSupportTestData.Conversation().Id, CancellationToken.None));
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
}
