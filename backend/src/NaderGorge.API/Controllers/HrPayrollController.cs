using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common.HR;
using NaderGorge.Application.Features.HR.Payroll;
using NaderGorge.Application.Features.HR.Payroll.Commands;
using NaderGorge.Application.Features.HR.Payroll.FinancialRequests;
using NaderGorge.Application.Features.HR.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController, Route("api/hr/payroll"), Authorize]
public sealed class HrPayrollController(IAppDbContext db, PayrollRunService runService, FinancialRequestService financialRequestService, IMediator mediator) : ControllerBase
{
    [HttpGet("config"), HasPermission(HrPermissions.PayrollConfigure)]
    public async Task<IActionResult> Config(CancellationToken ct) => Ok(new
    {
        components = await db.PayComponents.AsNoTracking().OrderBy(item => item.Code).ToListAsync(ct),
        rules = await db.PayrollRules.AsNoTracking().OrderBy(item => item.Priority).Select(item => new { item.Id, item.PayComponentId,
            component = item.PayComponent!.Name, item.Name, item.Expression, item.Rate, item.EffectiveFrom, item.EffectiveTo, item.Priority, item.Version, item.IsActive }).ToListAsync(ct)
    });

    [HttpPost("components"), HasPermission(HrPermissions.PayrollConfigure)]
    public async Task<IActionResult> CreateComponent(CreatePayComponentRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreatePayComponentCommand(request.Code, request.Name, request.Classification, request.IsTaxable, request.IsInsurable), ct);
        return result.Success ? Ok(new { Id = result.Data }) : result.Errors?.Contains("PAY_COMPONENT_CODE_EXISTS") == true ? Conflict(result) : BadRequest(result);
    }

    [HttpPost("rules"), HasPermission(HrPermissions.PayrollConfigure)]
    public async Task<IActionResult> CreateRule(CreatePayrollRuleRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreatePayrollRuleCommand(request.PayComponentId, request.Name, request.Expression,
            request.Rate, request.EffectiveFrom, request.EffectiveTo, request.Priority), ct);
        return result.Success ? Ok(new { Id = result.Data, Version = int.Parse(result.Message!) }) : BadRequest(result);
    }

    [HttpPost("compensations"), HasPermission(HrPermissions.PayrollConfigure)]
    public async Task<IActionResult> CreateCompensation(CreateCompensationRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateEmployeeCompensationCommand(request.EmployeeId, request.BaseSalary,
            request.Currency, request.EffectiveFrom, request.EffectiveTo, request.Reason), ct);
        if (result.Success) return Ok(new { Id = result.Data, Version = int.Parse(result.Message!) });
        if (result.Errors?.Contains("EMPLOYEE_NOT_FOUND") == true) return NotFound(result);
        return result.Errors?.Contains("COMPENSATION_PERIOD_OVERLAP") == true ? Conflict(result) : BadRequest(result);
    }

    [HttpGet("runs"), HasPermission(HrPermissions.PayrollView)]
    public async Task<IActionResult> Runs(CancellationToken ct) => Ok(await db.HrPayrollRuns.AsNoTracking().OrderByDescending(item => item.PeriodStart)
        .Select(item => new { item.Id, item.RunNumber, item.PeriodStart, item.PeriodEnd, status = item.Status.ToString(), item.TotalGross, item.TotalDeductions,
            item.TotalNet, employees = item.Employees.Count, item.ReconciliationHash, item.Version }).Take(100).ToListAsync(ct));

    [HttpGet("runs/{runId:guid}"), HasPermission(HrPermissions.PayrollView)]
    public async Task<IActionResult> Run(Guid runId, CancellationToken ct) => Ok(await db.EmployeePayrolls.AsNoTracking().Where(item => item.PayrollRunId == runId)
        .OrderBy(item => item.EmployeeNameSnapshot).Select(item => new { item.Id, item.EmployeeId, item.EmployeeNumberSnapshot, item.EmployeeNameSnapshot,
            item.BaseSalarySnapshot, item.Currency, item.Gross, item.Deductions, item.Net, status = item.Status.ToString(),
            lines = item.Lines.OrderBy(line => line.PayComponent!.Code).Select(line => new { line.Id, component = line.PayComponent!.Name, classification = line.PayComponent.Classification.ToString(), line.Amount, line.Explanation, line.SourceType, line.SourceId }) }).ToListAsync(ct));

    [HttpPost("runs/prepare"), HasPermission(HrPermissions.PayrollPrepare)]
    public async Task<IActionResult> Prepare(PreparePayrollRunRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new PreparePayrollCommand(request.PeriodStart, request.PeriodEnd,
            request.CutoffAt, User.RequireUserId()), ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("runs/{runId:guid}/finance-review"), HasPermission(HrPermissions.PayrollReview)]
    public Task<IActionResult> FinanceReview(Guid runId, PayrollTransitionRequest request, CancellationToken ct) => Move(runId, request, HrPayrollRunStatus.FinanceReview, ct);
    [HttpPost("runs/{runId:guid}/finance-approve"), HasPermission(HrPermissions.PayrollReview)]
    public Task<IActionResult> FinanceApprove(Guid runId, PayrollTransitionRequest request, CancellationToken ct) => Move(runId, request, HrPayrollRunStatus.FinanceApproved, ct);
    [HttpPost("runs/{runId:guid}/gm-approve"), HasPermission(HrPermissions.PayrollFinalApprove)]
    public Task<IActionResult> GmApprove(Guid runId, PayrollTransitionRequest request, CancellationToken ct) => Move(runId, request, HrPayrollRunStatus.GMApproved, ct);
    [HttpPost("runs/{runId:guid}/pay"), HasPermission(HrPermissions.PayrollPay)]
    public Task<IActionResult> Pay(Guid runId, PayrollTransitionRequest request, CancellationToken ct) => Move(runId, request, HrPayrollRunStatus.Paid, ct);
    [HttpPost("runs/{runId:guid}/close"), HasPermission(HrPermissions.PayrollPay)]
    public Task<IActionResult> Close(Guid runId, PayrollTransitionRequest request, CancellationToken ct) => Move(runId, request, HrPayrollRunStatus.Closed, ct);
    [HttpPost("runs/{runId:guid}/return"), HasPermission(HrPermissions.PayrollReview)]
    public Task<IActionResult> Return(Guid runId, PayrollTransitionRequest request, CancellationToken ct) => Move(runId, request, HrPayrollRunStatus.Returned, ct);

    [HttpPost("settlements"), HasPermission(HrPermissions.PayrollConfigure)]
    public async Task<IActionResult> Settlement(CreatePayrollSettlementRequest request, CancellationToken ct)
    {
        var result = await runService.AddSettlementAsync(request.OriginalLineId, request.SettlementRunId, request.Amount, request.Reason, User.RequireUserId(), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("self/payslips"), HasPermission(HrPermissions.PayrollSelf)]
    public async Task<IActionResult> MyPayslips(CancellationToken ct)
    {
        var userId = User.RequireUserId();
        return Ok(await db.EmployeePayrolls.AsNoTracking().Where(item => item.Employee!.UserId == userId && item.PayrollRun!.Status >= HrPayrollRunStatus.Paid)
            .OrderByDescending(item => item.PayrollRun!.PeriodStart).Select(item => new { item.Id, item.PayrollRun!.RunNumber, item.PayrollRun.PeriodStart,
                item.PayrollRun.PeriodEnd, item.BaseSalarySnapshot, item.Currency, item.Gross, item.Deductions, item.Net,
                lines = item.Lines.Select(line => new { component = line.PayComponent!.Name, classification = line.PayComponent.Classification.ToString(), line.Amount, line.Explanation }),
                payslip = db.Payslips.Where(slip => slip.EmployeePayrollId == item.Id).OrderByDescending(slip => slip.Version).Select(slip => new { slip.Id, slip.Version, slip.AssetReference, slip.ContentHash }).FirstOrDefault() }).ToListAsync(ct));
    }

    [HttpGet("self/financial-requests"), HasPermission(HrPermissions.PayrollSelf)]
    public async Task<IActionResult> MyFinancialRequests(CancellationToken ct)
    {
        var userId = User.RequireUserId();
        return Ok(await db.HrFinancialRequests.AsNoTracking().Where(item => item.Employee!.UserId == userId).OrderByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, type = item.Type.ToString(), state = item.State.ToString(), item.Amount, item.OutstandingBalance,
                item.RequestedInstallments, item.Reason, item.AttachmentReference, item.Version,
                installments = item.Installments.OrderBy(row => row.Sequence).Select(row => new { row.Id, row.Sequence, row.DueDate, row.Amount, state = row.State.ToString(), row.AppliedAt }) }).ToListAsync(ct));
    }

    [HttpPost("self/financial-requests"), HasPermission(HrPermissions.PayrollSelf)]
    public async Task<IActionResult> SubmitFinancialRequest(SubmitFinancialRequest request, CancellationToken ct)
    {
        var result = await financialRequestService.SubmitAsync(User.RequireUserId(), request.Type, request.Amount, request.Installments, request.Reason, request.AttachmentReference, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("financial-requests"), HasPermission(HrPermissions.PayrollReview)]
    public async Task<IActionResult> FinancialRequests(CancellationToken ct) => Ok(await db.HrFinancialRequests.AsNoTracking().OrderByDescending(item => item.CreatedAt)
        .Select(item => new { item.Id, item.EmployeeId, employee = item.Employee!.User!.FullName, type = item.Type.ToString(), state = item.State.ToString(),
            item.Amount, item.OutstandingBalance, item.RequestedInstallments, item.Reason, item.AttachmentReference, item.Version }).Take(200).ToListAsync(ct));

    [HttpPost("financial-requests/{requestId:guid}/approve"), HasPermission(HrPermissions.PayrollReview)]
    public async Task<IActionResult> ApproveFinancialRequest(Guid requestId, ApproveFinancialRequest request, CancellationToken ct)
    {
        var result = await financialRequestService.ApproveAsync(requestId, User.RequireUserId(), request.FirstDueDate, request.ExpectedVersion, ct);
        return result.Success ? Ok(result) : Conflict(result);
    }

    private async Task<IActionResult> Move(Guid runId, PayrollTransitionRequest request, HrPayrollRunStatus target, CancellationToken ct)
    {
        var result = await runService.MoveAsync(runId, target, User.RequireUserId(), request.ExpectedVersion, ct);
        return result.Success ? Ok(result) : Conflict(result);
    }
}

public sealed record CreatePayComponentRequest(string Code, string Name, PayComponentClass Classification, bool IsTaxable, bool IsInsurable);
public sealed record CreatePayrollRuleRequest(Guid PayComponentId, string Name, string Expression, decimal Rate, DateOnly EffectiveFrom, DateOnly? EffectiveTo, int Priority);
public sealed record CreateCompensationRequest(Guid EmployeeId, decimal BaseSalary, string Currency, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string Reason);
public sealed record PreparePayrollRunRequest(DateOnly PeriodStart, DateOnly PeriodEnd, DateTime CutoffAt);
public sealed record PayrollTransitionRequest(int ExpectedVersion);
public sealed record CreatePayrollSettlementRequest(Guid OriginalLineId, Guid SettlementRunId, decimal Amount, string Reason);
public sealed record SubmitFinancialRequest(HrFinancialRequestType Type, decimal Amount, int Installments, string Reason, string? AttachmentReference);
public sealed record ApproveFinancialRequest(DateOnly FirstDueDate, int ExpectedVersion);
