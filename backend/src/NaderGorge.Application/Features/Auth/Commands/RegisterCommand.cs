using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Auth.Commands;

// ---- Register Command (Single-Flow, Phase 3) ----
public record RegisterCommand(
    // Personal data
    string FullName,
    string PhoneNumber,
    string? SecondaryPhone,           // NEW: Student's optional 2nd phone
    string Password,
    DateTime DateOfBirth,
    Gender Gender,
    string Governorate,
    string? District,                  // NEW: Neighborhood/area
    string Address,

    // Parent data
    string ParentPhone,
    string? SecondaryParentPhone,      // NEW: Parent's optional 2nd phone
    bool IsFatherAlive,
    bool IsMotherAlive,

    // Academic data (conditional)
    EducationStage EducationStage,
    GradeLevel GradeLevel,
    StudyTrack? StudyTrack
) : IRequest<ApiResponse<RegisterResponse>>;

public record RegisterResponse(Guid UserId, string Message);

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        // Personal
        RuleFor(x => x.FullName)
            .NotEmpty().MaximumLength(200)
            .Must(name => name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 4)
            .WithMessage("Full name must contain at least 4 parts (الاسم رباعي)");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().MaximumLength(20)
            .Matches(@"^01[0125]\d{8}$")
            .WithMessage("Invalid Egyptian phone number");

        RuleFor(x => x.SecondaryPhone)
            .Matches(@"^01[0125]\d{8}$")
            .When(x => !string.IsNullOrEmpty(x.SecondaryPhone))
            .WithMessage("Invalid Egyptian phone number");

        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);

        RuleFor(x => x.DateOfBirth)
            .LessThan(DateTime.UtcNow)
            .WithMessage("Date of birth must be in the past");

        RuleFor(x => x.Governorate).NotEmpty();
        RuleFor(x => x.District).MaximumLength(200);
        RuleFor(x => x.Address).NotEmpty();

        // Parent
        RuleFor(x => x.ParentPhone)
            .NotEmpty().MaximumLength(20)
            .Matches(@"^01[0125]\d{8}$")
            .WithMessage("Invalid Egyptian parent phone number");

        RuleFor(x => x.SecondaryParentPhone)
            .Matches(@"^01[0125]\d{8}$")
            .When(x => !string.IsNullOrEmpty(x.SecondaryParentPhone))
            .WithMessage("Invalid Egyptian parent phone number");

        // Academic — basic required fields
        RuleFor(x => x.EducationStage).IsInEnum();
        RuleFor(x => x.GradeLevel).IsInEnum();

        // Academic — conditional: track required for 2nd level grades
        RuleFor(x => x.StudyTrack)
            .NotNull()
            .When(x => AcademicValidationService.RequiresTrack(x.GradeLevel))
            .WithMessage("Study track is required for this grade level");

        RuleFor(x => x.StudyTrack)
            .Null()
            .When(x => !AcademicValidationService.RequiresTrack(x.GradeLevel))
            .WithMessage("Study track must not be specified for this grade level");
    }
}

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, ApiResponse<RegisterResponse>>
{
    private readonly IAppDbContext _db;
    private readonly AcademicValidationService _academicValidator;

    public RegisterCommandHandler(IAppDbContext db, AcademicValidationService academicValidator)
    {
        _db = db;
        _academicValidator = academicValidator;
    }

    public async Task<ApiResponse<RegisterResponse>> Handle(RegisterCommand request, CancellationToken ct)
    {
        // Check duplicate phone
        if (await _db.Users.AnyAsync(u => u.PhoneNumber == request.PhoneNumber, ct))
            throw new InvalidOperationException("رقم الهاتف الأساسي مسجل بالفعل. استخدم رقمًا آخر.");

        // Validate academic field matrix
        var academicErrors = _academicValidator.Validate(request.EducationStage, request.GradeLevel, request.StudyTrack);
        if (academicErrors.Count > 0)
            throw new ValidationException(string.Join("; ", academicErrors));

        var studentRole = await _db.Roles.FirstAsync(r => r.Type == RoleType.Student, ct);

        var user = new User
        {
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsProfileComplete = true // Single-flow: profile complete on registration
        };

        // Create StudentProfile with all fields
        user.StudentProfile = new StudentProfile
        {
            DateOfBirth = request.DateOfBirth.ToUniversalTime(),
            Gender = request.Gender,
            Governorate = request.Governorate,
            District = request.District,
            Address = request.Address,
            SecondaryPhone = request.SecondaryPhone,
            ParentPhone = request.ParentPhone,
            SecondaryParentPhone = request.SecondaryParentPhone,
            IsFatherAlive = request.IsFatherAlive,
            IsMotherAlive = request.IsMotherAlive,
            EducationStage = request.EducationStage,
            GradeLevel = request.GradeLevel,
            StudyTrack = request.StudyTrack
        };

        // Create initial balance record (starts at 0)
        user.StudentBalance = new StudentBalance
        {
            CurrentBalance = 0m
        };

        _db.Users.Add(user);
        _db.UserRoles.Add(new UserRole { User = user, Role = studentRole });
        await _db.SaveChangesAsync(ct);

        return ApiResponse<RegisterResponse>.Ok(new RegisterResponse(user.Id, "Registration successful. Please log in."));
    }
}
