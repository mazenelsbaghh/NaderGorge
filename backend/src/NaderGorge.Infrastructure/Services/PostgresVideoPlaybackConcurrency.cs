using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Interfaces;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Infrastructure.Services;

public sealed class PostgresVideoPlaybackConcurrency(AppDbContext db) : IVideoPlaybackConcurrency
{
    public async Task AcquireAsync(Guid userId, Guid lessonVideoId, CancellationToken cancellationToken)
    {
        var playbackKey = userId.ToString("N") + lessonVideoId.ToString("N");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({playbackKey}, 169))",
            cancellationToken);
    }
}
