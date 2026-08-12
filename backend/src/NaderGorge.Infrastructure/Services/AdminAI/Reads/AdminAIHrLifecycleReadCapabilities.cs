using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

public sealed record AdminAIHrLifecycleSummary(int Cycles, int Goals, int Reviews, int Cases, int DisciplinaryActions, int Requisitions, int Candidates, int Offers, int LifecycleTasks, int OffboardingProcesses, int Rollouts, int MigrationBatches, int MigrationConflicts, DateTime DataAsOf);

public sealed class AdminAIHrLifecycleSummaryRead(IAppDbContext db) : IAdminAIReadCapability
{
    public string Key => "hr-lifecycle.summary";
    public Type OutputType => typeof(AdminAIHrLifecycleSummary);

    public async Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct)
    {
        var asOf = DateTime.UtcNow;
        var summary = new AdminAIHrLifecycleSummary(
            await db.PerformanceCycles.AsNoTracking().CountAsync(ct),
            await db.PerformanceGoals.AsNoTracking().CountAsync(ct),
            await db.PerformanceReviews.AsNoTracking().CountAsync(ct),
            await db.EmployeeCases.AsNoTracking().CountAsync(ct),
            await db.DisciplinaryActions.AsNoTracking().CountAsync(ct),
            await db.Requisitions.AsNoTracking().CountAsync(ct),
            await db.Candidates.AsNoTracking().CountAsync(ct),
            await db.CandidateOffers.AsNoTracking().CountAsync(ct),
            await db.EmployeeLifecycleTasks.AsNoTracking().CountAsync(ct),
            await db.OffboardingProcesses.AsNoTracking().CountAsync(ct),
            await db.HrModuleRollouts.AsNoTracking().CountAsync(ct),
            await db.HrMigrationBatches.AsNoTracking().CountAsync(ct),
            await db.HrMigrationConflicts.AsNoTracking().CountAsync(ct),
            asOf);
        return new(summary, 1, true, false, asOf, ["admin.hr.lifecycle"]);
    }
}
