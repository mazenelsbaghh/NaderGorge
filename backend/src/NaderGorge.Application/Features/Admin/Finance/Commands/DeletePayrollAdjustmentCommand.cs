using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Finance.Commands;

public record DeletePayrollAdjustmentCommand(
    Guid PayrollId,
    Guid AdjustmentId,
    Guid AdminUserId
) : IRequest<ApiResponse<bool>>;

public class DeletePayrollAdjustmentCommandHandler : IRequestHandler<DeletePayrollAdjustmentCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;
    private readonly IAuditRepository _audit;

    public DeletePayrollAdjustmentCommandHandler(IAppDbContext db, IAuditRepository audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ApiResponse<bool>> Handle(DeletePayrollAdjustmentCommand request, CancellationToken ct)
    {
        var adjustment = await _db.PayrollAdjustments
            .Include(pa => pa.PayrollRecord)
            .FirstOrDefaultAsync(pa => pa.Id == request.AdjustmentId && pa.PayrollRecordId == request.PayrollId, ct);

        if (adjustment == null)
        {
            return ApiResponse<bool>.Fail("التعديل غير موجود");
        }

        if (adjustment.PayrollRecord.Status == PayrollStatus.Approved)
        {
            return ApiResponse<bool>.Fail("لا يمكن تعديل سجل مرتبات معتمد ومغلق");
        }

        _db.PayrollAdjustments.Remove(adjustment);
        await _db.SaveChangesAsync(ct);

        // Audit log
        var auditEntry = new AuditLog
        {
            Action = "DeletePayrollAdjustment",
            EntityType = nameof(PayrollRecord),
            EntityId = request.PayrollId,
            PerformedByUserId = request.AdminUserId,
            OldValues = $"Type: {adjustment.Type}, Amount: {adjustment.Amount}, Reason: {adjustment.Reason}",
            CreatedAt = DateTime.UtcNow
        };
        await _audit.AddAsync(auditEntry);

        return ApiResponse<bool>.Ok(true, "تم حذف التعديل بنجاح");
    }
}
