using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Admin.Commands;

public record DeleteRoleCommand(Guid Id) : IRequest<ApiResponse>;

public class DeleteRoleCommandHandler : IRequestHandler<DeleteRoleCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public DeleteRoleCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (role == null)
        {
            return ApiResponse.Fail("الدور غير موجود", new List<string> { "ROLE_NOT_FOUND" });
        }

        // Prevent deletion of default system roles
        var systemRoles = new[] { "Admin", "Teacher", "Student" };
        if (systemRoles.Contains(role.Name))
        {
            return ApiResponse.Fail("لا يمكن حذف الأدوار الافتراضية للنظام", new List<string> { "SYSTEM_ROLE_READONLY" });
        }

        // Check if users are assigned to this role
        var hasUsers = await _db.UserRoles.AnyAsync(ur => ur.RoleId == request.Id, cancellationToken);
        if (hasUsers)
        {
            return ApiResponse.Fail("لا يمكن حذف الدور نظراً لوجود مستخدمين مسجلين عليه حالياً", new List<string> { "ROLE_IN_USE" });
        }

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync(cancellationToken);

        return ApiResponse.Ok("تم حذف الدور بنجاح");
    }
}
