using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;
using System.Text.Json;

namespace NaderGorge.Application.Features.Admin.Commands;

public record UpdateUserRoleCommand(Guid UserId, string[] Roles, Guid AdminId) : IRequest<ApiResponse>;

public class UpdateUserRoleCommandHandler : IRequestHandler<UpdateUserRoleCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public UpdateUserRoleCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse> Handle(UpdateUserRoleCommand request, CancellationToken ct)
    {
        var requestedRoles = request.Roles
            .Select(r => r.Trim())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (requestedRoles.Length == 0)
            return ApiResponse.Fail("At least one role must be assigned.");

        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct);

        if (user == null) return ApiResponse.Fail("User not found");

        // Identify requested role entities
        var rolesToAssign = await _db.Roles
            .Where(r => requestedRoles.Contains(r.Name))
            .ToListAsync(ct);

        if (rolesToAssign.Count != requestedRoles.Length)
            return ApiResponse.Fail("Invalid roles provided.");

        var currentlyAdmin = user.UserRoles.Any(ur => ur.Role.Name == "Admin");
        var willBeAdmin = rolesToAssign.Any(r => r.Name == "Admin");
        if (currentlyAdmin && !willBeAdmin)
        {
            var adminCount = await _db.UserRoles.CountAsync(ur => ur.Role.Name == "Admin", ct);
            if (adminCount <= 1)
                return ApiResponse.Fail("Cannot remove the last admin role.");
        }

        var oldRolesJson = JsonSerializer.Serialize(user.UserRoles.Select(r => r.Role.Name));

        // Clear existing
        _db.UserRoles.RemoveRange(user.UserRoles);

        // Add new
        foreach (var role in rolesToAssign)
        {
            _db.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id
            });
        }

        var newRolesJson = JsonSerializer.Serialize(rolesToAssign.Select(r => r.Name));

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "UpdateUserRoles",
            EntityType = "User",
            EntityId = user.Id,
            PerformedByUserId = request.AdminId,
            OldValues = $"Roles: {oldRolesJson}",
            NewValues = $"Roles: {newRolesJson}",
            IpAddress = "System"
        });

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok("User roles updated successfully.");
    }
}
