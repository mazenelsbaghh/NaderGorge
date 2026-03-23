using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Codes.Commands;

public record ActivateCodeCommand(Guid UserId, string Code) : IRequest<ApiResponse<ActivateCodeResponse>>;
public record ActivateCodeResponse(Guid GrantId, string Message, bool RequiresProfileCompletion);

public class ActivateCodeCommandValidator : AbstractValidator<ActivateCodeCommand>
{
    public ActivateCodeCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MinimumLength(6);
    }
}

public class ActivateCodeCommandHandler : IRequestHandler<ActivateCodeCommand, ApiResponse<ActivateCodeResponse>>
{
    private readonly IAppDbContext _db;

    public ActivateCodeCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<ActivateCodeResponse>> Handle(ActivateCodeCommand request, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.StudentProfile)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
            ?? throw new KeyNotFoundException("User not found");

        var accessCode = await _db.AccessCodes
            .Include(c => c.CodeGroup)
            .FirstOrDefaultAsync(c => c.CodePlaintext == request.Code && !c.IsConsumed, ct)
            ?? throw new KeyNotFoundException("Invalid or already used code");

        // Mark code as consumed
        accessCode.IsConsumed = true;
        accessCode.ConsumedByUserId = user.Id;
        accessCode.ConsumedAt = DateTime.UtcNow;

        // Create access grant
        var grant = new StudentAccessGrant
        {
            UserId = user.Id,
            PackageId = accessCode.CodeGroup.PackageId,
            LessonId = accessCode.CodeGroup.LessonId,
            AccessCodeId = accessCode.Id,
            IsActive = true
        };
        _db.StudentAccessGrants.Add(grant);
        await _db.SaveChangesAsync(ct);

        var requiresProfile = !user.IsProfileComplete;
        var message = requiresProfile
            ? "Code activated. Please complete your profile."
            : "Code activated successfully.";

        return ApiResponse<ActivateCodeResponse>.Ok(new ActivateCodeResponse(grant.Id, message, requiresProfile));
    }
}
