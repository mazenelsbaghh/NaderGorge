using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Finance.Commands;

public record ApprovePayrollCommand(
    Guid PayrollId,
    Guid AdminUserId
) : IRequest<ApiResponse<bool>>;

public class ApprovePayrollCommandHandler : IRequestHandler<ApprovePayrollCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;
    private readonly IAuditRepository _audit;

    public ApprovePayrollCommandHandler(IAppDbContext db, IAuditRepository audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ApiResponse<bool>> Handle(ApprovePayrollCommand request, CancellationToken ct)
    {
        var payroll = await _db.PayrollRecords
            .FirstOrDefaultAsync(pr => pr.Id == request.PayrollId, ct);

        if (payroll == null)
        {
            return ApiResponse<bool>.Fail("سجل المرتبات غير موجود");
        }

        if (payroll.Status == PayrollStatus.Approved)
        {
            return ApiResponse<bool>.Fail("سجل المرتبات معتمد ومغلق بالفعل");
        }

        payroll.Status = PayrollStatus.Approved;
        payroll.ApprovedByUserId = request.AdminUserId;
        payroll.ApprovedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        // Audit log
        var auditEntry = new AuditLog
        {
            Action = "ApprovePayroll",
            EntityType = nameof(PayrollRecord),
            EntityId = payroll.Id,
            PerformedByUserId = request.AdminUserId,
            OldValues = "Status: Draft",
            NewValues = $"Status: Approved, ApprovedAt: {payroll.ApprovedAt}",
            CreatedAt = DateTime.UtcNow
        };
        await _audit.AddAsync(auditEntry);

        return ApiResponse<bool>.Ok(true, "تم اعتماد وإغلاق سجل المرتبات بنجاح");
    }
}
