using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Teachers.Queries;

public record GetTeacherProfileStatsQuery(Guid TeacherId) : IRequest<ApiResponse<TeacherProfileStatsDto>>;

public record TeacherProfileStatsDto(
    int PackagesCount,
    int ActiveStudentsCount,
    decimal TotalEarnings,
    decimal CurrentBalance,
    int ExamsCount,
    int EssaysPendingCount,
    int EssaysGradedCount,
    int CodeGroupsCount,
    int QuestionBankItemsCount
);

public class GetTeacherProfileStatsQueryHandler : IRequestHandler<GetTeacherProfileStatsQuery, ApiResponse<TeacherProfileStatsDto>>
{
    private readonly IAppDbContext _db;

    public GetTeacherProfileStatsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<TeacherProfileStatsDto>> Handle(GetTeacherProfileStatsQuery request, CancellationToken ct)
    {
        var teacherExists = await _db.TeacherProfiles.AnyAsync(tp => tp.Id == request.TeacherId, ct);
        if (!teacherExists)
            return ApiResponse<TeacherProfileStatsDto>.Fail("Teacher profile not found");

        var packagesCount = await _db.Packages
            .CountAsync(p => p.TeacherId == request.TeacherId, ct);

        // Count distinct students enrolled across all teacher's packages
        var activeStudentsCount = await _db.StudentAccessGrants
            .Where(sag => sag.PackageId != null && sag.IsActive)
            .Where(sag => _db.Packages
                .Where(p => p.TeacherId == request.TeacherId)
                .Select(p => p.Id)
                .Contains(sag.PackageId!.Value))
            .Select(sag => sag.UserId)
            .Distinct()
            .CountAsync(ct);

        // Get TeacherAccount earnings/balance
        var account = await _db.TeacherAccounts
            .FirstOrDefaultAsync(ta => ta.TeacherId == request.TeacherId, ct);

        var totalEarnings = account?.TotalEarnings ?? 0m;
        var currentBalance = account?.CurrentBalance ?? 0m;

        var examsCount = await _db.Exams
            .CountAsync(e => e.CreatedByTeacherId == request.TeacherId, ct);

        var essaysPendingCount = await _db.EssaySubmissions
            .CountAsync(e => e.GradedByTeacherId == request.TeacherId
                && e.Status != EssaySubmissionStatus.TeacherGraded, ct);

        var essaysGradedCount = await _db.EssaySubmissions
            .CountAsync(e => e.GradedByTeacherId == request.TeacherId
                && e.Status == EssaySubmissionStatus.TeacherGraded, ct);

        var codeGroupsCount = await _db.CodeGroups
            .CountAsync(cg => cg.TeacherId == request.TeacherId, ct);

        var questionBankItemsCount = await _db.QuestionBankItems
            .CountAsync(q => q.CreatedByTeacherId == request.TeacherId, ct);

        var dto = new TeacherProfileStatsDto(
            packagesCount,
            activeStudentsCount,
            totalEarnings,
            currentBalance,
            examsCount,
            essaysPendingCount,
            essaysGradedCount,
            codeGroupsCount,
            questionBankItemsCount
        );

        return ApiResponse<TeacherProfileStatsDto>.Ok(dto);
    }
}
