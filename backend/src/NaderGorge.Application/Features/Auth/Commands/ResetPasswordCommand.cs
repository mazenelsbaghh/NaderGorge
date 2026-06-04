using System.Security.Claims;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Auth.Commands;

public record ResetPasswordCommand(
    string Token,
    string NewPassword
) : IRequest<ApiResponse>;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("رمز إعادة التعيين مطلوب");

        RuleFor(x => x.NewPassword)
            .NotEmpty().MinimumLength(8)
            .WithMessage("يجب أن تتكون كلمة المرور من 8 أحرف على الأقل");
    }
}

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokens;

    public ResetPasswordCommandHandler(IAppDbContext db, ITokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    public async Task<ApiResponse> Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        // Validate Token signature and expiration
        var principal = _tokens.ValidateToken(request.Token);
        if (principal == null)
            throw new UnauthorizedAccessException("رمز إعادة التعيين منتهي الصلاحية أو غير صالح.");

        // Check if token contains the PasswordReset role
        var isResetAuthorized = principal.IsInRole("PasswordReset");
        if (!isResetAuthorized)
            throw new UnauthorizedAccessException("رمز إعادة التعيين منتهي الصلاحية أو غير صالح.");

        // Extract User ID from NameIdentifier claim
        var userIdStr = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedAccessException("رمز إعادة التعيين منتهي الصلاحية أو غير صالح.");

        // Find the user in database
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null)
            throw new UnauthorizedAccessException("رمز إعادة التعيين منتهي الصلاحية أو غير صالح.");

        // Hash and update the password
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        var activeRefreshTokens = await _db.RefreshTokens
            .Where(rt => rt.UserId == user.Id && !rt.IsRevoked)
            .ToListAsync(ct);

        foreach (var refreshToken in activeRefreshTokens)
        {
            refreshToken.IsRevoked = true;
        }

        await _db.SaveChangesAsync(ct);

        return ApiResponse.Ok("تم تغيير كلمة المرور بنجاح. يمكنك تسجيل الدخول الآن.");
    }
}
