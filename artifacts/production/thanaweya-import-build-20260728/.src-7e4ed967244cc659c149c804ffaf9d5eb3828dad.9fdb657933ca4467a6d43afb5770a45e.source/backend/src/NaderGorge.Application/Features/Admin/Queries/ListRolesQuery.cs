using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Admin.Queries;

public record RoleDto(Guid Id, string Name, string Type, List<string> Permissions, string AllowedDomain, List<string> AllowedNavbarItems);

public record ListRolesQuery() : IRequest<ApiResponse<List<RoleDto>>>;

public class ListRolesQueryHandler : IRequestHandler<ListRolesQuery, ApiResponse<List<RoleDto>>>
{
    private readonly IAppDbContext _db;

    public ListRolesQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<RoleDto>>> Handle(ListRolesQuery request, CancellationToken cancellationToken)
    {
        var roles = await _db.Roles
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var dtos = new List<RoleDto>();
        foreach (var role in roles)
        {
            var permissions = new List<string>();
            if (!string.IsNullOrEmpty(role.PermissionsJson))
            {
                try
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<List<string>>(role.PermissionsJson);
                    if (parsed != null)
                    {
                        permissions = parsed;
                    }
                }
                catch { /* Ignore invalid JSON */ }
            }

            var allowedNavbarItems = new List<string>();
            if (!string.IsNullOrEmpty(role.AllowedNavbarItemsJson))
            {
                try
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<List<string>>(role.AllowedNavbarItemsJson);
                    if (parsed != null)
                    {
                        allowedNavbarItems = parsed;
                    }
                }
                catch { /* Ignore invalid JSON */ }
            }

            dtos.Add(new RoleDto(role.Id, role.Name, role.Type.ToString(), permissions, role.AllowedDomain, allowedNavbarItems));
        }

        return ApiResponse<List<RoleDto>>.Ok(dtos);
    }
}
