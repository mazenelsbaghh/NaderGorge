using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public class ToggleStudentSystemAccessCommand : IRequest<ApiResponse>
{
    public Guid UserId { get; set; }
    public bool IsActive { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid AdminId { get; set; }

    public ToggleStudentSystemAccessCommand(Guid userId, bool isActive, string reason, Guid adminId)
    {
        UserId = userId;
        IsActive = isActive;
        Reason = reason;
        AdminId = adminId;
    }
}

public class ToggleStudentSystemAccessCommandHandler : IRequestHandler<ToggleStudentSystemAccessCommand, ApiResponse>
{
    private readonly IAppDbContext _context;

    public ToggleStudentSystemAccessCommandHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse> Handle(ToggleStudentSystemAccessCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user == null) return ApiResponse.Fail("User not found.");

        var isAdmin = user.UserRoles.Any(ur => ur.Role.Name == "Admin");
        if (isAdmin && !request.IsActive)
        {
            return ApiResponse.Fail(
                "Admin accounts cannot be disabled.",
                new List<string> { "ADMIN_CANNOT_BE_DISABLED" });
        }

        bool wasActive = user.IsActive;
        user.IsActive = request.IsActive;
        user.SuspensionReason = request.IsActive ? null : request.Reason;

        var audit = new AuditLog
        {
            EntityType = "User",
            EntityId = request.UserId,
            Action = "TOGGLE_ACCESS",
            PerformedByUserId = request.AdminId,
            OldValues = JsonSerializer.Serialize(new { isActive = wasActive }),
            NewValues = JsonSerializer.Serialize(new { isActive = request.IsActive, reason = request.Reason })
        };
        _context.AuditLogs.Add(audit);

        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse.Ok("User access updated successfully.");
    }
}
