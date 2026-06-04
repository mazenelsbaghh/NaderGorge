using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
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
    string? Governorate,
    string? District,
    string? Address,
    string? SchoolName,
    string? DateOfBirth,
    string? Gender,
    string? EducationStage,
    string? GradeLevel,
    string? StudyTrack,
    string? SchoolType,
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
        if (!string.IsNullOrWhiteSpace(r.Phone)) user.PhoneNumber = r.Phone;

        // Update StudentProfile fields
        var profile = user.StudentProfile;
        if (profile == null) return ApiResponse.Fail("Student profile not found.");

        if (r.ParentPhone != null) profile.ParentPhone = r.ParentPhone;
        if (r.SecondaryPhone != null) profile.SecondaryPhone = r.SecondaryPhone;
        if (r.MotherPhone != null) profile.MotherPhone = r.MotherPhone;
        if (r.Governorate != null) profile.Governorate = r.Governorate;
        if (r.District != null) profile.District = r.District;
        if (r.Address != null) profile.Address = r.Address;
        if (r.SchoolName != null) profile.SchoolName = r.SchoolName;
        if (r.IsFatherAlive.HasValue) profile.IsFatherAlive = r.IsFatherAlive.Value;
        if (r.IsMotherAlive.HasValue) profile.IsMotherAlive = r.IsMotherAlive.Value;

        if (!string.IsNullOrWhiteSpace(r.DateOfBirth) && DateTime.TryParse(r.DateOfBirth, out var dob))
            profile.DateOfBirth = dob;

        if (!string.IsNullOrWhiteSpace(r.Gender) && Enum.TryParse<Gender>(r.Gender, true, out var gender))
            profile.Gender = gender;

        if (!string.IsNullOrWhiteSpace(r.EducationStage) && Enum.TryParse<EducationStage>(r.EducationStage, true, out var stage))
            profile.EducationStage = stage;

        if (!string.IsNullOrWhiteSpace(r.GradeLevel) && Enum.TryParse<GradeLevel>(r.GradeLevel, true, out var grade))
            profile.GradeLevel = grade;

        if (r.StudyTrack != null)
            profile.StudyTrack = Enum.TryParse<StudyTrack>(r.StudyTrack, true, out var track) ? track : null;

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
