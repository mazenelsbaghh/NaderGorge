using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common.HR;
using NaderGorge.Application.Features.HR.Lifecycle;
using NaderGorge.Application.Features.HR.Recruitment;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController, Route("api/hr"), Authorize]
public sealed class HrRecruitmentLifecycleController(IAppDbContext db, RecruitmentService recruitmentService, LifecycleOrchestrationService lifecycleService) : ControllerBase
{
    [HttpGet("admin/recruitment/board"), HasPermission(HrPermissions.RecruitmentRead)]
    public async Task<IActionResult> Board(CancellationToken ct) => Ok(await db.Requisitions.AsNoTracking().OrderByDescending(item => item.CreatedAt)
        .Select(item => new { item.Id, item.RequisitionNumber, item.Title, item.Openings, state = item.State.ToString(), item.Requirements,
            candidates = item.Candidates.OrderBy(candidate => candidate.CreatedAt).Select(candidate => new { candidate.Id, candidate.FullName, candidate.PhoneNumber,
                candidate.Email, stage = candidate.Stage.ToString(), candidate.CvAssetReference, candidate.EmployeeProfileId, candidate.Version,
                offers = candidate.Offers.Select(offer => new { offer.Id, offer.OfferNumber, offer.BaseSalary, offer.Currency, offer.ProposedStartDate, state = offer.State.ToString(), offer.Version }) }) }).ToListAsync(ct));

    [HttpPost("admin/recruitment/requisitions"), HasPermission(HrPermissions.RecruitmentManage)]
    public async Task<IActionResult> CreateRequisition(CreateRequisitionRequest request, CancellationToken ct)
    {
        var row = new Requisition { RequisitionNumber = $"REQ-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..27].ToUpperInvariant(), Title = request.Title.Trim(),
            OrganizationUnitId = request.OrganizationUnitId, Openings = request.Openings, Requirements = request.Requirements.Trim(), RequestedByUserId = User.RequireUserId(), State = RequisitionState.Open };
        db.Requisitions.Add(row); await db.SaveChangesAsync(ct); return Ok(new { row.Id });
    }

    [HttpPost("admin/recruitment/requisitions/{requisitionId:guid}/candidates"), HasPermission(HrPermissions.RecruitmentManage)]
    public async Task<IActionResult> AddCandidate(Guid requisitionId, AddCandidateRequest request, CancellationToken ct)
    {
        var candidate = new Candidate { RequisitionId = requisitionId, FullName = request.FullName.Trim(), PhoneNumber = request.PhoneNumber.Trim(), Email = request.Email, CvAssetReference = request.CvAssetReference };
        db.Candidates.Add(candidate); await db.SaveChangesAsync(ct); return Ok(new { candidate.Id });
    }

    [HttpPost("admin/recruitment/candidates/{candidateId:guid}/interviews"), HasPermission(HrPermissions.RecruitmentManage)]
    public async Task<IActionResult> ScheduleInterview(Guid candidateId, ScheduleInterviewRequest request, CancellationToken ct)
    {
        var candidate = await db.Candidates.SingleAsync(item => item.Id == candidateId, ct); candidate.Stage = CandidateStage.Interview; candidate.Version++;
        var interview = new CandidateInterview { CandidateId = candidateId, ScheduledAt = request.ScheduledAt, InterviewerUserId = request.InterviewerUserId };
        db.CandidateInterviews.Add(interview); await db.SaveChangesAsync(ct); return Ok(new { interview.Id });
    }

    [HttpPost("admin/recruitment/candidates/{candidateId:guid}/offers"), HasPermission(HrPermissions.RecruitmentManage)]
    public async Task<IActionResult> CreateOffer(Guid candidateId, CreateCandidateOfferRequest request, CancellationToken ct)
    {
        var candidate = await db.Candidates.SingleAsync(item => item.Id == candidateId, ct); candidate.Stage = CandidateStage.Offer; candidate.Version++;
        var offer = new CandidateOffer { CandidateId = candidateId, OfferNumber = $"OFF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..27].ToUpperInvariant(), BaseSalary = request.BaseSalary, Currency = request.Currency.ToUpperInvariant(), ProposedStartDate = request.ProposedStartDate, State = OfferState.Sent };
        db.CandidateOffers.Add(offer); await db.SaveChangesAsync(ct); return Ok(new { offer.Id });
    }

    [HttpPost("admin/recruitment/offers/{offerId:guid}/accept"), HasPermission(HrPermissions.RecruitmentManage)]
    public async Task<IActionResult> AcceptOffer(Guid offerId, CandidateVersionRequest request, CancellationToken ct)
    {
        var offer = await db.CandidateOffers.SingleAsync(item => item.Id == offerId, ct); if (offer.Version != request.ExpectedVersion) return Conflict();
        offer.State = OfferState.Accepted; offer.AcceptedAt = DateTime.UtcNow; offer.Version++; await db.SaveChangesAsync(ct); return Ok();
    }

    [HttpPost("admin/recruitment/candidates/{candidateId:guid}/hire"), HasPermission(HrPermissions.RecruitmentManage)]
    public async Task<IActionResult> Hire(Guid candidateId, HireCandidateRequest request, CancellationToken ct)
    {
        var result = await recruitmentService.HireAcceptedCandidateAsync(candidateId, request.OfferId, BCrypt.Net.BCrypt.HashPassword(request.TemporaryPassword), User.RequireUserId(), ct);
        return result.Success ? Ok(result) : Conflict(result);
    }

    [HttpGet("admin/lifecycle/tasks"), HasPermission(HrPermissions.RecruitmentRead)]
    public async Task<IActionResult> Tasks(CancellationToken ct) => Ok(await db.EmployeeLifecycleTasks.AsNoTracking().OrderBy(item => item.DueAt)
        .Select(item => new { item.Id, item.EmployeeId, employee = item.Employee!.User!.FullName, item.Phase, item.Title, item.DueAt, state = item.State.ToString(), overdue = item.DueAt < DateTime.UtcNow && item.State != LifecycleTaskState.Completed }).Take(300).ToListAsync(ct));

    [HttpPost("admin/lifecycle/offboarding"), HasPermission(HrPermissions.EmployeeManage)]
    public async Task<IActionResult> StartOffboarding(StartOffboardingRequest request, CancellationToken ct)
    {
        var result = await lifecycleService.StartOffboardingAsync(request.EmployeeId, request.LastWorkingDate, request.Reason, User.RequireUserId(), ct); return result.Success ? Ok(result) : Conflict(result);
    }

    [HttpPost("admin/lifecycle/offboarding/{processId:guid}/complete"), HasPermission(HrPermissions.EmployeeManage)]
    public async Task<IActionResult> CompleteOffboarding(Guid processId, CandidateVersionRequest request, CancellationToken ct)
    {
        var result = await lifecycleService.CompleteOffboardingAsync(processId, User.RequireUserId(), request.ExpectedVersion, ct); return result.Success ? Ok(result) : Conflict(result);
    }
}

public sealed record CreateRequisitionRequest(string Title, Guid? OrganizationUnitId, int Openings, string Requirements);
public sealed record AddCandidateRequest(string FullName, string PhoneNumber, string? Email, string? CvAssetReference);
public sealed record ScheduleInterviewRequest(DateTime ScheduledAt, Guid InterviewerUserId);
public sealed record CreateCandidateOfferRequest(decimal BaseSalary, string Currency, DateOnly ProposedStartDate);
public sealed record CandidateVersionRequest(int ExpectedVersion);
public sealed record HireCandidateRequest(Guid OfferId, string TemporaryPassword);
public sealed record StartOffboardingRequest(Guid EmployeeId, DateOnly LastWorkingDate, string Reason);
