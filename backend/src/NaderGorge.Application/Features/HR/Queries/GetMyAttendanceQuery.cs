using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Queries;

public record GetMyAttendanceQuery(Guid UserId) : IRequest<ApiResponse<MyAttendanceStatusDto>>;

public record MyAttendanceStatusDto(bool HasProfile, List<AttendanceLogDto> Logs);

public record AttendanceLogDto(
    Guid Id,
    DateOnly Date,
    DateTime ClockIn,
    DateTime? ClockOut,
    int LateMinutes,
    string Status,
    string IpAddress,
    string UserAgent,
    double? DurationMinutes
);

public class GetMyAttendanceQueryHandler : IRequestHandler<GetMyAttendanceQuery, ApiResponse<MyAttendanceStatusDto>>
{
    private readonly IAppDbContext _db;

    public GetMyAttendanceQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<MyAttendanceStatusDto>> Handle(GetMyAttendanceQuery request, CancellationToken ct)
    {
        var profile = await _db.EmployeeProfiles
            .FirstOrDefaultAsync(ep => ep.UserId == request.UserId, ct);

        if (profile == null)
        {
            return ApiResponse<MyAttendanceStatusDto>.Ok(new MyAttendanceStatusDto(false, new List<AttendanceLogDto>()));
        }

        var logs = await _db.AttendanceLogs
            .Where(al => al.EmployeeId == profile.Id)
            .OrderByDescending(al => al.Date)
            .ThenByDescending(al => al.ClockIn)
            .ToListAsync(ct);

        var dtos = logs.Select(al => new AttendanceLogDto(
            al.Id,
            al.Date,
            al.ClockIn,
            al.ClockOut,
            al.LateMinutes,
            al.Status.ToString(),
            al.IpAddress,
            al.UserAgent,
            al.ClockOut.HasValue ? (al.ClockOut.Value - al.ClockIn).TotalMinutes : null
        )).ToList();

        return ApiResponse<MyAttendanceStatusDto>.Ok(new MyAttendanceStatusDto(true, dtos));
    }
}
