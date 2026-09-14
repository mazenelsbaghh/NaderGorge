using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Payroll.FinancialRequests;

public sealed class FinancialRequestService(IAppDbContext db)
{
    public async Task<ApiResponse<Guid>> SubmitAsync(Guid userId, HrFinancialRequestType type, decimal amount, int installments,
        string reason, string? attachmentReference, CancellationToken ct)
    {
        if (amount <= 0 || installments is < 1 or > 60 || string.IsNullOrWhiteSpace(reason))
            return ApiResponse<Guid>.Fail("قيمة أو عدد أقساط أو سبب غير صالح", ["FINANCIAL_REQUEST_INVALID"]);
        if (string.IsNullOrWhiteSpace(attachmentReference)) return ApiResponse<Guid>.Fail("المستند المؤيد مطلوب", ["ATTACHMENT_REQUIRED"]);
        var employee = await db.EmployeeProfiles.SingleOrDefaultAsync(item => item.UserId == userId, ct);
        if (employee is null) return ApiResponse<Guid>.Fail("ملف الموظف غير موجود", ["EMPLOYEE_NOT_FOUND"]);
        var request = new HrFinancialRequest { EmployeeId = employee.Id, Type = type, Amount = decimal.Round(amount, 2),
            RequestedInstallments = type is HrFinancialRequestType.Expense or HrFinancialRequestType.Commission ? 1 : installments,
            Reason = reason.Trim(), AttachmentReference = attachmentReference.Trim() };
        db.HrFinancialRequests.Add(request); await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(request.Id);
    }

    public async Task<ApiResponse<bool>> ApproveAsync(Guid requestId, Guid actorUserId, DateOnly firstDueDate, int expectedVersion, CancellationToken ct)
    {
        var request = await db.HrFinancialRequests.Include(item => item.Employee).Include(item => item.Installments).SingleOrDefaultAsync(item => item.Id == requestId, ct);
        if (request is null) return ApiResponse<bool>.Fail("الطلب المالي غير موجود", ["FINANCIAL_REQUEST_NOT_FOUND"]);
        if (request.Employee?.UserId == actorUserId) return ApiResponse<bool>.Fail("لا يمكن اعتماد طلبك", ["SELF_APPROVAL_FORBIDDEN"]);
        if (request.Version != expectedVersion) return ApiResponse<bool>.Fail("تم تعديل الطلب", ["CONCURRENCY_CONFLICT"]);
        if (request.State == HrFinancialRequestState.Approved) return ApiResponse<bool>.Ok(true);
        if (request.State != HrFinancialRequestState.PendingApproval) return ApiResponse<bool>.Fail("حالة الطلب لا تسمح بالاعتماد", ["FINANCIAL_REQUEST_STATE_INVALID"]);
        var amountPerInstallment = decimal.Round(request.Amount / request.RequestedInstallments, 2, MidpointRounding.AwayFromZero);
        decimal scheduled = 0;
        for (var index = 1; index <= request.RequestedInstallments; index++)
        {
            var amount = index == request.RequestedInstallments ? request.Amount - scheduled : amountPerInstallment; scheduled += amount;
            var installment = new HrFinancialInstallment { FinancialRequestId = request.Id, FinancialRequest = request, Sequence = index,
                DueDate = firstDueDate.AddMonths(index - 1), Amount = amount };
            db.HrFinancialInstallments.Add(installment);
        }
        request.OutstandingBalance = request.Amount; request.State = HrFinancialRequestState.Approved; request.Version++;
        await db.SaveChangesAsync(ct); return ApiResponse<bool>.Ok(true);
    }

    public async Task<int> ApplyDueInputsAsync(Guid payrollRunId, CancellationToken ct)
    {
        var run = await db.HrPayrollRuns.Include(item => item.Employees).ThenInclude(item => item.Lines).SingleAsync(item => item.Id == payrollRunId, ct);
        if (run.Status is HrPayrollRunStatus.GMApproved or HrPayrollRunStatus.Paid or HrPayrollRunStatus.Closed) return 0;
        var installments = await db.HrFinancialInstallments.Include(item => item.FinancialRequest)
            .Where(item => item.State == HrInstallmentState.Scheduled && item.DueDate <= run.PeriodEnd &&
                item.FinancialRequest!.State == HrFinancialRequestState.Approved).OrderBy(item => item.DueDate).ThenBy(item => item.Sequence).ToListAsync(ct);
        var applied = 0;
        foreach (var installment in installments)
        {
            if (await db.HrPayrollInputSources.AnyAsync(item => item.SourceType == nameof(HrFinancialInstallment) && item.SourceId == installment.Id, ct)) continue;
            var request = installment.FinancialRequest!; var employeePayroll = run.Employees.SingleOrDefault(item => item.EmployeeId == request.EmployeeId);
            if (employeePayroll is null) continue;
            var isEarning = request.Type is HrFinancialRequestType.Expense or HrFinancialRequestType.Commission;
            var code = request.Type.ToString().ToUpperInvariant();
            var component = await db.PayComponents.SingleOrDefaultAsync(item => item.Code == code, ct);
            if (component is null)
            {
                component = new PayComponent { Code = code, Name = request.Type.ToString(), Classification = isEarning ? PayComponentClass.Earning : PayComponentClass.Deduction };
                db.PayComponents.Add(component);
            }
            var line = new PayrollLineItem { EmployeePayrollId = employeePayroll.Id, PayComponentId = component.Id, PayComponent = component,
                Amount = installment.Amount, InputsJson = JsonSerializer.Serialize(new { request.Id, request.Type, installment.Sequence, installment.DueDate }),
                Explanation = $"{request.Type} installment {installment.Sequence}: {installment.Amount:0.00}", SourceType = nameof(HrFinancialInstallment), SourceId = installment.Id };
            db.PayrollLineItems.Add(line); installment.PayrollLineItemId = line.Id; installment.State = HrInstallmentState.Applied; installment.AppliedAt = DateTime.UtcNow;
            request.OutstandingBalance = Math.Max(0, request.OutstandingBalance - installment.Amount); if (request.OutstandingBalance == 0) request.State = HrFinancialRequestState.Settled;
            if (isEarning) { employeePayroll.Gross += installment.Amount; run.TotalGross += installment.Amount; }
            else { employeePayroll.Deductions += installment.Amount; run.TotalDeductions += installment.Amount; }
            employeePayroll.Net = employeePayroll.Gross - employeePayroll.Deductions; run.TotalNet = run.TotalGross - run.TotalDeductions;
            db.HrPayrollInputSources.Add(new HrPayrollInputSource { SourceType = nameof(HrFinancialInstallment), SourceId = installment.Id,
                EmployeePayrollId = employeePayroll.Id, PayrollLineItemId = line.Id }); applied++;
        }
        if (applied > 0) { run.Version++; await db.SaveChangesAsync(ct); }
        return applied;
    }
}
