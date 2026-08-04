using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record UpdateStudentProfileCommand(
    Guid StudentId,
    string? FullName,
    string? Phone,
    string? ParentPhone,
    string? SecondaryPhone,
    string? MotherPhone,
    string? SecondaryParentPhone,
    string? Nationality,
    string? Governorate,
    string? District,
    string? Address,
    string? SchoolName,
    string? DateOfBirth,
    string? FatherDateOfBirth,
    string? MotherDateOfBirth,
    string? Gender,
    string? EducationStage,
    string? GradeLevel,
    string? StudyTrack,
    string? SchoolType,
    string? StudentCode,
    bool? IsFatherAlive,
    bool? IsMotherAlive,
    Guid AdminId
) : IRequest<ApiResponse>;

public class UpdateStudentProfileCommandHandler : IRequestHandler<UpdateStudentProfileCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public UpdateStudentProfileCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(UpdateStudentProfileCommand r, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.StudentProfile)
            .FirstOrDefaultAsync(u => u.Id == r.StudentId, ct);

        if (user == null) return ApiResponse.Fail("Student not found.");

        // Update User-level fields
        if (!string.IsNullOrWhiteSpace(r.FullName)) user.FullName = r.FullName;
        if (!string.IsNullOrWhiteSpace(r.Phone) && !string.Equals(user.PhoneNumber, r.Phone, StringComparison.Ordinal))
        {
            var phoneAlreadyAssigned = await _db.Users
                .AsNoTracking()
                .AnyAsync(existingUser => existingUser.Id != r.StudentId && existingUser.PhoneNumber == r.Phone, ct);

            if (phoneAlreadyAssigned)
                return ApiResponse.Fail("رقم هاتف الطالب مسجل بالفعل في حساب آخر.");

            user.PhoneNumber = r.Phone;
        }

        // Update StudentProfile fields
        var profile = user.StudentProfile;
        if (profile == null) return ApiResponse.Fail("Student profile not found.");

        if (r.ParentPhone != null) profile.ParentPhone = r.ParentPhone;
        if (r.SecondaryPhone != null) profile.SecondaryPhone = r.SecondaryPhone;
        if (r.MotherPhone != null) profile.MotherPhone = r.MotherPhone;
        if (r.SecondaryParentPhone != null) profile.SecondaryParentPhone = r.SecondaryParentPhone;
        if (r.Nationality != null) profile.Nationality = r.Nationality;
        if (r.Governorate != null) profile.Governorate = r.Governorate;
        if (r.District != null) profile.District = r.District;
        if (r.Address != null) profile.Address = r.Address;
        if (r.SchoolName != null) profile.SchoolName = r.SchoolName;
        if (r.StudentCode != null) profile.StudentCode = r.StudentCode;
        if (r.IsFatherAlive.HasValue) profile.IsFatherAlive = r.IsFatherAlive.Value;
        if (r.IsMotherAlive.HasValue) profile.IsMotherAlive = r.IsMotherAlive.Value;

        if (!string.IsNullOrWhiteSpace(r.DateOfBirth) && DateTime.TryParse(r.DateOfBirth, out var dob))
            profile.DateOfBirth = dob;

        if (r.FatherDateOfBirth != null)
            profile.FatherDateOfBirth = string.IsNullOrWhiteSpace(r.FatherDateOfBirth)
                ? null
                : DateTime.TryParse(r.FatherDateOfBirth, out var fatherDob) ? fatherDob : profile.FatherDateOfBirth;

        if (r.MotherDateOfBirth != null)
            profile.MotherDateOfBirth = string.IsNullOrWhiteSpace(r.MotherDateOfBirth)
                ? null
                : DateTime.TryParse(r.MotherDateOfBirth, out var motherDob) ? motherDob : profile.MotherDateOfBirth;

        if (!string.IsNullOrWhiteSpace(r.Gender) && Enum.TryParse<Gender>(r.Gender, true, out var gender))
            profile.Gender = gender;

        var nextStage = profile.EducationStage;
        var nextGrade = profile.GradeLevel;
        var nextTrack = profile.StudyTrack;

        if (!string.IsNullOrWhiteSpace(r.EducationStage))
        {
            if (!Enum.TryParse<EducationStage>(r.EducationStage, true, out nextStage))
                return ApiResponse.Fail("المرحلة الدراسية غير صالحة.");
        }

        if (r.StudyTrack != null)
        {
            if (string.IsNullOrWhiteSpace(r.StudyTrack))
            {
                nextTrack = null;
            }
            else if (Enum.TryParse<StudyTrack>(r.StudyTrack, true, out var track))
            {
                nextTrack = track;
            }
            else
            {
                return ApiResponse.Fail("الشعبة الدراسية غير صالحة.");
            }
        }

        if (!string.IsNullOrWhiteSpace(r.GradeLevel))
        {
            if (!Enum.TryParse<GradeLevel>(r.GradeLevel, true, out nextGrade))
                return ApiResponse.Fail("الصف الدراسي غير صالح.");
        }

        var academicErrors = new AcademicValidationService().Validate(nextStage, nextGrade, nextTrack);
        if (academicErrors.Count > 0)
            return ApiResponse.Fail("بيانات المرحلة والصف والشعبة غير متوافقة.");

        profile.EducationStage = nextStage;
        profile.GradeLevel = nextGrade;
        profile.StudyTrack = nextTrack;

        if (r.SchoolType != null)
            profile.SchoolType = Enum.TryParse<SchoolType>(r.SchoolType, true, out var schoolType) ? schoolType : null;

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "UpdateStudentProfile",
            EntityType = "User",
            EntityId = r.StudentId,
            PerformedByUserId = r.AdminId,
            NewValues = "Admin updated student profile fields",
            IpAddress = "System"
        });

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok("Student profile updated successfully.");
    }
}
