using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record ArchiveStaffCommand(Guid UserId, Guid AdminId) : IRequest<ApiResponse>;

public class ArchiveStaffCommandHandler(IAppDbContext db) : IRequestHandler<ArchiveStaffCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(ArchiveStaffCommand request, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == request.AdminId && u.IsActive && !u.IsDeleted &&
            u.UserRoles.Any(r => r.Role.Type == RoleType.Admin), ct))
            return ApiResponse.Fail("غير مصرح بهذا الإجراء.");

        var user = await db.Users.Include(u => u.UserRoles).ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct);
        if (user == null) return ApiResponse.Fail("الموظف غير موجود.");
        var roles = user.UserRoles.Select(r => r.Role.Type).ToArray();
        if (user.Id == request.AdminId || roles.Length == 0 || roles.Any(r =>
            r is not (RoleType.Assistant or RoleType.AssistantReviewer or RoleType.AssistantAcademic or RoleType.Supervisor or RoleType.Staff)))
            return ApiResponse.Fail("لا يمكن أرشفة هذا الحساب من قائمة الموظفين.");
        if (user.IsDeleted) return ApiResponse.Ok("الموظف مؤرشف بالفعل.");

        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        user.IsActive = false;
        user.SecurityStampVersion++;
        var tokens = await db.RefreshTokens.Where(t => t.UserId == user.Id && !t.IsRevoked).ToListAsync(ct);
        foreach (var token in tokens) token.IsRevoked = true;
        db.AuditLogs.Add(new AuditLog
        {
            Action = "ArchiveStaff", EntityType = "User", EntityId = user.Id,
            PerformedByUserId = request.AdminId, NewValues = "Archived; records retained", IpAddress = "System"
        });
        await db.SaveChangesAsync(ct);
        return ApiResponse.Ok("تم إخفاء الموظف وإيقاف دخوله مع الاحتفاظ بسجلاته.");
    }
}
