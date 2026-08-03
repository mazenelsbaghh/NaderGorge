using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Student.Commands;

public record UpdateStudentProfileCommand(
    Guid UserId,
    string FullName,
    string Address,
    string? SecondaryPhone,
    string? ParentPhone,
    string? SecondaryParentPhone,
    string? MotherPhone,
    string? SchoolName,
    string EducationStage,
    string GradeLevel,
    string? StudyTrack
) : IRequest<ApiResponse<bool>>;

public class UpdateStudentProfileCommandHandler : IRequestHandler<UpdateStudentProfileCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;

    public UpdateStudentProfileCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<bool>> Handle(UpdateStudentProfileCommand request, CancellationToken ct)
    {
        var profile = await _db.StudentProfiles
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, ct);

        if (profile == null)
        {
            return ApiResponse<bool>.Fail("ملف الطالب غير موجود");
        }

        var fullName = request.FullName.Trim();
        if (fullName.Length < 2)
            return ApiResponse<bool>.Fail("الاسم يجب أن يتكون من حرفين على الأقل.");

        if (!Enum.TryParse<EducationStage>(request.EducationStage, true, out var educationStage)
            || !Enum.TryParse<GradeLevel>(request.GradeLevel, true, out var gradeLevel))
        {
            return ApiResponse<bool>.Fail("المرحلة أو الصف الدراسي غير صالح.");
        }

        StudyTrack? studyTrack = null;
        if (!string.IsNullOrWhiteSpace(request.StudyTrack))
        {
            if (!Enum.TryParse<StudyTrack>(request.StudyTrack, true, out var parsedStudyTrack))
                return ApiResponse<bool>.Fail("الشعبة الدراسية غير صالحة.");

            studyTrack = parsedStudyTrack;
        }

        if (new AcademicValidationService().Validate(educationStage, gradeLevel, studyTrack).Count > 0)
            return ApiResponse<bool>.Fail("بيانات المرحلة والصف والشعبة غير متوافقة.");

        profile.User.FullName = fullName;
        profile.Address = request.Address.Trim();
        profile.SecondaryPhone = request.SecondaryPhone;
        profile.ParentPhone = request.ParentPhone;
        profile.SecondaryParentPhone = request.SecondaryParentPhone;
        profile.MotherPhone = request.MotherPhone;
        profile.SchoolName = request.SchoolName;
        profile.EducationStage = educationStage;
        profile.GradeLevel = gradeLevel;
        profile.StudyTrack = studyTrack;

        await _db.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true);
    }
}
