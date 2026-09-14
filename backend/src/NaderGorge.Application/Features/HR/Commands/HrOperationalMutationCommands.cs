using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.HR.Payroll;
using NaderGorge.Application.Features.HR.Payroll.Commands;
using NaderGorge.Application.Features.HR.Payroll.FinancialRequests;
using NaderGorge.Application.Features.HR.Scheduling;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Commands;

public sealed record CreateHrAssetCommand(string Code, string Name, string? SerialNumber, decimal Value) : IRequest<ApiResponse<Guid>>;
public sealed record CreatePayComponentCommand(string Code, string Name, PayComponentClass Classification, bool IsTaxable, bool IsInsurable) : IRequest<ApiResponse<Guid>>;
public sealed record CreatePayrollRuleCommand(Guid PayComponentId, string Name, string Expression, decimal Rate, DateOnly EffectiveFrom, DateOnly? EffectiveTo, int Priority) : IRequest<ApiResponse<Guid>>;
public sealed record CreateEmployeeCompensationCommand(Guid EmployeeId, decimal BaseSalary, string Currency, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string Reason) : IRequest<ApiResponse<Guid>>;
public sealed record PerformanceGoalInput(string Name, decimal Weight);
public sealed record CreatePerformanceCycleCommand(string Name, DateOnly StartsOn, DateOnly EndsOn, IReadOnlyList<PerformanceGoalInput> Goals) : IRequest<ApiResponse<Guid>>;
public sealed record AddCaseEvidenceCommand(Guid CaseId, string AssetReference, string ContentHash, Guid ActorUserId) : IRequest<ApiResponse<Guid>>;
public sealed record SubmitCaseResponseCommand(Guid CaseId, string Response, string? AttachmentReference, Guid ActorUserId) : IRequest<ApiResponse<Guid>>;
public sealed record PreparePayrollCommand(DateOnly PeriodStart, DateOnly PeriodEnd, DateTime CutoffAt, Guid ActorUserId)
    : IRequest<ApiResponse<Guid>>;

public sealed class HrOperationalMutationHandler(IAppDbContext db, PayrollRunService? payrollRunService = null,
    FinancialRequestService? financialRequestService = null) :
    IRequestHandler<CreateHrAssetCommand, ApiResponse<Guid>>,
    IRequestHandler<CreatePayComponentCommand, ApiResponse<Guid>>,
    IRequestHandler<CreatePayrollRuleCommand, ApiResponse<Guid>>,
    IRequestHandler<CreateEmployeeCompensationCommand, ApiResponse<Guid>>,
    IRequestHandler<CreatePerformanceCycleCommand, ApiResponse<Guid>>,
    IRequestHandler<AddCaseEvidenceCommand, ApiResponse<Guid>>,
    IRequestHandler<SubmitCaseResponseCommand, ApiResponse<Guid>>,
    IRequestHandler<PreparePayrollCommand, ApiResponse<Guid>>
{
    public async Task<ApiResponse<Guid>> Handle(PreparePayrollCommand request, CancellationToken ct)
    {
        if (payrollRunService is null || financialRequestService is null)
            throw new InvalidOperationException("Payroll services are required for payroll preparation.");
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var preparation = await payrollRunService.PrepareAsync(request.PeriodStart, request.PeriodEnd, request.CutoffAt,
            request.ActorUserId, ct);
        if (!preparation.Success) return preparation;
        await financialRequestService.ApplyDueInputsAsync(preparation.Data, ct);
        await transaction.CommitAsync(ct);
        return preparation;
    }

    public async Task<ApiResponse<Guid>> Handle(CreateHrAssetCommand request, CancellationToken ct)
    {
        var asset = new HrAsset { Code = request.Code.Trim().ToUpper(), Name = request.Name.Trim(), SerialNumber = request.SerialNumber, Value = request.Value };
        db.HrAssets.Add(asset); await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(asset.Id);
    }
    public async Task<ApiResponse<Guid>> Handle(CreatePayComponentCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant(); var name = request.Name.Trim();
        if (code.Length is < 2 or > 50 || name.Length is < 2 or > 200) return ApiResponse<Guid>.Fail("Invalid component", ["PAY_COMPONENT_INVALID"]);
        if (await db.PayComponents.AnyAsync(item => item.Code == code, ct)) return ApiResponse<Guid>.Fail("Code exists", ["PAY_COMPONENT_CODE_EXISTS"]);
        var row = new PayComponent { Code = code, Name = name, Classification = request.Classification, IsTaxable = request.IsTaxable, IsInsurable = request.IsInsurable };
        db.PayComponents.Add(row); await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(row.Id);
    }
    public async Task<ApiResponse<Guid>> Handle(CreatePayrollRuleCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Rate < 0 || request.Priority < 0 ||
            !PayrollCalculationEngine.IsValidExpression(request.Expression) || request.EffectiveTo < request.EffectiveFrom)
            return ApiResponse<Guid>.Fail("Invalid rule", ["PAYROLL_RULE_INVALID"]);
        if (!await db.PayComponents.AnyAsync(component => component.Id == request.PayComponentId, ct))
            return ApiResponse<Guid>.Fail("Component not found", ["PAY_COMPONENT_NOT_FOUND"]);
        var version = (await db.PayrollRules.Where(item => item.PayComponentId == request.PayComponentId).Select(item => (int?)item.Version).MaxAsync(ct) ?? 0) + 1;
        var row = new PayrollRule { PayComponentId = request.PayComponentId, Name = request.Name.Trim(), Expression = request.Expression.Trim(), Rate = request.Rate,
            EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo, Priority = request.Priority, Version = version };
        db.PayrollRules.Add(row); await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(row.Id, version.ToString());
    }
    public async Task<ApiResponse<Guid>> Handle(CreateEmployeeCompensationCommand request, CancellationToken ct)
    {
        var currency = request.Currency.Trim().ToUpperInvariant();
        if (request.BaseSalary < 0 || request.EffectiveTo < request.EffectiveFrom || currency.Length != 3 || string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<Guid>.Fail("Invalid compensation", ["COMPENSATION_INVALID"]);
        if (!await db.EmployeeProfiles.AnyAsync(employee => employee.Id == request.EmployeeId, ct))
            return ApiResponse<Guid>.Fail("Employee not found", ["EMPLOYEE_NOT_FOUND"]);
        if (await db.EmployeeCompensations.AnyAsync(compensation => compensation.EmployeeId == request.EmployeeId &&
            compensation.EffectiveFrom <= (request.EffectiveTo ?? DateOnly.MaxValue) && (!compensation.EffectiveTo.HasValue || compensation.EffectiveTo >= request.EffectiveFrom) &&
            (compensation.EffectiveTo.HasValue || compensation.EffectiveFrom >= request.EffectiveFrom), ct))
            return ApiResponse<Guid>.Fail("Overlap", ["COMPENSATION_PERIOD_OVERLAP"]);
        var previous = await db.EmployeeCompensations.Where(item => item.EmployeeId == request.EmployeeId && !item.EffectiveTo.HasValue)
            .OrderByDescending(item => item.EffectiveFrom).FirstOrDefaultAsync(ct);
        if (previous is not null && previous.EffectiveFrom < request.EffectiveFrom) previous.EffectiveTo = request.EffectiveFrom.AddDays(-1);
        var version = (await db.EmployeeCompensations.Where(item => item.EmployeeId == request.EmployeeId).Select(item => (int?)item.Version).MaxAsync(ct) ?? 0) + 1;
        var row = new EmployeeCompensation { EmployeeId = request.EmployeeId, BaseSalary = request.BaseSalary, Currency = currency,
            EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo, Reason = request.Reason.Trim(), Version = version };
        db.EmployeeCompensations.Add(row); await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(row.Id, version.ToString());
    }
    public async Task<ApiResponse<Guid>> Handle(CreatePerformanceCycleCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.EndsOn < request.StartsOn || request.Goals.Count == 0 ||
            request.Goals.Any(goal => string.IsNullOrWhiteSpace(goal.Name) || goal.Weight <= 0) || request.Goals.Sum(goal => goal.Weight) != 100)
            return ApiResponse<Guid>.Fail("Invalid cycle", ["PERFORMANCE_CYCLE_INVALID"]);
        var cycle = new PerformanceCycle { Name = request.Name.Trim(), StartsOn = request.StartsOn, EndsOn = request.EndsOn };
        foreach (var goal in request.Goals) cycle.Goals.Add(new PerformanceGoal { PerformanceCycleId = cycle.Id, Name = goal.Name.Trim(), Weight = goal.Weight });
        db.PerformanceCycles.Add(cycle); await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(cycle.Id);
    }
    public async Task<ApiResponse<Guid>> Handle(AddCaseEvidenceCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.AssetReference) || string.IsNullOrWhiteSpace(request.ContentHash) ||
            !await db.EmployeeCases.AnyAsync(item => item.Id == request.CaseId, ct)) return ApiResponse<Guid>.Fail("Invalid evidence", ["CASE_EVIDENCE_INVALID"]);
        var row = new CaseEvidence { EmployeeCaseId = request.CaseId, AssetReference = request.AssetReference, ContentHash = request.ContentHash, AddedByUserId = request.ActorUserId };
        db.CaseEvidence.Add(row); await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(row.Id);
    }
    public async Task<ApiResponse<Guid>> Handle(SubmitCaseResponseCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Response)) return ApiResponse<Guid>.Fail("Response required", ["CASE_RESPONSE_REQUIRED"]);
        if (!await db.EmployeeCases.AnyAsync(item => item.Id == request.CaseId && item.Employee!.UserId == request.ActorUserId, ct))
            return ApiResponse<Guid>.Fail("Forbidden", ["CASE_RESPONSE_FORBIDDEN"]);
        var row = new CaseResponse { EmployeeCaseId = request.CaseId, SubmittedByUserId = request.ActorUserId, Response = request.Response.Trim(), AttachmentReference = request.AttachmentReference };
        db.CaseResponses.Add(row); await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(row.Id);
    }
}
