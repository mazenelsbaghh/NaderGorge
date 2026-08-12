using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

public sealed record AdminAIHrPeopleSummary(int Employees, int OrganizationUnits, int Positions, int Locations, int Contracts, int Documents, int Assets, int AssetCustodies, DateTime DataAsOf);

public sealed class AdminAIHrPeopleSummaryRead(IAppDbContext db) : IAdminAIReadCapability
{
    public string Key => "hr-people.summary";
    public Type OutputType => typeof(AdminAIHrPeopleSummary);

    public async Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct)
    {
        var asOf = DateTime.UtcNow;
        var summary = new AdminAIHrPeopleSummary(
            await db.EmployeeProfiles.AsNoTracking().CountAsync(ct),
            await db.OrganizationUnits.AsNoTracking().CountAsync(ct),
            await db.JobPositions.AsNoTracking().CountAsync(ct),
            await db.WorkLocations.AsNoTracking().CountAsync(ct),
            await db.EmploymentContracts.AsNoTracking().CountAsync(ct),
            await db.EmployeeDocuments.AsNoTracking().CountAsync(ct),
            await db.HrAssets.AsNoTracking().CountAsync(ct),
            await db.AssetCustodies.AsNoTracking().CountAsync(ct),
            asOf);
        return new(summary, 1, true, false, asOf, ["admin.hr.people"]);
    }
}
