using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests.LiveSupport;

public sealed class RatingAndMetricsTests
{
    [Fact]
    public async Task SameConversationRatingIsAttributedToEveryParticipatingOwner()
    {
        await using var fixture = await LiveSupportTestDb.CreateSeededAsync();
        fixture.Db.LiveSupportAssignments.Add(new LiveSupportAssignment { ConversationId = LiveSupportTestData.Conversation().Id, StaffUserId = LiveSupportTestData.StaffBId, StartedAt = DateTime.UtcNow, EndedAt = DateTime.UtcNow, AssignmentSequence = 2 });
        fixture.Db.LiveSupportRatings.Add(new LiveSupportRating { ConversationId = LiveSupportTestData.Conversation().Id, Stars = 4, SubmittedAt = DateTime.UtcNow });
        await fixture.Db.SaveChangesAsync();
        var dashboard = await new LiveSupportService(fixture.Db, new LiveSupportEnabledSettings(), new LiveSupportConnectedPresence()).GetAdminDashboardAsync(CancellationToken.None);
        Assert.All(dashboard.StaffPerformance.Where(x => x.StaffUserId == LiveSupportTestData.StaffAId || x.StaffUserId == LiveSupportTestData.StaffBId), x => { Assert.Equal(1, x.RatingCount); Assert.Equal(4, x.AverageRating); });
    }
}
