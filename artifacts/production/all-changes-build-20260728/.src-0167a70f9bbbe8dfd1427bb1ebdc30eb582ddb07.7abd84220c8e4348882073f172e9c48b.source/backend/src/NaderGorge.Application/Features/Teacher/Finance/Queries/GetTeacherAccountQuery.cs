using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Teacher.Finance.Queries;

public record GetTeacherAccountQuery(Guid TeacherUserId) : IRequest<ApiResponse<TeacherAccountDto>>;

public record TeacherAccountDto(
    Guid TeacherId,
    string TeacherName,
    decimal TodayEarnings,
    decimal TotalEarnings,
    decimal CurrentBalance,
    decimal ReservedBalance,
    decimal AvailableBalance,
    decimal DebtBalance,
    decimal CommissionRate
);

public class GetTeacherAccountQueryHandler : IRequestHandler<GetTeacherAccountQuery, ApiResponse<TeacherAccountDto>>
{
    private readonly IAppDbContext _db;

    public GetTeacherAccountQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<TeacherAccountDto>> Handle(GetTeacherAccountQuery request, CancellationToken ct)
    {
        var teacherProfile = await _db.TeacherProfiles
            .Include(tp => tp.User)
            .FirstOrDefaultAsync(tp => tp.UserId == request.TeacherUserId, ct);

        if (teacherProfile == null)
        {
            return ApiResponse<TeacherAccountDto>.Fail("حساب المعلم غير موجود");
        }

        var account = await _db.TeacherAccounts
            .FirstOrDefaultAsync(ta => ta.TeacherId == teacherProfile.Id, ct);

        if (account == null)
        {
            account = new TeacherAccount
            {
                Id = Guid.NewGuid(),
                TeacherId = teacherProfile.Id,
                TotalEarnings = 0m,
                CurrentBalance = 0m,
                ReservedBalance = 0m,
                CommissionRate = teacherProfile.CommissionRate
            };
            _db.TeacherAccounts.Add(account);
            await _db.SaveChangesAsync(ct);
        }

        var (todayStartUtc, tomorrowStartUtc) = CairoTime.GetCurrentDayRangeUtc();
        var todayEarnings = await _db.TeacherFinancialAllocations
            .Where(a => a.TeacherId == teacherProfile.Id
                && a.TeacherShareAmount > 0
                && a.ReviewStatus != Domain.Enums.TeacherFinancialReviewStatus.Rejected
                && a.TeacherFinancialEvent.OccurredAt >= todayStartUtc
                && a.TeacherFinancialEvent.OccurredAt < tomorrowStartUtc)
            .SumAsync(a => a.TeacherShareAmount, ct);

        var debtBalance = Math.Abs(await _db.TeacherPayoutAdjustments
            .Where(a => a.TeacherId == teacherProfile.Id
                && a.Status == Domain.Enums.TeacherPayoutAdjustmentStatus.Open
                && a.Amount < 0)
            .SumAsync(a => a.Amount, ct));

        var dto = new TeacherAccountDto(
            teacherProfile.Id,
            teacherProfile.User?.FullName ?? "Unknown",
            todayEarnings,
            account.TotalEarnings,
            account.CurrentBalance,
            account.ReservedBalance,
            account.CurrentBalance - account.ReservedBalance,
            debtBalance,
            account.CommissionRate
        );

        return ApiResponse<TeacherAccountDto>.Ok(dto);
    }
}
