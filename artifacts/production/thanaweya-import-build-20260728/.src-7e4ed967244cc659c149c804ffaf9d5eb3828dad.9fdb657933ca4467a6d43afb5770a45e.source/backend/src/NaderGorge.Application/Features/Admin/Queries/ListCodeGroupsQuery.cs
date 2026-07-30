using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record ListCodeGroupsQuery(Guid? CurrentUserId = null) : IRequest<ApiResponse<List<CodeGroupDto>>>;

public record CodeGroupDto(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    CodeType CodeType,
    Guid? PackageId,
    Guid? TermId,
    Guid? ContentSectionId,
    Guid? LessonId,
    Guid? ExamId,
    Guid? PublicExamProductId,
    Guid? VideoTypeId,
    bool IncludeFutureVideos,
    decimal? BalanceAmount,
    DateTime? ExpiresAt,
    bool ExpireActivatedAccess,
    decimal? DiscountPercentage,
    SalesOwnerType? RevenueOwner,
    TeacherAllocationMode? RevenueAllocationMode,
    decimal? RevenueAllocationValue,
    CodeAccountingTiming AccountingTiming,
    DateTime? AccountingRecordedAt,
    int CodeCount,
    int UsedCount,
    Guid? TeacherId);

public class ListCodeGroupsQueryHandler : IRequestHandler<ListCodeGroupsQuery, ApiResponse<List<CodeGroupDto>>>
{
    private readonly IAppDbContext _db;

    public ListCodeGroupsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<List<CodeGroupDto>>> Handle(ListCodeGroupsQuery request, CancellationToken ct)
    {
        Guid? teacherId = null;
        bool isTeacher = false;
        if (request.CurrentUserId.HasValue)
        {
            var user = await _db.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Include(u => u.TeacherProfile)
                .FirstOrDefaultAsync(u => u.Id == request.CurrentUserId.Value, ct);

            if (user != null && user.UserRoles.Any(ur => ur.Role.Type == RoleType.Teacher))
            {
                isTeacher = true;
                teacherId = user.TeacherProfile?.Id;
                if (teacherId == null)
                {
                    teacherId = await _db.TeacherStaffMembers
                        .Where(member => member.UserId == request.CurrentUserId.Value && member.IsActive && member.User.IsActive)
                        .Select(member => (Guid?)member.TeacherId)
                        .FirstOrDefaultAsync(ct);
                }
            }
        }

        var query = _db.CodeGroups.AsQueryable();

        if (isTeacher)
        {
            var targetId = teacherId ?? Guid.Empty;
            query = query.Where(cg => cg.TeacherId.HasValue && cg.TeacherId.Value == targetId);
        }

        var dtos = await query
            .AsNoTracking()
            .OrderByDescending(cg => cg.CreatedAt)
            .Select(cg => new CodeGroupDto(
                cg.Id,
                cg.Name,
                cg.CreatedAt,
                cg.CodeType,
                cg.PackageId,
                cg.TermId,
                cg.ContentSectionId,
                cg.LessonId,
                cg.ExamId,
                cg.PublicExamProductId,
                cg.VideoTypeId,
                cg.IncludeFutureVideos,
                cg.BalanceAmount,
                cg.ExpiresAt,
                cg.ExpireActivatedAccess,
                cg.DiscountPercentage,
                cg.RevenueOwner,
                cg.RevenueAllocationMode,
                cg.RevenueAllocationValue,
                cg.AccountingTiming,
                cg.AccountingRecordedAt,
                cg.AccessCodes.Count,
                cg.AccessCodes.Count(c => c.IsConsumed),
                cg.TeacherId
            ))
            .ToListAsync(ct);

        return ApiResponse<List<CodeGroupDto>>.Ok(dtos);
    }
}
