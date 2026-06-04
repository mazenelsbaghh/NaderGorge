using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record AdminResetPasswordCommand(Guid StudentId, string NewPassword, Guid AdminId) : IRequest<ApiResponse>;

public class AdminResetPasswordCommandHandler : IRequestHandler<AdminResetPasswordCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public AdminResetPasswordCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(AdminResetPasswordCommand request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.StudentId, ct);
        if (user == null) return ApiResponse.Fail("Student not found.");

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 4)
            return ApiResponse.Fail("Password must be at least 4 characters.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "AdminResetPassword",
            EntityType = "User",
            EntityId = request.StudentId,
            PerformedByUserId = request.AdminId,
            NewValues = "Admin reset student password",
            IpAddress = "System"
        });

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok("Password updated successfully.");
    }
}
