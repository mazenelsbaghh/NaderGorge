using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Commands;

public record ConsumeVideoSessionCommand(Guid SessionId, Guid UserId) : IRequest<ApiResponse<bool>>;

public class ConsumeVideoSessionCommandHandler : IRequestHandler<ConsumeVideoSessionCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;

    public ConsumeVideoSessionCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<bool>> Handle(ConsumeVideoSessionCommand request, CancellationToken ct)
    {
        var session = await _db.VideoPlaybackSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.UserId == request.UserId, ct);

        if (session == null)
            return ApiResponse<bool>.Fail("Session not found", new List<string> { "SESSION_NOT_FOUND" });

        if (session.IsConsumed)
            return ApiResponse<bool>.Fail("Session already consumed", new List<string> { "SESSION_CONSUMED" });

        if (session.ExpiresAt < DateTime.UtcNow)
            return ApiResponse<bool>.Fail("Session expired", new List<string> { "SESSION_EXPIRED" });

        session.IsConsumed = true;
        
        // Minor optimization: could delete here instead of marking consumed
        // but keeping it consumed helps audit tracking vs immediate deletion

        await _db.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, "Session consumed successfully");
    }
}
