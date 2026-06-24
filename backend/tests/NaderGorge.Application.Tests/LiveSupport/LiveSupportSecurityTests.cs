using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services;
using Microsoft.AspNetCore.RateLimiting;
using NaderGorge.API.Controllers;

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

    [Fact]
    public void PublicMutationsAndSensitiveActionsHaveDedicatedRateLimits()
    {
        static string? Policy(Type controller, string method) => controller.GetMethod(method)!.GetCustomAttributes(typeof(EnableRateLimitingAttribute), true).Cast<EnableRateLimitingAttribute>().Single().PolicyName;
        Assert.Equal("live-support-public", Policy(typeof(LiveSupportParticipantController), nameof(LiveSupportParticipantController.CreateGuestSession)));
        Assert.Equal("live-support-public", Policy(typeof(LiveSupportParticipantController), nameof(LiveSupportParticipantController.Create)));
        Assert.Equal("live-support-ai-message", Policy(typeof(LiveSupportParticipantController), nameof(LiveSupportParticipantController.Send)));
        Assert.Equal("live-support-public", Policy(typeof(LiveSupportParticipantController), nameof(LiveSupportParticipantController.Upload)));
        Assert.Equal("live-support-action", Policy(typeof(LiveSupportStaffController), nameof(LiveSupportStaffController.ExecuteAction)));
    }
}
