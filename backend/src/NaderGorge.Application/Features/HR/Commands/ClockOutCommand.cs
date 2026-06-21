using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Application.Features.LiveSupport.Interfaces;

namespace NaderGorge.Application.Features.HR.Commands;

public record ClockOutCommand(
    Guid UserId
) : IRequest<ApiResponse<Guid>>;

public class ClockOutCommandValidator : AbstractValidator<ClockOutCommand>
{
    public ClockOutCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class ClockOutCommandHandler : IRequestHandler<ClockOutCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;
    private readonly ILiveSupportService? _liveSupport;

    public ClockOutCommandHandler(IAppDbContext db, ILiveSupportService? liveSupport = null)
    {
        _db = db;
        _liveSupport = liveSupport;
    }

    public async Task<ApiResponse<Guid>> Handle(ClockOutCommand request, CancellationToken ct)
    {
        var profile = await _db.EmployeeProfiles
            .FirstOrDefaultAsync(ep => ep.UserId == request.UserId, ct);

        if (profile == null)
        {
            throw new KeyNotFoundException("No employee profile found for this user.");
        }

        var activeLog = await _db.AttendanceLogs
            .FirstOrDefaultAsync(al => al.EmployeeId == profile.Id && al.ClockOut == null, ct);

        if (activeLog == null)
        {
            throw new InvalidOperationException("No active clock-in session found. Please clock in first.");
        }

        activeLog.ClockOut = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        if (_liveSupport is not null)
        {
            await _liveSupport.ReleaseStaffAssignmentsAsync(request.UserId, NaderGorge.Domain.Enums.LiveSupportAssignmentEndReason.AttendanceCheckout, ct);
        }

        return ApiResponse<Guid>.Ok(activeLog.Id);
    }
}
