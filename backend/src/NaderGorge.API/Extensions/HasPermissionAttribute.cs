using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Application.Common.HR;
using NaderGorge.Application.Common.Configuration;

namespace NaderGorge.API.Extensions;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class HasPermissionAttribute : TypeFilterAttribute
{
    public HasPermissionAttribute(string permission) : base(typeof(PermissionFilter))
    {
        Arguments = new object[] { permission };
    }
}

public class PermissionFilter : IAsyncAuthorizationFilter
{
    private readonly string _permission;
    private readonly IAppDbContext _db;

    public PermissionFilter(string permission, IAppDbContext db)
    {
        _permission = permission;
        _db = db;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Admins bypass all checks
        if (user.IsInRole("Admin"))
        {
            return;
        }

        // Attendance is an employee entitlement, not an optional staff-role permission.
        // Provisioned Staff accounts may not carry the legacy hr.attendance.self
        // claim, but must still be able to view and record their own attendance.
        if (_permission.Equals(HrPermissions.AttendanceSelf, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var employeeUserId)
            && await _db.EmployeeProfiles.AnyAsync(item => item.UserId == employeeUserId, context.HttpContext.RequestAborted))
        {
            return;
        }

        // Check for specific permission claim
        var hasPermission = user.Claims.Any(c => c.Type == "permission" && c.Value.Equals(_permission, StringComparison.OrdinalIgnoreCase));
        if (!hasPermission &&
            PlatformFinancePermissions.All.Contains(_permission, StringComparer.OrdinalIgnoreCase) &&
            user.Claims.Any(c => c.Type == "permission" && c.Value.Equals("finance.manage", StringComparison.OrdinalIgnoreCase)))
        {
            hasPermission = true;
        }
        if (!hasPermission)
        {
            if (_permission.Equals("gifts.manage", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Guid? actorId = Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId)
                        ? parsedId
                        : null;
                    _db.AuditLogs.Add(new AuditLog
                    {
                        Action = "GiftPermissionDenied",
                        EntityType = nameof(GiftIssuance),
                        PerformedByUserId = actorId,
                        OldValues = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            permission = _permission,
                            method = context.HttpContext.Request.Method,
                            path = context.HttpContext.Request.Path.Value
                        })
                    });
                    await _db.SaveChangesAsync(context.HttpContext.RequestAborted);
                }
                catch
                {
                    // Authorization denial must not depend on audit persistence availability.
                }
            }
            context.Result = new ForbidResult();
        }

    }
}
