using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Admin.Commands;

public record UpdateRoleCommand(Guid Id, string Name, List<string> Permissions) : IRequest<ApiResponse>;

public class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public UpdateRoleCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (role == null)
        {
            return ApiResponse.Fail("الدور غير موجود", new List<string> { "ROLE_NOT_FOUND" });
        }

        // Prevent modification of default system roles
        var systemRoles = new[] { "Admin", "Teacher", "Student" };
        if (systemRoles.Contains(role.Name))
        {
            return ApiResponse.Fail("لا يمكن تعديل الأدوار الافتراضية للنظام", new List<string> { "SYSTEM_ROLE_READONLY" });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiResponse.Fail("اسم الدور مطلوب", new List<string> { "ROLE_NAME_REQUIRED" });
        }

        var normalizedName = request.Name.Trim();

        // Check duplicates
        var exists = await _db.Roles.AnyAsync(r => r.Id != request.Id && r.Name.ToLower() == normalizedName.ToLower(), cancellationToken);
        if (exists)
        {
            return ApiResponse.Fail("اسم الدور مسجل بالفعل", new List<string> { "ROLE_NAME_DUPLICATE" });
        }

        role.Name = normalizedName;
        role.PermissionsJson = JsonSerializer.Serialize(request.Permissions ?? new List<string>());

        await _db.SaveChangesAsync(cancellationToken);

        return ApiResponse.Ok("تم تعديل الدور بنجاح");
    }
}
