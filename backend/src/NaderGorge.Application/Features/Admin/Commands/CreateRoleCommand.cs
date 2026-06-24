using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Admin.Commands;

public record CreateRoleCommand(string Name, List<string> Permissions, string AllowedDomain, List<string> AllowedNavbarItems) : IRequest<ApiResponse<Guid>>;

public class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public CreateRoleCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<Guid>> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiResponse<Guid>.Fail("اسم الدور مطلوب", new List<string> { "ROLE_NAME_REQUIRED" });
        }

        var normalizedName = request.Name.Trim();

        // Check duplicates
        var exists = await _db.Roles.AnyAsync(r => r.Name.ToLower() == normalizedName.ToLower(), cancellationToken);
        if (exists)
        {
            return ApiResponse<Guid>.Fail("اسم الدور مسجل بالفعل", new List<string> { "ROLE_NAME_DUPLICATE" });
        }

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Type = RoleType.Assistant, // Custom roles created are Assistants
            PermissionsJson = JsonSerializer.Serialize(request.Permissions ?? new List<string>()),
            AllowedDomain = request.AllowedDomain ?? "all",
            AllowedNavbarItemsJson = JsonSerializer.Serialize(request.AllowedNavbarItems ?? new List<string>())
        };

        _db.Roles.Add(role);
        await _db.SaveChangesAsync(cancellationToken);

        return ApiResponse<Guid>.Ok(role.Id, "تم إنشاء الدور بنجاح");
    }
}
