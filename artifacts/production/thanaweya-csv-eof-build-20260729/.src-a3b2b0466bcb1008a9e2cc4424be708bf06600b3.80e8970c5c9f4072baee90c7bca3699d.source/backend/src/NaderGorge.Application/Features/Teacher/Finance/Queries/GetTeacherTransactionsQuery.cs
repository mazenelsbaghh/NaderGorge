using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Teacher.Finance.Queries;

public record GetTeacherTransactionsQuery(
    Guid TeacherUserId,
    DateTime? Date = null,
    DateTime? From = null,
    DateTime? To = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<ApiResponse<PagedResult<TeacherTransactionDto>>>;

public record TeacherTransactionDto(
    Guid Id,
    DateTime OccurredAt,
    string SourceType,
    string ContentName,
    string StudentName,
    string? StudentPhone,
    long? CodeSerialNumber,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal PaidAmount,
    decimal TeacherShareAmount,
    decimal PlatformShareAmount,
    string AllocationMode,
    decimal AllocationValue,
    string ReviewStatus,
    string PayoutStatus
);

public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);

public class GetTeacherTransactionsQueryHandler : IRequestHandler<GetTeacherTransactionsQuery, ApiResponse<PagedResult<TeacherTransactionDto>>>
{
    private readonly IAppDbContext _db;

    public GetTeacherTransactionsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<PagedResult<TeacherTransactionDto>>> Handle(GetTeacherTransactionsQuery request, CancellationToken ct)
    {
        var teacherProfile = await _db.TeacherProfiles
            .FirstOrDefaultAsync(tp => tp.UserId == request.TeacherUserId, ct);

        if (teacherProfile == null)
        {
            return ApiResponse<PagedResult<TeacherTransactionDto>>.Fail("حساب المعلم غير موجود");
        }

        var query = _db.TeacherFinancialAllocations
            .Include(a => a.TeacherFinancialEvent)
            .Where(a => a.TeacherId == teacherProfile.Id);

        if (request.Date.HasValue)
        {
            var (day, nextDay) = CairoTime.GetDayRangeUtc(request.Date.Value);
            query = query.Where(a => a.TeacherFinancialEvent.OccurredAt >= day && a.TeacherFinancialEvent.OccurredAt < nextDay);
        }
        else
        {
            if (request.From.HasValue)
            {
                var (from, _) = CairoTime.GetDayRangeUtc(request.From.Value);
                query = query.Where(a => a.TeacherFinancialEvent.OccurredAt >= from);
            }

            if (request.To.HasValue)
            {
                var (_, to) = CairoTime.GetDayRangeUtc(request.To.Value);
                query = query.Where(a => a.TeacherFinancialEvent.OccurredAt < to);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var hasReviewStatus = Enum.TryParse<TeacherFinancialReviewStatus>(request.Status, true, out var reviewStatus);
            var hasPayoutStatus = Enum.TryParse<TeacherFinancialPayoutStatus>(request.Status, true, out var payoutStatus);

            if (hasReviewStatus && hasPayoutStatus)
            {
                query = query.Where(a => a.ReviewStatus == reviewStatus || a.PayoutStatus == payoutStatus);
            }
            else if (hasReviewStatus)
            {
                query = query.Where(a => a.ReviewStatus == reviewStatus);
            }
            else if (hasPayoutStatus)
            {
                query = query.Where(a => a.PayoutStatus == payoutStatus);
            }
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.TeacherFinancialEvent.OccurredAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var dtos = items.Select(a => new TeacherTransactionDto(
            a.Id,
            a.TeacherFinancialEvent.OccurredAt,
            a.TeacherFinancialEvent.SourceType.ToString(),
            a.ContentNameSnapshot,
            a.StudentNameSnapshot ?? "Unknown Student",
            a.StudentPhoneSnapshot,
            a.CodeSerialNumber,
            a.TeacherFinancialEvent.GrossAmount,
            a.TeacherFinancialEvent.DiscountAmount,
            a.TeacherFinancialEvent.PaidAmount,
            a.TeacherShareAmount,
            a.PlatformShareAmount,
            a.AllocationMode.ToString(),
            a.AllocationValue,
            a.ReviewStatus.ToString(),
            a.PayoutStatus.ToString()
        )).ToList();

        return ApiResponse<PagedResult<TeacherTransactionDto>>.Ok(
            new PagedResult<TeacherTransactionDto>(dtos, totalCount, request.Page, request.PageSize)
        );
    }
}
