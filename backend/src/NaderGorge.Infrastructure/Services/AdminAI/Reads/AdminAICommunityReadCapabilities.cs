using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

public sealed record AdminAICommunitySummary(int Posts, int Comments, int Likes, int PollOptions, int PollVotes, DateTime DataAsOf);

public sealed class AdminAICommunitySummaryRead(IAppDbContext db) : IAdminAIReadCapability
{
    public string Key => "community.summary";
    public Type OutputType => typeof(AdminAICommunitySummary);
    public async Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct)
    {
        var asOf = DateTime.UtcNow;
        var projection = new AdminAICommunitySummary(
            await db.CommunityPosts.AsNoTracking().CountAsync(ct),
            await db.CommunityPostComments.AsNoTracking().CountAsync(ct),
            await db.CommunityPostLikes.AsNoTracking().CountAsync(ct),
            await db.CommunityPostPollOptions.AsNoTracking().CountAsync(ct),
            await db.CommunityPostPollVotes.AsNoTracking().CountAsync(ct), asOf);
        return new(projection, 1, true, false, asOf, ["admin.community"]);
    }
}
