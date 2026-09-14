using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record GetCodeGroupCodesQuery(Guid GroupId) : IRequest<ApiResponse<List<CodeDetailDto>>>;

public record CodeDetailDto(
    string Code,
    long SerialNumber,
    bool IsUsed,
    DateTime? UsedAt,
    Guid? UsedByUserId,
    string? UsedByStudentName,
    string? UsedByStudentPhone,
    string RedemptionSummary);

public class GetCodeGroupCodesQueryHandler : IRequestHandler<GetCodeGroupCodesQuery, ApiResponse<List<CodeDetailDto>>>
{
    private readonly IAppDbContext _db;

    public GetCodeGroupCodesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<List<CodeDetailDto>>> Handle(GetCodeGroupCodesQuery request, CancellationToken ct)
    {
        var group = await _db.CodeGroups
            .Include(cg => cg.AccessCodes)
                .ThenInclude(ac => ac.ConsumedByUser)
            .FirstOrDefaultAsync(cg => cg.Id == request.GroupId, ct);

        if (group == null) return ApiResponse<List<CodeDetailDto>>.Fail("Code Group not found");

        var redemptionSummary = await BuildRedemptionSummaryAsync(group, ct);

        var dtos = group.AccessCodes.OrderBy(c => c.CreatedAt).Select(c => new CodeDetailDto(
            c.CodePlaintext ?? c.CodeHash,
            c.SerialNumber,
            c.IsConsumed,
            c.ConsumedAt,
            c.ConsumedByUserId,
            c.ConsumedByUser != null ? c.ConsumedByUser.FullName : null,
            c.ConsumedByUser != null ? c.ConsumedByUser.PhoneNumber : null,
            redemptionSummary
        )).ToList();

        return ApiResponse<List<CodeDetailDto>>.Ok(dtos);
    }

    private async Task<string> BuildRedemptionSummaryAsync(NaderGorge.Domain.Entities.CodeGroup group, CancellationToken ct)
    {
        switch (group.CodeType)
        {
            case CodeType.Balance:
                return $"شحن رصيد بقيمة {group.BalanceAmount ?? 0:0.##} ج.م";
            case CodeType.Package:
                return group.PackageId.HasValue
                    ? $"تفعيل باقة: {await _db.Packages.Where(item => item.Id == group.PackageId.Value).Select(item => item.Name).FirstOrDefaultAsync(ct) ?? group.Name}"
                    : "تفعيل باقة";
            case CodeType.Term:
                return group.TermId.HasValue
                    ? $"تفعيل ترم: {await _db.Terms.Where(item => item.Id == group.TermId.Value).Select(item => item.Title).FirstOrDefaultAsync(ct) ?? group.Name}"
                    : "تفعيل ترم";
            case CodeType.Month:
                return group.ContentSectionId.HasValue
                    ? $"تفعيل قسم / شهر: {await _db.ContentSections.Where(item => item.Id == group.ContentSectionId.Value).Select(item => item.Title).FirstOrDefaultAsync(ct) ?? group.Name}"
                    : "تفعيل قسم / شهر";
            case CodeType.Lesson:
                return group.LessonId.HasValue
                    ? $"تفعيل حصة: {await _db.Lessons.Where(item => item.Id == group.LessonId.Value).Select(item => item.Title).FirstOrDefaultAsync(ct) ?? group.Name}"
                    : "تفعيل حصة";
            case CodeType.Exam:
                var publicExamTitle = group.PublicExamProductId.HasValue
                    ? await _db.PublicExamProducts.Where(item => item.Id == group.PublicExamProductId.Value).Select(item => item.Exam.Title).FirstOrDefaultAsync(ct)
                    : null;
                var examTitle = publicExamTitle ?? (group.ExamId.HasValue
                    ? await _db.Exams.Where(item => item.Id == group.ExamId.Value).Select(item => item.Title).FirstOrDefaultAsync(ct)
                    : null);
                return $"تفعيل امتحان: {examTitle ?? group.Name}";
            case CodeType.Video:
                var videoType = group.VideoTypeId.HasValue
                    ? await _db.VideoTypes.Where(item => item.Id == group.VideoTypeId.Value).Select(item => item.Name).FirstOrDefaultAsync(ct)
                    : null;
                var targetTitles = await _db.CodeVideoTargets
                    .Where(item => item.CodeGroupId == group.Id)
                    .Select(item => item.LessonVideo.Title)
                    .Take(3)
                    .ToListAsync(ct);
                if (targetTitles.Count > 0)
                    return $"تفعيل فيديوهات: {string.Join("، ", targetTitles)}";
                return $"تفعيل فيديوهات{(string.IsNullOrWhiteSpace(videoType) ? string.Empty : $": {videoType}")}";
            default:
                return group.Name;
        }
    }
}
