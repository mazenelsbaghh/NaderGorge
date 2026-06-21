using NaderGorge.Infrastructure.Services;

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
}
