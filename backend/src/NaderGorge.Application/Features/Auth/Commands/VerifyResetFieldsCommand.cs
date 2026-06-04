using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Auth.Commands;

public record VerifyResetFieldsCommand(
    string PhoneNumber,
    string ParentPhone,
    string Governorate,
    string District
) : IRequest<ApiResponse<VerifyResetFieldsResponse>>;

public record VerifyResetFieldsResponse(string ResetToken);

public class VerifyResetFieldsCommandValidator : AbstractValidator<VerifyResetFieldsCommand>
{
    public VerifyResetFieldsCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().Matches(@"^01[0125]\d{8}$")
            .WithMessage("تأكد من كتابة رقم الهاتف بشكل صحيح، مثال: 01012345678");

        RuleFor(x => x.ParentPhone)
            .NotEmpty().Matches(@"^01[0125]\d{8}$")
            .WithMessage("تأكد من كتابة رقم ولي الأمر بشكل صحيح");

        RuleFor(x => x.Governorate)
            .NotEmpty().WithMessage("يرجى اختيار المحافظة");

        RuleFor(x => x.District)
            .NotEmpty().WithMessage("يرجى اختيار المنطقة / الحي");
    }
}

public class VerifyResetFieldsCommandHandler : IRequestHandler<VerifyResetFieldsCommand, ApiResponse<VerifyResetFieldsResponse>>
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokens;

    public VerifyResetFieldsCommandHandler(IAppDbContext db, ITokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    public async Task<ApiResponse<VerifyResetFieldsResponse>> Handle(VerifyResetFieldsCommand request, CancellationToken ct)
    {
        // Find user by phone number
        var user = await _db.Users
            .Include(u => u.StudentProfile)
            .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber, ct);

        if (user == null || user.StudentProfile == null)
            throw new InvalidOperationException("عذرًا، البيانات المدخلة غير متطابقة مع أي حساب مسجل لدينا.");

        // Check if the user has the Student role
        var isStudent = await _db.UserRoles
            .AnyAsync(ur => ur.UserId == user.Id && ur.Role.Type == RoleType.Student, ct);

        if (!isStudent)
            throw new InvalidOperationException("عذرًا، البيانات المدخلة غير متطابقة مع أي حساب مسجل لدينا.");

        // Match Governorate and District
        var profileGov = user.StudentProfile.Governorate?.Trim() ?? string.Empty;
        var profileDist = user.StudentProfile.District?.Trim() ?? string.Empty;
        var inputGov = request.Governorate?.Trim() ?? string.Empty;
        var inputDist = request.District?.Trim() ?? string.Empty;

        bool govMatch = string.Equals(profileGov, inputGov, StringComparison.OrdinalIgnoreCase);
        bool distMatch = string.Equals(profileDist, inputDist, StringComparison.OrdinalIgnoreCase);

        if (!govMatch || !distMatch)
            throw new InvalidOperationException("عذرًا، البيانات المدخلة غير متطابقة مع أي حساب مسجل لدينا.");

        // Match Parent Phone against Father, Mother, or Secondary Parent phone columns
        var inputParentPhone = request.ParentPhone.Trim();
        var profileFatherPhone = user.StudentProfile.ParentPhone?.Trim();
        var profileMotherPhone = user.StudentProfile.MotherPhone?.Trim();
        var profileSecParentPhone = user.StudentProfile.SecondaryParentPhone?.Trim();

        bool parentMatch = 
            (!string.IsNullOrEmpty(profileFatherPhone) && profileFatherPhone == inputParentPhone) ||
            (!string.IsNullOrEmpty(profileMotherPhone) && profileMotherPhone == inputParentPhone) ||
            (!string.IsNullOrEmpty(profileSecParentPhone) && profileSecParentPhone == inputParentPhone);

        if (!parentMatch)
            throw new InvalidOperationException("عذرًا، البيانات المدخلة غير متطابقة مع أي حساب مسجل لدينا.");

        // Generate a temporary 10-minute JWT token with "PasswordReset" role/claim
        var resetToken = _tokens.GenerateAccessToken(
            user, 
            new[] { "PasswordReset" }, 
            TimeSpan.FromMinutes(10)
        );

        return ApiResponse<VerifyResetFieldsResponse>.Ok(new VerifyResetFieldsResponse(resetToken), "تم التحقق بنجاح.");
    }
}
