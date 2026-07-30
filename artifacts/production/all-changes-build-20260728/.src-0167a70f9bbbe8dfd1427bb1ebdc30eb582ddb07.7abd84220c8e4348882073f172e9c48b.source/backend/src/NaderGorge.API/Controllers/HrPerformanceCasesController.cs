using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common.HR;
using NaderGorge.Application.Features.HR.Performance;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController, Route("api/hr"), Authorize]
public sealed class HrPerformanceCasesController(IAppDbContext db, PerformanceCaseService service) : ControllerBase
{
    [HttpGet("admin/performance/cycles"), HasPermission(HrPermissions.PerformanceTeam)]
    public async Task<IActionResult> Cycles(CancellationToken ct) => Ok(await db.PerformanceCycles.AsNoTracking().OrderByDescending(item => item.StartsOn)
        .Select(item => new { item.Id, item.Name, item.StartsOn, item.EndsOn, state = item.State.ToString(), goals = item.Goals.Select(goal => new { goal.Id, goal.Name, goal.Weight }) }).ToListAsync(ct));

    [HttpPost("admin/performance/cycles"), HasPermission(HrPermissions.PerformanceManage)]
    public async Task<IActionResult> CreateCycle(CreatePerformanceCycleRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.EndsOn < request.StartsOn || request.Goals.Count == 0 ||
            request.Goals.Any(goal => string.IsNullOrWhiteSpace(goal.Name) || goal.Weight <= 0) ||
            request.Goals.Sum(goal => goal.Weight) != 100)
            return BadRequest(new { errors = new[] { "PERFORMANCE_CYCLE_INVALID" } });
        var cycle = new PerformanceCycle { Name = request.Name.Trim(), StartsOn = request.StartsOn, EndsOn = request.EndsOn };
        foreach (var goal in request.Goals) cycle.Goals.Add(new PerformanceGoal { PerformanceCycleId = cycle.Id, Name = goal.Name.Trim(), Weight = goal.Weight });
        db.PerformanceCycles.Add(cycle); await db.SaveChangesAsync(ct); return Ok(new { cycle.Id });
    }

    [HttpPost("admin/performance/cycles/{cycleId:guid}/activate"), HasPermission(HrPermissions.PerformanceManage)]
    public async Task<IActionResult> Activate(Guid cycleId, CancellationToken ct)
    {
        var result = await service.ActivateCycleAsync(cycleId, ct); return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("admin/performance/reviews"), HasPermission(HrPermissions.PerformanceTeam)]
    public async Task<IActionResult> PublishReview(PublishPerformanceReviewRequest request, CancellationToken ct)
    {
        var result = await service.PublishReviewAsync(request.CycleId, request.EmployeeId, User.RequireUserId(), request.Scores, ct); return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("self/performance/reviews"), HasPermission(HrPermissions.PerformanceSelf)]
    public async Task<IActionResult> MyReviews(CancellationToken ct)
    {
        var userId = User.RequireUserId(); return Ok(await db.PerformanceReviews.AsNoTracking().Where(item => item.Employee!.UserId == userId && item.State >= PerformanceReviewState.Published)
            .OrderByDescending(item => item.PublishedAt).Select(item => new { item.Id, cycle = item.PerformanceCycle!.Name, item.WeightedScore, state = item.State.ToString(), item.PublishedAt, item.AppealReason, item.AppealResolution, item.Version }).ToListAsync(ct));
    }

    [HttpPost("self/performance/reviews/{reviewId:guid}/appeal"), HasPermission(HrPermissions.PerformanceSelf)]
    public async Task<IActionResult> Appeal(Guid reviewId, AppealPerformanceReviewRequest request, CancellationToken ct)
    {
        var result = await service.AppealAsync(reviewId, User.RequireUserId(), request.Reason, request.ExpectedVersion, ct); return result.Success ? Ok(result) : Conflict(result);
    }

    [HttpGet("admin/cases"), HasPermission(HrPermissions.CaseRead)]
    public async Task<IActionResult> Cases(CancellationToken ct) => Ok(await db.EmployeeCases.AsNoTracking().OrderByDescending(item => item.CreatedAt)
        .Select(item => new { item.Id, item.CaseNumber, item.EmployeeId, employee = item.Employee!.User!.FullName, item.Title, item.Description,
            item.IsConfidential, state = item.State.ToString(), item.Version, actions = item.Actions.Select(action => new { action.Id, type = action.Type.ToString(), action.FinancialAmount, action.Reason, action.PayrollLineItemId }) }).Take(200).ToListAsync(ct));

    [HttpPost("admin/cases"), HasPermission(HrPermissions.CaseManage)]
    public async Task<IActionResult> OpenCase(OpenEmployeeCaseRequest request, CancellationToken ct)
    {
        var result = await service.OpenCaseAsync(request.EmployeeId, User.RequireUserId(), request.Title, request.Description, request.IsConfidential, ct); return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("admin/cases/{caseId:guid}/evidence"), HasPermission(HrPermissions.CaseManage)]
    public async Task<IActionResult> AddEvidence(Guid caseId, AddCaseEvidenceRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.AssetReference) || string.IsNullOrWhiteSpace(request.ContentHash) ||
            !await db.EmployeeCases.AnyAsync(employeeCase => employeeCase.Id == caseId, ct)) return BadRequest();
        var evidence = new CaseEvidence { EmployeeCaseId = caseId, AssetReference = request.AssetReference, ContentHash = request.ContentHash, AddedByUserId = User.RequireUserId() };
        db.CaseEvidence.Add(evidence); await db.SaveChangesAsync(ct); return Ok(new { evidence.Id });
    }

    [HttpPost("self/cases/{caseId:guid}/response"), HasPermission(HrPermissions.PerformanceSelf)]
    public async Task<IActionResult> Respond(Guid caseId, CaseResponseRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Response)) return BadRequest();
        var userId = User.RequireUserId(); if (!await db.EmployeeCases.AnyAsync(item => item.Id == caseId && item.Employee!.UserId == userId, ct)) return Forbid();
        var response = new CaseResponse { EmployeeCaseId = caseId, SubmittedByUserId = userId, Response = request.Response.Trim(), AttachmentReference = request.AttachmentReference };
        db.CaseResponses.Add(response); await db.SaveChangesAsync(ct); return Ok(new { response.Id });
    }

    [HttpPost("admin/cases/{caseId:guid}/decision"), HasPermission(HrPermissions.CaseManage)]
    public async Task<IActionResult> Decide(Guid caseId, DecideEmployeeCaseRequest request, CancellationToken ct)
    {
        var result = await service.DecideCaseAsync(caseId, request.Type, request.FinancialAmount, request.Reason, User.RequireUserId(), request.ExpectedVersion, ct); return result.Success ? Ok(result) : Conflict(result);
    }

    [HttpPost("admin/cases/actions/{actionId:guid}/apply-payroll/{runId:guid}"), HasPermission(HrPermissions.CaseManage)]
    public async Task<IActionResult> ApplyPayroll(Guid actionId, Guid runId, CancellationToken ct) => Ok(new { applied = await service.ApplyPenaltyAsync(actionId, runId, ct) });
}

public sealed record PerformanceGoalRequest(string Name, decimal Weight);
public sealed record CreatePerformanceCycleRequest(string Name, DateOnly StartsOn, DateOnly EndsOn, IReadOnlyList<PerformanceGoalRequest> Goals);
public sealed record PublishPerformanceReviewRequest(Guid CycleId, Guid EmployeeId, IReadOnlyDictionary<Guid, decimal> Scores);
public sealed record AppealPerformanceReviewRequest(string Reason, int ExpectedVersion);
public sealed record OpenEmployeeCaseRequest(Guid EmployeeId, string Title, string Description, bool IsConfidential);
public sealed record AddCaseEvidenceRequest(string AssetReference, string ContentHash);
public sealed record CaseResponseRequest(string Response, string? AttachmentReference);
public sealed record DecideEmployeeCaseRequest(DisciplinaryActionType Type, decimal? FinancialAmount, string Reason, int ExpectedVersion);
