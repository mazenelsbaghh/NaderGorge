using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Teacher.Finance.Queries;

public record GetTeacherPayoutsQuery(Guid TeacherUserId) : IRequest<ApiResponse<List<TeacherPayoutDto>>>;

public record TeacherPayoutDto(
    Guid Id,
    decimal Amount,
    string Status,
    string? RejectionReason,
    DateTime CreatedAt,
    DateTime? HandledAt
);

public class GetTeacherPayoutsQueryHandler : IRequestHandler<GetTeacherPayoutsQuery, ApiResponse<List<TeacherPayoutDto>>>
{
    private readonly IAppDbContext _db;

    public GetTeacherPayoutsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<TeacherPayoutDto>>> Handle(GetTeacherPayoutsQuery request, CancellationToken ct)
    {
        var teacherProfile = await _db.TeacherProfiles
            .FirstOrDefaultAsync(tp => tp.UserId == request.TeacherUserId, ct);

        if (teacherProfile == null)
        {
            return ApiResponse<List<TeacherPayoutDto>>.Fail("حساب المعلم غير موجود");
        }

        var payouts = await _db.TeacherPayouts
            .Where(tp => tp.TeacherId == teacherProfile.Id)
            .OrderByDescending(tp => tp.CreatedAt)
            .ToListAsync(ct);

        var dtos = payouts.Select(tp => new TeacherPayoutDto(
            tp.Id,
            tp.Amount,
            tp.Status.ToString(),
            tp.RejectionReason,
            tp.CreatedAt,
            tp.HandledAt
        )).ToList();

        return ApiResponse<List<TeacherPayoutDto>>.Ok(dtos);
    }
}
