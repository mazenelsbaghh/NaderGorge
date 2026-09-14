using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Common.HR;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Application.Features.LiveSupport.Interfaces;

namespace NaderGorge.Application.Features.HR.Commands;

public record ClockOutCommand(
    Guid UserId
) : IRequest<ApiResponse<Guid>>, IHrAuthorizedRequest
{
    public string RequiredPermission => HrPermissions.AttendanceSelf;
    public HrAccessScope RequiredScope => HrAccessScope.Self;
    public Guid? ResourceUserId => UserId;
}

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
    private readonly IHrAuditWriter _audit;

    public ClockOutCommandHandler(IAppDbContext db, ILiveSupportService? liveSupport = null, IHrAuditWriter? audit = null)
    {
        _db = db;
        _liveSupport = liveSupport;
        _audit = audit ?? new HrAuditWriter(db, DetachedHrRequestContext.Instance);
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

        var before = new { activeLog.ClockOut };
        activeLog.ClockOut = DateTime.UtcNow;
        await _audit.WriteMutationAsync("ClockOut", nameof(AttendanceLog), activeLog.Id, before,
            new { activeLog.ClockOut }, "Employee clock-out", ct, request.UserId);

        await _db.SaveChangesAsync(ct);
        if (_liveSupport is not null)
        {
            await _liveSupport.ReleaseStaffAssignmentsAsync(request.UserId, NaderGorge.Domain.Enums.LiveSupportAssignmentEndReason.AttendanceCheckout, ct);
        }

        return ApiResponse<Guid>.Ok(activeLog.Id);
    }
}
