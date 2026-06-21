using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class LiveSupportSecurityTests
{
    [Fact]
    public async Task GuestCannotReadAnotherParticipantsConversation()
    {
        await using var fixture = await LiveSupportTestDb.CreateSeededAsync();
        var service = new LiveSupportService(fixture.Db, new LiveSupportEnabledSettings(), new LiveSupportConnectedPresence());
        var attacker = new LiveSupportParticipantIdentity(LiveSupportParticipantType.Guest, null, LiveSupportTestData.GuestId);
        var result = await service.GetParticipantConversationAsync(attacker, LiveSupportTestData.Conversation().Id, CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task UnconfiguredStaffAndTerminalMutationAreDenied()
    {
        await using var fixture = await LiveSupportTestDb.CreateSeededAsync();
        var outsider = Guid.NewGuid();
        var service = new LiveSupportService(fixture.Db, new LiveSupportEnabledSettings(), new LiveSupportConnectedPresence());
        var error = await Assert.ThrowsAsync<LiveSupportException>(() => service.GetStaffBootstrapAsync(outsider, false, CancellationToken.None));
        Assert.Equal(LiveSupportErrorCodes.Forbidden, error.Code);
        await service.CloseAsync(LiveSupportTestData.StaffAId, false, LiveSupportTestData.Conversation().Id, "تم الحل", CancellationToken.None);
        await Assert.ThrowsAsync<LiveSupportException>(() => service.SendStaffMessageAsync(LiveSupportTestData.StaffAId, false, LiveSupportTestData.Conversation().Id, Guid.NewGuid().ToString(), "retry", CancellationToken.None));
    }
}
