using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Auth.Commands;

// ---- Register Command (Single-Flow, Phase 3 — Student Profile V2) ----
public record RegisterCommand(
    // ── Personal data ─────────────────────────────────────────────────────
    string FullName,
    string PhoneNumber,
    string? SecondaryPhone,              // Student's optional 2nd phone
    string Password,
    DateTime DateOfBirth,
    Gender Gender,
    string? Nationality,                 // NEW: Arab nationality (max 100 chars)
    string Governorate,
    string? District,                    // Neighborhood/area
    string Address,

    // ── Parent data ───────────────────────────────────────────────────────
    string? ParentPhone,                 // Father's phone — required only if IsFatherAlive
    string? SecondaryParentPhone,        // Parent's optional 2nd phone
    string? MotherPhone,                 // NEW: Mother's phone (optional)
    bool IsFatherAlive,
    bool IsMotherAlive,
    DateTime? FatherDateOfBirth,         // NEW: Father's date of birth (optional)
    DateTime? MotherDateOfBirth,         // NEW: Mother's date of birth (optional)

    // ── School data ───────────────────────────────────────────────────────
    string? SchoolName,                  // NEW: School name (optional)
    SchoolType? SchoolType,              // NEW: School type enum (optional)

    // ── Academic data (conditional) ───────────────────────────────────────
    EducationStage EducationStage,
    GradeLevel GradeLevel,
    StudyTrack? StudyTrack,
    string? AvatarSlug
) : IRequest<ApiResponse<RegisterResponse>>;

public record RegisterResponse(Guid UserId, string Message);

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        // ── Personal ──────────────────────────────────────────────────────
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

        RuleFor(x => x.Nationality)
            .MaximumLength(100)
            .When(x => x.Nationality != null);

        RuleFor(x => x.Governorate).NotEmpty();
        RuleFor(x => x.District).MaximumLength(200);
        RuleFor(x => x.Address).NotEmpty();

        // ── Parent — conditional on alive status ──────────────────────────
        RuleFor(x => x.ParentPhone)
            .NotEmpty().MaximumLength(20)
            .Matches(@"^01[0125]\d{8}$")
            .When(x => x.IsFatherAlive)
            .WithMessage("Requires a valid father phone number when father is alive");

        RuleFor(x => x.ParentPhone)
            .Matches(@"^01[0125]\d{8}$")
            .When(x => !x.IsFatherAlive && !string.IsNullOrEmpty(x.ParentPhone))
            .WithMessage("Invalid Egyptian phone number");

        RuleFor(x => x.SecondaryParentPhone)
            .Matches(@"^01[0125]\d{8}$")
            .When(x => !string.IsNullOrEmpty(x.SecondaryParentPhone))
            .WithMessage("Invalid Egyptian parent phone number");

        RuleFor(x => x.MotherPhone)
            .Matches(@"^01[0125]\d{8}$")
            .When(x => !string.IsNullOrEmpty(x.MotherPhone))
            .WithMessage("Invalid Egyptian mother phone number");

        RuleFor(x => x.FatherDateOfBirth)
            .LessThan(DateTime.UtcNow)
            .When(x => x.FatherDateOfBirth.HasValue)
            .WithMessage("Father's date of birth must be in the past");

        RuleFor(x => x.MotherDateOfBirth)
            .LessThan(DateTime.UtcNow)
            .When(x => x.MotherDateOfBirth.HasValue)
            .WithMessage("Mother's date of birth must be in the past");

        // ── School ────────────────────────────────────────────────────────
        RuleFor(x => x.SchoolName)
            .MaximumLength(300)
            .When(x => x.SchoolName != null);

        RuleFor(x => x.SchoolType)
            .IsInEnum()
            .When(x => x.SchoolType != null);

        // ── Academic — basic required fields ──────────────────────────────
        RuleFor(x => x.EducationStage).IsInEnum();
        RuleFor(x => x.GradeLevel).IsInEnum();

        // ── Academic — conditional: track required for 2nd level grades ───
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
            // Personal
            DateOfBirth = request.DateOfBirth.ToUniversalTime(),
            Gender = request.Gender,
            Nationality = request.Nationality,
            Governorate = request.Governorate,
            District = request.District,
            Address = request.Address,
            SecondaryPhone = request.SecondaryPhone,

            // Parent
            ParentPhone = request.ParentPhone,
            SecondaryParentPhone = request.SecondaryParentPhone,
            MotherPhone = request.MotherPhone,
            IsFatherAlive = request.IsFatherAlive,
            IsMotherAlive = request.IsMotherAlive,
            FatherDateOfBirth = request.FatherDateOfBirth?.ToUniversalTime(),
            MotherDateOfBirth = request.MotherDateOfBirth?.ToUniversalTime(),

            // School
            SchoolName = request.SchoolName,
            SchoolType = request.SchoolType,

            // Academic
            EducationStage = request.EducationStage,
            GradeLevel = request.GradeLevel,
            StudyTrack = request.StudyTrack,
            AvatarSlug = request.AvatarSlug
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
