using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Finance.Commands;

public record AddPayrollAdjustmentCommand(
    Guid PayrollId,
    PayrollAdjustmentType Type,
    decimal Amount,
    string Reason,
    Guid AdminUserId
) : IRequest<ApiResponse<PayrollAdjustmentDto>>;

public record PayrollAdjustmentDto(
    Guid Id,
    string Type,
    decimal Amount,
    string Reason,
    DateTime CreatedAt
);

public class AddPayrollAdjustmentCommandHandler : IRequestHandler<AddPayrollAdjustmentCommand, ApiResponse<PayrollAdjustmentDto>>
{
    private readonly IAppDbContext _db;
    private readonly IAuditRepository _audit;

    public AddPayrollAdjustmentCommandHandler(IAppDbContext db, IAuditRepository audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ApiResponse<PayrollAdjustmentDto>> Handle(AddPayrollAdjustmentCommand request, CancellationToken ct)
    {
        var payroll = await _db.PayrollRecords
            .FirstOrDefaultAsync(pr => pr.Id == request.PayrollId, ct);

        if (payroll == null)
        {
            return ApiResponse<PayrollAdjustmentDto>.Fail("سجل المرتبات غير موجود");
        }

        if (payroll.Status == PayrollStatus.Approved)
        {
            return ApiResponse<PayrollAdjustmentDto>.Fail("لا يمكن تعديل سجل مرتبات معتمد ومغلق");
        }

        if (request.Amount <= 0)
        {
            return ApiResponse<PayrollAdjustmentDto>.Fail("القيمة يجب أن تكون أكبر من صفر");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ApiResponse<PayrollAdjustmentDto>.Fail("السبب مطلوب");
        }

        var adjustment = new PayrollAdjustment
        {
            Id = Guid.NewGuid(),
            PayrollRecordId = payroll.Id,
            Type = request.Type,
            Amount = request.Amount,
            Reason = request.Reason
        };

        _db.PayrollAdjustments.Add(adjustment);
        await _db.SaveChangesAsync(ct);

        // Audit log
        var auditEntry = new AuditLog
        {
            Action = "AddPayrollAdjustment",
            EntityType = nameof(PayrollRecord),
            EntityId = payroll.Id,
            PerformedByUserId = request.AdminUserId,
            NewValues = $"Type: {request.Type}, Amount: {request.Amount}, Reason: {request.Reason}",
            CreatedAt = DateTime.UtcNow
        };
        await _audit.AddAsync(auditEntry);

        var dto = new PayrollAdjustmentDto(
            adjustment.Id,
            adjustment.Type.ToString(),
            adjustment.Amount,
            adjustment.Reason,
            adjustment.CreatedAt
        );

        return ApiResponse<PayrollAdjustmentDto>.Ok(dto, "تم إضافة التعديل بنجاح");
    }
}
