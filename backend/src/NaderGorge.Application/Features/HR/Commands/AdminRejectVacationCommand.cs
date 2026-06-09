using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Commands;

public record AdminRejectVacationCommand(
    Guid VacationId,
    Guid AdminUserId
) : IRequest<ApiResponse<bool>>;

public class AdminRejectVacationCommandHandler : IRequestHandler<AdminRejectVacationCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;
    private readonly IAuditRepository _audit;

    public AdminRejectVacationCommandHandler(IAppDbContext db, IAuditRepository audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ApiResponse<bool>> Handle(AdminRejectVacationCommand request, CancellationToken ct)
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
        vacation.Status = VacationStatus.Rejected;
        vacation.HandledBy = request.AdminUserId;
        vacation.HandledAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        // Record sensitive HR action in Audit Logs
        var auditEntry = new AuditLog
        {
            Action = "RejectVacation",
            EntityType = nameof(EmployeeVacation),
            EntityId = vacation.Id,
            PerformedByUserId = request.AdminUserId,
            OldValues = $"Status: {oldStatus}",
            NewValues = $"Status: {VacationStatus.Rejected}",
            CreatedAt = DateTime.UtcNow
        };
        await _audit.AddAsync(auditEntry);

        return ApiResponse<bool>.Ok(true);
    }
}
