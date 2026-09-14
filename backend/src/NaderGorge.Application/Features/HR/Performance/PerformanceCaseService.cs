using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Performance;

public sealed class PerformanceCaseService(IAppDbContext db)
{
    public async Task<ApiResponse<bool>> ActivateCycleAsync(Guid cycleId, CancellationToken ct)
    {
        var cycle = await db.PerformanceCycles.Include(item => item.Goals).SingleOrDefaultAsync(item => item.Id == cycleId, ct);
        if (cycle is null) return ApiResponse<bool>.Fail("دورة التقييم غير موجودة", ["PERFORMANCE_CYCLE_NOT_FOUND"]);
        if (cycle.Goals.Count == 0 || cycle.Goals.Sum(item => item.Weight) != 100)
            return ApiResponse<bool>.Fail("مجموع الأوزان يجب أن يساوي 100%", ["PERFORMANCE_WEIGHT_INVALID"]);
        cycle.State = PerformanceCycleState.Active; await db.SaveChangesAsync(ct); return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<Guid>> PublishReviewAsync(Guid cycleId, Guid employeeId, Guid managerUserId,
        IReadOnlyDictionary<Guid, decimal> scores, CancellationToken ct)
    {
        var cycle = await db.PerformanceCycles.Include(item => item.Goals).SingleOrDefaultAsync(item => item.Id == cycleId && item.State == PerformanceCycleState.Active, ct);
        if (cycle is null || cycle.Goals.Sum(item => item.Weight) != 100 || scores.Keys.Except(cycle.Goals.Select(item => item.Id)).Any() || cycle.Goals.Any(item => !scores.ContainsKey(item.Id)))
            return ApiResponse<Guid>.Fail("أهداف أو أوزان التقييم غير صالحة", ["PERFORMANCE_SCORE_INVALID"]);
        if (!await db.EmployeeProfiles.AnyAsync(employee => employee.Id == employeeId, ct))
            return ApiResponse<Guid>.Fail("الموظف غير موجود", ["EMPLOYEE_NOT_FOUND"]);
        if (await db.PerformanceReviews.AnyAsync(review => review.PerformanceCycleId == cycleId && review.EmployeeId == employeeId, ct))
            return ApiResponse<Guid>.Fail("يوجد تقييم منشور للموظف في هذه الدورة", ["PERFORMANCE_REVIEW_EXISTS"]);
        if (scores.Values.Any(score => score is < 0 or > 100)) return ApiResponse<Guid>.Fail("الدرجة خارج النطاق", ["PERFORMANCE_SCORE_RANGE"]);
        var weighted = decimal.Round(cycle.Goals.Sum(goal => scores[goal.Id] * goal.Weight / 100m), 2, MidpointRounding.AwayFromZero);
        var review = new PerformanceReview { PerformanceCycleId = cycleId, EmployeeId = employeeId, ManagerUserId = managerUserId,
            ScoresJson = JsonSerializer.Serialize(scores), WeightedScore = weighted, State = PerformanceReviewState.Published, PublishedAt = DateTime.UtcNow };
        db.PerformanceReviews.Add(review); await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(review.Id);
    }

    public async Task<ApiResponse<bool>> AppealAsync(Guid reviewId, Guid actorUserId, string reason, int expectedVersion, CancellationToken ct)
    {
        var review = await db.PerformanceReviews.Include(item => item.Employee).SingleOrDefaultAsync(item => item.Id == reviewId, ct);
        if (review is null || review.Employee?.UserId != actorUserId) return ApiResponse<bool>.Fail("التقييم غير موجود", ["PERFORMANCE_REVIEW_NOT_FOUND"]);
        if (review.Version != expectedVersion) return ApiResponse<bool>.Fail("تم تعديل التقييم", ["CONCURRENCY_CONFLICT"]);
        if (review.State != PerformanceReviewState.Published || string.IsNullOrWhiteSpace(reason))
            return ApiResponse<bool>.Fail("لا يمكن الاستئناف", ["PERFORMANCE_APPEAL_INVALID"]);
        review.State = PerformanceReviewState.Appealed; review.AppealReason = reason.Trim(); review.Version++; await db.SaveChangesAsync(ct); return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<Guid>> OpenCaseAsync(Guid employeeId, Guid openedByUserId, string title, string description, bool confidential, CancellationToken ct)
    {
        var employeeUserId = await db.EmployeeProfiles.Where(item => item.Id == employeeId).Select(item => item.UserId).SingleOrDefaultAsync(ct);
        if (employeeUserId == Guid.Empty) return ApiResponse<Guid>.Fail("الموظف غير موجود", ["EMPLOYEE_NOT_FOUND"]);
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
            return ApiResponse<Guid>.Fail("عنوان ووصف القضية مطلوبان", ["CASE_DETAILS_REQUIRED"]);
        if (employeeUserId == openedByUserId) return ApiResponse<Guid>.Fail("لا يمكن فتح قضية واعتمادها لنفسك", ["SELF_CASE_FORBIDDEN"]);
        var employeeCase = new EmployeeCase { CaseNumber = $"CASE-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..28].ToUpperInvariant(), EmployeeId = employeeId,
            OpenedByUserId = openedByUserId, Title = title.Trim(), Description = description.Trim(), IsConfidential = confidential };
        db.EmployeeCases.Add(employeeCase); await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(employeeCase.Id);
    }

    public async Task<bool> CanViewCaseAsync(Guid caseId, Guid actorUserId, bool hasConfidentialPermission, CancellationToken ct)
    {
        var employeeCase = await db.EmployeeCases.Include(item => item.Employee).AsNoTracking().SingleOrDefaultAsync(item => item.Id == caseId, ct);
        if (employeeCase is null) return false; if (employeeCase.IsConfidential) return hasConfidentialPermission;
        return hasConfidentialPermission || employeeCase.OpenedByUserId == actorUserId || employeeCase.Employee?.UserId == actorUserId;
    }

    public async Task<ApiResponse<Guid>> DecideCaseAsync(Guid caseId, DisciplinaryActionType type, decimal? financialAmount,
        string reason, Guid actorUserId, int expectedVersion, CancellationToken ct)
    {
        var employeeCase = await db.EmployeeCases.Include(item => item.Employee).SingleOrDefaultAsync(item => item.Id == caseId, ct);
        if (employeeCase is null) return ApiResponse<Guid>.Fail("القضية غير موجودة", ["CASE_NOT_FOUND"]);
        if (employeeCase.Employee?.UserId == actorUserId) return ApiResponse<Guid>.Fail("لا يمكن اعتماد جزاء لنفسك", ["SELF_APPROVAL_FORBIDDEN"]);
        if (employeeCase.Version != expectedVersion) return ApiResponse<Guid>.Fail("تم تعديل القضية", ["CONCURRENCY_CONFLICT"]);
        if (string.IsNullOrWhiteSpace(reason)) return ApiResponse<Guid>.Fail("سبب القرار مطلوب", ["CASE_REASON_REQUIRED"]);
        if (type == DisciplinaryActionType.FinancialPenalty && (!financialAmount.HasValue || financialAmount <= 0)) return ApiResponse<Guid>.Fail("قيمة الجزاء مطلوبة", ["PENALTY_AMOUNT_REQUIRED"]);
        var action = new DisciplinaryAction { EmployeeCaseId = caseId, Type = type, FinancialAmount = financialAmount, Reason = reason.Trim(), ApprovedByUserId = actorUserId };
        db.DisciplinaryActions.Add(action); employeeCase.State = EmployeeCaseState.Decided; employeeCase.Version++; await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(action.Id);
    }

    public async Task<int> ApplyPenaltyAsync(Guid actionId, Guid payrollRunId, CancellationToken ct)
    {
        var action = await db.DisciplinaryActions.Include(item => item.EmployeeCase).SingleOrDefaultAsync(item => item.Id == actionId, ct);
        if (action?.Type != DisciplinaryActionType.FinancialPenalty || !action.FinancialAmount.HasValue || action.PayrollLineItemId.HasValue) return 0;
        if (await db.HrPayrollInputSources.AnyAsync(item => item.SourceType == nameof(DisciplinaryAction) && item.SourceId == actionId, ct)) return 0;
        var run = await db.HrPayrollRuns.Include(item => item.Employees).SingleAsync(item => item.Id == payrollRunId, ct);
        if (run.Status is HrPayrollRunStatus.GMApproved or HrPayrollRunStatus.Paid or HrPayrollRunStatus.Closed) return 0;
        var payroll = run.Employees.SingleOrDefault(item => item.EmployeeId == action.EmployeeCase!.EmployeeId); if (payroll is null) return 0;
        var component = await db.PayComponents.SingleOrDefaultAsync(item => item.Code == "DISCIPLINARY", ct);
        if (component is null) { component = new PayComponent { Code = "DISCIPLINARY", Name = "جزاء مالي", Classification = PayComponentClass.Deduction }; db.PayComponents.Add(component); }
        var line = new PayrollLineItem { EmployeePayrollId = payroll.Id, PayComponentId = component.Id, PayComponent = component, Amount = action.FinancialAmount.Value,
            InputsJson = JsonSerializer.Serialize(new { action.Id, action.EmployeeCaseId }), Explanation = action.Reason, SourceType = nameof(DisciplinaryAction), SourceId = action.Id };
        db.PayrollLineItems.Add(line); action.PayrollLineItemId = line.Id; payroll.Deductions += line.Amount; payroll.Net = payroll.Gross - payroll.Deductions;
        run.TotalDeductions += line.Amount; run.TotalNet = run.TotalGross - run.TotalDeductions; run.Version++;
        db.HrPayrollInputSources.Add(new HrPayrollInputSource { SourceType = nameof(DisciplinaryAction), SourceId = action.Id, EmployeePayrollId = payroll.Id, PayrollLineItemId = line.Id });
        await db.SaveChangesAsync(ct); return 1;
    }
}
