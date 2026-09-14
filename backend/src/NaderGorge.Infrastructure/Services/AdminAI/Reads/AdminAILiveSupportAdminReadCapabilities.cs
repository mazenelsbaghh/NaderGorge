using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

public sealed record AdminAILiveSupportSummary(int Conversations, int QueueEntries, int Assignments, int StaffConfigs, int ScheduleWindows, int Ratings, int Events, int ActionEvidence, int PolicyVersions, int KnowledgeEntries, DateTime DataAsOf);

public sealed class AdminAILiveSupportSummaryRead(IAppDbContext db) : IAdminAIReadCapability
{
    public string Key => "live-support.summary";
    public Type OutputType => typeof(AdminAILiveSupportSummary);

    public async Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct)
    {
        var asOf = DateTime.UtcNow;
        var summary = new AdminAILiveSupportSummary(
            await db.LiveSupportConversations.AsNoTracking().CountAsync(ct),
            await db.LiveSupportQueueEntries.AsNoTracking().CountAsync(ct),
            await db.LiveSupportAssignments.AsNoTracking().CountAsync(ct),
            await db.LiveSupportStaffConfigs.AsNoTracking().CountAsync(ct),
            await db.LiveSupportScheduleWindows.AsNoTracking().CountAsync(ct),
            await db.LiveSupportRatings.AsNoTracking().CountAsync(ct),
            await db.LiveSupportEvents.AsNoTracking().CountAsync(ct),
            await db.LiveSupportActionExecutions.AsNoTracking().CountAsync(ct),
            await db.LiveSupportAIPolicyVersions.AsNoTracking().CountAsync(ct),
            await db.LiveSupportAIKnowledgeEntries.AsNoTracking().CountAsync(ct),
            asOf);
        return new(summary, 1, true, false, asOf, ["admin.live-support"]);
    }
}
