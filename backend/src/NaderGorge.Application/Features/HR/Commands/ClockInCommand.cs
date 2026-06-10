using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Commands;

public record ClockInCommand(
    Guid UserId,
    string IpAddress,
    string UserAgent
) : IRequest<ApiResponse<Guid>>;

public class ClockInCommandValidator : AbstractValidator<ClockInCommand>
{
    public ClockInCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class ClockInCommandHandler : IRequestHandler<ClockInCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public ClockInCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<Guid>> Handle(ClockInCommand request, CancellationToken ct)
    {
        var profile = await _db.EmployeeProfiles
            .FirstOrDefaultAsync(ep => ep.UserId == request.UserId, ct);

        if (profile == null)
        {
            throw new KeyNotFoundException("No employee profile found for this user. Please contact an Administrator to configure your profile.");
        }

        // Check for any active attendance session (where ClockOut is null)
        var activeSessionExists = await _db.AttendanceLogs
            .AnyAsync(al => al.EmployeeId == profile.Id && al.ClockOut == null, ct);

        if (activeSessionExists)
        {
            throw new InvalidOperationException("You already have an active clock-in session. Please clock out first.");
        }

        // Determine Egypt TimeZone to accurately compute standard working date and late minutes
        TimeZoneInfo cairoZone;
        try
        {
            cairoZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            cairoZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        }

        var nowUtc = DateTime.UtcNow;
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, cairoZone);
        var localDate = DateOnly.FromDateTime(localTime);
        var localTimeOfDay = localTime.TimeOfDay;

        // Prevent multiple check-ins on the same calendar day
        var alreadyRegisteredToday = await _db.AttendanceLogs
            .AnyAsync(al => al.EmployeeId == profile.Id && al.Date == localDate, ct);

        if (alreadyRegisteredToday)
        {
            throw new InvalidOperationException("لقد قمت بتسجيل الحضور بالفعل اليوم.");
        }

        var lateMinutes = 0;
        if (localTimeOfDay > profile.StandardStartTime)
        {
            lateMinutes = (int)Math.Ceiling((localTimeOfDay - profile.StandardStartTime).TotalMinutes);
        }

        var attendanceLog = new AttendanceLog
        {
            EmployeeId = profile.Id,
            Date = localDate,
            ClockIn = nowUtc,
            LateMinutes = lateMinutes,
            Status = lateMinutes > 0 ? AttendanceStatus.Late : AttendanceStatus.Present,
            IpAddress = request.IpAddress ?? string.Empty,
            UserAgent = request.UserAgent ?? string.Empty
        };

        _db.AttendanceLogs.Add(attendanceLog);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<Guid>.Ok(attendanceLog.Id);
    }
}
