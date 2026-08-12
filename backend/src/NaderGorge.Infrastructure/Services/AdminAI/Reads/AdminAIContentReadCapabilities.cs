using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

public sealed record AdminAIContentSummary(int Packages, int Terms, int Sections, int Lessons, int Videos, int Resources, int VideoTypes, int BunnyAssets, int BunnySnapshots, int VideoChapters, DateTime DataAsOf);
public sealed class AdminAIContentSummaryRead(IAppDbContext db) : IAdminAIReadCapability
{
    public string Key => "content.summary"; public Type OutputType => typeof(AdminAIContentSummary);
    public async Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct)
    {
        var asOf = DateTime.UtcNow;
        var summary = new AdminAIContentSummary(
            await db.Packages.AsNoTracking().CountAsync(ct),
            await db.Terms.AsNoTracking().CountAsync(ct),
            await db.ContentSections.AsNoTracking().CountAsync(ct),
            await db.Lessons.AsNoTracking().CountAsync(ct),
            await db.LessonVideos.AsNoTracking().CountAsync(ct),
            await db.LessonResources.AsNoTracking().CountAsync(ct),
            await db.VideoTypes.AsNoTracking().CountAsync(ct),
            await db.BunnyVideoAssets.AsNoTracking().CountAsync(ct),
            await db.BunnyUsageSnapshots.AsNoTracking().CountAsync(ct),
            await db.VideoChapters.AsNoTracking().CountAsync(ct),
            asOf);
        return new(summary, 1, true, false, asOf, ["admin.content"]);
    }
}
