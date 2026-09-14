using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record RemoveUnusedCodesCommand(Guid GroupId, Guid AdminId, bool KeepEmptyGroup) : IRequest<ApiResponse<RemoveUnusedCodesResult>>;
public record RemoveUnusedCodesResult(int RemovedCount, int KeptUsedCount, bool GroupDeleted);

public sealed class RemoveUnusedCodesCommandHandler : IRequestHandler<RemoveUnusedCodesCommand, ApiResponse<RemoveUnusedCodesResult>>
{
    private readonly IAppDbContext _db;
    private readonly IAuditService _audit;

    public RemoveUnusedCodesCommandHandler(IAppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ApiResponse<RemoveUnusedCodesResult>> Handle(RemoveUnusedCodesCommand request, CancellationToken ct)
    {
        var isAllowed = await _db.Users
            .Where(user => user.Id == request.AdminId)
            .SelectMany(user => user.UserRoles)
            .AnyAsync(role => role.Role != null && (role.Role.Type == RoleType.Admin || (role.Role.PermissionsJson ?? string.Empty).Contains("codes.manage")), ct);
        if (!isAllowed) return ApiResponse<RemoveUnusedCodesResult>.Fail("Unauthorized: You do not have permission to manage codes.");

        var group = await _db.CodeGroups
            .Include(item => item.AccessCodes)
            .Include(item => item.CodeVideoTargets)
            .FirstOrDefaultAsync(item => item.Id == request.GroupId, ct);
        if (group == null) return ApiResponse<RemoveUnusedCodesResult>.Fail("Code Group not found");

        var unusedCodes = group.AccessCodes.Where(code => !code.IsConsumed).ToList();
        var keptUsedCount = group.AccessCodes.Count - unusedCodes.Count;
        _db.AccessCodes.RemoveRange(unusedCodes);

        var deleteGroup = !request.KeepEmptyGroup && keptUsedCount == 0;
        if (deleteGroup)
        {
            _db.CodeVideoTargets.RemoveRange(group.CodeVideoTargets);
            _db.CodeGroups.Remove(group);
        }
        else
        {
            group.TotalCodes = keptUsedCount;
            group.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(
            action: "RemoveUnusedCodes",
            entityType: "CodeGroup",
            entityId: request.GroupId,
            userId: request.AdminId,
            oldValues: new { TotalCodes = group.TotalCodes + unusedCodes.Count, UnusedCodes = unusedCodes.Count },
            newValues: new { RemovedCount = unusedCodes.Count, KeptUsedCount = keptUsedCount, GroupDeleted = deleteGroup });

        return ApiResponse<RemoveUnusedCodesResult>.Ok(new RemoveUnusedCodesResult(unusedCodes.Count, keptUsedCount, deleteGroup));
    }
}
