using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class StudentLinkTests
{
    [Fact]
    public async Task LinkRequiresOwnerVersionAndPersistsHistory()
    {
        await using var fixture = await LiveSupportTestDb.CreateSeededAsync();
        var target = new User { FullName = "طالب بديل", PhoneNumber = "01055555142", PasswordHash = "test" };
        fixture.Db.Users.Add(target);
        fixture.Db.StudentProfiles.Add(new StudentProfile { UserId = target.Id });
        await fixture.Db.SaveChangesAsync();
        var service = new LiveSupportService(fixture.Db, new LiveSupportEnabledSettings(), new LiveSupportConnectedPresence());
        await Assert.ThrowsAsync<NaderGorge.Application.Features.LiveSupport.Interfaces.LiveSupportException>(() => service.ChangeStudentLinkAsync(LiveSupportTestData.StaffAId, false, LiveSupportTestData.Conversation().Id, target.Id, "تصحيح الربط", 999, CancellationToken.None));
        var updated = await service.ChangeStudentLinkAsync(LiveSupportTestData.StaffAId, false, LiveSupportTestData.Conversation().Id, target.Id, "تصحيح الربط", 1, CancellationToken.None);
        Assert.Equal(target.Id, updated.LinkedStudentUserId);
        Assert.Equal(1, await fixture.Db.LiveSupportStudentLinkHistories.CountAsync());
        var search = await service.SearchStudentsAsync(LiveSupportTestData.StaffAId, false, updated.Id, "555", CancellationToken.None);
        Assert.Contains(search, x => x.UserId == target.Id && x.MaskedPhone != target.PhoneNumber);
    }
}
