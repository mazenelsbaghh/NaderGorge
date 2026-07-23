using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Teacher.Finance.Queries;

public record GetTeacherFinanceCalendarQuery(
    Guid TeacherUserId,
    DateTime From,
    DateTime To
) : IRequest<ApiResponse<List<TeacherFinanceDayDto>>>;

public record TeacherFinanceDayDto(
    DateTime Date,
    decimal GrossAmount,
    decimal TeacherShareAmount,
    decimal PlatformShareAmount,
    int TransactionCount,
    int PendingReviewCount,
    List<TeacherFinanceDayTransactionDto> Transactions
);

public record TeacherFinanceDayTransactionDto(
    Guid Id,
    DateTime OccurredAt,
    string StudentName,
    string? StudentPhone,
    string ContentName,
    long? CodeSerialNumber,
    decimal PaidAmount,
    decimal TeacherShareAmount,
    string SourceType,
    string ReviewStatus,
    string PayoutStatus
);

public class GetTeacherFinanceCalendarQueryHandler
    : IRequestHandler<GetTeacherFinanceCalendarQuery, ApiResponse<List<TeacherFinanceDayDto>>>
{
    private readonly IAppDbContext _db;

    public GetTeacherFinanceCalendarQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<TeacherFinanceDayDto>>> Handle(GetTeacherFinanceCalendarQuery request, CancellationToken ct)
    {
        var teacherProfile = await _db.TeacherProfiles
            .FirstOrDefaultAsync(tp => tp.UserId == request.TeacherUserId, ct);

        if (teacherProfile == null)
        {
            return ApiResponse<List<TeacherFinanceDayDto>>.Fail("حساب المعلم غير موجود");
        }

        var (from, _) = CairoTime.GetDayRangeUtc(request.From);
        var (_, toExclusive) = CairoTime.GetDayRangeUtc(request.To);

        var allocations = await _db.TeacherFinancialAllocations
            .Include(a => a.TeacherFinancialEvent)
            .Where(a => a.TeacherId == teacherProfile.Id
                && a.TeacherFinancialEvent.OccurredAt >= from
                && a.TeacherFinancialEvent.OccurredAt < toExclusive)
            .ToListAsync(ct);

        var rows = allocations
            .GroupBy(a => CairoTime.ToLocal(a.TeacherFinancialEvent.OccurredAt).Date)
            .Select(g => new TeacherFinanceDayDto(
                g.Key,
                g.Sum(a => a.TeacherFinancialEvent.GrossAmount),
                g.Sum(a => a.TeacherShareAmount),
                g.Sum(a => a.PlatformShareAmount),
                g.Count(),
                g.Count(a => a.ReviewStatus == TeacherFinancialReviewStatus.PendingReview),
                g.OrderByDescending(a => a.TeacherFinancialEvent.OccurredAt)
                    .Select(a => new TeacherFinanceDayTransactionDto(
                        a.Id,
                        a.TeacherFinancialEvent.OccurredAt,
                        a.StudentNameSnapshot ?? "Unknown Student",
                        a.StudentPhoneSnapshot,
                        a.ContentNameSnapshot,
                        a.CodeSerialNumber,
                        a.TeacherFinancialEvent.PaidAmount,
                        a.TeacherShareAmount,
                        a.TeacherFinancialEvent.SourceType.ToString(),
                        a.ReviewStatus.ToString(),
                        a.PayoutStatus.ToString()
                    ))
                    .ToList()
            ))
            .OrderBy(x => x.Date)
            .ToList();

        return ApiResponse<List<TeacherFinanceDayDto>>.Ok(rows);
    }
}
