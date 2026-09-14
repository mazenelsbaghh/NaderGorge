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

public record UpdateRoleCommand(Guid Id, string Name, List<string> Permissions, string AllowedDomain, List<string> AllowedNavbarItems, Guid ActorUserId) : IRequest<ApiResponse>;

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

        var requestedPermissions = request.Permissions ?? [];
        var requestedNavbarItems = request.AllowedNavbarItems ?? [];
        var permissionsJson = JsonSerializer.Serialize(requestedPermissions);
        if (!DelegatedRoleDomainPolicy.TryNormalize(request.AllowedDomain, out var allowedDomain))
        {
            return ApiResponse.Fail("اختر بوابة المدير أو بوابة المساعد للدور المفوض", new List<string> { "ROLE_DOMAIN_INVALID" });
        }

        var allowedNavbarItemsJson = JsonSerializer.Serialize(requestedNavbarItems);
        var roleDefinitionChanged = !string.Equals(role.Name, normalizedName, StringComparison.Ordinal)
            || !string.Equals(role.PermissionsJson, permissionsJson, StringComparison.Ordinal)
            || !string.Equals(role.AllowedDomain, allowedDomain, StringComparison.Ordinal)
            || !string.Equals(role.AllowedNavbarItemsJson, allowedNavbarItemsJson, StringComparison.Ordinal);

        // Check duplicates
        var exists = await _db.Roles.AnyAsync(r => r.Id != request.Id && r.Name.ToLower() == normalizedName.ToLower(), cancellationToken);
        if (exists)
        {
            return ApiResponse.Fail("اسم الدور مسجل بالفعل", new List<string> { "ROLE_NAME_DUPLICATE" });
        }

        var permissions = requestedPermissions;
        var previouslyRoutesConversations = HasRoutingPermission(role.PermissionsJson);
        var routesConversations = permissions.Contains(LiveSupportRoutingPermissions.ReceiveConversations, StringComparer.OrdinalIgnoreCase);
        role.Name = normalizedName;
        role.PermissionsJson = permissionsJson;
        role.AllowedDomain = allowedDomain;
        role.AllowedNavbarItemsJson = allowedNavbarItemsJson;

        var affectedUsers = roleDefinitionChanged
            ? await _db.Users
                .Where(user => user.UserRoles.Any(userRole => userRole.RoleId == role.Id))
                .ToListAsync(cancellationToken)
            : [];

        foreach (var affectedUser in affectedUsers)
        {
            affectedUser.SecurityStampVersion += 1;
            AddUserAuthorizationChangedEvent(affectedUser.Id, role.Id, affectedUser.SecurityStampVersion, request.ActorUserId);
        }

        if (previouslyRoutesConversations != routesConversations)
        {
            var affectedUserIds = await _db.UserRoles
                .Where(userRole => userRole.RoleId == role.Id)
                .Select(userRole => userRole.UserId)
                .ToListAsync(cancellationToken);
            foreach (var userId in affectedUserIds)
            {
                var otherPermissionSets = await _db.UserRoles
                    .Where(userRole => userRole.UserId == userId && userRole.RoleId != role.Id)
                    .Select(userRole => userRole.Role.PermissionsJson)
                    .ToListAsync(cancellationToken);
                var receivesFromAnotherRole = otherPermissionSets.Any(HasRoutingPermission);
                await LiveSupportRoutingPermissionSync.SetEligibilityAsync(
                    _db,
                    new LiveSupportRoutingEligibilityChange(userId, routesConversations || receivesFromAnotherRole, request.ActorUserId),
                    cancellationToken);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return ApiResponse.Ok("تم تعديل الدور بنجاح");
    }

    private void AddUserAuthorizationChangedEvent(Guid userId, Guid roleId, int authorizationVersion, Guid actorUserId)
    {
        _db.OutboxEvents.Add(new NaderGorge.Domain.Entities.OutboxEvent
        {
            Type = "StaffDataChanged",
            TargetUserId = userId.ToString(),
            TargetGroup = "Role_Staff",
            PayloadJson = JsonSerializer.Serialize(new
            {
                schemaVersion = "2",
                eventId = Guid.NewGuid(),
                occurredAt = DateTime.UtcNow,
                scopes = new[] { "users", "settings" },
                operation = "updated",
                entityType = "Role",
                entityIds = new[] { roleId },
                userId,
                authorizationVersion,
                actorUserId
            })
        });
    }

    private static bool HasRoutingPermission(string? permissionsJson)
    {
        if (string.IsNullOrWhiteSpace(permissionsJson)) return false;
        return (JsonSerializer.Deserialize<List<string>>(permissionsJson) ?? []).Contains(
            LiveSupportRoutingPermissions.ReceiveConversations,
            StringComparer.OrdinalIgnoreCase);
    }
}
