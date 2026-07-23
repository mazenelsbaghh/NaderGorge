using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Payroll.Commands;

public sealed class PayrollRunService(IAppDbContext db, PayrollCalculationEngine engine)
{
    public async Task<ApiResponse<Guid>> PrepareAsync(DateOnly periodStart, DateOnly periodEnd, DateTime cutoffAt, Guid actorUserId, CancellationToken ct)
    {
        if (periodEnd < periodStart || cutoffAt == default)
            return ApiResponse<Guid>.Fail("فترة الراتب غير صالحة", ["PAYROLL_PERIOD_INVALID"]);
        var existing = await db.HrPayrollRuns.SingleOrDefaultAsync(item => item.PeriodStart == periodStart && item.PeriodEnd == periodEnd, ct);
        if (existing is not null) return ApiResponse<Guid>.Ok(existing.Id);
        var rules = await db.PayrollRules.Include(item => item.PayComponent).Where(item => item.IsActive && item.EffectiveFrom <= periodEnd &&
            (!item.EffectiveTo.HasValue || item.EffectiveTo >= periodStart)).OrderBy(item => item.Priority).ToListAsync(ct);
        if (rules.Count == 0)
        {
            var component = new PayComponent { Code = "BASE", Name = "الراتب الأساسي", Classification = PayComponentClass.Earning, IsTaxable = true, IsInsurable = true };
            var rule = new PayrollRule { PayComponent = component, PayComponentId = component.Id, Name = "الراتب الأساسي", Expression = "base", EffectiveFrom = periodStart, Priority = 1 };
            db.PayComponents.Add(component); db.PayrollRules.Add(rule); await db.SaveChangesAsync(ct); rules.Add(rule);
        }
        if (rules.Any(item => !PayrollCalculationEngine.IsValidExpression(item.Expression)))
            return ApiResponse<Guid>.Fail("توجد قاعدة راتب غير آمنة", ["PAYROLL_EXPRESSION_INVALID"]);

        var employees = await db.EmployeeProfiles.Include(item => item.User).Where(item => item.EmploymentStatus != EmployeeEmploymentStatus.Terminated &&
            item.HireDate <= periodEnd && (!item.TerminationDate.HasValue || item.TerminationDate >= periodStart)).OrderBy(item => item.EmployeeNumber).ToListAsync(ct);
        var run = new HrPayrollRun { RunNumber = $"PAY-{periodStart:yyyyMM}-{Guid.NewGuid():N}"[..20].ToUpperInvariant(), PeriodStart = periodStart,
            PeriodEnd = periodEnd, CutoffAt = cutoffAt, SourceDataVersion = cutoffAt.ToUniversalTime().ToString("O") };
        foreach (var employee in employees)
        {
            var compensation = await db.EmployeeCompensations.Where(item => item.EmployeeId == employee.Id && item.EffectiveFrom <= periodEnd &&
                (!item.EffectiveTo.HasValue || item.EffectiveTo >= periodStart)).OrderByDescending(item => item.EffectiveFrom).FirstOrDefaultAsync(ct);
            var attendance = await db.AttendanceSessions.Where(item => item.EmployeeId == employee.Id && item.WorkDate >= periodStart && item.WorkDate <= periodEnd)
                .GroupBy(_ => 1).Select(group => new { Late = group.Sum(item => item.LateMinutes), Overtime = group.Sum(item => item.OvertimeMinutes) }).SingleOrDefaultAsync(ct);
            var absenceDays = await db.WorkdayClassifications.CountAsync(item => item.EmployeeId == employee.Id && item.WorkDate >= periodStart && item.WorkDate <= periodEnd && item.Kind == WorkdayClassificationKind.Absence, ct);
            var baseSalary = compensation?.BaseSalary ?? employee.BasicSalary;
            var calculation = engine.Calculate(new PayrollCalculationInput(employee.Id, baseSalary, attendance?.Late ?? 0, absenceDays, attendance?.Overtime ?? 0), rules);
            var payroll = new EmployeePayroll { PayrollRunId = run.Id, EmployeeId = employee.Id, EmployeeNumberSnapshot = employee.EmployeeNumber,
                EmployeeNameSnapshot = employee.User?.FullName ?? employee.EmployeeNumber, BaseSalarySnapshot = baseSalary, Currency = compensation?.Currency ?? "EGP",
                Gross = calculation.Gross, Deductions = calculation.Deductions, Net = calculation.Net };
            foreach (var line in calculation.Lines) payroll.Lines.Add(new PayrollLineItem { EmployeePayrollId = payroll.Id, PayComponentId = line.ComponentId,
                Amount = line.Amount, InputsJson = line.InputsJson, Explanation = line.Explanation, SourceType = "PayrollRule", SourceId = line.RuleId, RuleVersionId = line.RuleId });
            run.Employees.Add(payroll);
        }
        run.TotalGross = run.Employees.Sum(item => item.Gross); run.TotalDeductions = run.Employees.Sum(item => item.Deductions); run.TotalNet = run.Employees.Sum(item => item.Net);
        run.ReconciliationHash = Hash($"{run.PeriodStart}|{run.PeriodEnd}|{run.TotalGross}|{run.TotalDeductions}|{run.TotalNet}|{run.Employees.Count}");
        db.HrPayrollRuns.Add(run); PayrollRunTransitions.TryMove(run, HrPayrollRunStatus.Prepared, actorUserId, DateTime.UtcNow);
        await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(run.Id);
    }

    public async Task<ApiResponse<bool>> MoveAsync(Guid runId, HrPayrollRunStatus target, Guid actorUserId, int expectedVersion, CancellationToken ct)
    {
        var run = await db.HrPayrollRuns.Include(item => item.Employees).ThenInclude(item => item.Lines).SingleOrDefaultAsync(item => item.Id == runId, ct);
        if (run is null) return ApiResponse<bool>.Fail("دورة الراتب غير موجودة", ["PAYROLL_RUN_NOT_FOUND"]);
        if (run.Version != expectedVersion) return ApiResponse<bool>.Fail("تم تعديل الدورة", ["CONCURRENCY_CONFLICT"]);
        if (!PayrollRunTransitions.TryMove(run, target, actorUserId, DateTime.UtcNow)) return ApiResponse<bool>.Fail("انتقال حالة غير مسموح", ["PAYROLL_TRANSITION_INVALID"]);
        if (target == HrPayrollRunStatus.Paid) foreach (var employee in run.Employees) employee.Status = EmployeePayrollStatus.Paid;
        if (target == HrPayrollRunStatus.Closed)
        {
            foreach (var employee in run.Employees)
            {
                employee.Status = EmployeePayrollStatus.Settled;
                if (!await db.Payslips.AnyAsync(item => item.EmployeePayrollId == employee.Id, ct)) db.Payslips.Add(new Payslip { EmployeePayrollId = employee.Id,
                    AssetReference = $"payslips/{run.RunNumber}/{employee.EmployeeNumberSnapshot}.pdf", ContentHash = Hash($"{run.ReconciliationHash}|{employee.Id}|{employee.Net}") });
            }
        }
        await db.SaveChangesAsync(ct); return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<Guid>> AddSettlementAsync(Guid originalLineId, Guid settlementRunId, decimal amount, string reason, Guid actorUserId, CancellationToken ct)
    {
        if (amount == 0 || string.IsNullOrWhiteSpace(reason))
            return ApiResponse<Guid>.Fail("قيمة وسبب التسوية مطلوبان", ["PAYROLL_SETTLEMENT_INVALID"]);
        var original = await db.PayrollLineItems.Include(item => item.EmployeePayroll).ThenInclude(item => item!.PayrollRun).SingleOrDefaultAsync(item => item.Id == originalLineId, ct);
        var settlementRun = await db.HrPayrollRuns.SingleOrDefaultAsync(item => item.Id == settlementRunId, ct);
        if (original?.EmployeePayroll?.PayrollRun?.Status != HrPayrollRunStatus.Closed || settlementRun is null || settlementRun.Status is HrPayrollRunStatus.GMApproved or HrPayrollRunStatus.Paid or HrPayrollRunStatus.Closed)
            return ApiResponse<Guid>.Fail("التسوية غير صالحة", ["PAYROLL_SETTLEMENT_INVALID"]);
        var adjustment = new PayrollSettlementAdjustment { OriginalPayrollLineItemId = originalLineId, SettlementPayrollRunId = settlementRunId,
            Amount = amount, Reason = reason.Trim(), CreatedByUserId = actorUserId };
        db.PayrollSettlementAdjustments.Add(adjustment); await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(adjustment.Id);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
