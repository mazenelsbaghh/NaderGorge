using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Auth.Commands;

public record LogoutCommand(string RefreshToken) : IRequest<ApiResponse>;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public LogoutCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse> Handle(LogoutCommand request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            return ApiResponse.Ok("Already logged out");
        }

        await _db.RefreshTokens
            .Where(r => r.Token == request.RefreshToken && !r.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsRevoked, true), ct);

        return ApiResponse.Ok("Logged out successfully");
    }
}
