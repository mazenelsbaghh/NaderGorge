using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Gifts.Models;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Gifts.Queries;

public sealed record GetGiftsQuery(
    string? Search = null,
    GiftTargetType? TargetType = null,
    GiftIssuanceStatus? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<ApiResponse<GiftPageDto>>;

public sealed class GetGiftsQueryHandler : IRequestHandler<GetGiftsQuery, ApiResponse<GiftPageDto>>
{
    private readonly IAppDbContext _db;

    public GetGiftsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<GiftPageDto>> Handle(GetGiftsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = _db.GiftIssuances.AsNoTracking().AsQueryable();

        if (request.TargetType.HasValue)
            query = query.Where(x => x.TargetType == request.TargetType);
        if (request.Status.HasValue)
            query = query.Where(x => x.Status == request.Status);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(x => x.Reason.ToLower().Contains(term) ||
                                     x.IssuedByUser.FullName.ToLower().Contains(term) ||
                                     x.Recipients.Any(r => r.Student.FullName.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var now = DateTime.UtcNow;
        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.TargetType,
                TargetId = x.PackageId ?? x.TermId ?? x.ContentSectionId ?? x.LessonId ?? x.LessonVideoId ?? x.ExamId,
                x.TeacherId,
                x.Amount,
                Status = x.ExpiresAt != null && x.ExpiresAt <= now && x.Status != GiftIssuanceStatus.Revoked
                    ? GiftIssuanceStatus.Expired
                    : x.Status,
                IssuerName = x.IssuedByUser.FullName,
                RecipientCount = x.Recipients.Count,
                SuccessfulCount = x.Recipients.Count(r => r.Status != GiftRecipientStatus.Failed && r.Status != GiftRecipientStatus.AlreadyEntitled),
                Original = x.Recipients.Select(r => r.PromotionalBalanceAllocation == null ? 0m : r.PromotionalBalanceAllocation.OriginalAmount).Sum(),
                Available = x.Recipients.Select(r => r.PromotionalBalanceAllocation == null ? 0m : r.PromotionalBalanceAllocation.AvailableAmount).Sum(),
                x.ExpiresAt,
                x.CreatedAt
            })
            .ToListAsync(ct);

        var items = new List<GiftListItemDto>(rows.Count);
        foreach (var row in rows)
        {
            var values = GiftValueSemantics.Resolve(
                row.TargetType,
                row.Amount,
                row.SuccessfulCount,
                row.Original,
                row.Available);

            items.Add(new GiftListItemDto(
                row.Id,
                row.TargetType,
                await ResolveTargetNameAsync(_db, row.TargetType, row.TargetId, row.TeacherId, ct),
                row.Status,
                row.IssuerName,
                row.RecipientCount,
                row.SuccessfulCount,
                values.OriginalValue,
                values.AvailableValue,
                row.ExpiresAt,
                row.CreatedAt));
        }

        return ApiResponse<GiftPageDto>.Ok(new GiftPageDto(
            items,
            page,
            pageSize,
            total,
            (int)Math.Ceiling(total / (double)pageSize)));
    }

    internal static async Task<string> ResolveTargetNameAsync(IAppDbContext db, GiftTargetType type, Guid? targetId, Guid? teacherId, CancellationToken ct)
    {
        if (type == GiftTargetType.GeneralBalance)
            return "رصيد عام من المنصة";
        if (type == GiftTargetType.TeacherBalance)
        {
            var teacher = await db.TeacherProfiles.Where(x => x.Id == teacherId).Select(x => x.User.FullName).FirstOrDefaultAsync(ct);
            return teacher == null ? "رصيد مدرس غير متاح" : $"رصيد مدرس: {teacher}";
        }

        if (!targetId.HasValue)
            return "هدف غير متاح";

        return type switch
        {
            GiftTargetType.Package => await db.Packages.Where(x => x.Id == targetId).Select(x => x.Name).FirstOrDefaultAsync(ct),
            GiftTargetType.Term => await db.Terms.Where(x => x.Id == targetId).Select(x => x.IsSystemContainer ? x.Package.Name : x.Title).FirstOrDefaultAsync(ct),
            GiftTargetType.ContentSection => await db.ContentSections.Where(x => x.Id == targetId).Select(x => x.IsSystemContainer ? x.Term.Package.Name : x.Title).FirstOrDefaultAsync(ct),
            GiftTargetType.Lesson => await db.Lessons.Where(x => x.Id == targetId).Select(x => x.Title).FirstOrDefaultAsync(ct),
            GiftTargetType.Video => await db.LessonVideos.Where(x => x.Id == targetId).Select(x => x.Title).FirstOrDefaultAsync(ct),
            GiftTargetType.Exam => await db.Exams.Where(x => x.Id == targetId).Select(x => x.Title).FirstOrDefaultAsync(ct),
            _ => null
        } ?? "هدف غير متاح";
    }
}
