using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

public sealed record AdminAIOperationsSummary(int Tasks, int TaskComments, int CrmStatuses, int CrmCalls, int ChatRooms, int ChatParticipants, DateTime DataAsOf);

public sealed class AdminAIOperationsSummaryRead(IAppDbContext db) : IAdminAIReadCapability
{
    public string Key => "operations.summary";
    public Type OutputType => typeof(AdminAIOperationsSummary);

    public async Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct)
    {
        var asOf = DateTime.UtcNow;
        var value = new AdminAIOperationsSummary(
            await db.TaskItems.AsNoTracking().CountAsync(ct),
            await db.TaskComments.AsNoTracking().CountAsync(ct),
            await db.CrmStudentStatuses.AsNoTracking().CountAsync(ct),
            await db.CrmCallLogs.AsNoTracking().CountAsync(ct),
            await db.ChatRooms.AsNoTracking().CountAsync(ct),
            await db.ChatParticipants.AsNoTracking().CountAsync(ct),
            asOf);
        return new(value, 1, true, false, asOf, ["admin.operations"]);
    }
}
