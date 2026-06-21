using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupport.Services;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class LiveSupportRecoveryTests
{
    [Fact]
    public async Task DurableEventCreatesOnlyAllowlistedPostCommitTargets()
    {
        await using var fixture = await LiveSupportTestDb.CreateSeededAsync();
        var writer = new LiveSupportEventWriter(fixture.Db);
        var sequence = await writer.AppendAsync(new LiveSupportEventWriteRequest(LiveSupportTestData.Conversation().Id, LiveSupportEventType.MessageSent), CancellationToken.None);
        await fixture.Db.SaveChangesAsync();
        Assert.True(sequence > 0);
        var groups = await fixture.Db.OutboxEvents.Select(x => x.TargetGroup).ToListAsync();
        Assert.All(groups, group => Assert.StartsWith("LiveSupport:", group));
    }
}
