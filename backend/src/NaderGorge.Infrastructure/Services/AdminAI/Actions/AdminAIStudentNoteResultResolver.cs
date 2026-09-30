using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed class AdminAIStudentNoteResultResolver(IAppDbContext db) : IAdminAIExternalResultResolver
{
    public string CapabilityKey => "admin.identity.student-note.create";

    public async Task<AdminAIActionOutcome?> ResolveAsync(
        string externalOperationId, string executionId, CancellationToken cancellationToken)
    {
        if (!string.Equals(externalOperationId, executionId, StringComparison.Ordinal))
            return null;

        var exists = await db.AuthoritativeOperationReceipts.AsNoTracking()
            .AnyAsync(item => item.OperationId == externalOperationId
                && item.Scope == "student-note.create", cancellationToken);
        return exists
            ? AdminAIActionOutcomeFactory.Success(new { Message = "Note added successfully." },
                1, ["students", "student-notes"])
            : null;
    }
}
