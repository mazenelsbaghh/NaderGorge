using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record UpdateUserStatusCommand(Guid UserId, string NewStatus, Guid AdminId) : IRequest<ApiResponse>;

public class UpdateUserStatusCommandHandler : IRequestHandler<UpdateUserStatusCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public UpdateUserStatusCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse> Handle(UpdateUserStatusCommand request, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct);
        if (user == null) return ApiResponse.Fail("User not found");

        var oldStatus = user.IsActive ? "Active" : "Disabled";
        var isNewActive = request.NewStatus.Equals("Active", StringComparison.OrdinalIgnoreCase);
        var isAdmin = user.UserRoles.Any(ur => ur.Role.Name == "Admin");

        if (isAdmin && !isNewActive)
        {
            return ApiResponse.Fail(
                "Admin accounts cannot be disabled.",
                new List<string> { "ADMIN_CANNOT_BE_DISABLED" });
        }

        user.IsActive = isNewActive;

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "UpdateUserStatus",
            EntityType = "User",
            EntityId = user.Id,
            PerformedByUserId = request.AdminId,
            OldValues = $"Status: {oldStatus}",
            NewValues = $"Status: {request.NewStatus}",
            IpAddress = "System"
        });

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok($"User status updated to {request.NewStatus}");
    }
}
