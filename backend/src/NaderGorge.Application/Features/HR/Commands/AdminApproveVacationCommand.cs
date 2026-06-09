using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Commands;

public record AdminApproveVacationCommand(
    Guid VacationId,
    Guid AdminUserId
) : IRequest<ApiResponse<bool>>;

public class AdminApproveVacationCommandHandler : IRequestHandler<AdminApproveVacationCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;
    private readonly IAuditRepository _audit;

    public AdminApproveVacationCommandHandler(IAppDbContext db, IAuditRepository audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ApiResponse<bool>> Handle(AdminApproveVacationCommand request, CancellationToken ct)
    {
        var vacation = await _db.EmployeeVacations
            .FirstOrDefaultAsync(ev => ev.Id == request.VacationId, ct);

        if (vacation == null)
        {
            throw new KeyNotFoundException("Vacation request not found.");
        }

        if (vacation.Status != VacationStatus.Pending)
        {
            throw new InvalidOperationException("This vacation request is already resolved.");
        }

        var oldStatus = vacation.Status;
        vacation.Status = VacationStatus.Approved;
        vacation.HandledBy = request.AdminUserId;
        vacation.HandledAt = DateTime.UtcNow;

        // Auto-populate Leave status logs for the vacation dates
        for (var date = vacation.StartDate; date <= vacation.EndDate; date = date.AddDays(1))
        {
            var existingLog = await _db.AttendanceLogs
                .FirstOrDefaultAsync(al => al.EmployeeId == vacation.EmployeeId && al.Date == date, ct);

            if (existingLog != null)
            {
                existingLog.Status = AttendanceStatus.Leave;
            }
            else
            {
                var log = new AttendanceLog
                {
                    EmployeeId = vacation.EmployeeId,
                    Date = date,
                    ClockIn = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc),
                    ClockOut = null,
                    LateMinutes = 0,
                    Status = AttendanceStatus.Leave,
                    IpAddress = "System (Vacation)",
                    UserAgent = "System (Vacation)"
                };
                _db.AttendanceLogs.Add(log);
            }
        }

        await _db.SaveChangesAsync(ct);

        // Record sensitive HR action in Audit Logs
        var auditEntry = new AuditLog
        {
            Action = "ApproveVacation",
            EntityType = nameof(EmployeeVacation),
            EntityId = vacation.Id,
            PerformedByUserId = request.AdminUserId,
            OldValues = $"Status: {oldStatus}",
            NewValues = $"Status: {VacationStatus.Approved}",
            CreatedAt = DateTime.UtcNow
        };
        await _audit.AddAsync(auditEntry);

        return ApiResponse<bool>.Ok(true);
    }
}
