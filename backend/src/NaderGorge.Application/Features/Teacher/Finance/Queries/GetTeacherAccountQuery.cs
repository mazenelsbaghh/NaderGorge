using NaderGorge.Application.Services;
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
    decimal CommissionRate,
    TeacherFinanceAccountSnapshot? Account = null
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

        var account = (await new TeacherFinanceAccountService(_db).GetAsync(teacherProfile.Id, ct))!;
        var dto = new TeacherAccountDto(account.TeacherId, account.TeacherName, account.TodayEarnings,
            account.TotalEarned, account.Available, account.Reserved, account.NetPayable, account.Debt,
            account.CommissionRate, account);

        return ApiResponse<TeacherAccountDto>.Ok(dto);
    }
}
