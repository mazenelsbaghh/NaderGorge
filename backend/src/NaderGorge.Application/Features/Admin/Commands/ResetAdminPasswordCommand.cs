using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record ResetAdminPasswordCommand(Guid UserId, string NewPassword, Guid AdminId) : IRequest<ApiResponse>;

public class ResetAdminPasswordCommandHandler(IAppDbContext db) : IRequestHandler<ResetAdminPasswordCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(ResetAdminPasswordCommand request, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == request.AdminId && u.IsActive && !u.IsDeleted &&
            u.UserRoles.Any(r => r.Role.Type == RoleType.Admin), ct))
            return ApiResponse.Fail("غير مصرح بهذا الإجراء.");
        if (!PasswordPolicy.IsValid(request.NewPassword)) return ApiResponse.Fail(PasswordPolicy.ValidationMessage);
        if (System.Text.Encoding.UTF8.GetByteCount(request.NewPassword) > 72)
            return ApiResponse.Fail("كلمة المرور أطول من الحد المسموح (72 بايت).");
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && !u.IsDeleted &&
            u.UserRoles.Any(r => r.Role.Type == RoleType.Admin), ct);
        if (user == null) return ApiResponse.Fail("حساب المدير غير موجود.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordResetVersion++;
        user.SecurityStampVersion++;
        var tokens = await db.RefreshTokens.Where(t => t.UserId == user.Id && !t.IsRevoked).ToListAsync(ct);
        foreach (var token in tokens) token.IsRevoked = true;
        db.AuditLogs.Add(new AuditLog
        {
            Action = "ResetAdminPassword", EntityType = "User", EntityId = user.Id,
            PerformedByUserId = request.AdminId, NewValues = "Password changed; sessions revoked", IpAddress = "System"
        });
        await db.SaveChangesAsync(ct);
        return ApiResponse.Ok("تم تغيير كلمة المرور وإلغاء الجلسات القديمة.");
    }
}
