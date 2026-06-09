using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Codes.Queries;

public record ValidateCodeResponseDto(
    string Code,
    CodeType CodeType,
    Guid? TargetId,
    string TargetName,
    Guid TeacherId,
    string TeacherName,
    string? TeacherProfileImageUrl
);

public record ValidateCodeQuery(string Code) : IRequest<ApiResponse<ValidateCodeResponseDto>>;

public class ValidateCodeQueryHandler : IRequestHandler<ValidateCodeQuery, ApiResponse<ValidateCodeResponseDto>>
{
    private readonly IAppDbContext _db;

    public ValidateCodeQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<ValidateCodeResponseDto>> Handle(ValidateCodeQuery request, CancellationToken ct)
    {
        var accessCode = await _db.AccessCodes
            .AsNoTracking()
            .Include(c => c.CodeGroup)
                .ThenInclude(cg => cg.Teacher)
                    .ThenInclude(t => t.User)
            .FirstOrDefaultAsync(c => c.CodePlaintext == request.Code, ct);

        if (accessCode == null)
            return ApiResponse<ValidateCodeResponseDto>.Fail("الكود غير صحيح أو تم استخدامه من قبل");

        if (accessCode.IsConsumed)
            return ApiResponse<ValidateCodeResponseDto>.Fail("الكود تم استخدامه من قبل");

        var now = DateTime.UtcNow;
        if (accessCode.ExpiresAt.HasValue && accessCode.ExpiresAt.Value < now)
            return ApiResponse<ValidateCodeResponseDto>.Fail("انتهت صلاحية هذا الكود");

        if (accessCode.CodeGroup.ExpiresAt.HasValue && accessCode.CodeGroup.ExpiresAt.Value < now)
            return ApiResponse<ValidateCodeResponseDto>.Fail("انتهت صلاحية هذه المجموعة من الأكواد");

        var codeGroup = accessCode.CodeGroup;
        var codeType = codeGroup.CodeType;

        Guid? targetId = null;
        string targetName = "شحن رصيد";
        Guid teacherId = codeGroup.TeacherId;
        string teacherName = codeGroup.Teacher?.User?.FullName ?? "غير معروف";
        string? teacherProfileImageUrl = codeGroup.Teacher?.ProfileImageUrl;

        switch (codeType)
        {
            case CodeType.Package:
                targetId = codeGroup.PackageId;
                var pkg = await _db.Packages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == codeGroup.PackageId, ct);
                targetName = pkg?.Name ?? "كورس دراسي";
                break;
            case CodeType.Term:
                targetId = codeGroup.TermId;
                var term = await _db.Terms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == codeGroup.TermId, ct);
                targetName = term?.Title ?? "ترم دراسي";
                break;
            case CodeType.Month:
                targetId = codeGroup.ContentSectionId;
                var section = await _db.ContentSections.AsNoTracking().FirstOrDefaultAsync(s => s.Id == codeGroup.ContentSectionId, ct);
                targetName = section?.Title ?? "شهر/قسم دراسي";
                break;
            case CodeType.Lesson:
                targetId = codeGroup.LessonId;
                var lesson = await _db.Lessons.AsNoTracking().FirstOrDefaultAsync(l => l.Id == codeGroup.LessonId, ct);
                targetName = lesson?.Title ?? "حصة دراسية";
                break;
            case CodeType.Exam:
                targetId = codeGroup.ExamId;
                var exam = await _db.Exams.AsNoTracking().FirstOrDefaultAsync(e => e.Id == codeGroup.ExamId, ct);
                targetName = exam?.Title ?? "امتحان";
                break;
            case CodeType.Video:
                targetName = "فيديوهات حصة";
                break;
            case CodeType.Balance:
                targetName = $"شحن رصيد بقيمة {codeGroup.BalanceAmount} جنيه";
                break;
        }

        var dto = new ValidateCodeResponseDto(
            request.Code,
            codeType,
            targetId,
            targetName,
            teacherId,
            teacherName,
            teacherProfileImageUrl
        );

        return ApiResponse<ValidateCodeResponseDto>.Ok(dto);
    }
}
