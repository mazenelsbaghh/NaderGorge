using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

public sealed record AdminAICodeSummary(int Groups, int Codes, int Grants, int PageProfiles, int PrintableBatches, int PrintableCodes, int SharedPackages, int DeliveryConfirmations, DateTime DataAsOf);

public sealed class AdminAICodeSummaryRead(IAppDbContext db) : IAdminAIReadCapability
{
    public string Key => "codes.summary";
    public Type OutputType => typeof(AdminAICodeSummary);

    public async Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct)
    {
        var asOf = DateTime.UtcNow;
        var summary = new AdminAICodeSummary(
            await db.CodeGroups.AsNoTracking().CountAsync(ct),
            await db.AccessCodes.AsNoTracking().CountAsync(ct),
            await db.StudentAccessGrants.AsNoTracking().CountAsync(ct),
            await db.PackageCodePageProfiles.AsNoTracking().CountAsync(ct),
            await db.PrintableCodeBatches.AsNoTracking().CountAsync(ct),
            await db.PrintableSalesCodes.AsNoTracking().CountAsync(ct),
            await db.SharedTeacherPackages.AsNoTracking().CountAsync(ct),
            await db.CodeGroupDeliveryConfirmations.AsNoTracking().CountAsync(ct),
            asOf);
        return new(summary, 1, true, false, asOf, ["admin.codes"]);
    }
}
