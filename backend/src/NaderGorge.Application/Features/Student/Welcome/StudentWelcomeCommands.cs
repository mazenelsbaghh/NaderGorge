using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Welcome;

public sealed record WelcomeClaimDto(Guid Token, string Kind, DateTime ExpiresAt);
public sealed record ClaimStudentWelcomeCommand(Guid UserId) : IRequest<ApiResponse<WelcomeClaimDto?>>;
public sealed record CompleteStudentWelcomeCommand(Guid UserId, Guid Token) : IRequest<ApiResponse<bool>>;
public sealed record ReleaseStudentWelcomeCommand(Guid UserId, Guid Token) : IRequest<ApiResponse<bool>>;

public sealed class StudentWelcomeCommandHandler(IAppDbContext db, TimeProvider? clock = null) :
    IRequestHandler<ClaimStudentWelcomeCommand, ApiResponse<WelcomeClaimDto?>>,
    IRequestHandler<CompleteStudentWelcomeCommand, ApiResponse<bool>>,
    IRequestHandler<ReleaseStudentWelcomeCommand, ApiResponse<bool>>
{
    private DateTime UtcNow => (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;

    public async Task<ApiResponse<WelcomeClaimDto?>> Handle(ClaimStudentWelcomeCommand request, CancellationToken ct)
    {
        var now = UtcNow;
        var today = CairoTime.ToDate(now);
        var token = Guid.NewGuid();
        var expires = now.AddMinutes(3);
        // One atomic update arbitrates concurrent devices/tabs; no Redis or browser flag is authoritative.
        var claimed = await db.StudentProfiles
            .Where(p => p.UserId == request.UserId
                && (p.WelcomeClaimExpiresAt == null || p.WelcomeClaimExpiresAt <= now)
                && (p.FirstWelcomeCompletedAt == null || p.LastWelcomeDate == null || p.LastWelcomeDate < today))
            .ExecuteUpdateAsync(update => update
                .SetProperty(p => p.WelcomeClaimToken, token)
                .SetProperty(p => p.WelcomeClaimExpiresAt, expires)
                .SetProperty(p => p.WelcomeClaimDate, today), ct);
        if (claimed == 0) return ApiResponse<WelcomeClaimDto?>.Ok(null);
        var first = await db.StudentProfiles.AsNoTracking()
            .Where(p => p.UserId == request.UserId && p.WelcomeClaimToken == token)
            .Select(p => p.FirstWelcomeCompletedAt == null).SingleAsync(ct);
        return ApiResponse<WelcomeClaimDto?>.Ok(new(token, first ? "first" : "returning", expires));
    }

    public async Task<ApiResponse<bool>> Handle(CompleteStudentWelcomeCommand request, CancellationToken ct)
    {
        var now = UtcNow;
        var changed = await db.StudentProfiles
            .Where(p => p.UserId == request.UserId && p.WelcomeClaimToken == request.Token
                && p.WelcomeClaimExpiresAt > now && p.WelcomeClaimDate != null)
            .ExecuteUpdateAsync(update => update
                .SetProperty(p => p.FirstWelcomeCompletedAt, p => p.FirstWelcomeCompletedAt ?? now)
                .SetProperty(p => p.LastWelcomeDate, p => p.WelcomeClaimDate)
                .SetProperty(p => p.WelcomeClaimExpiresAt, (DateTime?)null), ct);
        // Preserve the receipt token until the next claim so retries after a lost response are idempotent.
        var completed = changed == 1 || await db.StudentProfiles.AsNoTracking().AnyAsync(p =>
            p.UserId == request.UserId && p.WelcomeClaimToken == request.Token
            && p.WelcomeClaimExpiresAt == null && p.FirstWelcomeCompletedAt != null
            && p.LastWelcomeDate == p.WelcomeClaimDate, ct);
        return ApiResponse<bool>.Ok(completed);
    }

    public async Task<ApiResponse<bool>> Handle(ReleaseStudentWelcomeCommand request, CancellationToken ct)
    {
        await db.StudentProfiles.Where(p => p.UserId == request.UserId
            && p.WelcomeClaimToken == request.Token && p.WelcomeClaimExpiresAt != null)
            .ExecuteUpdateAsync(update => update
                .SetProperty(p => p.WelcomeClaimToken, (Guid?)null)
                .SetProperty(p => p.WelcomeClaimExpiresAt, (DateTime?)null)
                .SetProperty(p => p.WelcomeClaimDate, (DateOnly?)null), ct);
        return ApiResponse<bool>.Ok(true);
    }
}
