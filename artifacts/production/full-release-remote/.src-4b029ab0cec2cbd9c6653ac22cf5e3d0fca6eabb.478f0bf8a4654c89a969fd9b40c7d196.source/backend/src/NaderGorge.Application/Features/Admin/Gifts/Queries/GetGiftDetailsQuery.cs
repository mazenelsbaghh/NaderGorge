using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Gifts.Models;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Gifts.Queries;

public sealed record GetGiftDetailsQuery(Guid Id) : IRequest<ApiResponse<GiftDetailsDto>>;

public sealed class GetGiftDetailsQueryHandler : IRequestHandler<GetGiftDetailsQuery, ApiResponse<GiftDetailsDto>>
{
    private readonly IAppDbContext _db;

    public GetGiftDetailsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<GiftDetailsDto>> Handle(GetGiftDetailsQuery request, CancellationToken ct)
    {
        var issuance = await _db.GiftIssuances
            .AsNoTracking()
            .Include(x => x.IssuedByUser)
            .Include(x => x.Recipients)
                .ThenInclude(x => x.Student)
            .Include(x => x.Recipients)
                .ThenInclude(x => x.PromotionalBalanceAllocation)
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);

        if (issuance == null)
            return ApiResponse<GiftDetailsDto>.Fail("الهدية غير موجودة.", ["NOT_FOUND"]);

        var targetName = await GetGiftsQueryHandler.ResolveTargetNameAsync(
            _db,
            issuance.TargetType,
            issuance.PackageId ?? issuance.LessonId ?? issuance.LessonVideoId ?? issuance.ExamId,
            issuance.TeacherId,
            ct);

        var allocations = issuance.Recipients
            .Select(x => x.PromotionalBalanceAllocation)
            .Where(x => x != null)
            .ToList();

        var expired = issuance.ExpiresAt.HasValue && issuance.ExpiresAt <= DateTime.UtcNow && issuance.Status != Domain.Enums.GiftIssuanceStatus.Revoked;
        var academicScopes = await ResolveScopeSummariesAsync(
            issuance.TargetType,
            issuance.PackageId ?? issuance.LessonId ?? issuance.LessonVideoId ?? issuance.ExamId,
            issuance.TeacherId,
            ct);

        return ApiResponse<GiftDetailsDto>.Ok(new GiftDetailsDto(
            issuance.Id,
            issuance.RequestId,
            issuance.TargetType,
            targetName,
            expired ? Domain.Enums.GiftIssuanceStatus.Expired : issuance.Status,
            issuance.IssuedByUser.FullName,
            issuance.Reason,
            issuance.Amount,
            allocations.Sum(x => x!.AvailableAmount),
            allocations.Sum(x => x!.ConsumedAmount),
            allocations.Sum(x => x!.ExpiredAmount),
            allocations.Sum(x => x!.RevokedAmount),
            issuance.ExpiresAt,
            issuance.MaxUses,
            issuance.CreatedAt,
            academicScopes,
            issuance.Recipients.Select(x => new GiftRecipientResultDto(
                x.StudentId,
                x.Student.FullName,
                expired && x.Status is (Domain.Enums.GiftRecipientStatus.Active or Domain.Enums.GiftRecipientStatus.PartiallyUsed)
                    ? Domain.Enums.GiftRecipientStatus.Expired
                    : x.Status,
                x.OutcomeCode,
                x.OutcomeMessage,
                x.UsesConsumed,
                issuance.MaxUses)).ToList()));
    }

    private async Task<IReadOnlyList<AcademicScopeSummaryDto>?> ResolveScopeSummariesAsync(
        GiftTargetType targetType,
        Guid? targetId,
        Guid? teacherId,
        CancellationToken ct)
    {
        var owner = targetType switch
        {
            GiftTargetType.Package when targetId.HasValue => (StudentFacingScopeOwnerType.Package, targetId.Value),
            GiftTargetType.Lesson when targetId.HasValue => (StudentFacingScopeOwnerType.Lesson, targetId.Value),
            GiftTargetType.Video when targetId.HasValue => (StudentFacingScopeOwnerType.LessonVideo, targetId.Value),
            GiftTargetType.Exam when targetId.HasValue => (StudentFacingScopeOwnerType.Exam, targetId.Value),
            GiftTargetType.TeacherBalance when teacherId.HasValue => (StudentFacingScopeOwnerType.Teacher, teacherId.Value),
            _ => ((StudentFacingScopeOwnerType, Guid)?)null
        };

        if (owner == null)
            return null;

        var scopes = await _db.StudentFacingAcademicScopes
            .AsNoTracking()
            .Where(x => x.OwnerType == owner.Value.Item1 && x.OwnerId == owner.Value.Item2)
            .ToListAsync(ct);

        return AcademicScopeService.ToScopeSummaries(scopes);
    }
}
