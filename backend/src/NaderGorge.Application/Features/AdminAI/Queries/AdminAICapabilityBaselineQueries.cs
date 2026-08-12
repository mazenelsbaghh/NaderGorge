using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Dtos;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.AdminAI.Queries;

public sealed class AdminAICapabilityBaselineQueries(IAppDbContext db, IAdminAIAccessGate access)
{
    public async Task<AdminAIBaselineSummary> ActiveAsync(Guid actorId, CancellationToken cancellationToken)
    {
        await access.RequireCurrentAdminAsync(actorId, null, cancellationToken);
        return await db.AdminAICapabilityBaselines.AsNoTracking()
            .Where(item => item.Status == AdminAICapabilityBaselineStatus.Active)
            .Select(item => new AdminAIBaselineSummary(item.Version, item.ManifestHash, item.SourceRevision,
                item.SupportedReadCount, item.SupportedActionCount, item.ExcludedCount, item.ApprovedAt!.Value))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("An active Admin AI baseline is not available.");
    }
}
