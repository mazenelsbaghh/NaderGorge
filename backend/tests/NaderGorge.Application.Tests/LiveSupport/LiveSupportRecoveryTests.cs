using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Application.Features.LiveSupport.Services;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class LiveSupportRecoveryTests
{
    [Fact]
    public async Task ProductionIncident20260809_StaleOpenQueueEntryIsExcludedFromWaitingCount()
    {
        await using var fixture = await LiveSupportTestDb.CreateSeededAsync();
        fixture.Db.LiveSupportQueueEntries.Add(new LiveSupportQueueEntry
        {
            ConversationId = LiveSupportTestData.Conversation().Id,
            EnteredAt = DateTime.UtcNow,
            Sequence = DateTime.UtcNow.Ticks
        });
        await fixture.Db.SaveChangesAsync();

        var bootstrap = await new LiveSupportService(fixture.Db, new LiveSupportEnabledSettings(), new LiveSupportConnectedPresence())
            .GetStaffBootstrapAsync(LiveSupportTestData.StaffAId, false, CancellationToken.None);

        Assert.Equal(0, bootstrap.WaitingCount);
    }

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
